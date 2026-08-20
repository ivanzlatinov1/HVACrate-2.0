using HVACrate2.Core.Models;
using netDxf;

namespace HVACrate2.Core.Openings;

/// <summary>
/// Orchestrates the full name-agnostic opening-extraction pipeline: normalize entities → find
/// wall-like geometry → run independent candidate strategies → classify exterior/interior + type →
/// validate → deduplicate → produce the public <see cref="Opening"/> list plus a diagnostics report.
/// See docs/decisions.md for why this replaced the earlier name-gated (W Marker/D Marker) approach.
/// </summary>
internal static class OpeningExtractor
{
    private const double MinOpeningM = 0.4;
    private const double MaxOpeningM = 3.5;

    private static readonly IOpeningCandidateStrategy[] Strategies =
    [
        new BlockAttributeStrategy(),
        new PerpendicularLabeledLineStrategy(),
    ];

    public static (List<Opening> Openings, OpeningExtractionDiagnostics Diagnostics) Extract(
        DxfDocument doc, List<(double x1, double y1, double x2, double y2)> ovkEdges,
        double northDeg, double ccwSign, double coordDivisor)
    {
        var diagnostics = new OpeningExtractionDiagnostics();

        var entities = DxfEntityIndex.Build(doc, coordDivisor);
        diagnostics.EntitiesInspected = entities.Count;

        var wallLikeSegments = WallGeometryClassifier.CollectWallLikeSegments(entities, ovkEdges);
        var explicitInteriorSegments = WallGeometryClassifier.CollectExplicitInteriorSegments(entities);
        diagnostics.WallLikePointsFound = wallLikeSegments.Count;

        var context = new OpeningDetectionContext(entities, ovkEdges);
        var candidates = new List<OpeningCandidate>();
        foreach (var strategy in Strategies)
        {
            var found = strategy.Detect(context);
            diagnostics.CandidatesByStrategy[strategy.Name] = found.Count;
            candidates.AddRange(found);
        }

        foreach (var candidate in candidates)
        {
            ExteriorClassifier.Classify(candidate, wallLikeSegments, explicitInteriorSegments, ovkEdges);
            TypeClassifier.Classify(candidate, entities);
        }

        var accepted = new List<OpeningCandidate>();
        foreach (var candidate in candidates)
        {
            string? rejectReason = Validate(candidate);
            if (rejectReason is not null)
            {
                diagnostics.RejectedCount++;
                diagnostics.RejectionReasons[rejectReason] = diagnostics.RejectionReasons.GetValueOrDefault(rejectReason) + 1;
                continue;
            }
            accepted.Add(candidate);
        }

        var deduped = OpeningDeduper.Dedupe(accepted);

        var openings = deduped.Select(c => ToOpening(c, ovkEdges, northDeg, ccwSign)).ToList();
        diagnostics.AcceptedCount = openings.Count;

        if (openings.Count == 0)
        {
            diagnostics.Warnings.Add(wallLikeSegments.Count > 0
                ? "0 openings detected — extraction confidence low; no recognized opening geometry/relationship found despite wall geometry being present near the OVK boundary."
                : "0 openings detected, and no wall-like geometry was found near the OVK boundary either — check that wall geometry exists close to the traced OVK outline in this file.");
        }

        return (openings, diagnostics);
    }

    private static string? Validate(OpeningCandidate c)
    {
        if (c.OvkEdgeIndex is null) return "not near the exterior boundary (OVK)";
        if (c.WidthM is null || c.HeightM is null) return "no dimension found";
        if (c.WidthM < MinOpeningM || c.WidthM > MaxOpeningM) return "width outside plausible range";
        if (c.HeightM < MinOpeningM || c.HeightM > MaxOpeningM) return "height outside plausible range";
        return null;
    }

    private static Opening ToOpening(
        OpeningCandidate c, List<(double x1, double y1, double x2, double y2)> ovkEdges, double northDeg, double ccwSign)
    {
        var (ex1, ey1, ex2, ey2) = ovkEdges[c.OvkEdgeIndex!.Value];
        string direction = FloorProcessor.EdgeOutwardDirection(ex1, ey1, ex2, ey2, northDeg, ccwSign);

        string type = c.TypeConfidenceDoor > c.TypeConfidenceWindow && c.TypeConfidenceDoor >= 0.3 ? "Door"
            : c.TypeConfidenceWindow > c.TypeConfidenceDoor && c.TypeConfidenceWindow >= 0.3 ? "Window"
            : "Unknown";

        return new Opening
        {
            WidthM = c.WidthM!.Value,
            HeightM = c.HeightM!.Value,
            Direction = direction,
            PositionXM = c.AnchorM.x,
            PositionYM = c.AnchorM.y,
            Type = type,
            Confidence = Math.Round(Confidence(c), 2),
            DimensionSource = c.DimensionSource,
            Evidence = c.Evidence,
        };
    }

    private static double Confidence(OpeningCandidate c)
    {
        double exteriorScore = Math.Clamp(1.0 - c.ExteriorDistanceM / 0.4, 0.3, 1.0);
        double dimensionScore = c.DimensionSource switch { "attribute" => 0.9, "text-pair" => 0.8, _ => 0.4 };
        double typeScore = 0.5 + Math.Max(c.TypeConfidenceDoor, c.TypeConfidenceWindow) * 0.5;
        return (exteriorScore + dimensionScore + typeScore) / 3.0;
    }
}
