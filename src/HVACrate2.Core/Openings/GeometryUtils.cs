namespace HVACrate2.Core.Openings;

internal static class GeometryUtils
{
    public static double Distance(double x1, double y1, double x2, double y2)
    {
        double dx = x2 - x1, dy = y2 - y1;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public static double DistancePointToSegment(double px, double py, double x1, double y1, double x2, double y2)
    {
        double dx = x2 - x1, dy = y2 - y1;
        double lenSq = dx * dx + dy * dy;
        if (lenSq < 1e-9) return Distance(px, py, x1, y1);
        double t = Math.Clamp(((px - x1) * dx + (py - y1) * dy) / lenSq, 0.0, 1.0);
        double cx = x1 + t * dx, cy = y1 + t * dy;
        return Distance(px, py, cx, cy);
    }

    /// <summary>Distance from a point to the nearest OVK edge, and which edge (index into <paramref name="ovkEdges"/>) it was.</summary>
    public static double DistancePointToOvk(
        double px, double py, IReadOnlyList<(double x1, double y1, double x2, double y2)> ovkEdges, out int nearestEdgeIndex)
    {
        double best = double.MaxValue;
        int bestIdx = -1;
        for (int i = 0; i < ovkEdges.Count; i++)
        {
            var (x1, y1, x2, y2) = ovkEdges[i];
            double d = DistancePointToSegment(px, py, x1, y1, x2, y2);
            if (d < best) { best = d; bestIdx = i; }
        }
        nearestEdgeIndex = bestIdx;
        return best;
    }

    /// <summary>Angle (0-90 deg) between a line direction and an OVK edge's own direction — 0 means parallel, 90 means perpendicular.</summary>
    public static double AngleToEdgeDeg(double lineDx, double lineDy, double edgeX1, double edgeY1, double edgeX2, double edgeY2)
    {
        double edx = edgeX2 - edgeX1, edy = edgeY2 - edgeY1;
        double dot = lineDx * edx + lineDy * edy;
        double mag = Math.Sqrt(lineDx * lineDx + lineDy * lineDy) * Math.Sqrt(edx * edx + edy * edy);
        if (mag < 1e-9) return 0.0;
        double cos = Math.Clamp(Math.Abs(dot) / mag, -1.0, 1.0);
        return Math.Acos(cos) * 180.0 / Math.PI;
    }
}
