using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using HVACrate2.App.Heating;

namespace HVACrate2.App.Projects;

public sealed class ProjectRecord : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public Guid Id { get; } = Guid.NewGuid();

    public string Name { get; set; } = "";

    public DateTime CreatedAt { get; } = DateTime.Now;

    private int _floorCount;
    public int FloorCount
    {
        get => _floorCount;
        set { _floorCount = value; Raise(); }
    }

    /// <summary>Floor Heating's own floor/room list — independent of the DXF-driven floors above.
    /// Lives on the project so data survives leaving and re-entering the Floor Heating page.</summary>
    public ObservableCollection<HeatingFloorViewModel> HeatingFloors { get; } = new();
}
