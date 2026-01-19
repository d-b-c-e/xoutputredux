namespace XOutputRedux.Core.Mapping;

/// <summary>
/// Represents a binding from a physical input source to an output.
/// </summary>
public class InputBinding
{
    /// <summary>
    /// Unique ID of the input device.
    /// </summary>
    public required string DeviceId { get; init; }

    /// <summary>
    /// Index of the input source on the device.
    /// </summary>
    public required int SourceIndex { get; init; }

    /// <summary>
    /// Display name for this binding (e.g., "Wheel Button 4").
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Whether to invert the input value.
    /// For buttons: inverts pressed state.
    /// For axes: inverts the axis direction.
    /// </summary>
    public bool Invert { get; set; }

    /// <summary>
    /// Minimum input value to consider (for axis scaling).
    /// Values below this are treated as the minimum.
    /// </summary>
    public double MinValue { get; set; } = 0.0;

    /// <summary>
    /// Maximum input value to consider (for axis scaling).
    /// Values above this are treated as the maximum.
    /// </summary>
    public double MaxValue { get; set; } = 1.0;

    /// <summary>
    /// Threshold for button activation (0.0 - 1.0).
    /// When an axis/trigger is used as a button, values above this trigger the button.
    /// </summary>
    public double ButtonThreshold { get; set; } = 0.5;

    /// <summary>
    /// Transforms an input value according to this binding's settings.
    /// </summary>
    /// <param name="inputValue">The raw input value (0.0 - 1.0).</param>
    /// <returns>The transformed value (0.0 - 1.0).</returns>
    public double TransformValue(double inputValue)
    {
        // Apply min/max scaling
        double range = MaxValue - MinValue;
        double scaled = range > 0.0001
            ? (inputValue - MinValue) / range
            : 0.0;

        // Clamp to 0-1 range
        scaled = Math.Clamp(scaled, 0.0, 1.0);

        // Apply inversion
        if (Invert)
        {
            scaled = 1.0 - scaled;
        }

        return scaled;
    }

    /// <summary>
    /// Evaluates an input value as a button press.
    /// </summary>
    /// <param name="inputValue">The raw input value (0.0 - 1.0).</param>
    /// <returns>True if the button should be considered pressed.</returns>
    public bool EvaluateAsButton(double inputValue)
    {
        double transformed = TransformValue(inputValue);
        return transformed >= ButtonThreshold;
    }
}
