using System.Collections.ObjectModel;

namespace HVACrate2.App.Projects;

/// <summary>In-memory list of projects for the current app session — not persisted across restarts.</summary>
public static class ProjectStore
{
    public static ObservableCollection<ProjectRecord> Projects { get; } = new();

    /// <summary>The project selected in Project Management — gates the Energy Efficiency and Floor Heating entry points.</summary>
    public static ProjectRecord? CurrentProject { get; set; }

    public static ProjectRecord AddProject(string name)
    {
        var project = new ProjectRecord { Name = name };
        Projects.Add(project);
        return project;
    }

    public static void DeleteProject(ProjectRecord project)
    {
        Projects.Remove(project);
        if (CurrentProject == project)
            CurrentProject = null;
    }
}
