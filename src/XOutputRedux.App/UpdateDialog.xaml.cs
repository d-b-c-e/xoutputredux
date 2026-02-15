using System.Diagnostics;
using System.Windows;

namespace XOutputRedux.App;

/// <summary>
/// Dialog for displaying update information and downloading updates.
/// </summary>
public partial class UpdateDialog : Window
{
    private readonly ReleaseInfo _release;
    private readonly UpdateService _updateService;
    private CancellationTokenSource? _downloadCts;
    private bool _isDownloading;
    private string? _downloadedInstallerPath;

    public UpdateDialog(ReleaseInfo release)
    {
        InitializeComponent();

        _release = release;
        _updateService = new UpdateService();

        // Apply dark title bar
        DarkModeHelper.EnableDarkTitleBar(this);

        // Populate UI
        CurrentVersionText.Text = UpdateService.GetCurrentVersion().ToString();
        NewVersionText.Text = release.Version.ToString();
        PrereleaseLabel.Visibility = release.IsPrerelease ? Visibility.Visible : Visibility.Collapsed;
        ReleaseNameText.Text = release.Name;
        ReleaseDateText.Text = $"Published: {release.PublishedAt:MMMM d, yyyy} ({release.FormattedSize})";

        // Format changelog (basic markdown to plain text)
        ChangelogText.Text = FormatChangelog(release.Body);

        // In portable mode, replace download with a link to the release page
        if (AppPaths.IsPortable)
        {
            DownloadButton.Content = "View Release";
            PortableModeNote.Visibility = Visibility.Visible;
        }
    }

    private static string FormatChangelog(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return "No changelog available.";

        // Basic markdown cleanup
        var text = body
            .Replace("###", "")
            .Replace("##", "")
            .Replace("**", "")
            .Replace("*", "•")
            .Trim();

        return text;
    }

    private void ViewOnGitHubButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _release.HtmlUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open browser: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SkipButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isDownloading)
        {
            // Cancel download
            _downloadCts?.Cancel();
            ResetToInitialState();
        }
        else
        {
            DialogResult = false;
            Close();
        }
    }

    private async void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        // In portable mode, open the release page instead of downloading
        if (AppPaths.IsPortable)
        {
            ViewOnGitHubButton_Click(sender, e);
            return;
        }

        if (_downloadedInstallerPath != null)
        {
            // Already downloaded, launch installer
            LaunchInstaller();
            return;
        }

        // Start download
        _isDownloading = true;
        _downloadCts = new CancellationTokenSource();

        DownloadButton.IsEnabled = false;
        DownloadButton.Content = "Downloading...";
        SkipButton.Content = "Cancel";
        ViewOnGitHubButton.IsEnabled = false;
        ProgressPanel.Visibility = Visibility.Visible;
        ProgressText.Text = "Downloading update...";

        try
        {
            _downloadedInstallerPath = await _updateService.DownloadInstallerAsync(
                _release,
                progress =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        DownloadProgress.Value = progress;
                        ProgressText.Text = $"Downloading... {progress}%";
                    });
                },
                _downloadCts.Token);

            // Download complete
            ProgressText.Text = "Download complete! Ready to install.";
            DownloadButton.Content = "Install Now";
            DownloadButton.IsEnabled = true;
            SkipButton.Content = "Later";
            _isDownloading = false;
        }
        catch (OperationCanceledException)
        {
            ResetToInitialState();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Download failed: {ex.Message}", "Download Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            ResetToInitialState();
        }
    }

    private void ResetToInitialState()
    {
        _isDownloading = false;
        _downloadedInstallerPath = null;
        DownloadButton.Content = "Download & Install";
        DownloadButton.IsEnabled = true;
        SkipButton.Content = "Skip";
        ViewOnGitHubButton.IsEnabled = true;
        ProgressPanel.Visibility = Visibility.Collapsed;
        DownloadProgress.Value = 0;
    }

    private void LaunchInstaller()
    {
        if (_downloadedInstallerPath == null) return;

        var result = MessageBox.Show(
            "XOutputRedux will close and the installer will start.\n\nContinue?",
            "Install Update",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                UpdateService.LaunchInstallerAndExit(_downloadedInstallerPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to launch installer: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _downloadCts?.Cancel();
        _downloadCts?.Dispose();
        base.OnClosed(e);
    }
}
