using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace HVACrate2.App.Pages;

public partial class InstructionsPage : Page
{
    private const string Video1Url = "https://github.com/ivanzlatinov1/HVACrate-2.0/releases/download/Pre-Release/video1.mp4";
    private const string Video2Url = "https://github.com/ivanzlatinov1/HVACrate-2.0/releases/download/Pre-Release/video2.mp4";

    public InstructionsPage()
    {
        InitializeComponent();
        Video1.SetSource(ResolveVideoSource("video1.mp4", Video1Url));
        Video2.SetSource(ResolveVideoSource("video2.mp4", Video2Url));
    }

    /// <summary>
    /// Prefers a local videos/&lt;fileName&gt; next to the repo root (fast, works offline, dev convenience),
    /// falling back to the GitHub Releases URL when the local file isn't present.
    /// </summary>
    private static Uri ResolveVideoSource(string fileName, string remoteUrl)
    {
        string? local = FindRepoFile(Path.Combine("videos", fileName));
        return local is not null
            ? new Uri(local, UriKind.Absolute)
            : new Uri(remoteUrl, UriKind.Absolute);
    }

    private static string? FindRepoFile(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        NavigationService?.Navigate(new StartPage());
    }
}
