using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace XOutputRedux.App;

/// <summary>
/// Application settings that persist across sessions.
/// </summary>
public class AppSettings
{
    /// <summary>
    /// Current schema version. Increment when making breaking changes.
    /// </summary>
    public const int CurrentSchemaVersion = 2;

    private static string SettingsPath => AppPaths.AppSettings;

    private const string StartupRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "XOutputRedux";

    /// <summary>
    /// Schema version of this settings file. Used for migration.
    /// </summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>
    /// When true, closing the window minimizes to tray instead of exiting.
    /// </summary>
    public bool MinimizeToTrayOnClose { get; set; } = true;

    /// <summary>
    /// When true, toast notifications are shown for profile start/stop and game detection.
    /// </summary>
    public bool ToastNotificationsEnabled { get; set; } = true;

    /// <summary>
    /// Profile to start automatically when the app launches.
    /// </summary>
    public string? StartupProfile { get; set; }

    /// <summary>
    /// Whether the user has declined the HidHide installation prompt.
    /// </summary>
    public bool HidHidePromptDeclined { get; set; }

    /// <summary>
    /// Whether the user has declined the ViGEmBus installation prompt.
    /// </summary>
    public bool ViGEmBusPromptDeclined { get; set; }

    /// <summary>
    /// Whether game monitoring should be enabled on startup.
    /// </summary>
    public bool GameMonitoringEnabled { get; set; }

    /// <summary>
    /// Whether to check for updates on startup.
    /// </summary>
    public bool CheckForUpdatesOnStartup { get; set; } = true;

    /// <summary>
    /// Last time we checked for updates (for rate limiting).
    /// </summary>
    public DateTime? LastUpdateCheck { get; set; }

    /// <summary>
    /// Whether to show crash reporting dialog when an exception occurs.
    /// </summary>
    public bool CrashReportingEnabled { get; set; } = true;

    /// <summary>
    /// Whether to include active profile name in crash reports.
    /// </summary>
    public bool IncludeProfileInCrashReport { get; set; } = true;

    /// <summary>
    /// Checks if enough time has passed to check for updates again.
    /// </summary>
    public bool ShouldCheckForUpdates()
    {
        if (!CheckForUpdatesOnStartup)
            return false;

        if (LastUpdateCheck == null)
            return true;

        // Check once per 24 hours
        return DateTime.UtcNow - LastUpdateCheck.Value > TimeSpan.FromHours(24);
    }

    /// <summary>
    /// Records that an update check was performed.
    /// </summary>
    public void RecordUpdateCheck()
    {
        LastUpdateCheck = DateTime.UtcNow;
        Save();
    }

    /// <summary>
    /// Whether this settings instance needs migration.
    /// </summary>
    [JsonIgnore]
    public bool NeedsMigration => SchemaVersion < CurrentSchemaVersion;

    /// <summary>
    /// Migrates settings to the current schema version.
    /// </summary>
    private void Migrate()
    {
        // Migration from version 0 (no version field) to version 1
        if (SchemaVersion < 1)
        {
            SchemaVersion = 1;
        }

        // Migration from version 1 to version 2: add crash reporting settings
        if (SchemaVersion < 2)
        {
            CrashReportingEnabled = true;
            IncludeProfileInCrashReport = true;
            SchemaVersion = 2;
        }

        SchemaVersion = CurrentSchemaVersion;
    }

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
                var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();

                // Check if migration is needed
                if (settings.NeedsMigration)
                {
                    settings.Migrate();
                    settings.Save();
                }

                return settings;
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
