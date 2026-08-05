using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Threading;

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
                $"Something went wrong and the action couldn't complete:\n\n{args.Exception.Message}\n\nDetails were saved to:\n{logPath}",
                "HVACrate — unexpected error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
    }
}

