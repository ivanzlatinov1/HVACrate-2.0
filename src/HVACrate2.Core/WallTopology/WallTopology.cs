using netDxf;
using netDxf.Blocks;
using netDxf.Entities;

namespace HVACrate2.Core;

/// <summary>
/// Determines which physical wall an opening (window/door) is cut into, from wall geometry
/// alone, and whether that wall is part of the OVK perimeter. This replaces straight-line
/// marker-to-OVK distance, which was found unable to separate exterior from interior openings
/// in real floor plans (see docs/decisions.md for the investigation): a marker's own position
/// can sit anywhere from ~0.25m to several meters from its true wall depending on the CAD
/// tool's annotation convention, with no reliable gap between "exterior" and "interior" cases.
/// Wall-network topology does not have this problem — validated on real data, exterior openings
/// land 1-2 hops / ~1-2m of wall from an OVK-coincident node, interior ones 12+ hops / 80m+.
/// </summary>
internal static class WallTopology
{
    // Snapping tolerance for merging nearly-coincident wall endpoints into one graph node.
    private const double NodeSnapM = 0.05;

    // How close a wall-graph node must be to an OVK edge to count as "on" the OVK perimeter.
    // Drafting-precision tolerance only — not the exterior/interior decision (that's topology).
    private const double OvkNodeToleranceM = 0.05;

    // How far from a marker its real (non-annotation) window/door body may be, when no
    // structural host-wall link exists and the body must be found by proximity.
    private const double CompanionSearchRadiusM = 6.0;

    // How close the transformed real window/door body geometry must land to a wall-graph node
    // to count as "touching" it. Drafting precision only (mirrors NodeSnapM/OvkNodeToleranceM).
    private const double CompanionWallTouchToleranceM = 0.15;

    // Maximum direction change, in degrees, allowed between consecutive wall-graph edges while
    // still counting as "the same wall run". Real walls zigzag slightly at drafting/party-wall
    // seams (validated real case: ~15-20 deg kink across a two-segment run); genuine interior
    // partitions meet their host exterior wall at a real corner, near 90 degrees. This is a
    // shape test on the wall itself, not a distance or hop-count budget.
    private const double MaxCollinearDeviationDeg = 35.0;

    // Safety cap on how far a single-direction trace may run, purely to bound worst-case work —
    // not a classification distance. Real wall runs terminate (via the deviation test above) long
    // before this in every case observed.
    private const int MaxTraceSteps = 200;

    internal sealed class WallGraph
    {
        public readonly Dictionary<(double x, double y), HashSet<(double x, double y)>> Adjacency = new();
        public readonly HashSet<(double x, double y)> OvkNodes = new();

        // Which OVK edge (index into the ovkEdges list passed to MarkOvkNodes) each OVK node was
        // matched to, at the same tight (OvkNodeToleranceM) precision used to decide it was an OVK
        // node at all. This is the authoritative source for an opening's direction — see
        // AssignOvkEdgeIndex — because it only ever reflects a genuine wall-face-to-boundary touch,
        // never a coincidentally-nearby but unrelated edge (unlike re-deriving direction from a
        // traced path's own segments, which can include short perpendicular jamb ticks — see
        // docs/decisions.md for the specific case this caused).
        public readonly Dictionary<(double x, double y), int> OvkEdgeIndexByNode = new();

        // Same raw-units-to-meters divisor FloorProcessor detected for this file's OVK geometry
        // (see FloorProcessor.DetectCoordinateDivisor) — a DXF uses one coordinate convention
        // throughout, so wall geometry must be converted the same way as the OVK boundary was.
        public double Divisor = 100.0;
    }

    private static bool IsWallLayer(string layerName) => layerName.ToLowerInvariant().Contains("wall");

    private static bool IsOpeningBodyLayer(string layerName)
    {
        var lower = layerName.ToLowerInvariant();
        if (lower.Contains("marker")) return false;
        return lower.Contains("window") || lower.Contains("door");
    }

    private static (double x, double y) Snap(double xM, double yM)
        => (Math.Round(xM / NodeSnapM) * NodeSnapM, Math.Round(yM / NodeSnapM) * NodeSnapM);

    /// <summary>
    /// Standard DXF INSERT transform: subtract the block's own base point, scale, rotate, then
    /// translate to the insertion position. The base-point subtraction matters here: floor1-4's
    /// Wall_N_2 blocks have a non-zero base point equal to their own insertion position (their
    /// entity geometry is authored directly in world/absolute coordinates), so skipping it would
    /// double-count that offset. Passing a null insert is the identity transform, for wall
    /// geometry already at top level in model space.
    /// </summary>
    private static (double x, double y) TransformPoint(double lx, double ly, Insert? insert)
    {
        if (insert is null) return (lx, ly);
        double bx = insert.Block.Origin.X, by = insert.Block.Origin.Y;
        double sx = insert.Scale.X, sy = insert.Scale.Y;
        double rot = insert.Rotation * Math.PI / 180.0;
        double cos = Math.Cos(rot), sin = Math.Sin(rot);
        double x0 = (lx - bx) * sx, y0 = (ly - by) * sy;
        double wx = x0 * cos - y0 * sin;
        double wy = x0 * sin + y0 * cos;
        return (insert.Position.X + wx, insert.Position.Y + wy);
    }

    private static double DistancePointToSegment(double px, double py, double x1, double y1, double x2, double y2)
    {
        double dx = x2 - x1, dy = y2 - y1;
        double lenSq = dx * dx + dy * dy;
        if (lenSq < 1e-9) return Math.Sqrt((px - x1) * (px - x1) + (py - y1) * (py - y1));
        double t = Math.Clamp(((px - x1) * dx + (py - y1) * dy) / lenSq, 0.0, 1.0);
        double cx = x1 + t * dx, cy = y1 + t * dy;
        return Math.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
    }

    private static void AddSegment(WallGraph graph, (double x, double y) rawA, (double x, double y) rawB)
    {
        var a = Snap(rawA.x / graph.Divisor, rawA.y / graph.Divisor);
        var b = Snap(rawB.x / graph.Divisor, rawB.y / graph.Divisor);
        if (a == b) return;
        if (!graph.Adjacency.TryGetValue(a, out var setA)) graph.Adjacency[a] = setA = new();
        if (!graph.Adjacency.TryGetValue(b, out var setB)) graph.Adjacency[b] = setB = new();
        setA.Add(b);
        setB.Add(a);
    }

    /// <summary>
    /// Builds one connectivity graph of every wall-layer LINE/Polyline2D segment in the document,
    /// in world coordinates. Covers both wall-drawing conventions seen in real files with the same
    /// logic, not separate heuristics: entities drawn directly in model space (e.g. Archicad's flat
    /// "STR- Exterior/Interior walls" layers) are added as-is; entities nested one level inside a
    /// per-wall block (e.g. floor1-4's Wall_N_2 convention) are transformed through that block's own
    /// INSERT first. A top-level wall segment is just the degenerate case of an identity transform.
    /// </summary>
    internal static WallGraph BuildWallGraph(DxfDocument doc, double coordDivisor)
    {
        var graph = new WallGraph { Divisor = coordDivisor };

        foreach (var line in doc.Entities.Lines.Where(l => IsWallLayer(l.Layer.Name)))
            AddSegment(graph, (line.StartPoint.X, line.StartPoint.Y), (line.EndPoint.X, line.EndPoint.Y));

        foreach (var poly in doc.Entities.Polylines2D.Where(p => IsWallLayer(p.Layer.Name)))
        {
            var verts = poly.Vertexes.Select(v => (v.Position.X, v.Position.Y)).ToList();
            for (int i = 0; i < verts.Count - 1; i++)
                AddSegment(graph, verts[i], verts[i + 1]);
        }

        foreach (var insert in doc.Entities.Inserts)
        {
            foreach (var line in insert.Block.Entities.OfType<Line>().Where(l => IsWallLayer(l.Layer.Name)))
                AddSegment(graph,
                    TransformPoint(line.StartPoint.X, line.StartPoint.Y, insert),
                    TransformPoint(line.EndPoint.X, line.EndPoint.Y, insert));

            foreach (var poly in insert.Block.Entities.OfType<Polyline2D>().Where(p => IsWallLayer(p.Layer.Name)))
            {
                var verts = poly.Vertexes.Select(v => TransformPoint(v.Position.X, v.Position.Y, insert)).ToList();
                for (int i = 0; i < verts.Count - 1; i++)
                    AddSegment(graph, verts[i], verts[i + 1]);
            }
        }

        return graph;
    }

    /// <summary>
    /// Marks every wall-graph node that coincides with an OVK edge, within drafting-precision
    /// tolerance, and records which specific edge each one matched (see
    /// <see cref="WallGraph.OvkEdgeIndexByNode"/>).
    /// </summary>
    internal static void MarkOvkNodes(WallGraph graph, List<(double x1, double y1, double x2, double y2)> ovkEdges)
    {
        foreach (var node in graph.Adjacency.Keys)
        {
            double best = double.MaxValue;
            int bestIdx = -1;
            for (int e = 0; e < ovkEdges.Count; e++)
            {
                var (x1, y1, x2, y2) = ovkEdges[e];
                double d = DistancePointToSegment(node.x, node.y, x1, y1, x2, y2);
                if (d < best) { best = d; bestIdx = e; }
            }
            if (best <= OvkNodeToleranceM)
            {
                graph.OvkNodes.Add(node);
                graph.OvkEdgeIndexByNode[node] = bestIdx;
            }
        }
    }

    /// <summary>
    /// Assigns the OVK edge index for an opening from every path its host wall reached OVK by,
    /// deterministically: group paths by which edge their reached node was matched to (an
    /// authoritative fact from <see cref="MarkOvkNodes"/>, not re-derived here) and take the edge
    /// reached by the most paths — plurality vote, not total path length. Length was tried first
    /// and rejected: a single long detour through an unrelated, incidentally-collinear-enough wall
    /// run can outweigh many short, genuinely-correct paths to the opening's real wall (confirmed
    /// case: 24 short paths of ~0-2.7m each correctly reached the opening's own east wall, while one
    /// 10.1m path wandered the entire south wall and would have won on length alone — see
    /// docs/decisions.md). A short local path is exactly as much evidence as a long one; counting
    /// how many independent trace attempts agree is robust to that one outlier the way summing
    /// length is not. Ties break on lower total path length (the more local, more likely genuine
    /// signal), then on lower edge index, for full determinism.
    /// </summary>
    internal static int? AssignOvkEdgeIndex(List<List<(double x, double y)>> paths, WallGraph graph)
    {
        var countByEdge = new Dictionary<int, int>();
        var lengthByEdge = new Dictionary<int, double>();
        foreach (var path in paths)
        {
            if (path.Count == 0) continue;
            if (!graph.OvkEdgeIndexByNode.TryGetValue(path[^1], out int edgeIdx)) continue;

            double len = 0.0;
            for (int i = 0; i < path.Count - 1; i++)
            {
                double dx = path[i + 1].x - path[i].x, dy = path[i + 1].y - path[i].y;
                len += Math.Sqrt(dx * dx + dy * dy);
            }
            countByEdge[edgeIdx] = countByEdge.GetValueOrDefault(edgeIdx) + 1;
            lengthByEdge[edgeIdx] = lengthByEdge.GetValueOrDefault(edgeIdx) + len;
        }

        if (countByEdge.Count == 0) return null;
        return countByEdge.Keys
            .OrderByDescending(e => countByEdge[e])
            .ThenBy(e => lengthByEdge[e])
            .ThenBy(e => e)
            .First();
    }

    /// <summary>
    /// Finds every wall-graph node belonging to this opening's host wall — not just the single
    /// closest one. A wall run has multiple nodes (its face line's endpoints, jamb ticks, etc.);
    /// picking only the Euclidean-nearest one to the marker risks landing on a short dead-end tick
    /// rather than the face line that actually reaches OVK, misclassifying an opening whose
    /// neighbor in the very same wall block is correctly classified. Exterior/interior is decided
    /// by whether ANY of these candidates reaches OVK, not by which one the marker happens to sit
    /// closest to.
    /// Nested convention (marker inside a per-wall block, e.g. floor1-4's Wall_N_2): the host wall
    /// is a structural fact, not a spatial guess — it's whichever block directly contains the
    /// marker. Top-level convention (e.g. Archicad, where markers are annotation-only): no such
    /// structural link exists in the DXF (confirmed by investigation), so the opening's real
    /// geometry object — the actual window/door body, not its label — is located by proximity,
    /// transformed through its own INSERT, and matched to the wall graph at drafting precision.
    /// </summary>
    internal static List<(double x, double y)> FindHostWallNodes(DxfDocument doc, WallGraph graph, Insert markerInsert, Block? parentBlock)
        => parentBlock is not null
            ? FindHostWallNodesNested(doc, graph, parentBlock)
            : FindHostWallNodesTopLevel(doc, graph, markerInsert);

    private static List<(double x, double y)> FindHostWallNodesNested(DxfDocument doc, WallGraph graph, Block parentBlock)
    {
        var wallInsert = doc.Entities.Inserts.FirstOrDefault(i => i.Block.Name == parentBlock.Name);
        if (wallInsert is null) return [];

        var found = new List<(double x, double y)>();

        void Consider(double lx, double ly)
        {
            var (wx, wy) = TransformPoint(lx, ly, wallInsert);
            var key = Snap(wx / graph.Divisor, wy / graph.Divisor);
            if (graph.Adjacency.ContainsKey(key))
                found.Add(key);
        }

        foreach (var line in parentBlock.Entities.OfType<Line>().Where(l => IsWallLayer(l.Layer.Name)))
        {
            Consider(line.StartPoint.X, line.StartPoint.Y);
            Consider(line.EndPoint.X, line.EndPoint.Y);
        }
        foreach (var poly in parentBlock.Entities.OfType<Polyline2D>().Where(p => IsWallLayer(p.Layer.Name)))
            foreach (var v in poly.Vertexes)
                Consider(v.Position.X, v.Position.Y);

        return found;
    }

    private static List<(double x, double y)> FindHostWallNodesTopLevel(DxfDocument doc, WallGraph graph, Insert markerInsert)
    {
        Insert? companion = null;
        double companionDist = double.MaxValue;
        foreach (var candidate in doc.Entities.Inserts)
        {
            if (ReferenceEquals(candidate, markerInsert)) continue;
            if (!IsOpeningBodyLayer(candidate.Layer.Name)) continue;
            double d = Math.Sqrt(Math.Pow(candidate.Position.X - markerInsert.Position.X, 2) + Math.Pow(candidate.Position.Y - markerInsert.Position.Y, 2));
            if (d <= CompanionSearchRadiusM * graph.Divisor && d < companionDist) { companionDist = d; companion = candidate; }
        }
        if (companion is null) return [];

        var found = new List<(double x, double y)>();

        void Consider(double lx, double ly)
        {
            var (wx, wy) = TransformPoint(lx, ly, companion);
            var (nodeKey, nodeDist) = NearestGraphNode(graph, wx / graph.Divisor, wy / graph.Divisor);
            if (nodeKey is not null && nodeDist <= CompanionWallTouchToleranceM)
                found.Add(nodeKey.Value);
        }

        foreach (var line in companion.Block.Entities.OfType<Line>())
        {
            Consider(line.StartPoint.X, line.StartPoint.Y);
            Consider(line.EndPoint.X, line.EndPoint.Y);
        }
        foreach (var poly in companion.Block.Entities.OfType<Polyline2D>())
            foreach (var v in poly.Vertexes)
                Consider(v.Position.X, v.Position.Y);

        return found;
    }

    private static ((double x, double y)? node, double dist) NearestGraphNode(WallGraph graph, double x, double y)
    {
        (double x, double y)? best = null;
        double bestDist = double.MaxValue;
        foreach (var node in graph.Adjacency.Keys)
        {
            double d = Math.Sqrt(Math.Pow(node.x - x, 2) + Math.Pow(node.y - y, 2));
            if (d < bestDist) { bestDist = d; best = node; }
        }
        return (best, bestDist);
    }

    /// <summary>
    /// True only if the host wall's own geometry contributes at least one node that is itself
    /// OVK-coincident — i.e. this wall physically forms part of the exterior perimeter, not merely
    /// touches it through a connector or T-junction. Validated against real data (see
    /// docs/decisions.md, wall-topology investigation): every genuine facade wall in the sample set
    /// owns 6-7 of its own nodes on the OVK boundary; every connector/corridor wall that reaches OVK
    /// only via a foreign wall's node owns exactly zero — a clean binary split, not a threshold call.
    /// </summary>
    internal static bool HostWallOwnsOvkNode(List<(double x, double y)> hostNodes, WallGraph graph)
        => hostNodes.Any(n => graph.OvkNodes.Contains(n));

    internal sealed class WallBlockGeometry
    {
        public string Name = "";
        public readonly List<(double x, double y)> Nodes = new();
        public readonly List<((double x, double y) a, (double x, double y) b)> Segments = new();
    }

    /// <summary>
    /// Indexes every top-level-inserted block's own wall-layer geometry (nested convention: each
    /// Wall_N_2 block is inserted exactly once, with an identity transform — see decisions.md,
    /// 2026-08-04). Built once per document and reused across every opening in it.
    /// </summary>
    internal static Dictionary<string, WallBlockGeometry> BuildWallBlockIndex(DxfDocument doc, double coordDivisor)
    {
        var index = new Dictionary<string, WallBlockGeometry>();
        foreach (var insert in doc.Entities.Inserts)
        {
            if (index.ContainsKey(insert.Block.Name)) continue;
            var geom = new WallBlockGeometry { Name = insert.Block.Name };
            void AddSeg((double x, double y) rawA, (double x, double y) rawB)
            {
                var wa = TransformPoint(rawA.x, rawA.y, insert);
                var wb = TransformPoint(rawB.x, rawB.y, insert);
                var a = Snap(wa.x / coordDivisor, wa.y / coordDivisor);
                var b = Snap(wb.x / coordDivisor, wb.y / coordDivisor);
                if (a == b) return;
                geom.Nodes.Add(a); geom.Nodes.Add(b);
                geom.Segments.Add((a, b));
            }
            foreach (var l in insert.Block.Entities.OfType<Line>().Where(l => IsWallLayer(l.Layer.Name)))
                AddSeg((l.StartPoint.X, l.StartPoint.Y), (l.EndPoint.X, l.EndPoint.Y));
            foreach (var p in insert.Block.Entities.OfType<Polyline2D>().Where(p => IsWallLayer(p.Layer.Name)))
            {
                var verts = p.Vertexes.Select(v => (v.Position.X, v.Position.Y)).ToList();
                for (int i = 0; i < verts.Count - 1; i++) AddSeg(verts[i], verts[i + 1]);
            }
            if (geom.Segments.Count > 0) index[insert.Block.Name] = geom;
        }
        return index;
    }

    // Length-weighted average orientation of a block's own segments, mod 180 degrees (a wall's
    // direction has no forward/backward sense) — the standard doubled-angle trick so a mix of
    // "0 deg" and "179 deg" segments (same line, opposite winding) doesn't average to 90 deg.
    private static double BlockDominantAngleDeg(WallBlockGeometry geom)
    {
        double sumSin = 0, sumCos = 0;
        foreach (var (a, b) in geom.Segments)
        {
            double dx = b.x - a.x, dy = b.y - a.y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-9) continue;
            double theta = Math.Atan2(dy, dx);
            sumSin += len * Math.Sin(2 * theta);
            sumCos += len * Math.Cos(2 * theta);
        }
        if (Math.Abs(sumSin) < 1e-9 && Math.Abs(sumCos) < 1e-9) return 0.0;
        double deg = 0.5 * Math.Atan2(sumSin, sumCos) * 180.0 / Math.PI;
        return deg < 0 ? deg + 180.0 : deg;
    }

    private static double AngleDiffMod180(double a, double b)
    {
        double d = Math.Abs(a - b) % 180.0;
        return Math.Min(d, 180.0 - d);
    }

    internal sealed class WallRun
    {
        public readonly List<string> BlockNames = new();
        public readonly HashSet<(double x, double y)> Nodes = new();
        public bool OwnsGenuineOvkSegment;
    }

    /// <summary>
    /// Reconstructs the continuous physical wall a Wall_N_2 block belongs to: walks outward through
    /// every neighboring block that (a) shares an endpoint with the current block and (b) continues
    /// in roughly the same direction (within <see cref="MaxCollinearDeviationDeg"/> of the current
    /// block's own dominant orientation — the same corner-vs-continuation distinction
    /// <see cref="FindExteriorOvkPaths"/> already applies at the node level, applied here at the
    /// block level). Stops at a real ~90 degree turn or a dead end.
    /// Nested convention only: verified by direct investigation (docs/decisions.md) that the CAD
    /// exporter gives each short pier/segment between openings its own named block with no explicit
    /// link between them (no XDATA, extension dictionary, or reactor connects sibling blocks) — a
    /// wall run reconstructs that missing link from geometry (shared endpoints + collinearity) alone.
    /// "Owns a genuine OVK segment" requires an actual block in the run to have a segment whose BOTH
    /// endpoints sit within drafting tolerance of the SAME finite OVK edge — i.e. the run's own
    /// geometry runs along the boundary, not merely touches it at one corner (see docs/decisions.md:
    /// a corner touch alone does not distinguish a facade run from a connector/T-junction wall).
    /// </summary>
    internal static WallRun BuildWallRun(
        Dictionary<string, WallBlockGeometry> blockIndex, WallGraph graph,
        List<(double x1, double y1, double x2, double y2)> ovkEdges, string startBlockName)
    {
        var run = new WallRun();
        if (!blockIndex.TryGetValue(startBlockName, out var startGeom)) return run;

        var nodeOwners = new Dictionary<(double x, double y), List<string>>();
        foreach (var (name, geom) in blockIndex)
            foreach (var n in geom.Nodes)
            {
                if (!nodeOwners.TryGetValue(n, out var owners)) nodeOwners[n] = owners = new();
                owners.Add(name);
            }

        bool OwnsGenuine(WallBlockGeometry geom) => geom.Segments.Any(s =>
            ovkEdges.Any(e => DistancePointToSegment(s.a.x, s.a.y, e.x1, e.y1, e.x2, e.y2) <= OvkNodeToleranceM
                            && DistancePointToSegment(s.b.x, s.b.y, e.x1, e.y1, e.x2, e.y2) <= OvkNodeToleranceM));

        void AddBlockToRun(string name, WallBlockGeometry geom)
        {
            run.BlockNames.Add(name);
            foreach (var n in geom.Nodes) run.Nodes.Add(n);
            if (OwnsGenuine(geom)) run.OwnsGenuineOvkSegment = true;
        }

        var visited = new HashSet<string> { startBlockName };
        AddBlockToRun(startBlockName, startGeom);
        double startAngle = BlockDominantAngleDeg(startGeom);

        void WalkDirection(string first)
        {
            string? current = first;
            while (current is not null)
            {
                if (!visited.Add(current)) return;
                if (!blockIndex.TryGetValue(current, out var curGeom)) return;
                AddBlockToRun(current, curGeom);

                double curAngle = BlockDominantAngleDeg(curGeom);
                var next = curGeom.Nodes
                    .SelectMany(n => nodeOwners.TryGetValue(n, out var o) ? o : new List<string>())
                    .Distinct()
                    .Where(n => n != current && !visited.Contains(n) && blockIndex.ContainsKey(n))
                    .Where(n => AngleDiffMod180(BlockDominantAngleDeg(blockIndex[n]), curAngle) <= MaxCollinearDeviationDeg)
                    .ToList();
                current = next.Count > 0 ? next[0] : null;
            }
        }

        var initialDirections = startGeom.Nodes
            .SelectMany(n => nodeOwners.TryGetValue(n, out var o) ? o : new List<string>())
            .Distinct()
            .Where(n => n != startBlockName && blockIndex.ContainsKey(n))
            .Where(n => AngleDiffMod180(BlockDominantAngleDeg(blockIndex[n]), startAngle) <= MaxCollinearDeviationDeg)
            .Take(2); // a linear wall run has at most two continuations from any interior block

        foreach (var d in initialDirections)
            if (!visited.Contains(d)) WalkDirection(d);

        return run;
    }

    /// <summary>
    /// Finds every path — as a full sequence of wall-graph nodes, not just a single edge — from any
    /// of the opening's host-wall candidates to an OVK-coincident node, following the SAME wall run
    /// at each step (not the wall network at large). This is the distinction the classification
    /// actually needs: an interior partition typically joins its host exterior wall at a real
    /// corner (~90 degrees), so a network-wide hop search would wrongly credit it with the exterior
    /// wall it merely touches. Tracing only near-straight continuations (see
    /// <see cref="MaxCollinearDeviationDeg"/>) stops at that corner instead of crossing into a
    /// different wall's identity, while still tolerating the small kinks real (non-bug) wall runs
    /// have at drafting/party-wall seams.
    /// Returns every viable path, not just the first found: a touch point can have more than one
    /// direction the wall leaves it in (e.g. a mid-run touch has two), and the caller needs all of
    /// them to pick the OVK segment by plurality vote (see AssignOvkEdgeIndex) rather than by
    /// whichever one happened to be tried first.
    /// </summary>
    internal static List<List<(double x, double y)>> FindExteriorOvkPaths(List<(double x, double y)> hostNodes, WallGraph graph)
    {
        var results = new List<List<(double x, double y)>>();
        var seenStarts = new HashSet<(double x, double y)>();

        foreach (var start in hostNodes)
        {
            if (!seenStarts.Add(start)) continue;

            if (graph.OvkNodes.Contains(start))
                results.Add([start]);

            if (!graph.Adjacency.TryGetValue(start, out var neighbors)) continue;
            foreach (var first in neighbors)
            {
                if (graph.OvkNodes.Contains(first))
                {
                    results.Add([start, first]);
                    continue;
                }
                var path = TraceStraightRunToOvk(start, first, graph);
                if (path is not null) results.Add(path);
            }
        }

        return results;
    }

    private static List<(double x, double y)>? TraceStraightRunToOvk((double x, double y) prev, (double x, double y) cur, WallGraph graph)
    {
        var path = new List<(double x, double y)> { prev, cur };
        var visited = new HashSet<(double x, double y)> { prev, cur };
        double dirX = cur.x - prev.x, dirY = cur.y - prev.y;

        for (int step = 0; step < MaxTraceSteps; step++)
        {
            if (graph.OvkNodes.Contains(cur)) return path;
            if (!graph.Adjacency.TryGetValue(cur, out var neighbors)) return null;

            (double x, double y)? best = null;
            double bestAngle = double.MaxValue;
            foreach (var next in neighbors)
            {
                if (visited.Contains(next)) continue;
                double angle = AngleBetweenDeg(dirX, dirY, next.x - cur.x, next.y - cur.y);
                if (angle < bestAngle) { bestAngle = angle; best = next; }
            }
            if (best is null || bestAngle > MaxCollinearDeviationDeg) return null;

            visited.Add(best.Value);
            path.Add(best.Value);
            dirX = best.Value.x - cur.x;
            dirY = best.Value.y - cur.y;
            cur = best.Value;
        }
        return null;
    }

    private static double AngleBetweenDeg(double x1, double y1, double x2, double y2)
    {
        double dot = x1 * x2 + y1 * y2;
        double mag = Math.Sqrt(x1 * x1 + y1 * y1) * Math.Sqrt(x2 * x2 + y2 * y2);
        if (mag < 1e-9) return 180.0;
        double cos = Math.Clamp(dot / mag, -1.0, 1.0);
        return Math.Acos(cos) * 180.0 / Math.PI;
    }
}
