using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using HVACrate2.App.Shared;

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
        set { _floorNumber = value; Raise(); Raise(nameof(FloorLabel)); }
    }

    public string FloorLabel => Loc.Get("Str_FloorLabel", FloorNumber);

    public ObservableCollection<HeatingRoomViewModel> Rooms { get; } = new();
}
