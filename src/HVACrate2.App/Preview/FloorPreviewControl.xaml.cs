using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using HVACrate2.Core.Models;

namespace HVACrate2.App.Preview;

/// <summary>
/// Draws a floor's extracted OVK boundary and opening positions on a 2D canvas, for visual review
/// before writing to Excel. Purely a preview — reads already-computed <see cref="FloorResult"/>
/// data, no extraction or classification logic lives here.
/// </summary>
public partial class FloorPreviewControl : UserControl
{
    public static readonly DependencyProperty ResultProperty = DependencyProperty.Register(
        nameof(Result), typeof(FloorResult), typeof(FloorPreviewControl),
        new PropertyMetadata(null, OnDataChanged));

    public static readonly DependencyProperty NorthDegProperty = DependencyProperty.Register(
        nameof(NorthDeg), typeof(double), typeof(FloorPreviewControl),
        new PropertyMetadata(0.0, OnDataChanged));

    public FloorResult? Result
    {
        get => (FloorResult?)GetValue(ResultProperty);
        set => SetValue(ResultProperty, value);
    }

    public double NorthDeg
    {
        get => (double)GetValue(NorthDegProperty);
        set => SetValue(NorthDegProperty, value);
    }

    private const double CanvasPadding = 28;

    public FloorPreviewControl()
    {
        InitializeComponent();
    }

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((FloorPreviewControl)d).Redraw();

    private void Redraw()
    {
        DrawCanvas.Children.Clear();

        var result = Result;
        if (result is null || result.OvkVerticesM.Count < 2) return;

        double minX = result.OvkVerticesM.Min(v => v.X);
        double maxX = result.OvkVerticesM.Max(v => v.X);
        double minY = result.OvkVerticesM.Min(v => v.Y);
        double maxY = result.OvkVerticesM.Max(v => v.Y);
        double spanX = Math.Max(maxX - minX, 0.01);
        double spanY = Math.Max(maxY - minY, 0.01);

        double drawableW = DrawCanvas.Width - 2 * CanvasPadding;
        double drawableH = DrawCanvas.Height - 2 * CanvasPadding;
        double scale = Math.Min(drawableW / spanX, drawableH / spanY);

        // DXF Y grows "up"; canvas Y grows down — flip so the drawing reads the right way round.
        Point ToScreen(double x, double y) => new(
            CanvasPadding + (x - minX) * scale,
            CanvasPadding + (maxY - y) * scale);

        var boundary = new Polyline
        {
            Points = new PointCollection(result.OvkVerticesM.Select(v => ToScreen(v.X, v.Y))),
            StrokeThickness = 2,
        };
        boundary.SetResourceReference(Shape.StrokeProperty, "AccentBrush");
        DrawCanvas.Children.Add(boundary);

        foreach (var opening in result.Openings)
        {
            var p = ToScreen(opening.PositionXM, opening.PositionYM);
            var marker = new Ellipse { Width = 8, Height = 8 };
            marker.SetResourceReference(Shape.FillProperty, "TextPrimaryBrush");
            Canvas.SetLeft(marker, p.X - 4);
            Canvas.SetTop(marker, p.Y - 4);
            marker.ToolTip = $"{opening.WidthM:0.##}×{opening.HeightM:0.##} m — {opening.Direction}";
            DrawCanvas.Children.Add(marker);
        }

        DrawNorthArrow();
    }

    /// <summary>
    /// Small "N" indicator showing which way true north points inside the drawing, given the
    /// floor's own north-offset — derived from the exact same bearing convention
    /// FloorProcessor.BearingToDirection uses (bearing = 90 - mathAngle), so it always agrees with
    /// the direction letters shown in the results table, not a separately-guessed rotation.
    /// </summary>
    private void DrawNorthArrow()
    {
        double mathAngleRad = (90.0 - NorthDeg) * Math.PI / 180.0;
        double dxfDx = Math.Cos(mathAngleRad), dxfDy = Math.Sin(mathAngleRad);
        double screenDx = dxfDx, screenDy = -dxfDy; // same Y-flip as ToScreen

        // Anchor needs enough clearance for the label to stay inside the canvas (which clips) in
        // every direction the arrow can point (North=0 sends it straight up, North=180 straight
        // down, etc.) — not just the corner position that happened to work for one specific angle.
        // Line length 14 + label gap 8 + the label's own half-extent (~9px for "N" at this size)
        // means the label center can land up to ~31px from the anchor; 40 leaves a safe margin.
        const double cx = 40, cy = 40, len = 14;
        var line = new Line
        {
            X1 = cx, Y1 = cy,
            X2 = cx + screenDx * len, Y2 = cy + screenDy * len,
            StrokeThickness = 2,
        };
        line.SetResourceReference(Shape.StrokeProperty, "TextSecondaryBrush");
        DrawCanvas.Children.Add(line);

        var label = new TextBlock { Text = "N", FontSize = 11, FontWeight = FontWeights.Bold };
        label.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
        Canvas.SetLeft(label, cx + screenDx * (len + 8) - 5);
        Canvas.SetTop(label, cy + screenDy * (len + 8) - 7);
        DrawCanvas.Children.Add(label);
    }
}
