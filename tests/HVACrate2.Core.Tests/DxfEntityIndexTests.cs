using HVACrate2.Core.Openings;
using netDxf;
using netDxf.Blocks;
using netDxf.Entities;
using netDxf.Tables;

namespace HVACrate2.Core.Tests;

public class DxfEntityIndexTests
{
    private const double Divisor = 100.0; // centimeters -> meters

    [Test]
    public async Task Build_FlattensTopLevelLine()
    {
        var doc = new DxfDocument();
        doc.Entities.Add(new Line(new Vector2(0, 0), new Vector2(500, 0)) { Layer = new Layer("Walls") });

        var flat = DxfEntityIndex.Build(doc, Divisor);

        var line = flat.Single(f => f.Kind == FlatKind.Line);
        await Assert.That(line.PointsM[0]).IsEqualTo((0.0, 0.0));
        await Assert.That(line.PointsM[1]).IsEqualTo((5.0, 0.0));
        await Assert.That(line.LayerName).IsEqualTo("Walls");
    }

    [Test]
    public async Task Build_FlattensArcWithScaledRadius()
    {
        var doc = new DxfDocument();
        doc.Entities.Add(new Arc(new Vector2(100, 200), 50, 0, 360) { Layer = new Layer("Any") });

        var flat = DxfEntityIndex.Build(doc, Divisor);

        var arc = flat.Single(f => f.Kind == FlatKind.Arc);
        await Assert.That(arc.PointsM[0]).IsEqualTo((1.0, 2.0));
        await Assert.That(arc.RadiusM).IsEqualTo(0.5).Within(1e-9);
    }

    [Test]
    public async Task Build_FlattensPolyline2DAllVertices()
    {
        var doc = new DxfDocument();
        var poly = new Polyline2D(new[]
        {
            new Polyline2DVertex(0, 0),
            new Polyline2DVertex(100, 0),
            new Polyline2DVertex(100, 100),
        }) { Layer = new Layer("OVK") };
        doc.Entities.Add(poly);

        var flat = DxfEntityIndex.Build(doc, Divisor);

        var p = flat.Single(f => f.Kind == FlatKind.Polyline2D);
        await Assert.That(p.PointsM.Count).IsEqualTo(3);
        await Assert.That(p.PointsM[2]).IsEqualTo((1.0, 1.0));
    }

    [Test]
    public async Task Build_FlattensSingleLineTextEntity()
    {
        var doc = new DxfDocument();
        doc.Entities.Add(new Text("90", new Vector2(10, 20), 5) { Layer = new Layer("Dim") });

        var flat = DxfEntityIndex.Build(doc, Divisor);

        var t = flat.Single(f => f.Kind == FlatKind.Text);
        await Assert.That(t.Text).IsEqualTo("90");
        await Assert.That(t.PointsM[0]).IsEqualTo((0.1, 0.2));
    }

    [Test]
    public async Task Build_FlattensMTextEntity()
    {
        var doc = new DxfDocument();
        doc.Entities.Add(new MText("210", new Vector2(30, 40), 5) { Layer = new Layer("Dim") });

        var flat = DxfEntityIndex.Build(doc, Divisor);

        var t = flat.Single(f => f.Kind == FlatKind.Text);
        await Assert.That(t.Text).IsEqualTo("210");
    }

    [Test]
    public async Task Build_TopLevelInsert_CapturesBlockNameAndAttributes()
    {
        var doc = new DxfDocument();
        var block = new Block("MarkerBlock");
        block.AttributeDefinitions.Add(new AttributeDefinition("TAG1"));
        var insert = new Insert(block, new Vector2(0, 0)) { Layer = new Layer("Markers") };
        insert.Attributes.AttributeWithTag("TAG1").Value = "90";
        doc.Entities.Add(insert);

        var flat = DxfEntityIndex.Build(doc, Divisor);

        var ins = flat.Single(f => f.Kind == FlatKind.Insert);
        await Assert.That(ins.BlockName).IsEqualTo("MarkerBlock");
        await Assert.That(ins.Attributes["TAG1"]).IsEqualTo("90");
    }

    [Test]
    public async Task Build_NestedInsert_TransformsChildGeometryToWorldCoordinates()
    {
        var doc = new DxfDocument();
        var innerBlock = new Block("Inner");
        // A line from (0,0) to (100,0) local to the block.
        innerBlock.Entities.Add(new Line(new Vector2(0, 0), new Vector2(100, 0)) { Layer = new Layer("Inner") });

        // Insert the block at world position (500, 500), no rotation, unit scale.
        var insert = new Insert(innerBlock, new Vector2(500, 500)) { Layer = new Layer("Outer") };
        doc.Entities.Add(insert);

        var flat = DxfEntityIndex.Build(doc, Divisor);

        var line = flat.Single(f => f.Kind == FlatKind.Line);
        // World coords: (500,500) + (0,0) and (500,500) + (100,0), then /100 for meters.
        await Assert.That(line.PointsM[0]).IsEqualTo((5.0, 5.0));
        await Assert.That(line.PointsM[1]).IsEqualTo((6.0, 5.0));
    }

    [Test]
    public async Task Build_InsertWithRotationAndScale_TransformsCorrectly()
    {
        var doc = new DxfDocument();
        var block = new Block("Rotated");
        block.Entities.Add(new Line(new Vector2(0, 0), new Vector2(10, 0)) { Layer = new Layer("L") });

        // Scale x2, rotate 90 deg CCW, positioned at world origin: local (10,0) -> scaled (20,0)
        // -> rotated 90 deg -> (0,20).
        var insert = new Insert(block, new Vector2(0, 0))
        {
            Layer = new Layer("L"),
            Scale = new Vector3(2, 2, 1),
            Rotation = 90,
        };
        doc.Entities.Add(insert);

        var flat = DxfEntityIndex.Build(doc, Divisor);

        var line = flat.Single(f => f.Kind == FlatKind.Line);
        await Assert.That(line.PointsM[1].x).IsEqualTo(0.0).Within(1e-9);
        await Assert.That(line.PointsM[1].y).IsEqualTo(0.2).Within(1e-9);
    }

    [Test]
    public async Task Build_DoublyNestedInsert_ComposesBothTransforms()
    {
        var doc = new DxfDocument();
        var innermost = new Block("Innermost");
        innermost.Entities.Add(new Line(new Vector2(0, 0), new Vector2(10, 0)) { Layer = new Layer("L") });

        var middleBlock = new Block("Middle");
        middleBlock.Entities.Add(new Insert(innermost, new Vector2(100, 0)) { Layer = new Layer("L") });

        var outerInsert = new Insert(middleBlock, new Vector2(1000, 1000)) { Layer = new Layer("L") };
        doc.Entities.Add(outerInsert);

        var flat = DxfEntityIndex.Build(doc, Divisor);

        var line = flat.Single(f => f.Kind == FlatKind.Line);
        // Innermost line starts at local (0,0); +100 (middle insert) +1000 (outer insert) = 1100, /100 = 11.0m.
        await Assert.That(line.PointsM[0]).IsEqualTo((11.0, 10.0));
    }
}
