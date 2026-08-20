using ClosedXML.Excel;
using HVACrate2.Core.Models;
using HVACrate2.Core.Openings;
using netDxf;

namespace HVACrate2.Core;

public static class FloorProcessor
{
    internal static readonly string[] DirOrder = ["С", "СИ", "И", "ЮИ", "Ю", "ЮЗ", "З", "СЗ"];

    private const int FirstFloorGeometryRow = 7;
    private const int FirstFloorWallRow = 31;
    private const int OpeningsTableStartRow = 57;

    internal static string BearingToDirection(double mathAngleDeg, double northDeg)
    {
        double bearing = ((90.0 - mathAngleDeg) % 360.0 + 360.0) % 360.0;
        double adjusted = ((bearing - northDeg) % 360.0 + 360.0) % 360.0;
        int idx = (int)((adjusted + 22.5) % 360.0 / 45.0) % 8;
        return DirOrder[idx];
    }

    internal static string EdgeOutwardDirection(double x1, double y1, double x2, double y2, double northDeg, double ccwSign)
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

    private const double CentimeterDivisor = 100.0;
    private const double MillimeterDivisor = 1000.0;

    private const double ImplausibleFloorAreaM2 = 20000.0;

    private static List<(double x, double y)> OvkVertices(DxfDocument doc, string ovkLayer, double coordDivisor)
    {
        List<(double x, double y)> ToClosedRing(IEnumerable<(double x, double y)> raw)
        {
            var pts = raw.ToList();
            if (pts.Count >= 2)
            {
                var (fx, fy) = pts[0];
                var (lx, ly) = pts[^1];
                if (Math.Abs(fx - lx) > 1e-6 || Math.Abs(fy - ly) > 1e-6)
                    pts.Add(pts[0]);
            }
            return pts;
        }

        var candidates = doc.Entities.Polylines2D
            .Where(p => p.Layer.Name.Contains(ovkLayer))
            .Select(p => ToClosedRing(p.Vertexes.Select(v => (v.Position.X / coordDivisor, v.Position.Y / coordDivisor))))
            .Where(v => v.Count >= 4)
            .ToList();

        if (candidates.Count == 0)
            throw new InvalidOperationException($"Не е намерена гранична полилиния на слой '{ovkLayer}'.");

        return candidates.OrderByDescending(v => Math.Abs(SignedArea(v))).First();
    }

    /// <summary>
    /// Picks the coordinate-to-meters divisor for this file: centimeters by default, falling back
    /// to millimeters only if that produces an implausible floor area. A real floor plate's area
    /// scales as the square of the divisor error, so the two conventions are never close enough to
    /// confuse — a centimeter-file misread as millimeters undershoots by 100x, not by a few percent.
    /// </summary>
    private static double DetectCoordinateDivisor(DxfDocument doc, string ovkLayer)
    {
        var atCm = OvkVertices(doc, ovkLayer, CentimeterDivisor);
        return Math.Abs(SignedArea(atCm)) <= ImplausibleFloorAreaM2 ? CentimeterDivisor : MillimeterDivisor;
    }

    private static List<(double x1, double y1, double x2, double y2)> OvkEdges(List<(double x, double y)> vertices)
    {
        var edges = new List<(double, double, double, double)>();
        for (int i = 0; i < vertices.Count - 1; i++)
            edges.Add((vertices[i].x, vertices[i].y, vertices[i + 1].x, vertices[i + 1].y));
        return edges;
    }

    private static int CountConvexCorners(List<(double x, double y)> vertices, double ccwSign)
    {
        int n = vertices.Count - 1;
        int convex = 0;
        for (int i = 0; i < n; i++)
        {
            var (x, y) = vertices[(i - 1 + n) % n];
            var cur = vertices[i];
            var next = vertices[i + 1];
            double ex1 = cur.x - x, ey1 = cur.y - y;
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

    /// <summary>Reads one floor's DXF and computes area/volume/corners/wall lengths/openings from its OVK boundary.</summary>
    public static FloorResult ProcessFloor(FloorInput input, string ovkLayer = "OVK")
        => ProcessFloorFromDocument(DxfDocument.Load(input.DxfPath), input, ovkLayer);

    /// <summary>Same as <see cref="ProcessFloor"/> but takes an already-loaded/constructed document — the entry point synthetic tests use to exercise the pipeline without needing a file on disk.</summary>
    public static FloorResult ProcessFloorFromDocument(DxfDocument doc, FloorInput input, string ovkLayer = "OVK")
    {
        double coordDivisor = DetectCoordinateDivisor(doc, ovkLayer);
        var ovkVertices = OvkVertices(doc, ovkLayer, coordDivisor);
        var ovkEdges = OvkEdges(ovkVertices);
        double signedArea = SignedArea(ovkVertices);
        double ccwSign = Math.Sign(signedArea);

        var wallTotals = WallLengthByDirection(ovkEdges, input.NorthDeg, ccwSign);
        double areaM2 = Math.Abs(signedArea);
        double volumeM3 = areaM2 * input.HeightM;
        int convexCorners = CountConvexCorners(ovkVertices, ccwSign);

        var (openings, openingDiagnostics) = OpeningExtractor.Extract(doc, ovkEdges, input.NorthDeg, ccwSign, coordDivisor);
        var openingGroups = GroupOpenings(openings);

        return new FloorResult
        {
            AreaM2 = areaM2,
            VolumeM3 = volumeM3,
            ConvexCorners = convexCorners,
            WallTotals = wallTotals,
            OpeningGroups = openingGroups,
            OvkVerticesM = ovkVertices,
            Openings = openings,
            OpeningDiagnostics = openingDiagnostics,
        };
    }

    /// <summary>Writes one floor's computed result into the workbook at the row slot for the given 0-based floor index (0 = Floor I).</summary>
    public static void WriteFloorToExcel(XLWorkbook wb, FloorInput input, FloorResult result, int floorIndex)
    {
        var ws = wb.Worksheet("Изчисления");

        double totalPerimeter = result.WallTotals.Values.Sum(v => Math.Round(v, 2));

        int fr = FirstFloorGeometryRow + floorIndex;
        ws.Cell($"C{fr}").Value = Math.Round(result.AreaM2, 2);
        ws.Cell($"E{fr}").Value = input.HeightM;
        ws.Cell($"F{fr}").Value = Math.Round(result.VolumeM3, 2);
        ws.Cell($"G{fr}").Value = Math.Round(totalPerimeter, 2);
        ws.Cell($"K{fr}").Value = result.ConvexCorners;

        int r = FirstFloorWallRow + floorIndex;
        ws.Cell($"C{r}").Value = input.HeightM;
        foreach (var (dir, col) in DirCols)
        {
            double val = Math.Round(result.WallTotals.GetValueOrDefault(dir, 0.0), 2);
            if (val > 0)
                ws.Cell($"{col}{r}").Value = val;
        }
        ws.Cell($"L{r}").Value = Math.Round(totalPerimeter, 2);
    }

    /// <summary>Adds one floor's opening counts into a building-wide accumulator, summing counts for any (width, height) size already seen on an earlier floor instead of keeping them as separate entries.</summary>
    private static void MergeOpeningGroups(
        Dictionary<(double w, double h), Dictionary<string, int>> target,
        Dictionary<(double w, double h), Dictionary<string, int>> source)
    {
        foreach (var (key, byDir) in source)
        {
            if (!target.TryGetValue(key, out var totals))
            {
                totals = DirOrder.ToDictionary(d => d, d => 0);
                target[key] = totals;
            }
            foreach (var (dir, count) in byDir)
                totals[dir] += count;
        }
    }

    /// <summary>Writes the building-wide openings table (one row per distinct width×height size, summed across all floors) starting at <see cref="OpeningsTableStartRow"/>.</summary>
    private static void WriteOpeningsTable(XLWorkbook wb, Dictionary<(double w, double h), Dictionary<string, int>> mergedGroups)
    {
        var ws = wb.Worksheet("Изчисления");

        int row = OpeningsTableStartRow;
        while (!ws.Cell($"B{row}").IsEmpty())
            row++;

        foreach (var kvp in mergedGroups.OrderBy(g => g.Key.w).ThenBy(g => g.Key.h))
        {
            var (widthM, heightM) = kvp.Key;
            var byDir = kvp.Value;

            ws.Cell($"A{row}").Value = row - OpeningsTableStartRow + 1;
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

    /// <summary>Writes the building-wide "electric consumers" / lamp counts, derived from total apartment count and total floor area across all floors.</summary>
    private static void WriteApplianceBlock(XLWorkbook wb, int totalApartments, double totalAreaM2)
    {
        var ws = wb.Worksheet("Изчисления");

        ws.Cell("D317").Value = totalApartments;
        ws.Cell("D321").Value = totalApartments;
        ws.Cell("D331").Value = 2 * totalApartments;
        ws.Cell("D332").Value = totalApartments;
        ws.Cell("D333").Value = 2 * totalApartments;
        ws.Cell("D336").Value = 5 * totalApartments;
        ws.Cell("D291").Value = Math.Ceiling(7 * totalAreaM2 / 20.0);
        ws.Cell("D348").Value = totalApartments;
    }

    /// <summary>Computes each floor's DXF result (in order, floor 0 = Floor I) without writing Excel — for a review/preview step before committing to the write.</summary>
    public static List<FloorResult> ProcessFloors(IReadOnlyList<FloorInput> floors, string ovkLayer = "OVK")
        => floors.Select(f => ProcessFloor(f, ovkLayer)).ToList();

    /// <summary>
    /// Writes already-computed per-floor results into a copy of the template workbook saved at
    /// <paramref name="outputExcelPath"/>. The template file itself is never modified. Takes
    /// pre-computed <see cref="FloorResult"/>s (e.g. from <see cref="ProcessFloors"/>) so a caller
    /// that already showed the user a preview doesn't have to re-parse every DXF to write it.
    /// </summary>
    public static void WriteFloorsToExcel(
        IReadOnlyList<FloorInput> floors, IReadOnlyList<FloorResult> results,
        string templateExcelPath, string outputExcelPath)
    {
        using var wb = new XLWorkbook(templateExcelPath);
        int totalApartments = 0;
        double totalAreaM2 = 0.0;
        var mergedOpenings = new Dictionary<(double w, double h), Dictionary<string, int>>();
        for (int i = 0; i < floors.Count; i++)
        {
            WriteFloorToExcel(wb, floors[i], results[i], i);
            MergeOpeningGroups(mergedOpenings, results[i].OpeningGroups);
            totalApartments += floors[i].ApartmentCount;
            totalAreaM2 += results[i].AreaM2;
        }
        WriteOpeningsTable(wb, mergedOpenings);
        WriteApplianceBlock(wb, totalApartments, totalAreaM2);
        wb.SaveAs(outputExcelPath);
    }

    /// <summary>
    /// Full pipeline for a building: processes each floor's DXF (in order, floor 0 = Floor I) and
    /// writes all results into a copy of the template workbook saved at <paramref name="outputExcelPath"/>.
    /// The template file itself is never modified.
    /// </summary>
    public static void ProcessAndWriteFloors(
        IReadOnlyList<FloorInput> floors, string templateExcelPath, string outputExcelPath, string ovkLayer = "OVK")
    {
        var results = ProcessFloors(floors, ovkLayer);
        WriteFloorsToExcel(floors, results, templateExcelPath, outputExcelPath);
    }
}
