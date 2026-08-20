namespace HVACrate2.Core.Openings;

/// <summary>
/// Decides whether a candidate is exterior from three checks against its own anchor — none alone is
/// reliable:
/// 1. Direct distance to the OVK boundary *curve* (within <see cref="OpeningCandidate.ExteriorToleranceM"/>).
/// 2. Distance to the nearest actual wall-like segment (see <see cref="WallGeometryClassifier.CollectWallLikeSegments"/>)
///    — geometry already confirmed to lie on OVK at both endpoints, not just a lone corner touch.
/// 3. Whether an explicitly interior-labeled wall (see
///    <see cref="WallGeometryClassifier.CollectExplicitInteriorSegments"/>) sits closer to the
///    candidate than the nearest wall-like segment from check 2 — if so, that interior wall is almost
///    certainly the candidate's true host, regardless of how the distance checks alone came out.
/// Checks 1-2 alone still let one real case through (an interior partition door leading to a balcony,
/// flagged directly by the user): it passed the OVK-curve distance check (0.59m) and even found some
/// wall-like backing within tolerance (a nearby exterior wall's own corner geometry, not the wall the
/// door actually sits in), while its true host wall — confirmed on-layer as "Стени - интериор" — sat
/// right against it. Tightening the tolerances to exclude this case purely by distance was tried and
/// reverted (see docs/decisions.md): it broke more genuinely-correct matches than it fixed, because
/// the bad case (0.29m) and several good cases sit too close together in raw distance to separate with
/// a single threshold. Check 3 resolves it directly instead, using the one piece of evidence that
/// isn't ambiguous here — the wall's own name says what it is.
/// </summary>
internal static class ExteriorClassifier
{
    private const double WallBackingToleranceM = 0.5;

    public static void Classify(
        OpeningCandidate candidate,
        List<(double x1, double y1, double x2, double y2)> wallLikeSegments,
        List<(double x1, double y1, double x2, double y2)> explicitInteriorSegments,
        List<(double x1, double y1, double x2, double y2)> ovkEdges)
    {
        candidate.ExteriorDistanceM = GeometryUtils.DistancePointToOvk(candidate.AnchorM.x, candidate.AnchorM.y, ovkEdges, out int edgeIdx);
        bool nearOvkCurve = candidate.ExteriorDistanceM <= candidate.ExteriorToleranceM;

        double nearestWallLikeDist = wallLikeSegments.Count == 0
            ? double.MaxValue
            : wallLikeSegments.Min(s => GeometryUtils.DistancePointToSegment(candidate.AnchorM.x, candidate.AnchorM.y, s.x1, s.y1, s.x2, s.y2));
        bool hasWallBacking = nearestWallLikeDist <= WallBackingToleranceM;

        const double InteriorOverrideMarginM = 0.15;
        double nearestInteriorDist = explicitInteriorSegments.Count == 0
            ? double.MaxValue
            : explicitInteriorSegments.Min(s => GeometryUtils.DistancePointToSegment(candidate.AnchorM.x, candidate.AnchorM.y, s.x1, s.y1, s.x2, s.y2));
        bool closerToInteriorWall = nearestInteriorDist + InteriorOverrideMarginM < nearestWallLikeDist;

        candidate.OvkEdgeIndex = nearOvkCurve && hasWallBacking && !closerToInteriorWall ? edgeIdx : null;
    }
}
