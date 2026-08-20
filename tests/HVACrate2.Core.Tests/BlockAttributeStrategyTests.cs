using HVACrate2.Core.Openings;

namespace HVACrate2.Core.Tests;

public class BlockAttributeStrategyTests
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

    private static readonly OpeningDetectionContext EmptyOvkContext = new([], []);

    private OpeningDetectionContext Ctx(params FlatEntity[] entities) => new(entities.ToList(), []);

    [Test]
    public async Task Detect_TwoNumericAttributes_ProducesOneCandidate()
    {
        var entity = Insert("Layer_ABC", "Zorp_9", new() { ["FOO1"] = "80", ["FOO2"] = "210" });

        var found = new BlockAttributeStrategy().Detect(Ctx(entity));

        await Assert.That(found.Count).IsEqualTo(1);
        await Assert.That(found[0].WidthM!.Value).IsEqualTo(0.80).Within(0.001);
        await Assert.That(found[0].HeightM!.Value).IsEqualTo(2.10).Within(0.001);
        await Assert.That(found[0].DimensionSource).IsEqualTo("attribute");
    }

    [Test]
    public async Task Detect_FewerThanTwoNumericAttributes_SkipsEntity()
    {
        var entity = Insert("Layer", "Block", new() { ["ONLY"] = "80" });

        var found = new BlockAttributeStrategy().Detect(Ctx(entity));

        await Assert.That(found).IsEmpty();
    }

    [Test]
    public async Task Detect_AttributesOutOfPlausibleRange_AreIgnored()
    {
        var entity = Insert("Layer", "Block", new() { ["A"] = "9999", ["B"] = "1", ["C"] = "80" });

        var found = new BlockAttributeStrategy().Detect(Ctx(entity));

        await Assert.That(found).IsEmpty();
    }

    [Test]
    public async Task Detect_NonNumericAttribute_IsIgnored()
    {
        var entity = Insert("Layer", "Block", new() { ["A"] = "not-a-number", ["B"] = "80", ["C"] = "210" });

        var found = new BlockAttributeStrategy().Detect(Ctx(entity));

        await Assert.That(found.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Detect_NonOpeningLayerHint_ExcludesEntity()
    {
        var entity = Insert("Furniture dimension", "Block", new() { ["A"] = "80", ["B"] = "210" });

        var found = new BlockAttributeStrategy().Detect(Ctx(entity));

        await Assert.That(found).IsEmpty();
    }

    [Test]
    public async Task Detect_NonOpeningBlockNameHint_ExcludesEntity()
    {
        var entity = Insert("Layer", "Furniture_01", new() { ["A"] = "80", ["B"] = "210" });

        var found = new BlockAttributeStrategy().Detect(Ctx(entity));

        await Assert.That(found).IsEmpty();
    }

    [Test]
    public async Task Detect_WidthHeightAssignment_LargerBecomesHeight()
    {
        var entity = Insert("Layer", "Block", new() { ["A"] = "210", ["B"] = "80" });

        var found = new BlockAttributeStrategy().Detect(Ctx(entity));

        await Assert.That(found[0].WidthM!.Value).IsEqualTo(0.80).Within(0.001);
        await Assert.That(found[0].HeightM!.Value).IsEqualTo(2.10).Within(0.001);
    }

    [Test]
    public async Task Detect_MarkerNameHint_AddsEvidenceNote()
    {
        var entity = Insert("Any", "W Marker", new() { ["A"] = "80", ["B"] = "210" });

        var found = new BlockAttributeStrategy().Detect(Ctx(entity));

        await Assert.That(found[0].Evidence.Any(e => e.Contains("name hint matched"))).IsTrue();
    }

    [Test]
    public async Task Detect_NoNameHint_EvidenceHasNoHintNote()
    {
        var entity = Insert("Layer_ABC", "Zorp_9", new() { ["A"] = "80", ["B"] = "210" });

        var found = new BlockAttributeStrategy().Detect(Ctx(entity));

        await Assert.That(found[0].Evidence.Any(e => e.Contains("name hint matched"))).IsFalse();
    }

    [Test]
    public async Task Detect_MoreThanTwoNumericAttributes_AddsAmbiguityNote()
    {
        var entity = Insert("Layer", "Block", new() { ["A"] = "80", ["B"] = "210", ["C"] = "90" });

        var found = new BlockAttributeStrategy().Detect(Ctx(entity));

        await Assert.That(found.Count).IsEqualTo(1);
        await Assert.That(found[0].Evidence.Any(e => e.Contains("ambiguous"))).IsTrue();
    }

    [Test]
    public async Task Detect_NonInsertEntity_IsIgnored()
    {
        var line = new FlatEntity { Kind = FlatKind.Line, LayerName = "L", PointsM = [(0, 0), (1, 0)], Source = new object() };

        var found = new BlockAttributeStrategy().Detect(Ctx(line));

        await Assert.That(found).IsEmpty();
    }

    [Test]
    public async Task Detect_NoEntities_ReturnsEmpty()
    {
        var found = new BlockAttributeStrategy().Detect(EmptyOvkContext);
        await Assert.That(found).IsEmpty();
    }

    [Test]
    public async Task Name_IsBlockAttribute()
    {
        await Assert.That(new BlockAttributeStrategy().Name).IsEqualTo("BlockAttribute");
    }
}
