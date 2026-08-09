using System.Windows;
using System.Windows.Controls;

using HVACrate2.App.Projects;
using HVACrate2.App.Instructions;

namespace HVACrate2.App.Start;

public partial class StartPage : Page
{
    public StartPage()
    {
        InitializeComponent();
    }

    private void OnStartClick(object sender, RoutedEventArgs e)
    {
        NavigationService?.Navigate(new ProjectsPage());
    }

    private void OnInstructionsClick(object sender, RoutedEventArgs e)
    {
        NavigationService?.Navigate(new InstructionsPage());
    }
}
