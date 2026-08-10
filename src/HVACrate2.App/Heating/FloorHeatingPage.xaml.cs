using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HVACrate2.App.Projects;
using HVACrate2.App.Start;
using HVACrate2.Core.FloorHeating;
using HVACrate2.Core.Models;

namespace HVACrate2.App.Heating;

public partial class FloorHeatingPage : Page
{
    private readonly ProjectRecord _project;
    private ObservableCollection<HeatingFloorViewModel> Floors => _project.HeatingFloors;

    public FloorHeatingPage(ProjectRecord project)
    {
        InitializeComponent();
        _project = project;
        TitleText.Text = $"Floor Heating — {project.Name}";
        FloorsList.ItemsSource = Floors;
        if (Floors.Count == 0)
            AddFloor();
    }

    private void AddFloor()
    {
        var floor = new HeatingFloorViewModel { FloorNumber = Floors.Count + 1 };
        Floors.Add(floor);
        AddRoom(floor);
    }

    private static void AddRoom(HeatingFloorViewModel floor)
    {
        floor.Rooms.Add(new HeatingRoomViewModel { RoomNumber = floor.Rooms.Count + 1 });
    }

    private static void RenumberRooms(HeatingFloorViewModel floor)
    {
        for (int i = 0; i < floor.Rooms.Count; i++)
            floor.Rooms[i].RoomNumber = i + 1;
    }

    private void RenumberFloors()
    {
        for (int i = 0; i < Floors.Count; i++)
            Floors[i].FloorNumber = i + 1;
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        NavigationService?.Navigate(new StartPage());
    }

    private void OnAddFloorClick(object sender, RoutedEventArgs e)
    {
        AddFloor();
    }

    private void OnRemoveFloorClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: HeatingFloorViewModel floor })
            return;

        if (Floors.Count <= 1)
        {
            ShowError("At least one floor is required.");
            return;
        }
        Floors.Remove(floor);
        RenumberFloors();
    }

    private void OnAddRoomClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: HeatingFloorViewModel floor })
            AddRoom(floor);
    }

    private void OnRemoveRoomClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: HeatingRoomViewModel room })
            return;

        var floor = Floors.FirstOrDefault(f => f.Rooms.Contains(room));
        if (floor is null)
            return;

        floor.Rooms.Remove(room);
        RenumberRooms(floor);
    }

    private void ShowError(string message)
    {
        StatusText.Text = message;
        StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xB3, 0x26, 0x1E));
        StatusText.Visibility = Visibility.Visible;
    }

    private void OnCalculateClick(object sender, RoutedEventArgs e)
    {
        StatusText.Visibility = Visibility.Collapsed;

        var pending = new List<(HeatingRoomViewModel Room, HeatingRoomInput Input)>();
        foreach (var floor in Floors)
        {
            foreach (var room in floor.Rooms)
            {
                if (!room.TryGetDeltaBetM(out double deltaBet))
                {
                    ShowError($"Floor {floor.FloorNumber}, Room {room.RoomNumber}: enter a valid δбет.");
                    return;
                }
                if (!room.TryGetDeltaZamPodM(out double deltaZamPod))
                {
                    ShowError($"Floor {floor.FloorNumber}, Room {room.RoomNumber}: enter a valid δзам-под.");
                    return;
                }
                if (!room.TryGetDeltaTerM(out double deltaTer))
                {
                    ShowError($"Floor {floor.FloorNumber}, Room {room.RoomNumber}: enter a valid δтер.");
                    return;
                }
                if (!room.TryGetDeltaIzolM(out double deltaIzol))
                {
                    ShowError($"Floor {floor.FloorNumber}, Room {room.RoomNumber}: enter a valid δизол.");
                    return;
                }
                if (!room.TryGetDeltaPlochM(out double deltaPloch))
                {
                    ShowError($"Floor {floor.FloorNumber}, Room {room.RoomNumber}: enter a valid δплоч.");
                    return;
                }
                if (!room.TryGetDeltaZamTavM(out double deltaZamTav))
                {
                    ShowError($"Floor {floor.FloorNumber}, Room {room.RoomNumber}: enter a valid δзам-таван.");
                    return;
                }
                if (!room.TryGetQptW(out double qpt) || qpt <= 0)
                {
                    ShowError($"Floor {floor.FloorNumber}, Room {room.RoomNumber}: enter a valid Qпт.");
                    return;
                }

                pending.Add((room, new HeatingRoomInput
                {
                    DeltaBetM = deltaBet,
                    DeltaZamPodM = deltaZamPod,
                    DeltaTerM = deltaTer,
                    DeltaIzolM = deltaIzol,
                    DeltaPlochM = deltaPloch,
                    DeltaZamTavM = deltaZamTav,
                    QptW = qpt,
                }));
            }
        }

        foreach (var (room, input) in pending)
        {
            var result = FloorHeatingCalculator.Calculate(input);
            room.RogText = result.RogM2KW.ToString("F4");
            room.RodText = result.RodM2KW.ToString("F4");
            room.RoText = result.RoM2KW.ToString("F4");
            room.RPdText = result.RPd.ToString("F4");
            room.QcText = result.QcW.ToString("F1");
            room.MText = result.MKgH.ToString("F2");
            room.QdolText = result.QdolW.ToString("F2");
            room.ResultVisibility = Visibility.Visible;
        }

        NavigationService?.Navigate(new HeatingResultsPage(_project));
    }
}
