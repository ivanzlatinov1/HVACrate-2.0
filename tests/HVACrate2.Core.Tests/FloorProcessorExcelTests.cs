using ClosedXML.Excel;
using HVACrate2.Core.Models;
using netDxf;
using netDxf.Entities;
using netDxf.Tables;

namespace HVACrate2.Core.Tests;

/// <summary>Exercises FloorProcessor's Excel-write path (WriteFloorToExcel/WriteOpeningsTable/
/// WriteApplianceBlock/ProcessAndWriteFloors) against the real, tracked blank template, plus the
/// OVK-selection and coordinate-divisor branches that aren't reachable through the synthetic
/// extraction tests alone.</summary>
public class FloorProcessorExcelTests
{
    private static readonly string TemplatePath = Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "output", "Топлотехника V6.0.16.xlsx");

    private static DxfDocument RectFloorDoc(double widthCm = 1000, double heightCm = 800)
    {
        var doc = new DxfDocument();
        var vertices = new[]
        {
            new Polyline2DVertex(0, 0),
            new Polyline2DVertex(widthCm, 0),
            new Polyline2DVertex(widthCm, heightCm),
            new Polyline2DVertex(0, heightCm),
            new Polyline2DVertex(0, 0),
        };
        doc.Entities.Add(new Polyline2D(vertices) { Layer = new Layer("OVK") });
        return doc;
    }

    [Test]
    public async Task WriteFloorsToExcel_SingleFloor_WritesGeometryAndWallBlocks()
    {
        var input = new FloorInput { DxfPath = "synthetic", HeightM = 2.89, NorthDeg = 0.0, ApartmentCount = 2 };
        var result = FloorProcessor.ProcessFloorFromDocument(RectFloorDoc(), input, "OVK");

        string outPath = Path.Combine(Path.GetTempPath(), $"hvacrate-test-{Guid.NewGuid():N}.xlsx");
        try
        {
            FloorProcessor.WriteFloorsToExcel([input], [result], TemplatePath, outPath);

            using var wb = new XLWorkbook(outPath);
            var ws = wb.Worksheet("Изчисления");

            await Assert.That(ws.Cell("C7").GetDouble()).IsEqualTo(80.0).Within(0.01); // 10m x 8m
            await Assert.That(ws.Cell("E7").GetDouble()).IsEqualTo(2.89).Within(0.001);
            await Assert.That(ws.Cell("F7").GetDouble()).IsEqualTo(80.0 * 2.89).Within(0.01);
            await Assert.That(ws.Cell("K7").GetDouble()).IsEqualTo(4.0).Within(0.01); // 4 convex corners

            // Wall row: a rectangle's perimeter length is split across N/E/S/W (10m+10m+8m+8m=36m).
            double perimeter = ws.Cell("D31").GetDouble() + ws.Cell("F31").GetDouble()
                + ws.Cell("H31").GetDouble() + ws.Cell("J31").GetDouble();
            await Assert.That(perimeter).IsEqualTo(36.0).Within(0.01);
            await Assert.That(ws.Cell("L31").GetDouble()).IsEqualTo(36.0).Within(0.01);
        }
        finally
        {
            if (File.Exists(outPath)) File.Delete(outPath);
        }
    }

    [Test]
    public async Task WriteFloorsToExcel_AppendsApplianceBlockFromApartmentAndAreaTotals()
    {
        var input1 = new FloorInput { DxfPath = "synthetic", HeightM = 2.5, NorthDeg = 0, ApartmentCount = 3 };
        var result1 = FloorProcessor.ProcessFloorFromDocument(RectFloorDoc(), input1, "OVK");
        var input2 = new FloorInput { DxfPath = "synthetic", HeightM = 2.5, NorthDeg = 0, ApartmentCount = 2 };
        var result2 = FloorProcessor.ProcessFloorFromDocument(RectFloorDoc(500, 400), input2, "OVK");

        string outPath = Path.Combine(Path.GetTempPath(), $"hvacrate-test-{Guid.NewGuid():N}.xlsx");
        try
        {
            FloorProcessor.WriteFloorsToExcel([input1, input2], [result1, result2], TemplatePath, outPath);

            using var wb = new XLWorkbook(outPath);
            var ws = wb.Worksheet("Изчисления");

            int totalApartments = 5;
            await Assert.That(ws.Cell("D317").GetDouble()).IsEqualTo(totalApartments);
            await Assert.That(ws.Cell("D321").GetDouble()).IsEqualTo(totalApartments);
            await Assert.That(ws.Cell("D331").GetDouble()).IsEqualTo(2 * totalApartments);
            await Assert.That(ws.Cell("D332").GetDouble()).IsEqualTo(totalApartments);
            await Assert.That(ws.Cell("D333").GetDouble()).IsEqualTo(2 * totalApartments);
            await Assert.That(ws.Cell("D336").GetDouble()).IsEqualTo(5 * totalApartments);
            await Assert.That(ws.Cell("D348").GetDouble()).IsEqualTo(totalApartments);

            double totalArea = 80.0 + 20.0; // 10x8 + 5x4
            double expectedLamps = Math.Ceiling(7 * totalArea / 20.0);
            await Assert.That(ws.Cell("D291").GetDouble()).IsEqualTo(expectedLamps);
        }
        finally
        {
            if (File.Exists(outPath)) File.Delete(outPath);
        }
    }

    [Test]
    public async Task WriteFloorsToExcel_OpeningsTable_WritesSizeGroupedByDirection()
    {
        var doc = RectFloorDoc();
        // A wall-like segment backing the left edge, plus a block-attribute opening near it.
        doc.Entities.Add(new Line(new Vector2(0, 100), new Vector2(0, 700)) { Layer = new Layer("Walls") });
        var block = new netDxf.Blocks.Block("Zorp");
        block.AttributeDefinitions.Add(new AttributeDefinition("A"));
        block.AttributeDefinitions.Add(new AttributeDefinition("B"));
        var insert = new Insert(block, new Vector2(0, 400)) { Layer = new Layer("Any") };
        insert.Attributes.AttributeWithTag("A").Value = "80";
        insert.Attributes.AttributeWithTag("B").Value = "210";
        doc.Entities.Add(insert);

        var input = new FloorInput { DxfPath = "synthetic", HeightM = 2.5, NorthDeg = 0, ApartmentCount = 1 };
        var result = FloorProcessor.ProcessFloorFromDocument(doc, input, "OVK");
        await Assert.That(result.Openings.Count).IsEqualTo(1);

        string outPath = Path.Combine(Path.GetTempPath(), $"hvacrate-test-{Guid.NewGuid():N}.xlsx");
        try
        {
            FloorProcessor.WriteFloorsToExcel([input], [result], TemplatePath, outPath);

            using var wb = new XLWorkbook(outPath);
            var ws = wb.Worksheet("Изчисления");

            await Assert.That(ws.Cell("B57").GetDouble()).IsEqualTo(0.80).Within(0.001);
            await Assert.That(ws.Cell("C57").GetDouble()).IsEqualTo(2.10).Within(0.001);
        }
        finally
        {
            if (File.Exists(outPath)) File.Delete(outPath);
        }
    }

    [Test]
    public async Task ProcessAndWriteFloors_FullPipeline_ProducesReadableWorkbook()
    {
        // Exercises ProcessAndWriteFloors end-to-end (ProcessFloors + WriteFloorsToExcel combined),
        // writing straight from FloorInput without a pre-computed FloorResult step.
        string dxfPath = Path.Combine(Path.GetTempPath(), $"hvacrate-test-{Guid.NewGuid():N}.dxf");
        RectFloorDoc().Save(dxfPath);
        string outPath = Path.Combine(Path.GetTempPath(), $"hvacrate-test-{Guid.NewGuid():N}.xlsx");
        try
        {
            var input = new FloorInput { DxfPath = dxfPath, HeightM = 3.0, NorthDeg = 0, ApartmentCount = 1 };
            FloorProcessor.ProcessAndWriteFloors([input], TemplatePath, outPath, "OVK");

            using var wb = new XLWorkbook(outPath);
            var ws = wb.Worksheet("Изчисления");
            await Assert.That(ws.Cell("C7").GetDouble()).IsEqualTo(80.0).Within(0.01);
        }
        finally
        {
            if (File.Exists(dxfPath)) File.Delete(dxfPath);
            if (File.Exists(outPath)) File.Delete(outPath);
        }
    }

    [Test]
    public async Task ProcessFloorFromDocument_MultipleOvkLayerPolylines_PicksLargestArea()
    {
        var doc = new DxfDocument();
        // A small "room" polygon and the true, much larger building envelope, both on the OVK layer.
        doc.Entities.Add(new Polyline2D(new[]
        {
            new Polyline2DVertex(0, 0), new Polyline2DVertex(50, 0),
            new Polyline2DVertex(50, 50), new Polyline2DVertex(0, 50), new Polyline2DVertex(0, 0),
        })
        { Layer = new Layer("OVK") });
        doc.Entities.Add(new Polyline2D(new[]
        {
            new Polyline2DVertex(0, 0), new Polyline2DVertex(1000, 0),
            new Polyline2DVertex(1000, 800), new Polyline2DVertex(0, 800), new Polyline2DVertex(0, 0),
        })
        { Layer = new Layer("OVK") });

        var input = new FloorInput { DxfPath = "synthetic", HeightM = 2.5, NorthDeg = 0, ApartmentCount = 1 };
        var result = FloorProcessor.ProcessFloorFromDocument(doc, input, "OVK");

        await Assert.That(result.AreaM2).IsEqualTo(80.0).Within(0.01);
    }

    [Test]
    public async Task ProcessFloorFromDocument_NoOvkLayer_Throws()
    {
        var doc = new DxfDocument();
        doc.Entities.Add(new Line(new Vector2(0, 0), new Vector2(10, 0)) { Layer = new Layer("Walls") });

        var input = new FloorInput { DxfPath = "synthetic", HeightM = 2.5, NorthDeg = 0, ApartmentCount = 1 };

        await Assert.That(() => FloorProcessor.ProcessFloorFromDocument(doc, input, "OVK"))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ProcessFloorFromDocument_MillimeterConvention_FallsBackWhenCentimeterAreaIsImplausible()
    {
        // A real ~10m x 8m floor authored in millimeters: raw coordinates 100x larger than the
        // centimeter convention. Misread as centimeters this would compute an 8000m x... wait,
        // 10000cm x 8000cm = 100m x 80m = 8000 m^2 (still plausible!) so use dimensions large enough
        // that the centimeter reading is implausible (>20000 m^2) to force the millimeter fallback.
        var doc = RectFloorDoc(widthCm: 500_000, heightCm: 400_000); // as centimeters: 5000m x 4000m
        var input = new FloorInput { DxfPath = "synthetic", HeightM = 2.5, NorthDeg = 0, ApartmentCount = 1 };

        var result = FloorProcessor.ProcessFloorFromDocument(doc, input, "OVK");

        // Correctly read as millimeters instead: 500m x 400m = 200,000 m^2.
        await Assert.That(result.AreaM2).IsEqualTo(200_000.0).Within(1.0);
    }

    [Test]
    public async Task ProcessFloorFromDocument_NonRectangularFloor_CountsReflexCornerCorrectly()
    {
        // An L-shaped floor: 7 convex corners, 1 reflex (the inner notch corner).
        var doc = new DxfDocument();
        var vertices = new[]
        {
            new Polyline2DVertex(0, 0),
            new Polyline2DVertex(1000, 0),
            new Polyline2DVertex(1000, 500),
            new Polyline2DVertex(500, 500),
            new Polyline2DVertex(500, 800),
            new Polyline2DVertex(0, 800),
            new Polyline2DVertex(0, 0),
        };
        doc.Entities.Add(new Polyline2D(vertices) { Layer = new Layer("OVK") });

        var input = new FloorInput { DxfPath = "synthetic", HeightM = 2.5, NorthDeg = 0, ApartmentCount = 1 };
        var result = FloorProcessor.ProcessFloorFromDocument(doc, input, "OVK");

        await Assert.That(result.ConvexCorners).IsEqualTo(5);
    }
}
