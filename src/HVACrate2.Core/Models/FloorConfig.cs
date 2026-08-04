namespace HVACrate2.Core.Models;

public class FloorConfig
{
    public string DxfPath { get; set; } = "Brizstroy_Misari.dxf";
    public string ExcelPath { get; set; } = "ЕЕ_МИСАРИ.xlsx";
    public int FloorRow { get; set; } = 7;   // A7 = I етаж, в блока "Геометрични характеристики"
    public int WallRow { get; set; } = 31;   // A31 = I етаж, в блока "Описание на стените"
    public double FloorHeightM { get; set; } = 2.89;
    public double NorthDeg { get; set; } = 0.0;
    public string WallLayer { get; set; } = "_A [walls]";
    public string OvkLayer { get; set; } = "OVK";
}
