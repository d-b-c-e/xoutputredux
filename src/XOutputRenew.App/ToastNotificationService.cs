using Microsoft.Toolkit.Uwp.Notifications;

namespace XOutputRenew.App;

/// <summary>
/// Service for showing Windows toast notifications.
/// </summary>
public static class ToastNotificationService
{
    private const string AppName = "XOutputRenew";

    /// <summary>
    /// Whether toast notifications are enabled.
    /// </summary>
    public static bool Enabled { get; set; } = true;

    /// <summary>
    /// Shows a toast notification when a profile starts.
    /// </summary>
    public static void ShowProfileStarted(string profileName)
    {
        if (!Enabled) return;

        try
        {
            new ToastContentBuilder()
                .AddText($"{AppName}")
                .AddText($"Profile '{profileName}' started")
                .AddAttributionText("Xbox controller emulation active")
                .Show();
        }
        catch (Exception ex)
        {
            AppLogger.Warning($"Failed to show toast notification: {ex.Message}");
        }
    }

    /// <summary>
    /// Shows a toast notification when a profile stops.
    /// </summary>
    public static void ShowProfileStopped(string profileName)
    {
        if (!Enabled) return;

        try
        {
            new ToastContentBuilder()
                .AddText($"{AppName}")
                .AddText($"Profile '{profileName}' stopped")
                .Show();
        }
        catch (Exception ex)
        {
            AppLogger.Warning($"Failed to show toast notification: {ex.Message}");
        }
    }

    /// <summary>
    /// Shows a toast notification when a game is launched.
    /// </summary>
    public static void ShowGameLaunched(string gameName, string profileName)
    {
        if (!Enabled) return;

        try
        {
            new ToastContentBuilder()
                .AddText($"{AppName}")
                .AddText($"Launched: {gameName}")
                .AddAttributionText($"Profile: {profileName}")
                .Show();
        }
        catch (Exception ex)
        {
            AppLogger.Warning($"Failed to show toast notification: {ex.Message}");
        }
    }

    /// <summary>
    /// Shows a toast notification when a game exits.
    /// </summary>
    public static void ShowGameExited(string gameName)
    {
        if (!Enabled) return;

        try
        {
            new ToastContentBuilder()
                .AddText($"{AppName}")
                .AddText($"Game exited: {gameName}")
                .AddAttributionText("Profile stopped")
                .Show();
        }
        catch (Exception ex)
        {
            AppLogger.Warning($"Failed to show toast notification: {ex.Message}");
        }
    }

    /// <summary>
    /// Shows a toast notification when game monitoring starts.
    /// </summary>
    public static void ShowMonitoringStarted(int gameCount)
    {
        if (!Enabled) return;

        try
        {
            new ToastContentBuilder()
                .AddText($"{AppName}")
                .AddText("Game monitoring enabled")
                .AddAttributionText($"Watching for {gameCount} game(s)")
                .Show();
        }
        catch (Exception ex)
        {
            AppLogger.Warning($"Failed to show toast notification: {ex.Message}");
        }
    }

    /// <summary>
    /// Shows a toast notification when game monitoring stops.
    /// </summary>
    public static void ShowMonitoringStopped()
    {
        if (!Enabled) return;

        try
        {
            new ToastContentBuilder()
                .AddText($"{AppName}")
                .AddText("Game monitoring disabled")
                .Show();
        }
        catch (Exception ex)
        {
            AppLogger.Warning($"Failed to show toast notification: {ex.Message}");
        }
    }

    /// <summary>
    /// Shows a toast notification when a backup is created.
    /// </summary>
    public static void ShowBackupCreated(string fileName)
    {
        if (!Enabled) return;

        try
        {
            new ToastContentBuilder()
                .AddText($"{AppName}")
                .AddText("Backup created successfully")
                .AddAttributionText(fileName)
                .Show();
        }
        catch (Exception ex)
        {
            AppLogger.Warning($"Failed to show toast notification: {ex.Message}");
        }
    }

    /// <summary>
    /// Shows a toast notification when settings are restored from backup.
    /// </summary>
    public static void ShowBackupRestored()
    {
        if (!Enabled) return;

        try
        {
            new ToastContentBuilder()
                .AddText($"{AppName}")
                .AddText("Settings restored successfully")
                .AddAttributionText("Restart may be required for some changes")
                .Show();
        }
        catch (Exception ex)
        {
            AppLogger.Warning($"Failed to show toast notification: {ex.Message}");
        }
    }

    /// <summary>
    /// Cleans up toast notification resources on app exit.
    /// Call this when the application is closing.
    /// </summary>
    public static void Cleanup()
    {
        try
        {
            // Clear any pending notifications from this app
            ToastNotificationManagerCompat.History.Clear();
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
