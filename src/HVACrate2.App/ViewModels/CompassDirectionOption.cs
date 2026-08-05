namespace HVACrate2.App.ViewModels;

public sealed record CompassDirectionOption(string Label, double Degrees)
{
    public static readonly CompassDirectionOption[] All =
    [
        new("North", 0),
        new("North-East", 45),
        new("East", 90),
        new("South-East", 135),
        new("South", 180),
        new("South-West", 225),
        new("West", 270),
        new("North-West", 315),
    ];

    public override string ToString() => Label;
}
