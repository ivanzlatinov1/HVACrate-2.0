namespace HVACrate2.Core.Models;

public sealed class FloorInput
{
    public string DxfPath { get; set; } = "";
    public double HeightM { get; set; }
    public double NorthDeg { get; set; }
    public int ApartmentCount { get; set; }
}
