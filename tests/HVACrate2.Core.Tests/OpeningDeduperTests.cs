using HVACrate2.Core.Openings;

namespace HVACrate2.Core.Tests;

public class OpeningDeduperTests
{
    private static OpeningCandidate MakeCandidate(
        double x, double y, int? edgeIndex, double? width = 0.9, double? height = 2.1,
        string strategy = "StrategyA", double exteriorDistance = 0.1)
        => new()
        {
            AnchorM = (x, y),
            WidthM = width,
            HeightM = height,
            OvkEdgeIndex = edgeIndex,
            StrategyName = strategy,
            ExteriorDistanceM = exteriorDistance,
        };

    [Test]
    public async Task Dedupe_TwoCandidatesSamePositionAndEdge_MergeIntoOne()
    {
        var a = MakeCandidate(1.0, 1.0, edgeIndex: 0, strategy: "BlockAttribute");
        var b = MakeCandidate(1.05, 1.0, edgeIndex: 0, strategy: "PerpendicularLabeledLine");

        var merged = OpeningDeduper.Dedupe([a, b]);

        await Assert.That(merged.Count).IsEqualTo(1);
        await Assert.That(merged[0].StrategyName).Contains("BlockAttribute");
        await Assert.That(merged[0].StrategyName).Contains("PerpendicularLabeledLine");
    }

    [Test]
    public async Task Dedupe_DifferentOvkEdge_KeptSeparate()
    {
        var a = MakeCandidate(1.0, 1.0, edgeIndex: 0);
        var b = MakeCandidate(1.0, 1.0, edgeIndex: 1);

        var merged = OpeningDeduper.Dedupe([a, b]);

        await Assert.That(merged.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Dedupe_TooFarApart_KeptSeparate()
    {
        var a = MakeCandidate(0.0, 0.0, edgeIndex: 0);
        var b = MakeCandidate(5.0, 0.0, edgeIndex: 0);

        var merged = OpeningDeduper.Dedupe([a, b]);

        await Assert.That(merged.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Dedupe_DifferentDimensions_KeptSeparate()
    {
        var a = MakeCandidate(1.0, 1.0, edgeIndex: 0, width: 0.9, height: 2.1);
        var b = MakeCandidate(1.0, 1.0, edgeIndex: 0, width: 1.6, height: 2.5);

        var merged = OpeningDeduper.Dedupe([a, b]);

        await Assert.That(merged.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Dedupe_MissingDimensionOnEitherSide_StillMerges()
    {
        var a = MakeCandidate(1.0, 1.0, edgeIndex: 0, width: null, height: null);
        var b = MakeCandidate(1.0, 1.0, edgeIndex: 0, width: 0.9, height: 2.1);

        var merged = OpeningDeduper.Dedupe([a, b]);

        await Assert.That(merged.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Dedupe_SameStrategyTwice_DoesNotDuplicateStrategyName()
    {
        var a = MakeCandidate(1.0, 1.0, edgeIndex: 0, strategy: "BlockAttribute");
        var b = MakeCandidate(1.0, 1.0, edgeIndex: 0, strategy: "BlockAttribute");

        var merged = OpeningDeduper.Dedupe([a, b]);

        await Assert.That(merged.Count).IsEqualTo(1);
        await Assert.That(merged[0].StrategyName).IsEqualTo("BlockAttribute");
    }

    [Test]
    public async Task Dedupe_MergedCandidate_TakesMaxTypeConfidence()
    {
        var a = MakeCandidate(1.0, 1.0, edgeIndex: 0);
        a.TypeConfidenceDoor = 0.2;
        a.TypeConfidenceWindow = 0.9;
        var b = MakeCandidate(1.0, 1.0, edgeIndex: 0);
        b.TypeConfidenceDoor = 0.7;
        b.TypeConfidenceWindow = 0.1;

        var merged = OpeningDeduper.Dedupe([a, b]);

        await Assert.That(merged.Count).IsEqualTo(1);
        await Assert.That(merged[0].TypeConfidenceDoor).IsEqualTo(0.7);
        await Assert.That(merged[0].TypeConfidenceWindow).IsEqualTo(0.9);
    }

    [Test]
    public async Task Dedupe_EmptyInput_ReturnsEmpty()
    {
        var merged = OpeningDeduper.Dedupe([]);
        await Assert.That(merged).IsEmpty();
    }
}
