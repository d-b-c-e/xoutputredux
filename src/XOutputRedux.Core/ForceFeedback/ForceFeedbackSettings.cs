namespace XOutputRedux.Core.ForceFeedback;

/// <summary>
/// Force feedback motor combination mode.
/// </summary>
public enum ForceFeedbackMode
{
    /// <summary>Use only the large motor value (low-frequency rumble).</summary>
    Large,

    /// <summary>Use only the small motor value (high-frequency rumble).</summary>
    Small,

    /// <summary>Combine both motors using max value.</summary>
    Combined,

    /// <summary>Swap large and small motor roles.</summary>
    Swap
}

/// <summary>
/// Force feedback configuration for a profile.
/// </summary>
public class ForceFeedbackSettings
{
    /// <summary>
    /// Whether force feedback is enabled for this profile.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Device ID of the target device for FFB output.
    /// Must be a DirectInput device with FFB support.
    /// </summary>
    public string? TargetDeviceId { get; set; }

    /// <summary>
    /// Motor combination mode.
    /// </summary>
    public ForceFeedbackMode Mode { get; set; } = ForceFeedbackMode.Combined;

    /// <summary>
    /// Gain multiplier (0.0 - 2.0). Values above 1.0 amplify, below 1.0 reduce.
    /// </summary>
    public double Gain { get; set; } = 1.0;

    /// <summary>
    /// Creates a deep copy of these settings.
    /// </summary>
    public ForceFeedbackSettings Clone() => new()
    {
        Enabled = Enabled,
        TargetDeviceId = TargetDeviceId,
        Mode = Mode,
        Gain = Gain
    };
}

/// <summary>
/// JSON serialization model for ForceFeedbackSettings.
/// </summary>
public class ForceFeedbackSettingsData
{
    public bool Enabled { get; set; }
    public string? TargetDeviceId { get; set; }
    public string Mode { get; set; } = "Combined";
    public double Gain { get; set; } = 1.0;

    public static ForceFeedbackSettingsData FromSettings(ForceFeedbackSettings settings)
    {
        return new ForceFeedbackSettingsData
        {
            Enabled = settings.Enabled,
            TargetDeviceId = settings.TargetDeviceId,
            Mode = settings.Mode.ToString(),
            Gain = settings.Gain
        };
    }

    public ForceFeedbackSettings ToSettings()
    {
        return new ForceFeedbackSettings
        {
            Enabled = Enabled,
            TargetDeviceId = TargetDeviceId,
            Mode = Enum.TryParse<ForceFeedbackMode>(Mode, out var mode)
                   ? mode : ForceFeedbackMode.Combined,
            Gain = Gain
        };
    }
}
