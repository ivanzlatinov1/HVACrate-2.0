using netDxf;
using netDxf.Entities;

namespace HVACrate2.Core.Openings;

internal enum FlatKind { Line, Arc, Polyline2D, Text, Insert }

/// <summary>
/// One DXF entity, flattened to world coordinates (in meters, already divided by the document's
/// detected coordinate divisor) regardless of how many levels of INSERT nesting it originally sat
/// behind. This is what lets every candidate-detection strategy work the same way whether a client's
/// export puts geometry directly in model space, one block deep (e.g. a per-wall block), or many
/// blocks deep (e.g. one INSERT wrapping an entire exploded floor plan).
/// </summary>
internal sealed class FlatEntity
{
    public required FlatKind Kind { get; init; }
    public required string LayerName { get; init; }
    public required List<(double x, double y)> PointsM { get; init; }
    public double RadiusM { get; init; }
    public string Text { get; init; } = "";
    public string BlockName { get; init; } = "";
    public Dictionary<string, string> Attributes { get; init; } = new();
    public required object Source { get; init; }
}

internal static class DxfEntityIndex
{
    private const int MaxNestingDepth = 24;

    /// <summary>Recursively flattens every Line/Arc/Polyline2D/Text/MText/Insert in the document (at any INSERT nesting depth) into world-meter coordinates.</summary>
    public static List<FlatEntity> Build(DxfDocument doc, double coordDivisor)
    {
        var result = new List<FlatEntity>();
        Walk(doc.Entities.All, p => p, 1.0, [], 0, result, coordDivisor);
        return result;
    }

    private static void Walk(
        IEnumerable<EntityObject> entities,
        Func<(double x, double y), (double x, double y)> xform,
        double cumulativeScale,
        List<string> blockChain,
        int depth,
        List<FlatEntity> result,
        double coordDivisor)
    {
        if (depth > MaxNestingDepth) return;

        (double x, double y) Pm((double x, double y) p)
        {
            var w = xform(p);
            return (w.x / coordDivisor, w.y / coordDivisor);
        }

        foreach (var e in entities)
        {
            switch (e)
            {
                case Line l:
                    result.Add(new FlatEntity
                    {
                        Kind = FlatKind.Line,
                        LayerName = l.Layer.Name,
                        PointsM = [Pm((l.StartPoint.X, l.StartPoint.Y)), Pm((l.EndPoint.X, l.EndPoint.Y))],
                        Source = l,
                    });
                    break;

                case Arc a:
                    result.Add(new FlatEntity
                    {
                        Kind = FlatKind.Arc,
                        LayerName = a.Layer.Name,
                        PointsM = [Pm((a.Center.X, a.Center.Y))],
                        RadiusM = a.Radius * cumulativeScale / coordDivisor,
                        Source = a,
                    });
                    break;

                case Polyline2D p2:
                    result.Add(new FlatEntity
                    {
                        Kind = FlatKind.Polyline2D,
                        LayerName = p2.Layer.Name,
                        PointsM = p2.Vertexes.Select(v => Pm((v.Position.X, v.Position.Y))).ToList(),
                        Source = p2,
                    });
                    break;

                case MText mt:
                    result.Add(new FlatEntity
                    {
                        Kind = FlatKind.Text,
                        LayerName = mt.Layer.Name,
                        PointsM = [Pm((mt.Position.X, mt.Position.Y))],
                        Text = mt.PlainText(),
                        Source = mt,
                    });
                    break;

                case Text t:
                    result.Add(new FlatEntity
                    {
                        Kind = FlatKind.Text,
                        LayerName = t.Layer.Name,
                        PointsM = [Pm((t.Position.X, t.Position.Y))],
                        Text = t.Value,
                        Source = t,
                    });
                    break;

                case Insert ins:
                    var attrs = ins.Attributes.ToDictionary(at => at.Tag, at => at.Value?.ToString() ?? "");
                    result.Add(new FlatEntity
                    {
                        Kind = FlatKind.Insert,
                        LayerName = ins.Layer.Name,
                        PointsM = [Pm((ins.Position.X, ins.Position.Y))],
                        BlockName = ins.Block.Name,
                        Attributes = attrs,
                        Source = ins,
                    });

                    Func<(double x, double y), (double x, double y)> childXform = p => xform(TransformPoint(p.x, p.y, ins));
                    double childScale = cumulativeScale * (Math.Abs(ins.Scale.X) + Math.Abs(ins.Scale.Y)) / 2.0;
                    var childChain = new List<string>(blockChain) { ins.Block.Name };
                    Walk(ins.Block.Entities, childXform, childScale, childChain, depth + 1, result, coordDivisor);
                    break;
            }
        }
    }

    /// <summary>Standard DXF INSERT transform: subtract the block's own base point, scale, rotate, then translate to the insertion position.</summary>
    private static (double x, double y) TransformPoint(double lx, double ly, Insert insert)
    {
        double bx = insert.Block.Origin.X, by = insert.Block.Origin.Y;
        double sx = insert.Scale.X, sy = insert.Scale.Y;
        double rot = insert.Rotation * Math.PI / 180.0;
        double cos = Math.Cos(rot), sin = Math.Sin(rot);
        double x0 = (lx - bx) * sx, y0 = (ly - by) * sy;
        double wx = x0 * cos - y0 * sin;
        double wy = x0 * sin + y0 * cos;
        return (insert.Position.X + wx, insert.Position.Y + wy);
    }
}
