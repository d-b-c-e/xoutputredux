namespace XOutputRenew.Input;

/// <summary>
/// Represents an input device (gamepad, wheel, joystick, etc.)
/// </summary>
public interface IInputDevice : IDisposable
{
    /// <summary>
    /// Unique identifier for this device (stable across reboots).
    /// </summary>
    string UniqueId { get; }

    /// <summary>
    /// Display name of the device.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Hardware ID (VID/PID format).
    /// </summary>
    string? HardwareId { get; }

    /// <summary>
    /// Device interface path (for HidHide integration).
    /// </summary>
    string? InterfacePath { get; }

    /// <summary>
    /// Input method (DirectInput, RawInput).
    /// </summary>
    InputMethod Method { get; }

    /// <summary>
    /// Whether the device is currently connected.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// All input sources on this device.
    /// </summary>
    IReadOnlyList<IInputSource> Sources { get; }

    /// <summary>
    /// Starts polling the device for input.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops polling the device.
    /// </summary>
    void Stop();

    /// <summary>
    /// Event raised when any input value changes.
    /// </summary>
    event EventHandler<InputChangedEventArgs>? InputChanged;

    /// <summary>
    /// Event raised when the device is disconnected.
    /// </summary>
    event EventHandler? Disconnected;
}

/// <summary>
/// Input method types.
/// </summary>
public enum InputMethod
{
    DirectInput,
    RawInput
}

/// <summary>
/// Event args for input changes.
/// </summary>
public class InputChangedEventArgs : EventArgs
{
    public required IInputSource Source { get; init; }
    public required double OldValue { get; init; }
    public required double NewValue { get; init; }
}
