namespace HVACrate2.Core.Openings;

/// <summary>
/// Generalizes both previously-hardcoded INSERT+ATTRIB conventions (a marker nested inside a
/// per-wall block; a detached marker with its own companion body object) into one name-independent
/// rule: any INSERT, at any nesting depth, carrying 2+ numeric ATTRIB values in a plausible opening
/// dimension range is a candidate — regardless of whether the block happens to be named
/// "W Marker"/"D Marker" or anything else. A block/layer name that hints at "marker"/"window"/"door"
/// only adds a note to the evidence trail; it is never required.
/// </summary>
internal sealed class BlockAttributeStrategy : IOpeningCandidateStrategy
{
    public string Name => "BlockAttribute";

    // Looser than the leader-line strategy's tolerance: a detached annotation block's own position
    // is not guaranteed to sit exactly at the wall (historically observed 0.25m up to ~2.5m from
    // OVK for genuine exterior openings on some CAD conventions — see docs/decisions.md). This is a
    // known trade-off versus full wall-topology tracing: without it, a detached marker whose real
    // wall happens to be exactly as far away as some interior opening cannot be perfectly resolved
    // by distance alone. Accepted here in favor of not depending on any block/layer name.
    private const double ExteriorToleranceM = 2.5;

    public List<OpeningCandidate> Detect(OpeningDetectionContext ctx)
    {
        var candidates = new List<OpeningCandidate>();

        foreach (var e in ctx.Entities.Where(e =>
            e.Kind == FlatKind.Insert && e.Attributes.Count >= 2 &&
            !WordHints.ContainsAny(e.LayerName, WordHints.NonOpening) && !WordHints.ContainsAny(e.BlockName, WordHints.NonOpening)))
        {
            var numeric = e.Attributes.Values
                .Select(v => TextNumberParsing.TryParseNumber(v, out double n) ? (double?)n : null)
                .Where(n => n is >= DimensionRange.MinCm and <= DimensionRange.MaxCm)
                .Select(n => n!.Value)
                .ToList();
            if (numeric.Count < 2) continue;

            double a = numeric[0], b = numeric[1];
            double heightCm = Math.Max(a, b), widthCm = Math.Min(a, b);

            bool hint = WordHints.ContainsAny(e.BlockName, WordHints.Marker)
                        || WordHints.ContainsAny(e.LayerName, WordHints.Marker)
                        || WordHints.ContainsAny(e.BlockName, WordHints.Window)
                        || WordHints.ContainsAny(e.BlockName, WordHints.Door);

            var candidate = new OpeningCandidate
            {
                AnchorM = e.PointsM[0],
                WidthM = Math.Round(widthCm / 100.0, 3),
                HeightM = Math.Round(heightCm / 100.0, 3),
                DimensionSource = "attribute",
                StrategyName = Name,
                SourceLayerHint = $"{e.LayerName} {e.BlockName}",
                ExteriorToleranceM = ExteriorToleranceM,
            };
            candidate.Evidence.Add(
                $"INSERT '{e.BlockName}' on layer '{e.LayerName}' with {numeric.Count} numeric ATTRIB value(s)" +
                (hint ? " (name hint matched)" : "") +
                (numeric.Count > 2 ? " — more than 2 numeric attributes found, took the first 2; ambiguous" : ""));
            candidates.Add(candidate);
        }

        return candidates;
    }
}
