namespace HVACrate2.Core.Models;

public sealed class HeatingRoomInput
{
    /// <summary>Half the thickness of the pipe screed, in meters. Shared by both Rог and Rод.</summary>
    public double DeltaBetM { get; set; }

    /// <summary>Thickness of the floor leveling screed (теракотена), in meters.</summary>
    public double DeltaZamPodM { get; set; }

    /// <summary>Thickness of the tile (теракота), in meters.</summary>
    public double DeltaTerM { get; set; }

    /// <summary>Thickness of the insulation (стиропор), in meters.</summary>
    public double DeltaIzolM { get; set; }

    /// <summary>Thickness of the slab (плоча), in meters.</summary>
    public double DeltaPlochM { get; set; }

    /// <summary>Thickness of the ceiling screed, in meters.</summary>
    public double DeltaZamTavM { get; set; }

    /// <summary>Required heat of the room, in watts.</summary>
    public double QptW { get; set; }
}
