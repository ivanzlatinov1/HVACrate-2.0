using HVACrate2.Core.Openings;

namespace HVACrate2.Core.Tests;

public class PerpendicularLabeledLineStrategyTests
{
    private static readonly List<(double x1, double y1, double x2, double y2)> RectOvk =
    [
        (0, 0, 10, 0),
        (10, 0, 10, 8),
        (10, 8, 0, 8),
        (0, 8, 0, 0),
    ];

    private static FlatEntity LineEntity(string layer, (double x, double y) a, (double x, double y) b)
        => new() { Kind = FlatKind.Line, LayerName = layer, PointsM = [a, b], Source = new object() };

    private static FlatEntity TextEntity(string text, (double x, double y) at)
        => new() { Kind = FlatKind.Text, LayerName = "Dim", PointsM = [at], Text = text, Source = new object() };

    private static readonly PerpendicularLabeledLineStrategy Strategy = new();

    [Test]
    public async Task Detect_PerpendicularLineWithLabelPair_ProducesCandidateAnchoredAtTip()
    {
        var entities = new List<FlatEntity>
        {
            LineEntity("Layer_XYZ", (0, 4), (-0.7, 4)),
            TextEntity("90", (-0.75, 3.95)),
            TextEntity("200", (-0.65, 3.95)),
        };

        var found = Strategy.Detect(new OpeningDetectionContext(entities, RectOvk));

        await Assert.That(found.Count).IsEqualTo(1);
        await Assert.That(found[0].AnchorM).IsEqualTo((0.0, 4.0));
        await Assert.That(found[0].WidthM!.Value).IsEqualTo(0.90).Within(0.001);
        await Assert.That(found[0].HeightM!.Value).IsEqualTo(2.00).Within(0.001);
        await Assert.That(found[0].DimensionSource).IsEqualTo("text-pair");
    }

    [Test]
    public async Task Detect_NoOvkEdges_ReturnsEmpty()
    {
        var entities = new List<FlatEntity>
        {
            LineEntity("L", (0, 4), (-0.7, 4)),
            TextEntity("90", (-0.75, 3.95)),
            TextEntity("200", (-0.65, 3.95)),
        };

        var found = Strategy.Detect(new OpeningDetectionContext(entities, []));

        await Assert.That(found).IsEmpty();
    }

    [Test]
    public async Task Detect_NoNumericText_ReturnsEmpty()
    {
        var entities = new List<FlatEntity> { LineEntity("L", (0, 4), (-0.7, 4)) };

        var found = Strategy.Detect(new OpeningDetectionContext(entities, RectOvk));

        await Assert.That(found).IsEmpty();
    }

    [Test]
    public async Task Detect_LineTooShort_IsSkipped()
    {
        var entities = new List<FlatEntity>
        {
            LineEntity("L", (0, 4), (-0.02, 4)),
            TextEntity("90", (-0.03, 3.98)),
            TextEntity("200", (-0.01, 3.98)),
        };

        var found = Strategy.Detect(new OpeningDetectionContext(entities, RectOvk));

        await Assert.That(found).IsEmpty();
    }

    [Test]
    public async Task Detect_LineTooLong_IsSkipped()
    {
        var entities = new List<FlatEntity>
        {
            LineEntity("L", (0, 4), (-2.0, 4)),
            TextEntity("90", (-2.05, 3.95)),
            TextEntity("200", (-1.95, 3.95)),
        };

        var found = Strategy.Detect(new OpeningDetectionContext(entities, RectOvk));

        await Assert.That(found).IsEmpty();
    }

    [Test]
    public async Task Detect_LineParallelToOvkEdge_IsSkipped()
    {
        var entities = new List<FlatEntity>
        {
            LineEntity("L", (0.3, 4), (0.3, 4.7)),
            TextEntity("90", (0.25, 4.72)),
            TextEntity("200", (0.35, 4.72)),
        };

        var found = Strategy.Detect(new OpeningDetectionContext(entities, RectOvk));

        await Assert.That(found).IsEmpty();
    }

    [Test]
    public async Task Detect_NonOpeningLayerHint_ExcludesLine()
    {
        var entities = new List<FlatEntity>
        {
            LineEntity("Furniture dimension", (0, 4), (-0.7, 4)),
            TextEntity("90", (-0.75, 3.95)),
            TextEntity("200", (-0.65, 3.95)),
        };

        var found = Strategy.Detect(new OpeningDetectionContext(entities, RectOvk));

        await Assert.That(found).IsEmpty();
    }

    [Test]
    public async Task Detect_LabelsTooFarApart_IsSkipped()
    {
        var entities = new List<FlatEntity>
        {
            LineEntity("L", (0, 4), (-0.7, 4)),
            TextEntity("90", (-0.7, 3.45)),
            TextEntity("200", (-0.65, 3.95)),
        };

        var found = Strategy.Detect(new OpeningDetectionContext(entities, RectOvk));

        await Assert.That(found).IsEmpty();
    }

    [Test]
    public async Task Detect_OnlyOneNearbyLabel_IsSkipped()
    {
        var entities = new List<FlatEntity>
        {
            LineEntity("L", (0, 4), (-0.7, 4)),
            TextEntity("90", (-0.75, 3.95)),
        };

        var found = Strategy.Detect(new OpeningDetectionContext(entities, RectOvk));

        await Assert.That(found).IsEmpty();
    }

    [Test]
    public async Task Detect_MultipleLinesSameLabelPair_KeepsOnlyTheOneClosestToOvk()
    {
        var entities = new List<FlatEntity>
        {
            LineEntity("L", (0, 4), (-0.7, 4)),
            LineEntity("L", (0.3, 4), (-0.4, 4)),
            TextEntity("90", (-0.75, 3.95)),
            TextEntity("200", (-0.65, 3.95)),
        };

        var found = Strategy.Detect(new OpeningDetectionContext(entities, RectOvk));

        await Assert.That(found.Count).IsEqualTo(1);
        await Assert.That(found[0].AnchorM).IsEqualTo((0.0, 4.0));
    }

    [Test]
    public async Task Detect_TipEndChosenAsFartherFromLabels()
    {
        var entities = new List<FlatEntity>
        {
            LineEntity("L", (-0.7, 4), (0, 4)),
            TextEntity("90", (-0.75, 3.95)),
            TextEntity("200", (-0.65, 3.95)),
        };

        var found = Strategy.Detect(new OpeningDetectionContext(entities, RectOvk));

        await Assert.That(found.Count).IsEqualTo(1);
        await Assert.That(found[0].AnchorM).IsEqualTo((0.0, 4.0));
    }

    [Test]
    public async Task Name_IsPerpendicularLabeledLine()
    {
        await Assert.That(Strategy.Name).IsEqualTo("PerpendicularLabeledLine");
    }
}
