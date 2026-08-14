using HVACrate2.Core;
using HVACrate2.Core.Models;

namespace HVACrate2.Core.Tests;

/// <summary>
/// Runs the real, unmodified extraction pipeline against the user's actual sample floors. Skips
/// (does not fail) when a sample file isn't present on disk — <c>samples/</c> is gitignored and
/// local-only by project convention, so these files won't exist in every checkout/CI run.
/// </summary>
public class OpeningExtractionRegressionTests
{
    private static readonly string SamplesDir = Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples");

    [Test]
    [Arguments("floor1.dxf")]
    [Arguments("floor2.dxf")]
    [Arguments("floor3.dxf")]
    public async Task RealSample_FindsExteriorOpenings(string fileName)
    {
        string path = Path.Combine(SamplesDir, fileName);
        if (!File.Exists(path))
            return; // not present in this checkout — nothing to regress against

        var input = new FloorInput { DxfPath = path, HeightM = 2.89, NorthDeg = 0.0, ApartmentCount = 1 };
        var result = FloorProcessor.ProcessFloor(input, "OVK");

        await Assert.That(result.Openings.Count).IsGreaterThan(0);
        await Assert.That(result.OpeningDiagnostics.Warnings.Count).IsEqualTo(0);
        foreach (var opening in result.Openings)
        {
            await Assert.That(opening.WidthM).IsGreaterThan(0.0);
            await Assert.That(opening.HeightM).IsGreaterThan(0.0);
            await Assert.That(opening.Direction).IsNotEqualTo("");
        }
    }
}
