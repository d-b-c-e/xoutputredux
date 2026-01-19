namespace XOutputRedux.Input;

/// <summary>
/// Represents a single input source on a device (button, axis, slider, etc.)
/// </summary>
public interface IInputSource
{
    /// <summary>
    /// Index of this source on the device.
    /// </summary>
    int Index { get; }

    /// <summary>
    /// Display name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Type of input source.
    /// </summary>
    InputSourceType Type { get; }

    /// <summary>
    /// Current value (0.0 - 1.0).
    /// For buttons: 0.0 = released, 1.0 = pressed
    /// For axes: 0.5 = center, 0.0/1.0 = extremes
    /// </summary>
    double Value { get; }

    /// <summary>
    /// Deadzone for this source (0.0 - 1.0).
    /// </summary>
    double Deadzone { get; set; }
}

/// <summary>
/// Types of input sources.
/// </summary>
public enum InputSourceType
{
    Button,
    Axis,
    Slider,
    DPad
}
