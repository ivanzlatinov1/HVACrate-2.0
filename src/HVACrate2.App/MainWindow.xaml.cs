using System.Windows;
using HVACrate2.App.Shared;
using HVACrate2.App.Start;

namespace HVACrate2.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
        RootFrame.Navigate(new StartPage());
    }

    private void OnThemeToggleClick(object sender, RoutedEventArgs e)
    {
        ThemeManager.SetTheme(ThemeToggle.IsChecked == true);
    }

    private void OnLanguageToggleClick(object sender, RoutedEventArgs e)
    {
        LocalizationManager.SetLanguage(LanguageToggle.IsChecked == true);
    }
}
