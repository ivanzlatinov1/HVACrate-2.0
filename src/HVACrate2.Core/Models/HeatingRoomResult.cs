namespace HVACrate2.Core.Models;

public sealed class HeatingRoomResult
{
    /// <summary>Thermal resistance from the pipes upward to the room, in m2K/W.</summary>
    public double RogM2KW { get; set; }

    /// <summary>Thermal resistance from the pipes downward to the room, in m2K/W.</summary>
    public double RodM2KW { get; set; }

    /// <summary>Total thermal resistance of the construction, in m2K/W.</summary>
    public double RoM2KW { get; set; }

    /// <summary>Fraction of the heat flow going to the room below.</summary>
    public double RPd { get; set; }

    /// <summary>Heat flow to the room below, in watts.</summary>
    public double QcW { get; set; }

    /// <summary>Water mass flow, in kg/h (3600 * Qc / 41870).</summary>
    public double MKgH { get; set; }

    /// <summary>Remaining/excess heat below the room's requirement, in watts (Qc - Qпт).</summary>
    public double QdolW { get; set; }
}
