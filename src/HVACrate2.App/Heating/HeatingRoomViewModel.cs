using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace HVACrate2.App.Heating;

public sealed class HeatingRoomViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private int _roomNumber;
    public int RoomNumber
    {
        get => _roomNumber;
        set { _roomNumber = value; Raise(); Raise(nameof(RoomLabel)); }
    }

    public string RoomLabel => $"пом.{RoomNumber:D2}";

    private string _deltaBetText = "";
    public string DeltaBetText
    {
        get => _deltaBetText;
        set { _deltaBetText = value; Raise(); }
    }

    private string _deltaZamPodText = "";
    public string DeltaZamPodText
    {
        get => _deltaZamPodText;
        set { _deltaZamPodText = value; Raise(); }
    }

    private string _deltaTerText = "";
    public string DeltaTerText
    {
        get => _deltaTerText;
        set { _deltaTerText = value; Raise(); }
    }

    private string _deltaIzolText = "";
    public string DeltaIzolText
    {
        get => _deltaIzolText;
        set { _deltaIzolText = value; Raise(); }
    }

    private string _deltaPlochText = "";
    public string DeltaPlochText
    {
        get => _deltaPlochText;
        set { _deltaPlochText = value; Raise(); }
    }

    private string _deltaZamTavText = "";
    public string DeltaZamTavText
    {
        get => _deltaZamTavText;
        set { _deltaZamTavText = value; Raise(); }
    }

    private string _qptText = "";
    public string QptText
    {
        get => _qptText;
        set { _qptText = value; Raise(); }
    }

    public bool TryGetDeltaBetM(out double value) => TryParse(DeltaBetText, out value);
    public bool TryGetDeltaZamPodM(out double value) => TryParse(DeltaZamPodText, out value);
    public bool TryGetDeltaTerM(out double value) => TryParse(DeltaTerText, out value);
    public bool TryGetDeltaIzolM(out double value) => TryParse(DeltaIzolText, out value);
    public bool TryGetDeltaPlochM(out double value) => TryParse(DeltaPlochText, out value);
    public bool TryGetDeltaZamTavM(out double value) => TryParse(DeltaZamTavText, out value);
    public bool TryGetQptW(out double value) => TryParse(QptText, out value);

    private static bool TryParse(string text, out double value)
        => double.TryParse(text.Replace(",", "."), out value) && value >= 0;

    private Visibility _resultVisibility = Visibility.Collapsed;
    public Visibility ResultVisibility
    {
        get => _resultVisibility;
        set { _resultVisibility = value; Raise(); }
    }

    private string _rogText = "";
    public string RogText
    {
        get => _rogText;
        set { _rogText = value; Raise(); }
    }

    private string _rodText = "";
    public string RodText
    {
        get => _rodText;
        set { _rodText = value; Raise(); }
    }

    private string _roText = "";
    public string RoText
    {
        get => _roText;
        set { _roText = value; Raise(); }
    }

    private string _rPdText = "";
    public string RPdText
    {
        get => _rPdText;
        set { _rPdText = value; Raise(); }
    }

    private string _qcText = "";
    public string QcText
    {
        get => _qcText;
        set { _qcText = value; Raise(); }
    }

    private string _mText = "";
    public string MText
    {
        get => _mText;
        set { _mText = value; Raise(); }
    }

    private string _qdolText = "";
    public string QdolText
    {
        get => _qdolText;
        set { _qdolText = value; Raise(); }
    }
}
