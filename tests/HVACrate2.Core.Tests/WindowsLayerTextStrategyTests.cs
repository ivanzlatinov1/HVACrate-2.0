using HVACrate2.Core.Openings;

namespace HVACrate2.Core.Tests;

public class WindowsLayerTextStrategyTests
{
    private static FlatEntity TextEntity(string layer, string text, (double x, double y)? at = null)
        => new()
        {
            Kind = FlatKind.Text,
            LayerName = layer,
            Text = text,
            PointsM = [at ?? (0.0, 0.0)],
            Source = new object(),
        };

    private OpeningDetectionContext Ctx(params FlatEntity[] entities) => new(entities.ToList(), []);

    [Test]
    public async Task Detect_TwoNumberLabelOnWindowsLayer_ProducesOneCandidate()
    {
        var entity = TextEntity("WINDOWS", "150\n300");

        var found = new WindowsLayerTextStrategy().Detect(Ctx(entity));

        await Assert.That(found.Count).IsEqualTo(1);
        await Assert.That(found[0].DimensionSource).IsEqualTo("windows-layer");
    }

    [Test]
    public async Task Detect_WidthThenHeightOrder_IsNeverSwapped()
    {
        // A real confirmed case (new_floor_1.dxf): 270cm wide x 200cm tall door — width is the LARGER
        // number here. The old BlockAttributeStrategy "larger value = height" rule would wrongly swap
        // this to width=200/height=270; WindowsLayerTextStrategy must preserve the label's own order instead.
        var entity = TextEntity("WINDOWS", "270\n200");

        var found = new WindowsLayerTextStrategy().Detect(Ctx(entity));

        await Assert.That(found[0].WidthM!.Value).IsEqualTo(2.70).Within(0.001);
        await Assert.That(found[0].HeightM!.Value).IsEqualTo(2.00).Within(0.001);
    }

    [Test]
    public async Task Detect_LayerNameCaseInsensitive_AndArbitrarySuffix_StillMatches()
    {
        var entity = TextEntity("Windows-EXT-2", "80\n210");

        var found = new WindowsLayerTextStrategy().Detect(Ctx(entity));

        await Assert.That(found.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Detect_NonWindowsLayer_IsIgnored()
    {
        var entity = TextEntity("РАЗМЕРИ", "80\n210");

        var found = new WindowsLayerTextStrategy().Detect(Ctx(entity));

        await Assert.That(found).IsEmpty();
    }

    [Test]
    public async Task Detect_SingleNumberLabel_IsSkipped()
    {
        // Real sill-height annotations on the same layer, e.g. "Нпп=50" (parses to one number after
        // the strategy strips non-numeric tokens) — must not be mistaken for a dimension pair.
        var entity = TextEntity("WINDOWS", "50");

        var found = new WindowsLayerTextStrategy().Detect(Ctx(entity));

        await Assert.That(found).IsEmpty();
    }

    [Test]
    public async Task Detect_NonNumericLabel_IsSkipped()
    {
        var entity = TextEntity("WINDOWS", "П-1");

        var found = new WindowsLayerTextStrategy().Detect(Ctx(entity));

        await Assert.That(found).IsEmpty();
    }

    [Test]
    public async Task Detect_ThreeNumberLabel_IsSkippedAsAmbiguous()
    {
        var entity = TextEntity("WINDOWS", "80\n210\n50");

        var found = new WindowsLayerTextStrategy().Detect(Ctx(entity));

        await Assert.That(found).IsEmpty();
    }

    [Test]
    public async Task Detect_ValuesOutsidePlausibleRange_AreExcluded()
    {
        var entity = TextEntity("WINDOWS", "5\n999");

        var found = new WindowsLayerTextStrategy().Detect(Ctx(entity));

        await Assert.That(found).IsEmpty();
    }

    [Test]
    public async Task Detect_AnchorIsTheLabelsOwnPosition()
    {
        var entity = TextEntity("WINDOWS", "80\n210", at: (12.5, 34.0));

        var found = new WindowsLayerTextStrategy().Detect(Ctx(entity));

        await Assert.That(found[0].AnchorM.x).IsEqualTo(12.5).Within(0.001);
        await Assert.That(found[0].AnchorM.y).IsEqualTo(34.0).Within(0.001);
    }

    [Test]
    public async Task HasWindowsLayer_DetectsPresenceCaseInsensitively()
    {
        var entities = new List<FlatEntity> { TextEntity("WINDOWS", "80\n210") };
        await Assert.That(WindowsLayerTextStrategy.HasWindowsLayer(entities)).IsTrue();
    }

    [Test]
    public async Task HasWindowsLayer_NoMatchingLayer_ReturnsFalse()
    {
        var entities = new List<FlatEntity> { TextEntity("РАЗМЕРИ", "80\n210") };
        await Assert.That(WindowsLayerTextStrategy.HasWindowsLayer(entities)).IsFalse();
    }

    [Test]
    public async Task Name_IsWindowsLayer()
    {
        await Assert.That(new WindowsLayerTextStrategy().Name).IsEqualTo("WindowsLayer");
    }
}
