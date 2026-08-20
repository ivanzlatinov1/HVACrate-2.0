namespace HVACrate2.Core.Openings;

/// <summary>
/// The primary, name-independent opening signal (per explicit user direction): an opening is a line
/// roughly perpendicular to the nearest OVK edge, with two numbers (dimension text) next to it —
/// regardless of what layer the line or the text sit on. This is what a plain leader-line + MText
/// dimension-pair annotation convention looks like geometrically, with no reliance on any block,
/// layer, or marker name.
/// The line's two endpoints are not equivalent: the one nearer the text pair is the label/tail side;
/// the other is the real wall-side tip (confirmed empirically — the far endpoint consistently sits
/// closer to real opening-body geometry than the label-side one, see docs/decisions.md). That far
/// endpoint is used as the candidate's anchor.
/// </summary>
internal sealed class PerpendicularLabeledLineStrategy : IOpeningCandidateStrategy
{
    public string Name => "PerpendicularLabeledLine";

    private const double LabelSearchRadiusM = 0.6;

    private const double MaxLabelPairDistanceM = 0.5;

    private const double MinPerpendicularAngleDeg = 70.0;

    private const double MinLineLengthM = 0.05;
    private const double MaxLineLengthM = 1.5;

    private const double ExteriorToleranceM = 0.8;

    public List<OpeningCandidate> Detect(OpeningDetectionContext ctx)
    {
        if (ctx.OvkEdges.Count == 0) return [];

        var numericTexts = ctx.Entities
            .Where(e => e.Kind == FlatKind.Text
                        && TextNumberParsing.TryParseNumber(e.Text, out double n)
                        && n is >= DimensionRange.MinCm and <= DimensionRange.MaxCm)
            .ToList();
        if (numericTexts.Count == 0) return [];

        var textIndex = numericTexts
            .Select((t, i) => (t, i))
            .ToDictionary(x => x.t, x => x.i);
        var bestByLabelPair = new Dictionary<(int, int), (OpeningCandidate candidate, double distToOvk)>();

        foreach (var line in ctx.Entities.Where(e => e.Kind == FlatKind.Line && !WordHints.ContainsAny(e.LayerName, WordHints.NonOpening)))
        {
            var (p1, p2) = (line.PointsM[0], line.PointsM[1]);
            double lineLenM = GeometryUtils.Distance(p1.x, p1.y, p2.x, p2.y);
            if (lineLenM < MinLineLengthM || lineLenM > MaxLineLengthM) continue;

            var near1 = NearestTwo(numericTexts, p1);
            var near2 = NearestTwo(numericTexts, p2);
            if (near1.items.Count < 2 && near2.items.Count < 2) continue;

            var (labelEnd, tipEnd, labels) = near1.avgDist <= near2.avgDist ? (p1, p2, near1.items) : (p2, p1, near2.items);

            var (l1, l2) = (labels[0].PointsM[0], labels[1].PointsM[0]);
            if (GeometryUtils.Distance(l1.x, l1.y, l2.x, l2.y) > MaxLabelPairDistanceM) continue;

            double dx = tipEnd.x - labelEnd.x, dy = tipEnd.y - labelEnd.y;
            double distToOvk = GeometryUtils.DistancePointToOvk(tipEnd.x, tipEnd.y, ctx.OvkEdges, out int edgeIdx);
            var (ex1, ey1, ex2, ey2) = ctx.OvkEdges[edgeIdx];
            double angle = GeometryUtils.AngleToEdgeDeg(dx, dy, ex1, ey1, ex2, ey2);
            if (angle < MinPerpendicularAngleDeg) continue;

            if (!TextNumberParsing.TryParseNumber(labels[0].Text, out double v1)) continue;
            if (!TextNumberParsing.TryParseNumber(labels[1].Text, out double v2)) continue;
            double heightCm = Math.Max(v1, v2), widthCm = Math.Min(v1, v2);

            var candidate = new OpeningCandidate
            {
                AnchorM = tipEnd,
                WidthM = Math.Round(widthCm / 100.0, 3),
                HeightM = Math.Round(heightCm / 100.0, 3),
                DimensionSource = "text-pair",
                StrategyName = Name,
                SourceLayerHint = line.LayerName,
                ExteriorToleranceM = ExteriorToleranceM,
            };
            candidate.Evidence.Add(
                $"Line on layer '{line.LayerName}' at {angle:F0} deg to nearest OVK edge, labeled '{labels[0].Text}'/'{labels[1].Text}' (dist to OVK {distToOvk:F2}m)");

            int i1 = textIndex[labels[0]], i2 = textIndex[labels[1]];
            var key = i1 < i2 ? (i1, i2) : (i2, i1);
            if (!bestByLabelPair.TryGetValue(key, out var existing) || distToOvk < existing.distToOvk)
                bestByLabelPair[key] = (candidate, distToOvk);
        }

        return bestByLabelPair.Values.Select(v => v.candidate).ToList();
    }

    private static (double avgDist, List<FlatEntity> items) NearestTwo(List<FlatEntity> texts, (double x, double y) point)
    {
        var ranked = texts
            .Select(t => (text: t, dist: GeometryUtils.Distance(point.x, point.y, t.PointsM[0].x, t.PointsM[0].y)))
            .Where(t => t.dist <= LabelSearchRadiusM)
            .OrderBy(t => t.dist)
            .Take(2)
            .ToList();
        if (ranked.Count < 2) return (double.MaxValue, []);
        return (ranked.Average(t => t.dist), ranked.Select(t => t.text).ToList());
    }
}
