using System.Windows;
using System.Windows.Controls;
using HVACrate2.App.Projects;

namespace HVACrate2.App.Heating;

public partial class HeatingResultsPage : Page
{
    public HeatingResultsPage(ProjectRecord project)
    {
        InitializeComponent();
        TitleText.Text = $"Floor Heating — Results — {project.Name}";
        ResultsList.ItemsSource = project.HeatingFloors;
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        // Return to the existing FloorHeatingPage instance, matching the Preview page's
        // convention — the input page's floor/room list is preserved either way now that it's
        // stored on ProjectRecord, but GoBack keeps the same instance rather than a fresh one.
        NavigationService?.GoBack();
    }
}
