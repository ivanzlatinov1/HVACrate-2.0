using System.Globalization;

namespace HVACrate2.Core.Openings;

internal static class TextNumberParsing
{
    public static bool TryParseNumber(string text, out double value)
        => double.TryParse(text.Trim().Replace(",", "."), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}

internal static class DimensionRange
{
    public const double MinCm = 10.0;
    public const double MaxCm = 500.0;
}

/// <summary>A candidate opening produced by one detection strategy, carrying an evidence trail — merged/scored/filtered by <see cref="OpeningExtractor"/> before becoming a public <see cref="Models.Opening"/>.</summary>
internal sealed class OpeningCandidate
{
    public (double x, double y) AnchorM { get; set; }
    public double? WidthM { get; set; }
    public double? HeightM { get; set; }

    /// <summary>Where the width/height came from: "attribute" (INSERT ATTRIB text), "text-pair" (nearby dimension labels), or "unknown".</summary>
    public string DimensionSource { get; set; } = "unknown";

    public string StrategyName { get; set; } = "";

    /// <summary>Layer/block name of the source entity, kept only as a confidence hint for type classification — never required for detection.</summary>
    public string SourceLayerHint { get; set; } = "";

    public List<string> Evidence { get; } = [];

    public double TypeConfidenceDoor { get; set; }
    public double TypeConfidenceWindow { get; set; }

    /// <summary>
    /// How far the candidate's own anchor may sit from OVK and still count as exterior. Each
    /// strategy sets this based on how precisely its anchor is expected to land on the real wall —
    /// e.g. tight for a leader-line tip already confirmed to sit at the wall, looser for a detached
    /// annotation block whose real body might not coincide with its label position.
    /// </summary>
    public double ExteriorToleranceM { get; set; } = 0.6;

    /// <summary>Distance (meters) from the candidate's anchor to the nearest OVK edge — set by <see cref="ExteriorClassifier"/>.</summary>
    public double ExteriorDistanceM { get; set; } = double.MaxValue;

    /// <summary>Which OVK edge this candidate faces — null if it wasn't classified as exterior (too far from OVK).</summary>
    public int? OvkEdgeIndex { get; set; }
}

internal sealed record OpeningDetectionContext(
    List<FlatEntity> Entities,
    List<(double x1, double y1, double x2, double y2)> OvkEdges);

internal interface IOpeningCandidateStrategy
{
    string Name { get; }
    List<OpeningCandidate> Detect(OpeningDetectionContext ctx);
}
