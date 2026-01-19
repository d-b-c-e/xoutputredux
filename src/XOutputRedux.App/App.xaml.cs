using System.Windows;
using System.Windows.Threading;
using XOutputRedux.Input;

namespace XOutputRedux.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static bool? _vigemAvailable;
    private static bool? _hidHideAvailable;
    private static string? _activeProfile;
    private static AppSettings? _settings;

    /// <summary>
    /// Sets the driver availability status for crash reports.
    /// Called from MainWindow after initialization.
    /// </summary>
    public static void SetDriverStatus(bool vigemAvailable, bool hidHideAvailable)
    {
        _vigemAvailable = vigemAvailable;
        _hidHideAvailable = hidHideAvailable;
    }

    /// <summary>
    /// Sets the currently active profile name for crash reports.
    /// Called when profile starts/stops.
    /// </summary>
    public static void SetActiveProfile(string? profileName)
    {
        _activeProfile = profileName;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Initialize logging early
        AppLogger.Initialize();

        // Wire up input logging to app logger
        InputLogger.LogAction = msg => AppLogger.Info(msg);

        // Load settings
        _settings = AppSettings.Load();

        // Global exception handlers
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

        // Create and show main window
        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppLogger.Info("Application shutting down");
        AppLogger.Shutdown();
        base.OnExit(e);
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLogger.Error("Unhandled UI exception", e.Exception);

        if (_settings?.CrashReportingEnabled == true)
        {
            try
            {
                var report = CrashReportService.CreateReport(
                    e.Exception,
                    CrashContext.UIThread,
                    isFatal: false,
                    activeProfile: _settings.IncludeProfileInCrashReport ? _activeProfile : null,
                    vigemAvailable: _vigemAvailable,
                    hidHideAvailable: _hidHideAvailable);

                var dialog = new CrashDialog(report);
                dialog.ShowDialog();

                e.Handled = dialog.ShouldContinue;
            }
            catch (Exception dialogEx)
            {
                // Crash dialog itself crashed - fall back to simple message box
                AppLogger.Error("Crash dialog failed", dialogEx);
                MessageBox.Show($"An error occurred:\n\n{e.Exception.Message}\n\nCheck log: {AppLogger.GetLogPath()}",
                    "XOutputRedux Error", MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = true;
            }
        }
        else
        {
            // Crash reporting disabled - fall back to simple message box
            MessageBox.Show($"An error occurred:\n\n{e.Exception.Message}\n\nCheck log: {AppLogger.GetLogPath()}",
                "XOutputRedux Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            AppLogger.Error("Unhandled domain exception (fatal)", ex);

            if (_settings?.CrashReportingEnabled == true)
            {
                try
                {
                    var report = CrashReportService.CreateReport(
                        ex,
                        CrashContext.AppDomain,
                        isFatal: true,
                        activeProfile: _settings.IncludeProfileInCrashReport ? _activeProfile : null,
                        vigemAvailable: _vigemAvailable,
                        hidHideAvailable: _hidHideAvailable);

                    // Must use Dispatcher.Invoke since this might be on a different thread
                    Dispatcher?.Invoke(() =>
                    {
                        var dialog = new CrashDialog(report);
                        dialog.ShowDialog();
                    });
                }
                catch
                {
                    // Last resort - don't let crash reporting crash
                }
            }
        }
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        AppLogger.Error("Unobserved task exception", e.Exception);

        if (_settings?.CrashReportingEnabled == true)
        {
            try
            {
                var report = CrashReportService.CreateReport(
                    e.Exception,
                    CrashContext.BackgroundTask,
                    isFatal: false,
                    activeProfile: _settings.IncludeProfileInCrashReport ? _activeProfile : null,
                    vigemAvailable: _vigemAvailable,
                    hidHideAvailable: _hidHideAvailable);

                Dispatcher?.Invoke(() =>
                {
                    var dialog = new CrashDialog(report);
                    dialog.ShowDialog();
                });
            }
            catch
            {
                // Silent failure - don't let crash reporting crash
            }
        }

        e.SetObserved(); // Prevent crash
    }
}
