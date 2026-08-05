using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HVACrate2.App.ViewModels;

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
}
