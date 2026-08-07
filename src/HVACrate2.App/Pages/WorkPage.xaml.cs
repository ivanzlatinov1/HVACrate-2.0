using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HVACrate2.App.ViewModels;
using HVACrate2.Core;
using HVACrate2.Core.Models;
using Microsoft.Win32;

namespace HVACrate2.App.Pages;

public partial class WorkPage : Page
{
    private readonly ProjectRecord _project;
    private readonly ObservableCollection<FloorRowViewModel> _floors = new();
    private string? _lastOutputPath;

    public WorkPage(ProjectRecord project)
    {
        InitializeComponent();
        _project = project;
        TitleText.Text = $"Floors — {project.Name}";
        FloorsList.ItemsSource = _floors;
        AddFloor();
    }

    private void AddFloor()
    {
        _floors.Add(new FloorRowViewModel { FloorNumber = _floors.Count + 1 });
        _project.FloorCount = _floors.Count;
    }

    private void Renumber()
    {
        for (int i = 0; i < _floors.Count; i++)
            _floors[i].FloorNumber = i + 1;
        _project.FloorCount = _floors.Count;
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        NavigationService?.Navigate(new ProjectsPage());
    }

    private void OnAddFloorClick(object sender, RoutedEventArgs e)
    {
        AddFloor();
    }

    private void OnRemoveFloorClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: FloorRowViewModel row })
        {
            if (_floors.Count <= 1)
            {
                ShowError("At least one floor is required.");
                return;
            }
            _floors.Remove(row);
            Renumber();
        }
    }

    private void OnChooseDxfClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: FloorRowViewModel row })
            return;

        var dialog = new OpenFileDialog
        {
            Title = "Choose a floor DXF drawing",
            Filter = "DXF drawings (*.dxf)|*.dxf",
        };
        if (dialog.ShowDialog() == true)
            row.DxfPath = dialog.FileName;
    }

    private void ShowError(string message)
    {
        StatusText.Text = message;
        StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xB3, 0x26, 0x1E));
        StatusText.Visibility = Visibility.Visible;
    }

    private void ShowSuccess(string message)
    {
        StatusText.Text = message;
        StatusText.Foreground = (Brush)FindResource("AccentBrush");
        StatusText.Visibility = Visibility.Visible;
    }

    private void OnExtractClick(object sender, RoutedEventArgs e)
    {
        DownloadButton.Visibility = Visibility.Collapsed;
        StatusText.Visibility = Visibility.Collapsed;
        _lastOutputPath = null;

        var floorInputs = new List<FloorInput>();
        foreach (var row in _floors)
        {
            if (row.DxfPath is null)
            {
                ShowError($"Floor {row.FloorNumber}: choose a DXF file first.");
                return;
            }
            if (!row.TryGetHeightM(out double heightM))
            {
                ShowError($"Floor {row.FloorNumber}: enter a valid height in meters.");
                return;
            }
            if (!row.TryGetApartmentCount(out int apartmentCount))
            {
                ShowError($"Floor {row.FloorNumber}: enter a valid number of apartments.");
                return;
            }
            floorInputs.Add(new FloorInput
            {
                DxfPath = row.DxfPath,
                HeightM = heightM,
                NorthDeg = row.SelectedDirection.Degrees,
                ApartmentCount = apartmentCount,
            });
        }

        string templatePath = Path.Combine(AppContext.BaseDirectory, "Assets", "Template.xlsx");
        if (!File.Exists(templatePath))
        {
            ShowError("Excel template not found alongside the app (Assets/Template.xlsx).");
            return;
        }

        string outputPath = Path.Combine(Path.GetTempPath(), $"HVACrate_{Guid.NewGuid():N}.xlsx");

        try
        {
            FloorProcessor.ProcessAndWriteFloors(floorInputs, templatePath, outputPath);
        }
        catch (Exception ex)
        {
            ShowError($"Extraction failed: {ex.Message}");
            return;
        }

        _lastOutputPath = outputPath;
        ShowSuccess($"Done — {floorInputs.Count} floor(s) written. Download the filled Excel file below.");
        DownloadButton.Visibility = Visibility.Visible;
    }

    private void OnDownloadClick(object sender, RoutedEventArgs e)
    {
        if (_lastOutputPath is null || !File.Exists(_lastOutputPath))
        {
            ShowError("No filled file to download — run Extract & Fill Excel again.");
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Save filled Excel file",
            FileName = "Топлотехника.xlsx",
            Filter = "Excel workbook (*.xlsx)|*.xlsx",
        };
        if (dialog.ShowDialog() == true)
            File.Copy(_lastOutputPath, dialog.FileName, overwrite: true);
    }
}
