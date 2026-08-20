namespace HVACrate2.Core.Models;

public sealed class FloorResult
{
    public double AreaM2 { get; set; }
    public double VolumeM3 { get; set; }
    public int ConvexCorners { get; set; }
    public Dictionary<string, double> WallTotals { get; set; } = new();
    public Dictionary<(double Width, double Height), Dictionary<string, int>> OpeningGroups { get; set; } = new();

    public List<(double X, double Y)> OvkVerticesM { get; set; } = new();
    public List<Opening> Openings { get; set; } = new();

    public OpeningExtractionDiagnostics OpeningDiagnostics { get; set; } = new();
}
