using HVACrate2.Core.Openings;
using netDxf;
using netDxf.Blocks;
using netDxf.Entities;
using netDxf.Tables;

namespace HVACrate2.Core.Tests;

/// <summary>Direct tests of OpeningExtractor.Extract's orchestration (validation/rejection reasons,
/// type classification, warnings) — complements the black-box synthetic tests that go through the
/// full FloorProcessor pipeline.</summary>
public class OpeningExtractorTests
{
    private static readonly List<(double x1, double y1, double x2, double y2)> RectOvkMeters =
    [
        (0, 0, 10, 0),
        (10, 0, 10, 8),
        (10, 8, 0, 8),
        (0, 8, 0, 0),
    ];

    private static DxfDocument NewDoc() => new();

    /// <summary>Adds a wall-like line whose endpoints both sit on the left OVK edge (x=0) — the
    /// exterior classifier requires this "wall backing" before it will accept any candidate near
    /// that edge, regardless of how close the candidate itself is to the boundary curve.</summary>
    private static void AddLeftEdgeWall(DxfDocument doc)
        => doc.Entities.Add(new Line(new Vector2(0, 100), new Vector2(0, 700)) { Layer = new Layer("Walls") });

    private static Insert AddAttributeInsert(DxfDocument doc, string blockName, (double x, double y) posCm, params string[] numericValues)
    {
        var block = new Block(blockName);
        var attrTags = new List<string>();
        for (int i = 0; i < numericValues.Length; i++)
        {
            string tag = $"V{i}";
            attrTags.Add(tag);
            block.AttributeDefinitions.Add(new AttributeDefinition(tag));
        }
        var insert = new Insert(block, new Vector2(posCm.x, posCm.y)) { Layer = new Layer("AnyLayer") };
        for (int i = 0; i < numericValues.Length; i++)
            insert.Attributes.AttributeWithTag(attrTags[i]).Value = numericValues[i];
        doc.Entities.Add(insert);
        return insert;
    }

    [Test]
    public async Task Extract_CandidateFarFromOvk_IsRejectedAsInterior()
    {
        var doc = NewDoc();
        AddAttributeInsert(doc, "Zorp", (500, 400), "80", "210");

        var (openings, diagnostics) = OpeningExtractor.Extract(doc, RectOvkMeters, northDeg: 0, ccwSign: 1, coordDivisor: 100);

        await Assert.That(openings).IsEmpty();
        await Assert.That(diagnostics.RejectionReasons.Keys).Contains("not near the exterior boundary (OVK)");
    }

    [Test]
    public async Task Extract_DimensionOutsidePlausibleWholeOpeningRange_IsRejected()
    {
        var doc = NewDoc();
        AddLeftEdgeWall(doc);
        AddAttributeInsert(doc, "Zorp", (0, 400), "20", "90");

        var (openings, diagnostics) = OpeningExtractor.Extract(doc, RectOvkMeters, northDeg: 0, ccwSign: 1, coordDivisor: 100);

        await Assert.That(openings).IsEmpty();
        await Assert.That(diagnostics.RejectionReasons.Keys).Contains("width outside plausible range");
    }

    [Test]
    public async Task Extract_DimensionAboveMaxWholeOpeningRange_IsRejected()
    {
        var doc = NewDoc();
        AddLeftEdgeWall(doc);
        AddAttributeInsert(doc, "Zorp", (0, 400), "90", "400");

        var (openings, diagnostics) = OpeningExtractor.Extract(doc, RectOvkMeters, northDeg: 0, ccwSign: 1, coordDivisor: 100);

        await Assert.That(openings).IsEmpty();
        await Assert.That(diagnostics.RejectionReasons.Keys).Contains("height outside plausible range");
    }

    [Test]
    public async Task Extract_ValidCandidate_IsAcceptedWithDirectionAndConfidence()
    {
        var doc = NewDoc();
        AddLeftEdgeWall(doc);
        AddAttributeInsert(doc, "Zorp", (0, 400), "80", "210");

        var (openings, diagnostics) = OpeningExtractor.Extract(doc, RectOvkMeters, northDeg: 0, ccwSign: 1, coordDivisor: 100);

        await Assert.That(openings.Count).IsEqualTo(1);
        await Assert.That(openings[0].Direction).IsNotEmpty();
        await Assert.That(openings[0].Confidence).IsGreaterThan(0.0);
        await Assert.That(diagnostics.AcceptedCount).IsEqualTo(1);
    }

    [Test]
    public async Task Extract_NoCandidatesFound_WarnsWithWallGeometryPresent()
    {
        var doc = NewDoc();
        AddLeftEdgeWall(doc);

        var (openings, diagnostics) = OpeningExtractor.Extract(doc, RectOvkMeters, northDeg: 0, ccwSign: 1, coordDivisor: 100);

        await Assert.That(openings).IsEmpty();
        await Assert.That(diagnostics.WallLikePointsFound).IsGreaterThan(0);
        await Assert.That(diagnostics.Warnings.Any(w => w.Contains("wall geometry being present"))).IsTrue();
    }

    [Test]
    public async Task Extract_NoCandidatesAndNoWallGeometry_WarnsAboutMissingWalls()
    {
        var doc = NewDoc();

        var (openings, diagnostics) = OpeningExtractor.Extract(doc, RectOvkMeters, northDeg: 0, ccwSign: 1, coordDivisor: 100);

        await Assert.That(openings).IsEmpty();
        await Assert.That(diagnostics.WallLikePointsFound).IsEqualTo(0);
        await Assert.That(diagnostics.Warnings.Any(w => w.Contains("no wall-like geometry was found"))).IsTrue();
    }

    [Test]
    public async Task Extract_CandidatesByStrategy_ReportsBothStrategyNames()
    {
        var doc = NewDoc();
        AddAttributeInsert(doc, "Zorp", (0, 400), "80", "210");

        var (_, diagnostics) = OpeningExtractor.Extract(doc, RectOvkMeters, northDeg: 0, ccwSign: 1, coordDivisor: 100);

        await Assert.That(diagnostics.CandidatesByStrategy.ContainsKey("BlockAttribute")).IsTrue();
        await Assert.That(diagnostics.CandidatesByStrategy.ContainsKey("PerpendicularLabeledLine")).IsTrue();
    }

    [Test]
    public async Task Extract_EntitiesInspectedCount_ReflectsFlattenedEntities()
    {
        var doc = NewDoc();
        AddAttributeInsert(doc, "Zorp", (0, 400), "80", "210");

        var (_, diagnostics) = OpeningExtractor.Extract(doc, RectOvkMeters, northDeg: 0, ccwSign: 1, coordDivisor: 100);

        await Assert.That(diagnostics.EntitiesInspected).IsGreaterThan(0);
    }
}
