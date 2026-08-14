namespace HVACrate2.Core.Models;

/// <summary>
/// Reports what the opening extractor actually did for one floor, so an unrecognized DXF convention
/// shows up as a visible "0 found, low confidence" event instead of a silently-empty openings table.
/// </summary>
public sealed class OpeningExtractionDiagnostics
{
    public int EntitiesInspected { get; set; }
    public int WallLikePointsFound { get; set; }
    public Dictionary<string, int> CandidatesByStrategy { get; set; } = new();
    public int AcceptedCount { get; set; }
    public int RejectedCount { get; set; }
    public Dictionary<string, int> RejectionReasons { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
