using System.Windows;
using System.Windows.Controls;
using HVACrate2.App.Shared;
using HVACrate2.App.Start;

namespace HVACrate2.App.Projects;

public partial class ProjectsPage : Page
{
    public ProjectsPage()
    {
        InitializeComponent();
        ProjectsList.ItemsSource = ProjectStore.Projects;
        UpdateEmptyState();
        ProjectStore.Projects.CollectionChanged += (_, _) => UpdateEmptyState();
    }

    private void UpdateEmptyState()
    {
        EmptyStateText.Visibility = ProjectStore.Projects.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        NavigationService?.Navigate(new StartPage());
    }

    private void OnCreateProjectClick(object sender, RoutedEventArgs e)
    {
        string name = NewProjectNameBox.Text.Trim();
        if (string.IsNullOrEmpty(name))
            return;

        var project = ProjectStore.AddProject(name);
        NewProjectNameBox.Text = "";
        ProjectStore.CurrentProject = project;
        NavigationService?.Navigate(new StartPage());
    }

    private void OnOpenProjectClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ProjectRecord project })
        {
            ProjectStore.CurrentProject = project;
            NavigationService?.Navigate(new StartPage());
        }
    }

    private void OnDeleteProjectClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ProjectRecord project })
            return;

        var result = MessageBox.Show(
            Loc.Get("Str_Projects_DeleteConfirmMessage", project.Name),
            Loc.Get("Str_Projects_DeleteConfirmTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
            ProjectStore.DeleteProject(project);
    }
}
