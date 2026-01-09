using System.Windows;
using System.Windows.Threading;
using XOutputRenew.Input;

namespace XOutputRenew.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Initialize logging early
        AppLogger.Initialize();

        // Wire up input logging to app logger
        InputLogger.LogAction = msg => AppLogger.Info(msg);

        // Global exception handlers
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

        // Create and show main window
        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLogger.Error("Unhandled UI exception", e.Exception);
        MessageBox.Show($"An error occurred:\n\n{e.Exception.Message}\n\nCheck log: {AppLogger.GetLogPath()}",
            "XOutputRenew Error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true; // Prevent crash, allow app to continue
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            AppLogger.Error("Unhandled domain exception (fatal)", ex);
        }
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        AppLogger.Error("Unobserved task exception", e.Exception);
        e.SetObserved(); // Prevent crash
    }
}
