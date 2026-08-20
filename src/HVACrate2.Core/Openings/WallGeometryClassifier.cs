namespace HVACrate2.Core.Openings;

/// <summary>
/// Identifies "wall-like" geometry from proximity to OVK alone — no layer-name involvement. Measured
/// directly on a real sample (see docs/decisions.md): every genuine exterior wall segment's endpoints
/// land within 0.25m of OVK, while every interior wall segment is 1.57m+ away — a clean, name-
/// independent split.
/// Returns whole segments, not just their endpoint vertices — <see cref="ExteriorClassifier"/> needs
/// point-to-*segment* distance (a candidate can sit near the middle of a long wall run, far from
/// either endpoint) the same way distance to an OVK edge already works, not just point-to-vertex.
/// Requires BOTH endpoints of a segment to sit near OVK, not just one. A single near-OVK endpoint is
/// not enough: an interior partition wall that merely joins an exterior wall at a T-junction shares
/// that one corner point with it, so its near endpoint alone would satisfy a single-point check even
/// though the wall itself runs away from the boundary — exactly what let a door embedded in such an
/// interior wall (flagged directly by the user, a balcony connector) pass the wall-backing check in
/// <see cref="ExteriorClassifier"/> that this collection feeds, before this fix. Requiring both
/// endpoints near OVK is what the deleted WallTopology.cs's "owns a genuine OVK segment" concept
/// meant, reimplemented here per-segment instead of per-reconstructed-wall-run.
/// An earlier version also accepted a segment whose layer name merely hinted at "wall" in some
/// language — found to be a real bug too, since Bulgarian "стен" (wall) matches both
/// "Стени - екстериор" and "Стени - интериор" layers equally. Proximity to OVK is the only signal
/// here now.
/// </summary>
internal static class WallGeometryClassifier
{
    private const double NearOvkToleranceM = 0.4;

    public static List<(double x1, double y1, double x2, double y2)> CollectWallLikeSegments(
        List<FlatEntity> entities, List<(double x1, double y1, double x2, double y2)> ovkEdges)
    {
        var segments = new List<(double x1, double y1, double x2, double y2)>();
        foreach (var e in entities)
        {
            if (e.Kind != FlatKind.Line && e.Kind != FlatKind.Polyline2D) continue;
            for (int i = 0; i < e.PointsM.Count - 1; i++)
            {
                var (ax, ay) = e.PointsM[i];
                var (bx, by) = e.PointsM[i + 1];
                bool bothNearOvk =
                    GeometryUtils.DistancePointToOvk(ax, ay, ovkEdges, out _) <= NearOvkToleranceM &&
                    GeometryUtils.DistancePointToOvk(bx, by, ovkEdges, out _) <= NearOvkToleranceM;
                if (bothNearOvk)
                    segments.Add((ax, ay, bx, by));
            }
        }
        return segments;
    }

    /// <summary>
    /// Wall geometry whose layer explicitly says "interior" (in a language <see cref="WordHints.ExplicitInterior"/>
    /// recognizes) — unlike <see cref="CollectWallLikeSegments"/>, no OVK proximity requirement, since
    /// this is deliberately used the other way around: a candidate closer to a wall that says it's
    /// interior than to any confirmed exterior-forming wall is real evidence its true host wall is the
    /// interior one, even when raw distance alone would have let it pass. This is a targeted use of an
    /// explicit, unambiguous name as a negative override — not a general naming requirement for
    /// detection, and it does nothing when no such name is present in a file.
    /// </summary>
    public static List<(double x1, double y1, double x2, double y2)> CollectExplicitInteriorSegments(List<FlatEntity> entities)
    {
        var segments = new List<(double x1, double y1, double x2, double y2)>();
        foreach (var e in entities)
        {
            if (e.Kind != FlatKind.Line && e.Kind != FlatKind.Polyline2D) continue;
            if (!WordHints.ContainsAny(e.LayerName, WordHints.ExplicitInterior)) continue;
            for (int i = 0; i < e.PointsM.Count - 1; i++)
            {
                var (ax, ay) = e.PointsM[i];
                var (bx, by) = e.PointsM[i + 1];
                segments.Add((ax, ay, bx, by));
            }
        }
        return segments;
    }
}
