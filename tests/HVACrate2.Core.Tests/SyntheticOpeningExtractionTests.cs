using HVACrate2.Core;
using HVACrate2.Core.Models;
using netDxf;
using netDxf.Blocks;
using netDxf.Entities;
using netDxf.Tables;

namespace HVACrate2.Core.Tests;

/// <summary>
/// Builds minimal synthetic DXF documents in memory, deliberately using arbitrary/nonsense layer
/// and block names (never "wall", "window", "door", "marker" in any language) to prove the extractor
/// finds openings from geometry/topology alone — not from recognizing a name. Each test corresponds
/// to one of the two implemented candidate strategies.
/// </summary>
public class SyntheticOpeningExtractionTests
{
    private static DxfDocument NewDocumentWithOvkRectangle()
    {
        var doc = new DxfDocument();
        var ovkLayer = new Layer("OVK");
        var vertices = new[]
        {
            new Polyline2DVertex(0, 0),
            new Polyline2DVertex(1000, 0),
            new Polyline2DVertex(1000, 800),
            new Polyline2DVertex(0, 800),
            new Polyline2DVertex(0, 0),
        };
        var ovk = new Polyline2D(vertices) { Layer = ovkLayer };
        doc.Entities.Add(ovk);
        return doc;
    }

    [Test]
    public async Task BlockAttributeConvention_DetectsOpening_WithArbitraryNames()
    {
        var doc = NewDocumentWithOvkRectangle();

        var block = new Block("Zorp_9");
        block.AttributeDefinitions.Add(new AttributeDefinition("FOO1"));
        block.AttributeDefinitions.Add(new AttributeDefinition("FOO2"));
        var insert = new Insert(block, new Vector2(500, 800)) { Layer = new Layer("Layer_ABC") };
        insert.Attributes.AttributeWithTag("FOO1").Value = "80";
        insert.Attributes.AttributeWithTag("FOO2").Value = "210";
        doc.Entities.Add(insert);

        var input = new FloorInput { DxfPath = "synthetic", HeightM = 2.5, NorthDeg = 0.0, ApartmentCount = 1 };
        var result = FloorProcessor.ProcessFloorFromDocument(doc, input, "OVK");

        await Assert.That(result.Openings.Count).IsEqualTo(1);
        var opening = result.Openings[0];
        await Assert.That(opening.WidthM).IsEqualTo(0.80).Within(0.01);
        await Assert.That(opening.HeightM).IsEqualTo(2.10).Within(0.01);
    }

    [Test]
    public async Task PerpendicularLabeledLineConvention_DetectsOpening_WithArbitraryNames()
    {
        var doc = NewDocumentWithOvkRectangle();

        var line = new Line(new Vector2(0, 400), new Vector2(-70, 400)) { Layer = new Layer("Layer_XYZ") };
        doc.Entities.Add(line);
        doc.Entities.Add(new MText("90", new Vector2(-75, 395), 5) { Layer = new Layer("Layer_QQQ") });
        doc.Entities.Add(new MText("200", new Vector2(-65, 395), 5) { Layer = new Layer("Layer_QQQ") });

        var input = new FloorInput { DxfPath = "synthetic", HeightM = 2.5, NorthDeg = 0.0, ApartmentCount = 1 };
        var result = FloorProcessor.ProcessFloorFromDocument(doc, input, "OVK");

        await Assert.That(result.Openings.Count).IsEqualTo(1);
        var opening = result.Openings[0];
        await Assert.That(opening.WidthM).IsEqualTo(0.90).Within(0.01);
        await Assert.That(opening.HeightM).IsEqualTo(2.00).Within(0.01);
    }

    [Test]
    public async Task InteriorLabeledLine_FarFromOvk_IsNotDetectedAsExterior()
    {
        var doc = NewDocumentWithOvkRectangle();

        var line = new Line(new Vector2(500, 400), new Vector2(430, 400)) { Layer = new Layer("Layer_XYZ") };
        doc.Entities.Add(line);
        doc.Entities.Add(new MText("90", new Vector2(425, 395), 5) { Layer = new Layer("Layer_QQQ") });
        doc.Entities.Add(new MText("200", new Vector2(435, 395), 5) { Layer = new Layer("Layer_QQQ") });

        var input = new FloorInput { DxfPath = "synthetic", HeightM = 2.5, NorthDeg = 0.0, ApartmentCount = 1 };
        var result = FloorProcessor.ProcessFloorFromDocument(doc, input, "OVK");

        await Assert.That(result.Openings.Count).IsEqualTo(0);
    }
}
