using System.Windows;
using System.Windows.Controls;

namespace HVACrate2.App.Pages;

public partial class WorkPage : Page
{
    public WorkPage()
    {
        InitializeComponent();
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        NavigationService?.Navigate(new StartPage());
    }
}
