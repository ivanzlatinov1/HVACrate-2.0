using ClosedXML.Excel;
using HVACrate2.Core.Models;
using netDxf;
using netDxf.Entities;

namespace HVACrate2.Core;

public static class Program
{
    // ред по посока на часовниковата стрелка, започвайки от север — трябва
    // да съвпада с реда на DirCols по-долу
    private static readonly string[] DirOrder = ["С", "СИ", "И", "ЮИ", "Ю", "ЮЗ", "З", "СЗ"];

    // ъгъл (математически, 0°=+X/изток, 90°=+Y/север при north=0) -> компасна посока
    private static string BearingToDirection(double mathAngleDeg, double northDeg)
    {
        double bearing = ((90.0 - mathAngleDeg) % 360.0 + 360.0) % 360.0;
        double adjusted = ((bearing - northDeg) % 360.0 + 360.0) % 360.0;
        int idx = (int)((adjusted + 22.5) % 360.0 / 45.0) % 8;
        return DirOrder[idx];
    }

    // посока на "навън" нормалата на ребро от затворен полигон (OVK граница).
    // ccwSign трябва да идва от общата ориентация (winding) на целия полигон —
    // само дължината/ъгълът на отделното ребро не е достатъчен, за да се
    // различи напр. северна от южна стена (двете биха дали еднакъв "неориентиран"
    // ъгъл).
    private static string EdgeOutwardDirection(double x1, double y1, double x2, double y2, double northDeg, double ccwSign)
    {
        double dx = x2 - x1, dy = y2 - y1;
        double nx = ccwSign >= 0 ? dy : -dy;
        double ny = ccwSign >= 0 ? -dx : dx;
        double angle = Math.Atan2(ny, nx) * 180.0 / Math.PI;
        return BearingToDirection(angle, northDeg);
    }

    private static double SignedArea(List<(double x, double y)> vertices)
    {
        double sum = 0.0;
        for (int i = 0; i < vertices.Count - 1; i++)
            sum += vertices[i].x * vertices[i + 1].y - vertices[i + 1].x * vertices[i].y;
        return sum / 2.0;
    }

    // --- граница на етажа (слой OVK), в метри ---

    private static List<(double x, double y)> OvkVertices(DxfDocument doc, string ovkLayer)
    {
        var poly = doc.Entities.Polylines2D.FirstOrDefault(p => p.Layer.Name.Contains(ovkLayer))
            ?? throw new InvalidOperationException($"Не е намерена гранична полилиния на слой '{ovkLayer}'.");
        return poly.Vertexes.Select(v => (v.Position.X / 100.0, v.Position.Y / 100.0)).ToList();
    }

    private static List<(double x1, double y1, double x2, double y2)> OvkEdges(List<(double x, double y)> vertices)
    {
        var edges = new List<(double, double, double, double)>();
        for (int i = 0; i < vertices.Count - 1; i++)
            edges.Add((vertices[i].x, vertices[i].y, vertices[i + 1].x, vertices[i + 1].y));
        return edges;
    }

    private static Dictionary<string, double> WallLengthByDirection(
        List<(double x1, double y1, double x2, double y2)> ovkEdges, double northDeg, double ccwSign)
    {
        var totals = DirOrder.ToDictionary(d => d, d => 0.0);
        foreach (var (x1, y1, x2, y2) in ovkEdges)
        {
            double length = Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
            if (length < 1e-6) continue;
            totals[EdgeOutwardDirection(x1, y1, x2, y2, northDeg, ccwSign)] += length;
        }
        return totals;
    }

    // --- прозорци/врати: маркери (W Marker/D Marker), вложени във всеки блок,
    // взети за "външни" само ако допират границата OVK ---

    private static double DistancePointToSegment(double px, double py, double x1, double y1, double x2, double y2)
    {
        double dx = x2 - x1, dy = y2 - y1;
        double lenSq = dx * dx + dy * dy;
        if (lenSq < 1e-9) return Math.Sqrt((px - x1) * (px - x1) + (py - y1) * (py - y1));
        double t = Math.Clamp(((px - x1) * dx + (py - y1) * dy) / lenSq, 0.0, 1.0);
        double cx = x1 + t * dx, cy = y1 + t * dy;
        return Math.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
    }

    private static string? NearestOvkDirection(
        double px, double py, List<(double x1, double y1, double x2, double y2)> ovkEdges,
        double northDeg, double ccwSign, double toleranceM)
    {
        double bestDist = double.MaxValue;
        string? bestDir = null;
        foreach (var (x1, y1, x2, y2) in ovkEdges)
        {
            double d = DistancePointToSegment(px, py, x1, y1, x2, y2);
            if (d < bestDist)
            {
                bestDist = d;
                bestDir = EdgeOutwardDirection(x1, y1, x2, y2, northDeg, ccwSign);
            }
        }
        return bestDist <= toleranceM ? bestDir : null;
    }

    private static List<Opening> ExtractOpeningsTouchingOvk(
        DxfDocument doc, List<(double x1, double y1, double x2, double y2)> ovkEdges,
        double northDeg, double ccwSign, double toleranceM = 0.5)
    {
        var markers = doc.Entities.Inserts
            .Concat(doc.Blocks.SelectMany(b => b.Entities.OfType<Insert>()))
            .Where(i => i.Block.Name.StartsWith("W Marker") || i.Block.Name.StartsWith("D Marker"));

        var openings = new List<Opening>();
        foreach (var ins in markers)
        {
            var attrs = ins.Attributes.ToDictionary(a => a.Tag, a => a.Value?.ToString() ?? "");
            if (!attrs.TryGetValue("AC_MarkerText_2", out var wStr)) continue;
            if (!attrs.TryGetValue("AC_MarkerText_3", out var hStr)) continue;
            if (!double.TryParse(wStr.Replace(",", "."), out double widthCm)) continue;
            if (!double.TryParse(hStr.Replace(",", "."), out double heightCm)) continue;

            double px = ins.Position.X / 100.0, py = ins.Position.Y / 100.0;
            string? dir = NearestOvkDirection(px, py, ovkEdges, northDeg, ccwSign, toleranceM);
            if (dir == null) continue; // не допира OVK -> вътрешен отвор, пропуска се

            openings.Add(new Opening
            {
                WidthM = Math.Round(widthCm / 100.0, 2),
                HeightM = Math.Round(heightCm / 100.0, 2),
                Direction = dir,
            });
        }
        return openings;
    }

    private static Dictionary<(double w, double h), Dictionary<string, int>> GroupOpenings(List<Opening> openings)
    {
        var groups = new Dictionary<(double, double), Dictionary<string, int>>();
        foreach (var o in openings)
        {
            var key = (o.WidthM, o.HeightM);
            if (!groups.ContainsKey(key))
                groups[key] = DirOrder.ToDictionary(d => d, d => 0);
            groups[key][o.Direction]++;
        }
        return groups;
    }

    private static readonly Dictionary<string, string> DirCols = new()
    {
        ["С"] = "D",
        ["СИ"] = "E",
        ["И"] = "F",
        ["ЮИ"] = "G",
        ["Ю"] = "H",
        ["ЮЗ"] = "I",
        ["З"] = "J",
        ["СЗ"] = "K",
    };

    private static void WriteToExcel(
        XLWorkbook wb, FloorConfig cfg,
        Dictionary<string, double> wallTotals,
        Dictionary<(double w, double h), Dictionary<string, int>> openingGroups)
    {
        var ws = wb.Worksheet("Изчисления");

        int r = cfg.WallRow;
        ws.Cell($"C{r}").Value = cfg.FloorHeightM;
        double totalPerimeter = 0.0;
        foreach (var (dir, col) in DirCols)
        {
            double val = Math.Round(wallTotals.GetValueOrDefault(dir, 0.0), 2);
            if (val > 0)
                ws.Cell($"{col}{r}").Value = val;
            totalPerimeter += val;
        }
        ws.Cell($"L{r}").Value = Math.Round(totalPerimeter, 2);

        int startRow = 57;
        int row = startRow;
        while (!ws.Cell($"A{row}").IsEmpty())
            row++;

        foreach (var kvp in openingGroups)
        {
            var (widthM, heightM) = kvp.Key;
            var byDir = kvp.Value;

            ws.Cell($"A{row}").Value = row - startRow + 1;
            ws.Cell($"B{row}").Value = widthM;
            ws.Cell($"C{row}").Value = heightM;
            foreach (var (dir, col) in DirCols)
            {
                if (byDir[dir] > 0)
                    ws.Cell($"{col}{row}").Value = byDir[dir];
            }
            row++;
        }
    }

    public static void Main(string[] args)
    {
        var cfg = new FloorConfig(); // TODO: зареждане от CLI аргументи / UI форма

        var doc = DxfDocument.Load(cfg.DxfPath);

        var ovkVertices = OvkVertices(doc, cfg.OvkLayer);
        var ovkEdges = OvkEdges(ovkVertices);
        double signedArea = SignedArea(ovkVertices);
        double ccwSign = Math.Sign(signedArea);

        var wallTotals = WallLengthByDirection(ovkEdges, cfg.NorthDeg, ccwSign);
        double areaM2 = Math.Abs(signedArea);
        double volumeM3 = areaM2 * cfg.FloorHeightM;

        var openings = ExtractOpeningsTouchingOvk(doc, ovkEdges, cfg.NorthDeg, ccwSign);
        var openingGroups = GroupOpenings(openings);

        using var wb = new XLWorkbook(cfg.ExcelPath);
        WriteToExcel(wb, cfg, wallTotals, openingGroups);
        wb.Save();

        Console.WriteLine("Дължини на стени по посока (по границата OVK):");
        foreach (var (dir, len) in wallTotals)
            Console.WriteLine($"  {dir}: {len:F2} m");
        Console.WriteLine($"Лице (Af): {areaM2:F2} m2, обем (V, h={cfg.FloorHeightM}m): {volumeM3:F2} m3");

        Console.WriteLine("Прозорци/врати (широчина x височина) -> бр. по посока:");
        foreach (var kvp in openingGroups)
            Console.WriteLine($"  {kvp.Key.w}m x {kvp.Key.h}m -> {string.Join(", ", kvp.Value.Where(x => x.Value > 0).Select(x => $"{x.Key}={x.Value}"))}");
    }
}
