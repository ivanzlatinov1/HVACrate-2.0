namespace HVACrate2.Core.Models;

public sealed class Opening
{
    public double WidthM { get; set; }
    public double HeightM { get; set; }
    public string Direction { get; set; } = "";

    public double PositionXM { get; set; }
    public double PositionYM { get; set; }

    public string Type { get; set; } = "Unknown";
    public double Confidence { get; set; }
    public string DimensionSource { get; set; } = "unknown";
    public List<string> Evidence { get; set; } = new();
}