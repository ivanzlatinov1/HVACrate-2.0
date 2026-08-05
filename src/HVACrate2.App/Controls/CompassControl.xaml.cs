using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace HVACrate2.App.Controls;

public partial class CompassControl : UserControl
{
    public static readonly DependencyProperty AngleDegreesProperty = DependencyProperty.Register(
        nameof(AngleDegrees), typeof(double), typeof(CompassControl),
        new PropertyMetadata(0.0, OnAngleChanged));

    public double AngleDegrees
    {
        get => (double)GetValue(AngleDegreesProperty);
        set => SetValue(AngleDegreesProperty, value);
    }

    /// <summary>Unbounded running angle (can exceed 0-360) so the needle always takes the shortest path when animating.</summary>
    private double _currentAngle;

    private static void OnAngleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (CompassControl)d;
        control.AnimateTo((double)e.NewValue);
    }

    private void AnimateTo(double targetDegrees)
    {
        double delta = ((targetDegrees - _currentAngle) % 360 + 540) % 360 - 180;
        double target = _currentAngle + delta;

        var animation = new DoubleAnimation
        {
            From = _currentAngle,
            To = target,
            Duration = TimeSpan.FromMilliseconds(500),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };
        Rotation.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, animation);

        _currentAngle = target;
    }

    public CompassControl()
    {
        InitializeComponent();
    }
}
