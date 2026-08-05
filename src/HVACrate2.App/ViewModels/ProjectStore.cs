using System.Collections.ObjectModel;

namespace HVACrate2.App.ViewModels;

/// <summary>In-memory list of projects for the current app session — not persisted across restarts.</summary>
public static class ProjectStore
{
    public static ObservableCollection<ProjectRecord> Projects { get; } = new();

    public static ProjectRecord AddProject(string name)
    {
        var project = new ProjectRecord { Name = name };
        Projects.Add(project);
        return project;
    }

    public static void DeleteProject(ProjectRecord project)
    {
        Projects.Remove(project);
    }
}
