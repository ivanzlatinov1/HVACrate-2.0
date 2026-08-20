namespace HVACrate2.Core.Openings;

/// <summary>
/// Merges candidates that different strategies independently found for the same physical opening —
/// judged by geometry (position/edge/dimensions), never by which strategy or name produced them, so
/// two agreeing strategies raise confidence in the final result instead of producing a duplicate row.
/// </summary>
internal static class OpeningDeduper
{
    private const double PositionToleranceM = 0.5;
    private const double DimensionTolerancePct = 0.15;
    private const double DimensionToleranceM = 0.10;

    public static List<OpeningCandidate> Dedupe(List<OpeningCandidate> candidates)
    {
        var merged = new List<OpeningCandidate>();
        foreach (var c in candidates.OrderBy(c => c.ExteriorDistanceM))
        {
            var match = merged.FirstOrDefault(m => IsSame(m, c));
            if (match is null)
            {
                merged.Add(c);
                continue;
            }
            match.Evidence.AddRange(c.Evidence);
            if (!match.StrategyName.Contains(c.StrategyName))
                match.StrategyName = $"{match.StrategyName}+{c.StrategyName}";
            match.TypeConfidenceDoor = Math.Max(match.TypeConfidenceDoor, c.TypeConfidenceDoor);
            match.TypeConfidenceWindow = Math.Max(match.TypeConfidenceWindow, c.TypeConfidenceWindow);
        }
        return merged;
    }

    private static bool IsSame(OpeningCandidate a, OpeningCandidate b)
    {
        if (a.OvkEdgeIndex != b.OvkEdgeIndex) return false;
        double posDist = GeometryUtils.Distance(a.AnchorM.x, a.AnchorM.y, b.AnchorM.x, b.AnchorM.y);
        if (posDist > PositionToleranceM) return false;
        if (a.WidthM is null || b.WidthM is null || a.HeightM is null || b.HeightM is null) return true;

        bool widthClose = Math.Abs(a.WidthM.Value - b.WidthM.Value) <= Math.Max(DimensionToleranceM, a.WidthM.Value * DimensionTolerancePct);
        bool heightClose = Math.Abs(a.HeightM.Value - b.HeightM.Value) <= Math.Max(DimensionToleranceM, a.HeightM.Value * DimensionTolerancePct);
        return widthClose && heightClose;
    }
}
