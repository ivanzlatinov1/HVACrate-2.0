namespace HVACrate2.Core.Openings;

/// <summary>
/// Small multilingual word lists used only as confidence hints (e.g. "this layer name mentions
/// 'window' in some language, so nudge type confidence up") — never as a requirement for detecting
/// an opening or a wall. A future client's CAD export using a language/word not in this list must
/// still work via the geometric signals in <see cref="Openings"/>; these lists only make detection
/// more confident when a recognizable hint happens to be present.
/// </summary>
internal static class WordHints
{
    public static readonly string[] Wall = ["wall", "стен", "wand", "mur", "muro", "paroi"];
    public static readonly string[] Window = ["window", "прозор", "fenster", "fenetre", "fenêtre", "finestra", "ventana"];
    public static readonly string[] Door = ["door", "врат", "tur", "tür", "porte", "porta", "puerta"];
    public static readonly string[] Marker = ["marker", "маркер", "annotation", "dim"];

    /// <summary>
    /// Layer-name categories that are unambiguously *not* a window/door, even when their geometry
    /// happens to coincidentally match the perpendicular-line-with-two-numbers pattern — a negative
    /// hint, not a positive requirement. Confirmed as real false-positive sources on real data: a
    /// furniture dimension callout, a generic dimension-string annotation elsewhere in the drawing,
    /// an MEP/installations annotation, and an explicitly interior-labeled wall segment all matched
    /// the geometric pattern coincidentally and were rejected once these hints were added. Kept
    /// narrow and evidence-driven, not a growing exclusion list — this suppresses concretely
    /// demonstrated noise, it doesn't gate detection for anything without a recognizable name.
    /// </summary>
    public static readonly string[] NonOpening =
        ["furniture", "мебел", "dimension", "дименси", "installation", "инсталаци", "interior", "интериор"];

    /// <summary>The "interior" subset of <see cref="NonOpening"/>, used specifically to identify wall geometry explicitly labeled as interior (as opposed to the whole non-opening list, which also covers unrelated categories like furniture).</summary>
    public static readonly string[] ExplicitInterior = ["interior", "интериор"];

    public static bool ContainsAny(string text, string[] hints)
    {
        var lower = text.ToLowerInvariant();
        return hints.Any(lower.Contains);
    }
}
