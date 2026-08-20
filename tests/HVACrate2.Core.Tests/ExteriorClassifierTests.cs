using HVACrate2.Core.Openings;

namespace HVACrate2.Core.Tests;

public class ExteriorClassifierTests
{
    private static readonly List<(double x1, double y1, double x2, double y2)> SquareOvk =
    [
        (0, 0, 10, 0),
        (10, 0, 10, 10),
        (10, 10, 0, 10),
        (0, 10, 0, 0),
    ];

    private static OpeningCandidate Candidate(double x, double y, double toleranceM = 0.6)
        => new() { AnchorM = (x, y), ExteriorToleranceM = toleranceM };

    [Test]
    public async Task Classify_NearOvkWithWallBacking_AssignsEdgeIndex()
    {
        var candidate = Candidate(0.1, 5.0);
        var wallLike = new List<(double x1, double y1, double x2, double y2)> { (0.0, 4.0, 0.0, 6.0) };

        ExteriorClassifier.Classify(candidate, wallLike, [], SquareOvk);

        await Assert.That(candidate.OvkEdgeIndex).IsEqualTo(3);
    }

    [Test]
    public async Task Classify_FarFromOvkCurve_IsRejectedEvenWithWallBacking()
    {
        var candidate = Candidate(5.0, 5.0);
        var wallLike = new List<(double x1, double y1, double x2, double y2)> { (4.5, 5.0, 5.5, 5.0) };

        ExteriorClassifier.Classify(candidate, wallLike, [], SquareOvk);

        await Assert.That(candidate.OvkEdgeIndex).IsNull();
    }

    [Test]
    public async Task Classify_NearOvkButNoWallBacking_IsRejected()
    {
        var candidate = Candidate(0.1, 5.0);

        ExteriorClassifier.Classify(candidate, [], [], SquareOvk);

        await Assert.That(candidate.OvkEdgeIndex).IsNull();
    }

    [Test]
    public async Task Classify_ExplicitInteriorWallMeaningfullyCloser_OverridesToInterior()
    {
        var candidate = Candidate(0.0, 5.0);
        var wallLike = new List<(double x1, double y1, double x2, double y2)> { (0.29, 4.0, 0.29, 6.0) };
        var explicitInterior = new List<(double x1, double y1, double x2, double y2)> { (-0.1, 4.9, -0.1, 5.1) };

        ExteriorClassifier.Classify(candidate, wallLike, explicitInterior, SquareOvk);

        await Assert.That(candidate.OvkEdgeIndex).IsNull();
    }

    [Test]
    public async Task Classify_ExplicitInteriorWallOnlyMarginallyCloser_DoesNotOverride()
    {
        var candidate = Candidate(0.1, 5.0);
        var wallLike = new List<(double x1, double y1, double x2, double y2)> { (0.1, 4.9, 0.1, 5.1) };
        var explicitInterior = new List<(double x1, double y1, double x2, double y2)> { (0.1, 4.95, 0.1, 5.05) };

        ExteriorClassifier.Classify(candidate, wallLike, explicitInterior, SquareOvk);

        await Assert.That(candidate.OvkEdgeIndex).IsEqualTo(3);
    }

    [Test]
    public async Task Classify_SetsExteriorDistanceEvenWhenRejected()
    {
        var candidate = Candidate(5.0, 5.0);

        ExteriorClassifier.Classify(candidate, [], [], SquareOvk);

        await Assert.That(candidate.ExteriorDistanceM).IsEqualTo(5.0).Within(1e-9);
    }
}
