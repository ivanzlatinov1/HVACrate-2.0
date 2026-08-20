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

    // How far a dimension label may sit from the line endpoint it annotates. Generous relative to
    // the ~0.15-0.30m offsets measured on real data, to tolerate other drafting scales/conventions,
    // but tight enough to not sweep in unrelated dimension-chain text elsewhere in the drawing.
    private const double LabelSearchRadiusM = 0.6;

    // The two labels of a genuine width/height pair sit right next to each other (measured ~0.25m
    // apart on real data) — this is what actually separates "a marker's own two numbers" from two
    // unrelated numeric labels that both happen to be within LabelSearchRadiusM of the same line.
    private const double MaxLabelPairDistanceM = 0.5;

    // A line within this many degrees of perpendicular (90 deg) to the nearest OVK edge counts.
    private const double MinPerpendicularAngleDeg = 70.0;

    // A real leader/jamb tick is short (measured ~0.25-0.9m on real data); long lines in this range
    // are dimension-chain segments elsewhere in the drawing, not an opening's own annotation line.
    private const double MinLineLengthM = 0.05;
    private const double MaxLineLengthM = 1.5;

    // The tip is already confirmed to sit at the real wall (see class remarks) — a tight tolerance
    // here is what actually discriminates a genuine facade opening from interior/unrelated geometry.
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

        // Keyed by the *identity* of the two label entities a candidate line matched — not by the
        // resulting candidate's position. A single real window/door commonly has several lines near
        // its own dimension labels (frame edges, a wall tick, the marker leader itself), each
        // independently satisfying the perpendicular+two-numbers pattern but anchored at slightly
        // different points along the opening's own width — often farther apart than a position-based
        // merge tolerance can safely use without also risking merging two distinct, similarly-sized
        // openings nearby. Two candidates that matched the exact same pair of label entities are
        // unambiguously the same real annotation, so only the one whose tip lands closest to OVK
        // (the most likely genuine wall touch point) is kept.
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
