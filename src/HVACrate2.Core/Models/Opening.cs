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
}