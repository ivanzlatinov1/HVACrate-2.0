using System.Windows;
using System.Windows.Controls;

using HVACrate2.App.Heating;
using HVACrate2.App.Instructions;
using HVACrate2.App.Projects;
using HVACrate2.App.Shared;
using HVACrate2.App.Work;

namespace HVACrate2.App.Start;

public partial class StartPage : Page
{
    public StartPage()
    {
        InitializeComponent();

        var current = ProjectStore.CurrentProject;
        bool hasProject = current is not null;
        EnergyEfficiencyButton.IsEnabled = hasProject;
        FloorHeatingButton.IsEnabled = hasProject;
        CurrentProjectText.Text = hasProject
            ? Loc.Get("Str_Start_CurrentProject", current!.Name)
            : Loc.Get("Str_Start_NoProject");
    }

    private void OnProjectManagementClick(object sender, RoutedEventArgs e)
    {
        NavigationService?.Navigate(new ProjectsPage());
    }

    private void OnEnergyEfficiencyClick(object sender, RoutedEventArgs e)
    {
        if (ProjectStore.CurrentProject is { } project)
            NavigationService?.Navigate(new WorkPage(project));
    }

    private void OnFloorHeatingClick(object sender, RoutedEventArgs e)
    {
        if (ProjectStore.CurrentProject is { } project)
            NavigationService?.Navigate(new FloorHeatingPage(project));
    }

    private void OnInstructionsClick(object sender, RoutedEventArgs e)
    {
        NavigationService?.Navigate(new InstructionsPage());
    }
}
