namespace HVACrate2.Core.Models;

public sealed class Opening
{
    public double WidthM { get; set; }
    public double HeightM { get; set; }
    public string Direction { get; set; } = "";

    // Marker world position (meters), for the 2D preview only — not used by classification or the
    // Excel write, which both work off the grouped/aggregated counts.
    public double PositionXM { get; set; }
    public double PositionYM { get; set; }

    // Best-effort, explainable metadata from the extraction pipeline — additive, not required by
    // the Excel write or the existing grouping/preview logic, which only read the fields above.
    public string Type { get; set; } = "Unknown";
    public double Confidence { get; set; }
    public string DimensionSource { get; set; } = "unknown";
    public List<string> Evidence { get; set; } = new();
}