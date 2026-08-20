using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using HVACrate2.App.Shared;

namespace HVACrate2.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            string logPath = Path.Combine(Path.GetTempPath(), "hvacrate-crash.log");
            File.WriteAllText(logPath, args.Exception.ToString());
            MessageBox.Show(
                Loc.Get("Str_Crash_Message", args.Exception.Message, logPath),
                Loc.Get("Str_Crash_Title"), MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
    }
}

