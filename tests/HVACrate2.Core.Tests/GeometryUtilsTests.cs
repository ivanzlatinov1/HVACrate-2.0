using HVACrate2.Core.Openings;

namespace HVACrate2.Core.Tests;

public class GeometryUtilsTests
{
    [Test]
    public async Task Distance_ComputesEuclideanDistance()
    {
        double d = GeometryUtils.Distance(0, 0, 3, 4);
        await Assert.That(d).IsEqualTo(5.0).Within(1e-9);
    }

    [Test]
    public async Task DistancePointToSegment_PointBeyondEnd_ClampsToEndpoint()
    {
        double d = GeometryUtils.DistancePointToSegment(15, 0, 0, 0, 10, 0);
        await Assert.That(d).IsEqualTo(5.0).Within(1e-9);
    }

    [Test]
    public async Task DistancePointToSegment_PointBeforeStart_ClampsToStart()
    {
        double d = GeometryUtils.DistancePointToSegment(-5, 0, 0, 0, 10, 0);
        await Assert.That(d).IsEqualTo(5.0).Within(1e-9);
    }

    [Test]
    public async Task DistancePointToSegment_PointAbovMidpoint_UsesPerpendicularDistance()
    {
        double d = GeometryUtils.DistancePointToSegment(5, 3, 0, 0, 10, 0);
        await Assert.That(d).IsEqualTo(3.0).Within(1e-9);
    }

    [Test]
    public async Task DistancePointToSegment_DegenerateZeroLengthSegment_FallsBackToPointDistance()
    {
        double d = GeometryUtils.DistancePointToSegment(3, 4, 1, 1, 1, 1);
        await Assert.That(d).IsEqualTo(GeometryUtils.Distance(3, 4, 1, 1)).Within(1e-9);
    }

    [Test]
    public async Task DistancePointToOvk_PicksNearestEdgeAndReturnsItsIndex()
    {
        var edges = new List<(double x1, double y1, double x2, double y2)>
        {
            (0, 0, 10, 0),
            (10, 0, 10, 10),
            (10, 10, 0, 10),
            (0, 10, 0, 0),
        };

        double d = GeometryUtils.DistancePointToOvk(9, 5, edges, out int nearestEdgeIndex);

        await Assert.That(nearestEdgeIndex).IsEqualTo(1);
        await Assert.That(d).IsEqualTo(1.0).Within(1e-9);
    }

    [Test]
    public async Task DistancePointToOvk_EmptyEdgeList_ReturnsMaxValueAndNoIndex()
    {
        double d = GeometryUtils.DistancePointToOvk(0, 0, [], out int nearestEdgeIndex);

        await Assert.That(d).IsEqualTo(double.MaxValue);
        await Assert.That(nearestEdgeIndex).IsEqualTo(-1);
    }

    [Test]
    public async Task AngleToEdgeDeg_ParallelLine_ReturnsZero()
    {
        double angle = GeometryUtils.AngleToEdgeDeg(1, 0, 0, 0, 5, 0);
        await Assert.That(angle).IsEqualTo(0.0).Within(0.01);
    }

    [Test]
    public async Task AngleToEdgeDeg_PerpendicularLine_ReturnsNinety()
    {
        double angle = GeometryUtils.AngleToEdgeDeg(0, 1, 0, 0, 5, 0);
        await Assert.That(angle).IsEqualTo(90.0).Within(0.01);
    }

    [Test]
    public async Task AngleToEdgeDeg_OppositeDirection_StillTreatedAsParallel()
    {
        double angle = GeometryUtils.AngleToEdgeDeg(-1, 0, 0, 0, 5, 0);
        await Assert.That(angle).IsEqualTo(0.0).Within(0.01);
    }

    [Test]
    public async Task AngleToEdgeDeg_ZeroLengthLine_ReturnsZeroWithoutDividingByZero()
    {
        double angle = GeometryUtils.AngleToEdgeDeg(0, 0, 0, 0, 5, 0);
        await Assert.That(angle).IsEqualTo(0.0);
    }
}
