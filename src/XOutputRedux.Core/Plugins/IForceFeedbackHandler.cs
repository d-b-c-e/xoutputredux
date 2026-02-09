namespace XOutputRedux.Core.Plugins;

/// <summary>
/// Plugin-provided force feedback handler that replaces the default
/// DirectInput FFB routing for a specific device.
/// </summary>
public interface IForceFeedbackHandler
{
    /// <summary>
    /// Sends a force feedback value to the device.
    /// Called at the same rate as the standard FFB pipeline (~10Hz).
    /// </summary>
    /// <param name="value">Force value (0.0 - 1.0), already processed through mode and gain settings.</param>
    void SendForceFeedback(double value);

    /// <summary>
    /// Stops all force feedback effects.
    /// </summary>
    void Stop();
}
