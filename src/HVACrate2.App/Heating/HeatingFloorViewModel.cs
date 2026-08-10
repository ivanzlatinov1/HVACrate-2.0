using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HVACrate2.App.Heating;

public sealed class HeatingFloorViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private int _floorNumber;
    public int FloorNumber
    {
        get => _floorNumber;
        set { _floorNumber = value; Raise(); }
    }

    public ObservableCollection<HeatingRoomViewModel> Rooms { get; } = new();
}
