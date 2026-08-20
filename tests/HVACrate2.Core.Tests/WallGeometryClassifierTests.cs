using HVACrate2.Core.Openings;

namespace HVACrate2.Core.Tests;

public class WallGeometryClassifierTests
{
    private static readonly List<(double x1, double y1, double x2, double y2)> SquareOvk =
    [
        (0, 0, 10, 0),
        (10, 0, 10, 10),
        (10, 10, 0, 10),
        (0, 10, 0, 0),
    ];

    private static FlatEntity LineEntity(string layer, (double x, double y) a, (double x, double y) b)
        => new() { Kind = FlatKind.Line, LayerName = layer, PointsM = [a, b], Source = new object() };

    private static FlatEntity Polyline(string layer, params (double x, double y)[] points)
        => new() { Kind = FlatKind.Polyline2D, LayerName = layer, PointsM = points.ToList(), Source = new object() };

    private static FlatEntity TextEntity(string layer, (double x, double y) at)
        => new() { Kind = FlatKind.Text, LayerName = layer, PointsM = [at], Source = new object() };

    [Test]
    public async Task CollectWallLikeSegments_BothEndpointsNearOvk_IsIncluded()
    {
        var entities = new List<FlatEntity> { LineEntity("Any", (0.1, 0.0), (5.0, 0.05)) };

        var segments = WallGeometryClassifier.CollectWallLikeSegments(entities, SquareOvk);

        await Assert.That(segments.Count).IsEqualTo(1);
    }

    [Test]
    public async Task CollectWallLikeSegments_OnlyOneEndpointNearOvk_IsExcluded()
    {
        // Simulates an interior partition that merely T-joins an exterior wall at one corner.
        var entities = new List<FlatEntity> { LineEntity("Any", (0.1, 0.0), (5.0, 5.0)) };

        var segments = WallGeometryClassifier.CollectWallLikeSegments(entities, SquareOvk);

        await Assert.That(segments).IsEmpty();
    }

    [Test]
    public async Task CollectWallLikeSegments_NeitherEndpointNearOvk_IsExcluded()
    {
        var entities = new List<FlatEntity> { LineEntity("Any", (5.0, 5.0), (6.0, 5.0)) };

        var segments = WallGeometryClassifier.CollectWallLikeSegments(entities, SquareOvk);

        await Assert.That(segments).IsEmpty();
    }

    [Test]
    public async Task CollectWallLikeSegments_NonLineEntities_AreIgnored()
    {
        var entities = new List<FlatEntity> { TextEntity("Any", (0.0, 0.0)) };

        var segments = WallGeometryClassifier.CollectWallLikeSegments(entities, SquareOvk);

        await Assert.That(segments).IsEmpty();
    }

    [Test]
    public async Task CollectWallLikeSegments_PolylineWithMultipleVertices_YieldsMultipleSegments()
    {
        var entities = new List<FlatEntity> { Polyline("Any", (0.0, 0.0), (5.0, 0.0), (10.0, 0.0)) };

        var segments = WallGeometryClassifier.CollectWallLikeSegments(entities, SquareOvk);

        await Assert.That(segments.Count).IsEqualTo(2);
    }

    [Test]
    public async Task CollectExplicitInteriorSegments_LayerNamedInterior_IsIncludedRegardlessOfOvkDistance()
    {
        // Deep in the interior — CollectWallLikeSegments would reject this, but the explicit-interior
        // collector has no OVK-proximity requirement at all.
        var entities = new List<FlatEntity> { LineEntity("Стени - интериор", (5.0, 5.0), (6.0, 5.0)) };

        var segments = WallGeometryClassifier.CollectExplicitInteriorSegments(entities);

        await Assert.That(segments.Count).IsEqualTo(1);
    }

    [Test]
    public async Task CollectExplicitInteriorSegments_ExteriorLayerName_IsExcluded()
    {
        var entities = new List<FlatEntity> { LineEntity("Стени - екстериор", (5.0, 5.0), (6.0, 5.0)) };

        var segments = WallGeometryClassifier.CollectExplicitInteriorSegments(entities);

        await Assert.That(segments).IsEmpty();
    }

    [Test]
    public async Task CollectExplicitInteriorSegments_UnrelatedLayerName_IsExcluded()
    {
        var entities = new List<FlatEntity> { LineEntity("Furniture", (5.0, 5.0), (6.0, 5.0)) };

        var segments = WallGeometryClassifier.CollectExplicitInteriorSegments(entities);

        await Assert.That(segments).IsEmpty();
    }
}
