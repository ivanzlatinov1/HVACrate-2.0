using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HVACrate2.App.Shared;
using HVACrate2.Core;
using HVACrate2.Core.Models;
using Microsoft.Win32;

namespace HVACrate2.App.Preview;

public partial class PreviewPage : Page
{
    private readonly List<FloorInput> _floorInputs;
    private readonly List<FloorResult> _results;
    private string? _lastOutputPath;

    public PreviewPage(List<FloorInput> floorInputs, List<FloorResult> results)
    {
        InitializeComponent();
        _floorInputs = floorInputs;
        _results = results;

        FloorsList.ItemsSource = floorInputs.Zip(results, (input, result) => new { input, result })
            .Select((x, i) => new PreviewFloorViewModel
            {
                FloorNumber = i + 1,
                Result = x.result,
                NorthDeg = x.input.NorthDeg,
            })
            .ToList();
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        NavigationService?.GoBack();
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

    private async void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        DownloadButton.Visibility = Visibility.Collapsed;
        StatusText.Visibility = Visibility.Collapsed;
        _lastOutputPath = null;

        string templatePath = Path.Combine(AppContext.BaseDirectory, "Assets", "Template.xlsx");
        if (!File.Exists(templatePath))
        {
            ShowError(Loc.Get("Str_Preview_Err_Template"));
            return;
        }

        string outputPath = Path.Combine(Path.GetTempPath(), $"HVACrate_{Guid.NewGuid():N}.xlsx");

        ConfirmButton.IsEnabled = false;
        ConfirmSpinner.Visibility = Visibility.Visible;
        try
        {
            await Task.Run(() => FloorProcessor.WriteFloorsToExcel(_floorInputs, _results, templatePath, outputPath));
        }
        catch (Exception ex)
        {
            ShowError(Loc.Get("Str_Preview_Err_Write", ex.Message));
            return;
        }
        finally
        {
            ConfirmSpinner.Visibility = Visibility.Collapsed;
            ConfirmButton.IsEnabled = true;
        }

        _lastOutputPath = outputPath;
        ShowSuccess(Loc.Get("Str_Preview_Success", _floorInputs.Count));
        DownloadButton.Visibility = Visibility.Visible;
    }

    private void OnDownloadClick(object sender, RoutedEventArgs e)
    {
        if (_lastOutputPath is null || !File.Exists(_lastOutputPath))
        {
            ShowError(Loc.Get("Str_Preview_Err_NoFile"));
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Save filled Excel file",
            FileName = "Топлотехника V6.0.16.xlsx",
            Filter = "Excel workbook (*.xlsx)|*.xlsx",
        };
        if (dialog.ShowDialog() == true)
            File.Copy(_lastOutputPath, dialog.FileName, overwrite: true);
    }
}
