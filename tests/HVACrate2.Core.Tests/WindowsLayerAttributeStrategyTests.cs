using HVACrate2.Core.Openings;

namespace HVACrate2.Core.Tests;

public class WindowsLayerAttributeStrategyTests
{
    private static FlatEntity Insert(
        string layer, string block, Dictionary<string, string> attrs, (double x, double y)? at = null)
        => new()
        {
            Kind = FlatKind.Insert,
            LayerName = layer,
            BlockName = block,
            Attributes = attrs,
            PointsM = [at ?? (0.0, 0.0)],
            Source = new object(),
        };

    private OpeningDetectionContext Ctx(params FlatEntity[] entities) => new(entities.ToList(), []);

    [Test]
    public async Task Detect_TwoNumericAttributesOnWindowsLayer_ProducesOneCandidate()
    {
        var entity = Insert("WINDOWS", "W Marker 22", new() { ["AC_MarkerText_2"] = "80", ["AC_MarkerText_3"] = "210" });

        var found = new WindowsLayerAttributeStrategy().Detect(Ctx(entity));

        await Assert.That(found.Count).IsEqualTo(1);
        await Assert.That(found[0].DimensionSource).IsEqualTo("attribute");
    }

    [Test]
    public async Task Detect_KnownTagNames_AssignsWidthAndHeightByTagNotMagnitude()
    {
        // Real confirmed case (new block.dxf): AC_MarkerText_2=233 (width), AC_MarkerText_3=203
        // (height) — width is the LARGER number here. BlockAttributeStrategy's "larger value =
        // height" heuristic would wrongly swap this to width=203/height=233; the known tag names
        // must be used instead whenever they're present.
        var entity = Insert("WINDOWS", "W Marker 22", new() { ["AC_MarkerText_2"] = "233", ["AC_MarkerText_3"] = "203" });

        var found = new WindowsLayerAttributeStrategy().Detect(Ctx(entity));

        await Assert.That(found[0].WidthM!.Value).IsEqualTo(2.33).Within(0.001);
        await Assert.That(found[0].HeightM!.Value).IsEqualTo(2.03).Within(0.001);
    }

    [Test]
    public async Task Detect_UnknownTagNames_FallsBackToMagnitudeHeuristic()
    {
        var entity = Insert("WINDOWS", "Zorp_9", new() { ["FOO1"] = "210", ["FOO2"] = "80" });

        var found = new WindowsLayerAttributeStrategy().Detect(Ctx(entity));

        await Assert.That(found[0].WidthM!.Value).IsEqualTo(0.80).Within(0.001);
        await Assert.That(found[0].HeightM!.Value).IsEqualTo(2.10).Within(0.001);
    }

    [Test]
    public async Task Detect_LayerNameCaseInsensitive_AndArbitrarySuffix_StillMatches()
    {
        var entity = Insert("Windows-EXT-2", "W Marker", new() { ["AC_MarkerText_2"] = "80", ["AC_MarkerText_3"] = "210" });

        var found = new WindowsLayerAttributeStrategy().Detect(Ctx(entity));

        await Assert.That(found.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Detect_NonWindowsLayerInsert_IsIgnored()
    {
        // This convention is BlockAttributeStrategy's job when the marker isn't on a windows-prefixed layer.
        var entity = Insert("Layer_ABC", "W Marker", new() { ["AC_MarkerText_2"] = "80", ["AC_MarkerText_3"] = "210" });

        var found = new WindowsLayerAttributeStrategy().Detect(Ctx(entity));

        await Assert.That(found).IsEmpty();
    }

    [Test]
    public async Task Detect_FewerThanTwoNumericAttributes_SkipsEntity()
    {
        var entity = Insert("WINDOWS", "Block", new() { ["ONLY"] = "80" });

        var found = new WindowsLayerAttributeStrategy().Detect(Ctx(entity));

        await Assert.That(found).IsEmpty();
    }

    [Test]
    public async Task Detect_ValuesOutsidePlausibleRange_AreExcluded()
    {
        var entity = Insert("WINDOWS", "Block", new() { ["A"] = "9999", ["B"] = "1", ["C"] = "80" });

        var found = new WindowsLayerAttributeStrategy().Detect(Ctx(entity));

        await Assert.That(found).IsEmpty();
    }

    [Test]
    public async Task Detect_KnownTagValueOutOfRange_FallsBackToOtherNumericAttributes()
    {
        var entity = Insert("WINDOWS", "Block", new() { ["AC_MarkerText_2"] = "9999", ["AC_MarkerText_3"] = "203", ["OTHER"] = "80" });

        var found = new WindowsLayerAttributeStrategy().Detect(Ctx(entity));

        await Assert.That(found.Count).IsEqualTo(1);
        await Assert.That(found[0].WidthM!.Value).IsEqualTo(0.80).Within(0.001);
        await Assert.That(found[0].HeightM!.Value).IsEqualTo(2.03).Within(0.001);
    }

    [Test]
    public async Task Detect_NonOpeningLayerHint_ExcludesEntity()
    {
        var entity = Insert("Windows - Dimension", "Block", new() { ["A"] = "80", ["B"] = "210" });

        var found = new WindowsLayerAttributeStrategy().Detect(Ctx(entity));

        await Assert.That(found).IsEmpty();
    }

    [Test]
    public async Task Detect_NonInsertEntity_IsIgnored()
    {
        var line = new FlatEntity { Kind = FlatKind.Line, LayerName = "WINDOWS", PointsM = [(0, 0), (1, 0)], Source = new object() };

        var found = new WindowsLayerAttributeStrategy().Detect(Ctx(line));

        await Assert.That(found).IsEmpty();
    }

    [Test]
    public async Task Name_IsWindowsLayerAttribute()
    {
        await Assert.That(new WindowsLayerAttributeStrategy().Name).IsEqualTo("WindowsLayerAttribute");
    }
}
