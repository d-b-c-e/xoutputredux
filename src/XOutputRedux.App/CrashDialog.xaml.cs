using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace XOutputRedux.App;

/// <summary>
/// Dialog shown when an unhandled exception occurs.
/// </summary>
public partial class CrashDialog : Window
{
    private readonly CrashReport _report;
    private readonly string _formattedReport;
    private readonly string _githubUrl;

    /// <summary>
    /// Gets whether the user chose to continue (non-fatal errors only).
    /// </summary>
    public bool ShouldContinue { get; private set; }

    public CrashDialog(CrashReport report)
    {
        InitializeComponent();

        _report = report;
        _formattedReport = CrashReportService.FormatReportAsText(report);
        _githubUrl = CrashReportService.GenerateGitHubIssueUrl(report);

        // Apply dark title bar
        Loaded += (_, _) => DarkModeHelper.EnableDarkTitleBar(this);

        // Populate UI
        ContextText.Text = $"Context: {GetContextDescription(report.Context)}";
        ExceptionTypeText.Text = GetShortExceptionType(report.ExceptionType);
        MessageText.Text = report.Message;
        StackTraceText.Text = report.StackTrace ?? "(No stack trace available)";

        // Show/hide continue button based on whether error is fatal
        if (!report.IsFatal)
        {
            ContinueButton.Visibility = Visibility.Visible;
            ExitButton.Content = "Exit";
        }
        else
        {
            ContinueButton.Visibility = Visibility.Collapsed;
            ExitButton.Content = "Exit Application";
        }
    }

    private static string GetContextDescription(CrashContext context) => context switch
    {
        CrashContext.UIThread => "UI Thread",
        CrashContext.BackgroundTask => "Background Task",
        CrashContext.AppDomain => "Application Domain (Fatal)",
        CrashContext.IpcServer => "IPC Communication",
        CrashContext.Startup => "Application Startup",
        CrashContext.ProfileRunning => "Profile Running",
        CrashContext.DevicePolling => "Device Polling",
        _ => context.ToString()
    };

    private static string GetShortExceptionType(string fullType)
    {
        var lastDot = fullType.LastIndexOf('.');
        return lastDot >= 0 ? fullType[(lastDot + 1)..] : fullType;
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(_formattedReport);
            CopyButton.Content = "Copied!";

            // Reset button text after delay
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            timer.Tick += (_, _) =>
            {
                CopyButton.Content = "Copy to Clipboard";
                timer.Stop();
            };
            timer.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to copy to clipboard: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ViewLogButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_report.LogFilePath != null && File.Exists(_report.LogFilePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _report.LogFilePath,
                    UseShellExecute = true
                });
            }
            else
            {
                // Open log directory instead
                var logDir = Path.GetDirectoryName(AppLogger.GetLogPath());
                if (logDir != null && Directory.Exists(logDir))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = logDir,
                        UseShellExecute = true
                    });
                }
                else
                {
                    MessageBox.Show("Log file not found.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open log file: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ReportButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _githubUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            // URL might be too long or browser issues, show fallback dialog
            AppLogger.Warning($"Failed to open GitHub issue URL: {ex.Message}");

            try
            {
                Clipboard.SetText(_formattedReport);
            }
            catch
            {
                // Ignore clipboard errors
            }

            MessageBox.Show(
                "Could not open browser. The crash report has been copied to clipboard.\n\n" +
                "Please create a new issue at:\n" +
                "https://github.com/d-b-c-e/XOutputRedux/issues/new\n\n" +
                "And paste the crash report.",
                "Report Error",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private void ContinueButton_Click(object sender, RoutedEventArgs e)
    {
        ShouldContinue = true;
        Close();
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        ShouldContinue = false;
        Close();
    }
}
