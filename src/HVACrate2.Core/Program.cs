using System.Diagnostics.CodeAnalysis;
using HVACrate2.Core.Models;

namespace HVACrate2.Core;

// A manual console harness for local testing against real sample files/paths (see ProjectConfig's
// hardcoded defaults) — not part of the library's public contract exercised by the app or the test
// suite, and not meaningfully testable without real local sample/template files.
[ExcludeFromCodeCoverage]
public static class Program
{
    public static void Main(string[] args)
    {
        var cfg = new ProjectConfig();

        var input = new FloorInput
        {
            DxfPath = cfg.DxfPath,
            HeightM = cfg.FloorHeightM,
            NorthDeg = cfg.NorthDeg,
        };

        var result = FloorProcessor.ProcessFloor(input, cfg.OvkLayer);

        // Never overwrite the tracked blank template — write to a scratch copy instead.
        string scratchOutputPath = Path.Combine(Path.GetDirectoryName(cfg.ExcelPath)!, "console-test-output.xlsx");
        FloorProcessor.ProcessAndWriteFloors([input], cfg.ExcelPath, scratchOutputPath, cfg.OvkLayer);
        Console.WriteLine($"Wrote scratch output to: {scratchOutputPath}");

        Console.WriteLine("Outer walls length by direction (layered by the border of OVK polyline):");
        foreach (var (dir, len) in result.WallTotals)
            Console.WriteLine($"  {dir}: {len:F2} m");
        Console.WriteLine($"Area (Af): {result.AreaM2:F2} m2, Volume (V, h={input.HeightM}m): {result.VolumeM3:F2} m3, Outer Edges n={result.ConvexCorners}");

        Console.WriteLine("Windows/doors (width x height) -> count on each direction:");
        foreach (var kvp in result.OpeningGroups)
            Console.WriteLine($"  {kvp.Key.Width}m x {kvp.Key.Height}m -> {string.Join(", ", kvp.Value.Where(x => x.Value > 0).Select(x => $"{x.Key}={x.Value}"))}");
    }
}
