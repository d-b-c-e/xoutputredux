using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace XOutputRenew.App;

/// <summary>
/// Application settings that persist across sessions.
/// </summary>
public class AppSettings
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "XOutputRenew",
        "app-settings.json");

    private const string StartupRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "XOutputRenew";

    /// <summary>
    /// When true, closing the window minimizes to tray instead of exiting.
    /// </summary>
    public bool MinimizeToTrayOnClose { get; set; } = true;

    /// <summary>
    /// Profile to start automatically when the app launches.
    /// </summary>
    public string? StartupProfile { get; set; }

    /// <summary>
    /// Whether the user has declined the HidHide installation prompt.
    /// </summary>
    public bool HidHidePromptDeclined { get; set; }

    /// <summary>
    /// Whether game monitoring should be enabled on startup.
    /// </summary>
    public bool GameMonitoringEnabled { get; set; }

    /// <summary>
    /// Loads settings from disk.
    /// </summary>
    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch
        {
            // Ignore load errors
        }
        return new AppSettings();
    }

    /// <summary>
    /// Saves settings to disk.
    /// </summary>
    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }

    /// <summary>
    /// Gets whether the app is configured to start with Windows.
    /// </summary>
    public static bool GetStartWithWindows()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, false);
            return key?.GetValue(AppName) != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Sets whether the app should start with Windows.
    /// </summary>
    public static void SetStartWithWindows(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, true);
            if (key == null) return;

            if (enabled)
            {
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    // Start minimized when launched at startup
                    key.SetValue(AppName, $"\"{exePath}\" --minimized");
                }
            }
            else
            {
                key.DeleteValue(AppName, false);
            }
        }
        catch
        {
            // Ignore registry errors
        }
    }

    /// <summary>
    /// Checks if the application is running with administrator privileges.
    /// </summary>
    public static bool IsRunningAsAdmin()
    {
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Restarts the application with administrator privileges.
    /// </summary>
    public static void RestartAsAdmin()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath)) return;

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                Verb = "runas"
            };

            System.Diagnostics.Process.Start(startInfo);
            System.Windows.Application.Current.Shutdown();
        }
        catch
        {
            // User cancelled UAC or other error
        }
    }
}
