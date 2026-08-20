using System.Windows;
using System.Windows.Controls;
using HVACrate2.App.Shared;

namespace HVACrate2.App.Instructions;

public partial class VideoPlayerControl : UserControl
{
    private bool _isPlaying;

    public VideoPlayerControl()
    {
        InitializeComponent();
    }

    /// <summary>Sets the video source (local file or http/https URL). Pass null to show a "not found" placeholder.</summary>
    public void SetSource(Uri? source)
    {
        if (source is null)
        {
            Player.Visibility = Visibility.Collapsed;
            Controls.Visibility = Visibility.Collapsed;
            LoadingText.Visibility = Visibility.Collapsed;
            MissingText.Text = Loc.Get("Str_Video_Missing");
            MissingText.Visibility = Visibility.Visible;
            return;
        }

        MissingText.Visibility = Visibility.Collapsed;
        bool isRemote = source.Scheme is "http" or "https";
        Player.Visibility = isRemote ? Visibility.Collapsed : Visibility.Visible;
        LoadingText.Visibility = isRemote ? Visibility.Visible : Visibility.Collapsed;
        Controls.Visibility = Visibility.Visible;
        Player.Source = source;
        Player.Position = TimeSpan.Zero;
        _isPlaying = false;
        PlayPauseButton.Content = Loc.Get("Str_Video_Play");
    }

    private void OnPlayPauseClick(object sender, RoutedEventArgs e)
    {
        if (_isPlaying)
        {
            Player.Pause();
            PlayPauseButton.Content = Loc.Get("Str_Video_Play");
        }
        else
        {
            Player.Play();
            Player.Visibility = Visibility.Visible;
            LoadingText.Visibility = Visibility.Collapsed;
            PlayPauseButton.Content = Loc.Get("Str_Video_Pause");
        }
        _isPlaying = !_isPlaying;
    }

    private void OnRestartClick(object sender, RoutedEventArgs e)
    {
        Player.Position = TimeSpan.Zero;
        Player.Play();
        Player.Visibility = Visibility.Visible;
        LoadingText.Visibility = Visibility.Collapsed;
        PlayPauseButton.Content = Loc.Get("Str_Video_Pause");
        _isPlaying = true;
    }

    private void OnMediaEnded(object sender, RoutedEventArgs e)
    {
        Player.Position = TimeSpan.Zero;
        Player.Stop();
        PlayPauseButton.Content = Loc.Get("Str_Video_Play");
        _isPlaying = false;
    }

    private void OnMediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        Player.Visibility = Visibility.Collapsed;
        LoadingText.Visibility = Visibility.Collapsed;
        Controls.Visibility = Visibility.Collapsed;
        MissingText.Text = Loc.Get("Str_Video_Failed");
        MissingText.Visibility = Visibility.Visible;
    }
}
