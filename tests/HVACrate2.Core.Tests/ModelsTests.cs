using HVACrate2.Core.Models;

namespace HVACrate2.Core.Tests;

/// <summary>Instantiation tests for the plain data-holder models — mainly to exercise field
/// initializers (e.g. ProjectConfig's hardcoded defaults) that otherwise never run.</summary>
public class ModelsTests
{
    [Test]
    public async Task ProjectConfig_HasExpectedDefaults()
    {
        var cfg = new ProjectConfig();

        await Assert.That(cfg.FloorRow).IsEqualTo(7);
        await Assert.That(cfg.WallRow).IsEqualTo(31);
        await Assert.That(cfg.FloorHeightM).IsEqualTo(2.89);
        await Assert.That(cfg.NorthDeg).IsEqualTo(0.0);
        await Assert.That(cfg.WallLayer).IsEqualTo("_A [walls]");
        await Assert.That(cfg.OvkLayer).IsEqualTo("OVK");
        await Assert.That(cfg.DxfPath).IsNotEmpty();
        await Assert.That(cfg.ExcelPath).IsNotEmpty();
    }

    [Test]
    public async Task FloorInput_PropertiesRoundTrip()
    {
        var input = new FloorInput { DxfPath = "a.dxf", HeightM = 2.5, NorthDeg = 45, ApartmentCount = 3 };

        await Assert.That(input.DxfPath).IsEqualTo("a.dxf");
        await Assert.That(input.HeightM).IsEqualTo(2.5);
        await Assert.That(input.NorthDeg).IsEqualTo(45);
        await Assert.That(input.ApartmentCount).IsEqualTo(3);
    }

    [Test]
    public async Task FloorResult_DefaultsToEmptyCollections()
    {
        var result = new FloorResult();

        await Assert.That(result.WallTotals).IsEmpty();
        await Assert.That(result.OpeningGroups).IsEmpty();
        await Assert.That(result.OvkVerticesM).IsEmpty();
        await Assert.That(result.Openings).IsEmpty();
        await Assert.That(result.OpeningDiagnostics).IsNotNull();
    }

    [Test]
    public async Task Opening_DefaultsToUnknownType()
    {
        var opening = new Opening();

        await Assert.That(opening.Type).IsEqualTo("Unknown");
        await Assert.That(opening.DimensionSource).IsEqualTo("unknown");
        await Assert.That(opening.Direction).IsEqualTo("");
        await Assert.That(opening.Evidence).IsEmpty();
    }

    [Test]
    public async Task OpeningExtractionDiagnostics_DefaultsToEmptyCollections()
    {
        var diagnostics = new OpeningExtractionDiagnostics();

        await Assert.That(diagnostics.CandidatesByStrategy).IsEmpty();
        await Assert.That(diagnostics.RejectionReasons).IsEmpty();
        await Assert.That(diagnostics.Warnings).IsEmpty();
        await Assert.That(diagnostics.EntitiesInspected).IsEqualTo(0);
    }

    [Test]
    public async Task HeatingRoomInput_PropertiesRoundTrip()
    {
        var input = new HeatingRoomInput
        {
            DeltaBetM = 0.01,
            DeltaZamPodM = 0.02,
            DeltaTerM = 0.03,
            DeltaIzolM = 0.04,
            DeltaPlochM = 0.05,
            DeltaZamTavM = 0.06,
            QptW = 123,
        };

        await Assert.That(input.DeltaBetM).IsEqualTo(0.01);
        await Assert.That(input.DeltaZamPodM).IsEqualTo(0.02);
        await Assert.That(input.DeltaTerM).IsEqualTo(0.03);
        await Assert.That(input.DeltaIzolM).IsEqualTo(0.04);
        await Assert.That(input.DeltaPlochM).IsEqualTo(0.05);
        await Assert.That(input.DeltaZamTavM).IsEqualTo(0.06);
        await Assert.That(input.QptW).IsEqualTo(123);
    }
}
