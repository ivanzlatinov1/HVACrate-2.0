using ClosedXML.Excel;
using HVACrate2.Core.Models;
using netDxf;
using netDxf.Entities;

namespace HVACrate2.Core;

public static class Program
{
    private static readonly string[] DirOrder = ["С", "СИ", "И", "ЮИ", "Ю", "ЮЗ", "З", "СЗ"];

    private static string BearingToDirection(double mathAngleDeg, double northDeg)
    {
        double bearing = ((90.0 - mathAngleDeg) % 360.0 + 360.0) % 360.0;
        double adjusted = ((bearing - northDeg) % 360.0 + 360.0) % 360.0;
        int idx = (int)((adjusted + 22.5) % 360.0 / 45.0) % 8;
        return DirOrder[idx];
    }

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

    // брой изпъкнали (изходящи навън) ъгли на OVK полигона — reflex
    // (вдлъбнати) ъгли се броят отделно и не влизат в n. vertices идва с
    // дублирана затваряща точка в края (както го връща OvkVertices).
    private static int CountConvexCorners(List<(double x, double y)> vertices, double ccwSign)
    {
        int n = vertices.Count - 1;
        int convex = 0;
        for (int i = 0; i < n; i++)
        {
            var prev = vertices[(i - 1 + n) % n];
            var cur = vertices[i];
            var next = vertices[i + 1];
            double ex1 = cur.x - prev.x, ey1 = cur.y - prev.y;
            double ex2 = next.x - cur.x, ey2 = next.y - cur.y;
            double cross = ex1 * ey2 - ey1 * ex2;
            if (Math.Sign(cross) == ccwSign || cross == 0)
                convex++;
        }
        return convex;
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
            if (dir == null) continue;

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
        double areaM2, double volumeM3, int convexCorners,
        Dictionary<string, double> wallTotals,
        Dictionary<(double w, double h), Dictionary<string, int>> openingGroups)
    {
        var ws = wb.Worksheet("Изчисления");

        double totalPerimeter = wallTotals.Values.Sum(v => Math.Round(v, 2));

        int fr = cfg.FloorRow;
        ws.Cell($"C{fr}").Value = Math.Round(areaM2, 2);
        ws.Cell($"E{fr}").Value = cfg.FloorHeightM;
        ws.Cell($"F{fr}").Value = Math.Round(volumeM3, 2);
        ws.Cell($"G{fr}").Value = Math.Round(totalPerimeter, 2);
        ws.Cell($"K{fr}").Value = convexCorners;

        int r = cfg.WallRow;
        ws.Cell($"C{r}").Value = cfg.FloorHeightM;
        foreach (var (dir, col) in DirCols)
        {
            double val = Math.Round(wallTotals.GetValueOrDefault(dir, 0.0), 2);
            if (val > 0)
                ws.Cell($"{col}{r}").Value = val;
        }
        ws.Cell($"L{r}").Value = Math.Round(totalPerimeter, 2);

        int startRow = 57;
        int row = startRow;
        while (!ws.Cell($"B{row}").IsEmpty())
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
        var cfg = new FloorConfig();

        var doc = DxfDocument.Load(cfg.DxfPath);

        var ovkVertices = OvkVertices(doc, cfg.OvkLayer);
        var ovkEdges = OvkEdges(ovkVertices);
        double signedArea = SignedArea(ovkVertices);
        double ccwSign = Math.Sign(signedArea);

        var wallTotals = WallLengthByDirection(ovkEdges, cfg.NorthDeg, ccwSign);
        double areaM2 = Math.Abs(signedArea);
        double volumeM3 = areaM2 * cfg.FloorHeightM;
        int convexCorners = CountConvexCorners(ovkVertices, ccwSign);

        var openings = ExtractOpeningsTouchingOvk(doc, ovkEdges, cfg.NorthDeg, ccwSign);
        var openingGroups = GroupOpenings(openings);

        using var wb = new XLWorkbook(cfg.ExcelPath);
        WriteToExcel(wb, cfg, areaM2, volumeM3, convexCorners, wallTotals, openingGroups);
        wb.Save();

        Console.WriteLine($"OVK ребра (общо {ovkEdges.Count}):");
        foreach (var (x1, y1, x2, y2) in ovkEdges)
        {
            double len = Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
            Console.WriteLine($"  ({x1:F2},{y1:F2}) -> ({x2:F2},{y2:F2})  len={len:F2}  dir={EdgeOutwardDirection(x1, y1, x2, y2, cfg.NorthDeg, ccwSign)}");
        }

        Console.WriteLine("Дължини на стени по посока (по границата OVK):");
        foreach (var (dir, len) in wallTotals)
            Console.WriteLine($"  {dir}: {len:F2} m");
        Console.WriteLine($"Лице (Af): {areaM2:F2} m2, обем (V, h={cfg.FloorHeightM}m): {volumeM3:F2} m3, външни ъгли n={convexCorners}");

        Console.WriteLine("Прозорци/врати (широчина x височина) -> бр. по посока:");
        foreach (var kvp in openingGroups)
            Console.WriteLine($"  {kvp.Key.w}m x {kvp.Key.h}m -> {string.Join(", ", kvp.Value.Where(x => x.Value > 0).Select(x => $"{x.Key}={x.Value}"))}");
    }
}
