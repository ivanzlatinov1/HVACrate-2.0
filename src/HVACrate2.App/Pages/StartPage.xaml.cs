using System.Windows;
using System.Windows.Controls;

namespace HVACrate2.App.Pages;

public partial class StartPage : Page
{
    public StartPage()
    {
        InitializeComponent();
    }

    private void OnStartClick(object sender, RoutedEventArgs e)
    {
        NavigationService?.Navigate(new WorkPage());
    }

    private void OnInstructionsClick(object sender, RoutedEventArgs e)
    {
        NavigationService?.Navigate(new InstructionsPage());
    }
}
