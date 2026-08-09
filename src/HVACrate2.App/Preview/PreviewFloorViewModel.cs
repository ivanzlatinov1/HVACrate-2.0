using HVACrate2.Core.Models;

namespace HVACrate2.App.Preview;

/// <summary>Read-only display wrapper around one floor's already-computed <see cref="FloorResult"/>, for the review page — no logic, just formatting.</summary>
public sealed class PreviewFloorViewModel
{
    public required int FloorNumber { get; init; }
    public required FloorResult Result { get; init; }
    public required double NorthDeg { get; init; }

    public string AreaText => $"{Result.AreaM2:0.##} m²";
    public string VolumeText => $"{Result.VolumeM3:0.##} m³";
    public string PerimeterText => $"{Result.WallTotals.Values.Sum():0.##} m";
    public string CornersText => Result.ConvexCorners.ToString();

    public List<string> WallLines =>
        Result.WallTotals.Where(kv => kv.Value > 0)
            .Select(kv => $"{kv.Key}: {kv.Value:0.##} m").ToList();

    public List<string> OpeningLines =>
        Result.OpeningGroups.OrderBy(g => g.Key.Width).ThenBy(g => g.Key.Height)
            .Select(g => (Size: g.Key, ByDir: g.Value.Where(d => d.Value > 0).ToList()))
            .Where(g => g.ByDir.Count > 0)
            .Select(g => $"{g.Size.Width:0.##}×{g.Size.Height:0.##} m — {string.Join(", ", g.ByDir.Select(d => $"{d.Key}: {d.Value}"))}")
            .ToList();
}
