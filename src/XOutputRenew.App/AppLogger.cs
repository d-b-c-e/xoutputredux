using System.IO;
using System.Runtime.CompilerServices;

namespace XOutputRenew.App;

/// <summary>
/// Simple file logger for debugging.
/// </summary>
public static class AppLogger
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "XOutputRenew",
        "logs",
        $"xoutputrenew-{DateTime.Now:yyyy-MM-dd}.log");

    private static readonly object Lock = new();
    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized) return;

        try
        {
            var dir = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            _initialized = true;
            Log("INFO", "Application started");
        }
        catch
        {
            // Ignore initialization errors
        }
    }

    public static void Info(string message, [CallerMemberName] string? caller = null)
    {
        Log("INFO", message, caller);
    }

    public static void Warning(string message, [CallerMemberName] string? caller = null)
    {
        Log("WARN", message, caller);
    }

    public static void Error(string message, Exception? ex = null, [CallerMemberName] string? caller = null)
    {
        if (ex != null)
        {
            Log("ERROR", $"{message}\n  Exception: {ex.GetType().Name}: {ex.Message}\n  StackTrace: {ex.StackTrace}", caller);
        }
        else
        {
            Log("ERROR", message, caller);
        }
    }

    private static void Log(string level, string message, string? caller = null)
    {
        if (!_initialized) return;

        try
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var callerInfo = caller != null ? $"[{caller}] " : "";
            var line = $"{timestamp} [{level}] {callerInfo}{message}";

            lock (Lock)
            {
                File.AppendAllText(LogPath, line + Environment.NewLine);
            }
        }
        catch
        {
            // Ignore logging errors
        }
    }

    /// <summary>
    /// Gets the path to the current log file.
    /// </summary>
    public static string GetLogPath() => LogPath;
}
