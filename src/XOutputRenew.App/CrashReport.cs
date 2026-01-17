namespace XOutputRenew.App;

/// <summary>
/// Encapsulates all data collected during a crash for reporting purposes.
/// </summary>
public class CrashReport
{
    /// <summary>
    /// Timestamp when the crash occurred.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// The type of exception (e.g., "NullReferenceException").
    /// </summary>
    public required string ExceptionType { get; init; }

    /// <summary>
    /// The exception message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// The full stack trace.
    /// </summary>
    public string? StackTrace { get; init; }

    /// <summary>
    /// Inner exception details if present.
    /// </summary>
    public string? InnerException { get; init; }

    /// <summary>
    /// Application version string.
    /// </summary>
    public required string AppVersion { get; init; }

    /// <summary>
    /// Operating system information (sanitized).
    /// </summary>
    public required string OsInfo { get; init; }

    /// <summary>
    /// .NET runtime version.
    /// </summary>
    public required string RuntimeVersion { get; init; }

    /// <summary>
    /// Whether ViGEm was available at crash time.
    /// </summary>
    public bool ViGEmAvailable { get; init; }

    /// <summary>
    /// Whether HidHide was available at crash time.
    /// </summary>
    public bool HidHideAvailable { get; init; }

    /// <summary>
    /// Name of the active profile when crash occurred (if any).
    /// </summary>
    public string? ActiveProfile { get; init; }

    /// <summary>
    /// Context where the crash occurred (UI thread, background task, IPC, etc).
    /// </summary>
    public required CrashContext Context { get; init; }

    /// <summary>
    /// Whether the exception is fatal (app must exit).
    /// </summary>
    public bool IsFatal { get; init; }

    /// <summary>
    /// Path to the log file at crash time.
    /// </summary>
    public string? LogFilePath { get; init; }
}

/// <summary>
/// Context in which a crash occurred.
/// </summary>
public enum CrashContext
{
    /// <summary>UI/dispatcher thread exception.</summary>
    UIThread,

    /// <summary>Background Task exception.</summary>
    BackgroundTask,

    /// <summary>AppDomain unhandled exception (fatal).</summary>
    AppDomain,

    /// <summary>Exception during IPC communication.</summary>
    IpcServer,

    /// <summary>Exception during startup initialization.</summary>
    Startup,

    /// <summary>Exception during profile execution.</summary>
    ProfileRunning,

    /// <summary>Exception during device polling.</summary>
    DevicePolling
}
