using System.IO;

namespace XOutputRedux.App;

/// <summary>
/// Centralized path provider for all app data storage.
/// Supports portable mode when portable.txt exists next to the executable.
/// </summary>
public static class AppPaths
{
    private static readonly Lazy<string> _baseDir = new(DetermineBaseDirectory);

    /// <summary>
    /// Whether the app is running in portable mode.
    /// </summary>
    public static bool IsPortable { get; private set; }

    /// <summary>
    /// Base directory for all app data.
    /// </summary>
    public static string BaseDirectory => _baseDir.Value;

    /// <summary>
    /// Path to app-settings.json.
    /// </summary>
    public static string AppSettings => Path.Combine(BaseDirectory, "app-settings.json");

    /// <summary>
    /// Path to device-settings.json.
    /// </summary>
    public static string DeviceSettings => Path.Combine(BaseDirectory, "device-settings.json");

    /// <summary>
    /// Path to games.json.
    /// </summary>
    public static string Games => Path.Combine(BaseDirectory, "games.json");

    /// <summary>
    /// Path to Profiles directory.
    /// </summary>
    public static string Profiles => Path.Combine(BaseDirectory, "Profiles");

    /// <summary>
    /// Path to logs directory.
    /// </summary>
    public static string Logs => Path.Combine(BaseDirectory, "logs");

    private static string DetermineBaseDirectory()
    {
        // Check for portable.txt next to executable
        var exeDir = AppContext.BaseDirectory;
        var portableMarker = Path.Combine(exeDir, "portable.txt");

        if (File.Exists(portableMarker))
        {
            IsPortable = true;
            var dataDir = Path.Combine(exeDir, "data");
            Directory.CreateDirectory(dataDir);
            return dataDir;
        }

        // Standard AppData location
        IsPortable = false;
        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "XOutputRedux");
        Directory.CreateDirectory(appDataDir);
        return appDataDir;
    }
}
