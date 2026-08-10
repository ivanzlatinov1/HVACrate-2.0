using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using HVACrate2.App.Shared;

namespace HVACrate2.App.Work;

public sealed class FloorRowViewModel : INotifyPropertyChanged
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

    private string? _dxfPath;
    public string? DxfPath
    {
        get => _dxfPath;
        set
        {
            _dxfPath = value;
            Raise();
            Raise(nameof(DxfFileName));
        }
    }

    public string DxfFileName => DxfPath is null ? Loc.Get("Str_Work_NoFileSelected") : Path.GetFileName(DxfPath);

    private string _heightText = "";
    public string HeightText
    {
        get => _heightText;
        set { _heightText = value; Raise(); }
    }

    public bool TryGetHeightM(out double heightM)
        => double.TryParse(HeightText.Replace(",", "."), out heightM) && heightM > 0;

    private string _apartmentsText = "";
    public string ApartmentsText
    {
        get => _apartmentsText;
        set { _apartmentsText = value; Raise(); }
    }

    public bool TryGetApartmentCount(out int apartmentCount)
        => int.TryParse(ApartmentsText, out apartmentCount) && apartmentCount > 0;

    private CompassDirectionOption _selectedDirection = CompassDirectionOption.All[0];
    public CompassDirectionOption SelectedDirection
    {
        get => _selectedDirection;
        set
        {
            _selectedDirection = value;
            Raise();
            Raise(nameof(CompassAngle));
        }
    }

    /// <summary>Needle rotation angle (degrees clockwise from N) matching the selected direction.</summary>
    public double CompassAngle => _selectedDirection.Degrees;
}
