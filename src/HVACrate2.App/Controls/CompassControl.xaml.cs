using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

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

    private static void OnAngleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (CompassControl)d;
        control.Rotation.Angle = (double)e.NewValue;
    }

    public CompassControl()
    {
        InitializeComponent();
        TryLoadCompassImage();
    }

    private void TryLoadCompassImage()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Assets/compass.png", UriKind.Absolute);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = uri;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();

            CompassImage.Source = bitmap;
            CompassImage.Visibility = Visibility.Visible;
            FallbackRose.Visibility = Visibility.Collapsed;
        }
        catch
        {
            // Asset not present yet — keep the vector fallback rose.
        }
    }
}
