using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace HVACrate2.App.Pages;

public partial class InstructionsPage : Page
{
    public InstructionsPage()
    {
        InitializeComponent();
        Video1.SetSource(FindRepoFile(Path.Combine("videos", "video1.mp4")));
        Video2.SetSource(FindRepoFile(Path.Combine("videos", "video2.mp4")));
    }

    /// <summary>
    /// Walks up from the app's output directory looking for a repo-relative file (e.g. "videos/video1.mp4").
    /// The videos folder isn't bundled with the build output, so this only resolves when running from
    /// a dev checkout that has it alongside the repo root.
    /// </summary>
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
