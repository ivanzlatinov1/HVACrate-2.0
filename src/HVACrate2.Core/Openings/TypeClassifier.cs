namespace HVACrate2.Core.Openings;

/// <summary>
/// Best-effort door/window type scoring from corroborating geometry near an already-detected
/// candidate — never a requirement for detecting the opening itself. A nearby arc (a door swing) is
/// door evidence; multiple nearby frame-like lines with no swing arc is window evidence; a
/// recognizable word in the source layer/block name (any of the languages in <see cref="WordHints"/>)
/// nudges confidence further. Type has no effect on the Excel write (windows and doors share one
/// combined table per CLAUDE.md) — this only feeds the diagnostic/explainable record.
/// </summary>
internal static class TypeClassifier
{
    private const double SearchRadiusM = 2.5;

    public static void Classify(OpeningCandidate candidate, List<FlatEntity> entities)
    {
        bool nearArc = entities.Any(e => e.Kind == FlatKind.Arc &&
            GeometryUtils.Distance(candidate.AnchorM.x, candidate.AnchorM.y, e.PointsM[0].x, e.PointsM[0].y) <= e.RadiusM + SearchRadiusM);
        if (nearArc)
        {
            candidate.TypeConfidenceDoor += 0.4;
            candidate.Evidence.Add("Nearby arc suggests a door swing");
        }
        else
        {
            int nearbyLines = entities.Count(e => e.Kind == FlatKind.Line &&
                GeometryUtils.Distance(candidate.AnchorM.x, candidate.AnchorM.y, e.PointsM[0].x, e.PointsM[0].y) <= SearchRadiusM);
            if (nearbyLines >= 2)
            {
                candidate.TypeConfidenceWindow += 0.3;
                candidate.Evidence.Add("No swing arc nearby with multiple frame-like lines present — likely a window");
            }
        }

        if (WordHints.ContainsAny(candidate.SourceLayerHint, WordHints.Door))
        {
            candidate.TypeConfidenceDoor += 0.3;
            candidate.Evidence.Add("Source name hints at 'door'");
        }
        if (WordHints.ContainsAny(candidate.SourceLayerHint, WordHints.Window))
        {
            candidate.TypeConfidenceWindow += 0.3;
            candidate.Evidence.Add("Source name hints at 'window'");
        }

        candidate.TypeConfidenceDoor = Math.Clamp(candidate.TypeConfidenceDoor, 0.0, 1.0);
        candidate.TypeConfidenceWindow = Math.Clamp(candidate.TypeConfidenceWindow, 0.0, 1.0);
    }
}
