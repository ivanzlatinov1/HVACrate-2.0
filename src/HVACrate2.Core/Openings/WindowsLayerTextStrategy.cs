namespace HVACrate2.Core.Openings;

/// <summary>
/// Primary detection strategy for projects that follow the newer, deterministic export convention:
/// a dedicated layer (name starting with "windows", case-insensitive) carries one TEXT/MTEXT label per
/// opening, holding both dimensions as two numeric lines/tokens in a single entity (e.g. "150\n300"),
/// width first then height — confirmed on two real samples (`new_floor_1.dxf`, `new_floor_2.dxf`,
/// see docs/decisions.md): every label whose second number is 200 (the standard Bulgarian door height)
/// only makes sense read as width-then-height, not the old BlockAttributeStrategy's "larger value is
/// always the height" rule (that would swap e.g. a real 270x200 door into 200x270).
/// Both exterior and interior openings share this same layer — this strategy does not attempt to tell
/// them apart itself; it only emits anchored candidates and lets the existing, layer-name-agnostic
/// <see cref="ExteriorClassifier"/>/<see cref="WallGeometryClassifier"/> distance-to-OVK check do that,
/// same as every other strategy. Validated on the same two real samples: exterior labels sit within
/// ~0.34m of the OVK boundary, interior ones 0.9m+ away — a wide, unambiguous gap in both files.
/// Matching is a case-insensitive layer-name *prefix* check, not a substring check: an older Archicad
/// sample (`floor1.dxf`) has unrelated layers named "Archicad Window Markers"/"Archicad Windows" that
/// a substring match would have wrongly claimed as this new convention, silently switching that file
/// onto a strategy that finds nothing in it and dropping every real opening it used to find via the
/// legacy strategies — caught by the existing regression tests before shipping.
/// Runs alongside <see cref="WindowsLayerAttributeStrategy"/> whenever a windows-prefixed layer is
/// present — see that class for the older INSERT+ATTRIB marker convention this one doesn't cover.
/// </summary>
internal sealed class WindowsLayerTextStrategy : IOpeningCandidateStrategy
{
    public string Name => "WindowsLayer";

    private const double ExteriorToleranceM = 0.5;

    private static readonly char[] TokenSeparators = ['\n', '\r', ' ', '\t'];

    public static bool HasWindowsLayer(List<FlatEntity> entities)
        => entities.Any(e => e.LayerName.StartsWith("windows", StringComparison.OrdinalIgnoreCase));

    public List<OpeningCandidate> Detect(OpeningDetectionContext ctx)
    {
        var candidates = new List<OpeningCandidate>();

        foreach (var e in ctx.Entities.Where(e =>
            e.Kind == FlatKind.Text && e.LayerName.StartsWith("windows", StringComparison.OrdinalIgnoreCase)))
        {
            var numbers = e.Text.Split(TokenSeparators, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => TextNumberParsing.TryParseNumber(t, out double n) ? (double?)n : null)
                .Where(n => n is >= DimensionRange.MinCm and <= DimensionRange.MaxCm)
                .Select(n => n!.Value)
                .ToList();
            if (numbers.Count != 2) continue;

            double widthCm = numbers[0], heightCm = numbers[1];

            var candidate = new OpeningCandidate
            {
                AnchorM = e.PointsM[0],
                WidthM = Math.Round(widthCm / 100.0, 3),
                HeightM = Math.Round(heightCm / 100.0, 3),
                DimensionSource = "windows-layer",
                StrategyName = Name,
                SourceLayerHint = e.LayerName,
                ExteriorToleranceM = ExteriorToleranceM,
            };
            candidate.Evidence.Add(
                $"TEXT '{e.Text.Replace("\n", "/")}' on layer '{e.LayerName}' parsed as width={widthCm}cm, height={heightCm}cm");
            candidates.Add(candidate);
        }

        return candidates;
    }
}
