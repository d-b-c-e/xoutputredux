namespace XOutputRenew.Input.ForceFeedback;

/// <summary>
/// Interface for devices that support force feedback output.
/// </summary>
public interface IForceFeedbackDevice
{
    /// <summary>
    /// Whether this device supports force feedback.
    /// </summary>
    bool SupportsForceFeedback { get; }

    /// <summary>
    /// Available force feedback targets (actuator axes).
    /// </summary>
    IReadOnlyList<ForceFeedbackTarget> ForceFeedbackTargets { get; }

    /// <summary>
    /// Sends force feedback to the device.
    /// </summary>
    /// <param name="value">Force value (0.0 - 1.0).</param>
    void SendForceFeedback(double value);

    /// <summary>
    /// Stops all force feedback effects.
    /// </summary>
    void StopForceFeedback();
}
