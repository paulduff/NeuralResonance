using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace NRE.WpfWorldSim;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static readonly string LogDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NRE.WpfWorldSim");

    private static readonly string LogPath = Path.Combine(LogDirectory, "worldsim-startup.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        Directory.CreateDirectory(LogDirectory);
        Log("Startup begin.");

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try
        {
            base.OnStartup(e);
            Log("Startup complete.");
        }
        catch (Exception ex)
        {
            Log($"Startup exception: {ex}");
            MessageBox.Show($"WorldSim failed to start.\n\n{ex.Message}\n\nLog: {LogPath}", "WorldSim Startup Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            throw;
        }
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log($"Dispatcher exception: {e.Exception}");
        MessageBox.Show($"WorldSim crashed.\n\n{e.Exception.Message}\n\nLog: {LogPath}", "WorldSim Error",
            MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        Log($"Unhandled exception: {e.ExceptionObject}");
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log($"Unobserved task exception: {e.Exception}");
        e.SetObserved();
    }

    private static void Log(string message)
    {
        var line = $"{DateTimeOffset.Now:O} {message}";
        try
        {
            File.AppendAllText(LogPath, line + Environment.NewLine, Encoding.UTF8);
        }
        catch
        {
            Trace.WriteLine(line);
        }
    }
}
