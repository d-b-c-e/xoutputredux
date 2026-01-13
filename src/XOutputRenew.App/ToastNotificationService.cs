using Microsoft.Toolkit.Uwp.Notifications;

namespace XOutputRenew.App;

/// <summary>
/// Service for showing Windows toast notifications.
/// </summary>
public static class ToastNotificationService
{
    private const string AppName = "XOutputRenew";

    /// <summary>
    /// Shows a toast notification when a profile starts.
    /// </summary>
    public static void ShowProfileStarted(string profileName)
    {
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
