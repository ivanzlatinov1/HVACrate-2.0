using HVACrate2.App.Shared;

namespace HVACrate2.App.Work;

/// <summary>Label holds a Loc resource key (e.g. "Str_Dir_North"), not display text directly —
/// resolved at ToString() time so the dropdown reflects whatever language is active when the
/// Work page is (re)built.</summary>
public sealed record CompassDirectionOption(string Label, double Degrees)
{
    public static readonly CompassDirectionOption[] All =
    [
        new("Str_Dir_North", 0),
        new("Str_Dir_NorthEast", 45),
        new("Str_Dir_East", 90),
        new("Str_Dir_SouthEast", 135),
        new("Str_Dir_South", 180),
        new("Str_Dir_SouthWest", 225),
        new("Str_Dir_West", 270),
        new("Str_Dir_NorthWest", 315),
    ];

    public override string ToString() => Loc.Get(Label);
}
