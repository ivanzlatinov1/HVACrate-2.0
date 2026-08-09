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
