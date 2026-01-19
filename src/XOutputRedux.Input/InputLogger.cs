namespace XOutputRedux.Input;

/// <summary>
/// Simple debug logger for input subsystem.
/// Set LogAction to receive log messages.
/// </summary>
public static class InputLogger
{
    /// <summary>
    /// Set this to receive debug log messages from input devices.
    /// </summary>
    public static Action<string>? LogAction { get; set; }

    /// <summary>
    /// Enable verbose logging (high volume - logs every poll).
    /// </summary>
    public static bool VerboseEnabled { get; set; }

    public static void Log(string message)
    {
        LogAction?.Invoke($"[Input] {message}");
    }

    public static void Verbose(string message)
    {
        if (VerboseEnabled)
        {
            LogAction?.Invoke($"[Input:Verbose] {message}");
        }
    }
}
