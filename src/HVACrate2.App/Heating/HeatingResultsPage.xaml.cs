using System.Windows;
using System.Windows.Controls;
using HVACrate2.App.Projects;
using HVACrate2.App.Shared;

namespace HVACrate2.App.Heating;

public partial class HeatingResultsPage : Page
{
    public HeatingResultsPage(ProjectRecord project)
    {
        InitializeComponent();
        TitleText.Text = $"{Loc.Get("Str_HeatingResults_TitlePrefix")}{project.Name}";
        ResultsList.ItemsSource = project.HeatingFloors;
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        NavigationService?.GoBack();
    }
}
