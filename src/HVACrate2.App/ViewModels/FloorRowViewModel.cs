using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace HVACrate2.App.ViewModels;

public sealed class FloorRowViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public int FloorNumber { get; set; }

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

    public string DxfFileName => DxfPath is null ? "No file selected" : Path.GetFileName(DxfPath);

    private string _heightText = "";
    public string HeightText
    {
        get => _heightText;
        set { _heightText = value; Raise(); }
    }

    public bool TryGetHeightM(out double heightM)
        => double.TryParse(HeightText.Replace(",", "."), out heightM) && heightM > 0;

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

    /// <summary>Rotation angle (degrees) so the selected direction's label sits at the top of the compass image.</summary>
    public double CompassAngle => -_selectedDirection.Degrees;
}
