namespace HVACrate2.Core.Openings;

/// <summary>
/// Covers the *older* marker convention when it happens to live on a layer that also matches the
/// newer "Windows" layer convention (see <see cref="WindowsLayerTextStrategy"/>): an INSERT of a
/// marker block (e.g. `W Marker NN`) carrying ATTRIB values, where the INSERT itself — not just a
/// bare TEXT/MTEXT label — sits on a layer whose name starts with "windows" (case-insensitive).
/// Confirmed on a real DXF (`new block.dxf`, see docs/decisions.md): 73 real markers on a layer
/// literally named "Windows", with dimensions in ATTRIB tags `AC_MarkerText_2` (width) /
/// `AC_MarkerText_3` (height) — the exact tag mapping already confirmed for the unscoped legacy
/// <see cref="BlockAttributeStrategy"/>. Before this strategy existed, <see cref="OpeningExtractor"/>
/// would see the "windows" layer, run only <see cref="WindowsLayerTextStrategy"/> (which looks for
/// TEXT/MTEXT and finds none here), and never fall back to <see cref="BlockAttributeStrategy"/> —
/// producing zero openings from a file that had 73 valid markers.
/// Width/height are assigned by known tag name first, not by magnitude: 2 of the 73 real markers in
/// that file have a tagged width *larger* than the tagged height (e.g. 233cm wide x 203cm tall), so
/// `BlockAttributeStrategy`'s "larger value is always the height" heuristic would silently swap
/// them. Only when the known tags aren't present does this fall back to that same magnitude
/// heuristic, so an unrecognized-but-still-windows-layer marker convention still degrades gracefully
/// instead of being dropped entirely.
/// Runs alongside <see cref="WindowsLayerTextStrategy"/> whenever a windows-prefixed layer is
/// present, still scoped to that layer only — <see cref="OpeningExtractor"/> does not fall back to
/// the unscoped legacy strategies in either case, to avoid reintroducing the false positives that
/// made the "Windows" layer convention necessary in the first place.
/// </summary>
internal sealed class WindowsLayerAttributeStrategy : IOpeningCandidateStrategy
{
    public string Name => "WindowsLayerAttribute";

    private const double ExteriorToleranceM = 2.5;

    private const string WidthTag = "AC_MarkerText_2";
    private const string HeightTag = "AC_MarkerText_3";

    public List<OpeningCandidate> Detect(OpeningDetectionContext ctx)
    {
        var candidates = new List<OpeningCandidate>();

        foreach (var e in ctx.Entities.Where(e =>
            e.Kind == FlatKind.Insert && e.LayerName.StartsWith("windows", StringComparison.OrdinalIgnoreCase) &&
            e.Attributes.Count >= 2 &&
            !WordHints.ContainsAny(e.LayerName, WordHints.NonOpening) && !WordHints.ContainsAny(e.BlockName, WordHints.NonOpening)))
        {
            if (!TryAssignDimensions(e, out double widthCm, out double heightCm, out string source)) continue;

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
                $"INSERT '{e.BlockName}' on layer '{e.LayerName}' with ATTRIB dimensions, assigned via {source}");
            candidates.Add(candidate);
        }

        return candidates;
    }

    private static bool TryAssignDimensions(FlatEntity e, out double widthCm, out double heightCm, out string source)
    {
        if (e.Attributes.TryGetValue(WidthTag, out string? wRaw) && e.Attributes.TryGetValue(HeightTag, out string? hRaw) &&
            TextNumberParsing.TryParseNumber(wRaw, out double w) && TextNumberParsing.TryParseNumber(hRaw, out double h) &&
            w is >= DimensionRange.MinCm and <= DimensionRange.MaxCm && h is >= DimensionRange.MinCm and <= DimensionRange.MaxCm)
        {
            widthCm = w;
            heightCm = h;
            source = $"known tags {WidthTag}(width)/{HeightTag}(height)";
            return true;
        }

        var numeric = AttributeNumberParsing.ParseInRangeNumbers(e.Attributes.Values);
        if (numeric.Count < 2)
        {
            widthCm = heightCm = 0;
            source = "";
            return false;
        }

        heightCm = Math.Max(numeric[0], numeric[1]);
        widthCm = Math.Min(numeric[0], numeric[1]);
        source = "magnitude heuristic (known width/height tags not present)";
        return true;
    }
}
