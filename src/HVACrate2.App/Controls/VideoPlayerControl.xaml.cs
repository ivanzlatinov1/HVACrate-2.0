using System.Windows;
using System.Windows.Controls;

namespace HVACrate2.App.Controls;

public partial class VideoPlayerControl : UserControl
{
    private bool _isPlaying;

    public VideoPlayerControl()
    {
        InitializeComponent();
    }

    /// <summary>Sets the video source. Pass null (or a missing path) to show a "not found" placeholder instead.</summary>
    public void SetSource(string? path)
    {
        if (path is null || !System.IO.File.Exists(path))
        {
            Player.Visibility = Visibility.Collapsed;
            Controls.Visibility = Visibility.Collapsed;
            MissingText.Text = "Video not found. This walkthrough video ships with the development checkout " +
                                "(the videos/ folder) and isn't bundled with every build yet.";
            MissingText.Visibility = Visibility.Visible;
            return;
        }

        MissingText.Visibility = Visibility.Collapsed;
        Player.Visibility = Visibility.Visible;
        Controls.Visibility = Visibility.Visible;
        Player.Source = new Uri(path, UriKind.Absolute);
        Player.Position = TimeSpan.Zero;
        _isPlaying = false;
        PlayPauseButton.Content = "Play";
    }

    private void OnPlayPauseClick(object sender, RoutedEventArgs e)
    {
        if (_isPlaying)
        {
            Player.Pause();
            PlayPauseButton.Content = "Play";
        }
        else
        {
            Player.Play();
            PlayPauseButton.Content = "Pause";
        }
        _isPlaying = !_isPlaying;
    }

    private void OnRestartClick(object sender, RoutedEventArgs e)
    {
        Player.Position = TimeSpan.Zero;
        Player.Play();
        PlayPauseButton.Content = "Pause";
        _isPlaying = true;
    }

    private void OnMediaEnded(object sender, RoutedEventArgs e)
    {
        Player.Position = TimeSpan.Zero;
        Player.Stop();
        PlayPauseButton.Content = "Play";
        _isPlaying = false;
    }
}
