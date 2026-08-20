using HVACrate2.Core.Openings;

namespace HVACrate2.Core.Tests;

public class TypeClassifierTests
{
    private static FlatEntity Arc(double cx, double cy, double radius)
        => new() { Kind = FlatKind.Arc, LayerName = "L", PointsM = [(cx, cy)], RadiusM = radius, Source = new object() };

    private static FlatEntity Line(double x, double y)
        => new() { Kind = FlatKind.Line, LayerName = "L", PointsM = [(x, y), (x + 1, y)], Source = new object() };

    private static OpeningCandidate Candidate(double x = 0, double y = 0, string sourceHint = "")
        => new() { AnchorM = (x, y), SourceLayerHint = sourceHint };

    [Test]
    public async Task Classify_NearbyArc_RaisesDoorConfidence()
    {
        var candidate = Candidate();
        var entities = new List<FlatEntity> { Arc(0.5, 0, 0.8) };

        TypeClassifier.Classify(candidate, entities);

        await Assert.That(candidate.TypeConfidenceDoor).IsGreaterThan(0.0);
        await Assert.That(candidate.Evidence.Any(e => e.Contains("door swing"))).IsTrue();
    }

    [Test]
    public async Task Classify_NoArcButMultipleNearbyLines_RaisesWindowConfidence()
    {
        var candidate = Candidate();
        var entities = new List<FlatEntity> { Line(0.1, 0), Line(0.2, 0) };

        TypeClassifier.Classify(candidate, entities);

        await Assert.That(candidate.TypeConfidenceWindow).IsGreaterThan(0.0);
        await Assert.That(candidate.TypeConfidenceDoor).IsEqualTo(0.0);
    }

    [Test]
    public async Task Classify_NoArcAndOnlyOneNearbyLine_NoWindowBoost()
    {
        var candidate = Candidate();
        var entities = new List<FlatEntity> { Line(0.1, 0) };

        TypeClassifier.Classify(candidate, entities);

        await Assert.That(candidate.TypeConfidenceWindow).IsEqualTo(0.0);
        await Assert.That(candidate.TypeConfidenceDoor).IsEqualTo(0.0);
    }

    [Test]
    public async Task Classify_LayerHintMentionsDoor_RaisesDoorConfidence()
    {
        var candidate = Candidate(sourceHint: "Door Marker layer");

        TypeClassifier.Classify(candidate, []);

        await Assert.That(candidate.TypeConfidenceDoor).IsGreaterThan(0.0);
        await Assert.That(candidate.Evidence.Any(e => e.Contains("'door'"))).IsTrue();
    }

    [Test]
    public async Task Classify_LayerHintMentionsWindow_RaisesWindowConfidence()
    {
        var candidate = Candidate(sourceHint: "Window Marker layer");

        TypeClassifier.Classify(candidate, []);

        await Assert.That(candidate.TypeConfidenceWindow).IsGreaterThan(0.0);
        await Assert.That(candidate.Evidence.Any(e => e.Contains("'window'"))).IsTrue();
    }

    [Test]
    public async Task Classify_ArcAndNameHint_ConfidenceClampedToOne()
    {
        var candidate = Candidate(sourceHint: "Door Marker");
        candidate.TypeConfidenceDoor = 0.9;
        var entities = new List<FlatEntity> { Arc(0, 0, 0.5) };

        TypeClassifier.Classify(candidate, entities);

        await Assert.That(candidate.TypeConfidenceDoor).IsEqualTo(1.0);
    }

    [Test]
    public async Task Classify_NoEvidenceAtAll_LeavesConfidenceZero()
    {
        var candidate = Candidate();

        TypeClassifier.Classify(candidate, []);

        await Assert.That(candidate.TypeConfidenceDoor).IsEqualTo(0.0);
        await Assert.That(candidate.TypeConfidenceWindow).IsEqualTo(0.0);
    }
}
