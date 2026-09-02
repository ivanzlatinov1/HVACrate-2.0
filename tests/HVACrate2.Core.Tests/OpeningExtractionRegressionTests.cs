using HVACrate2.Core;
using HVACrate2.Core.Models;
using HVACrate2.Core.Openings;

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
            return;

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

    /// <summary>
    /// The two samples that carry the new "Windows" layer convention — <see cref="OpeningExtractor"/>
    /// must take the primary-when-present path for these, not the legacy geometric strategies.
    /// </summary>
    [Test]
    [Arguments("new_floor_1.dxf")]
    [Arguments("new_floor_2.dxf")]
    public async Task RealSample_WithWindowsLayer_UsesWindowsLayerStrategies(string fileName)
    {
        string path = Path.Combine(SamplesDir, fileName);
        if (!File.Exists(path))
            return;

        var input = new FloorInput { DxfPath = path, HeightM = 2.8, NorthDeg = 0.0, ApartmentCount = 1 };
        var result = FloorProcessor.ProcessFloor(input, "OVK");

        await Assert.That(result.OpeningDiagnostics.UsedWindowsLayer).IsTrue();
        await Assert.That(result.OpeningDiagnostics.CandidatesByStrategy.ContainsKey("WindowsLayer")).IsTrue();
        await Assert.That(result.Openings.Count).IsGreaterThan(0);
        foreach (var opening in result.Openings)
        {
            await Assert.That(opening.DimensionSource).IsEqualTo("windows-layer");
            await Assert.That(opening.WidthM).IsGreaterThan(0.0);
            await Assert.That(opening.HeightM).IsGreaterThan(0.0);
            await Assert.That(opening.Direction).IsNotEqualTo("");
        }
    }

    /// <summary>
    /// The real DXF that surfaced this bug: window markers use the *older* INSERT+ATTRIB convention
    /// (block `W Marker NN` + `AC_MarkerText_2`/`AC_MarkerText_3` ATTRIB tags), but the marker's own
    /// INSERT sits on a layer literally named "Windows" — coincidentally matching the newer
    /// bare-TEXT-label convention's trigger name. Before <see cref="WindowsLayerAttributeStrategy"/>
    /// existed, this produced 0 openings (see docs/decisions.md). Not committed — <c>samples/</c> is
    /// gitignored/local-only, per project convention.
    /// </summary>
    [Test]
    public async Task RealSample_WindowsLayerWithAttributeMarkerConvention_ExtractsViaAttributeStrategy()
    {
        string path = Path.Combine(SamplesDir, "new_block_attribute_windows_layer.dxf");
        if (!File.Exists(path))
            return;

        var input = new FloorInput { DxfPath = path, HeightM = 2.8, NorthDeg = 0.0, ApartmentCount = 1 };
        var result = FloorProcessor.ProcessFloor(input, "OVK");

        await Assert.That(result.OpeningDiagnostics.UsedWindowsLayer).IsTrue();
        await Assert.That(result.OpeningDiagnostics.CandidatesByStrategy.ContainsKey("WindowsLayerAttribute")).IsTrue();
        await Assert.That(result.Openings.Count).IsGreaterThan(0);
        // Confirmed real marker: AC_MarkerText_2=233 (width), AC_MarkerText_3=203 (height) — width
        // is the larger number, so this also locks in the tag-name-based (not magnitude-based)
        // width/height assignment against real data.
        await Assert.That(result.Openings.Any(o => Math.Abs(o.WidthM - 2.33) < 0.005 && Math.Abs(o.HeightM - 2.03) < 0.005)).IsTrue();
        foreach (var opening in result.Openings)
        {
            await Assert.That(opening.DimensionSource).IsEqualTo("attribute");
            await Assert.That(opening.WidthM).IsGreaterThan(0.0);
            await Assert.That(opening.HeightM).IsGreaterThan(0.0);
            await Assert.That(opening.Direction).IsNotEqualTo("");
        }
    }
}
