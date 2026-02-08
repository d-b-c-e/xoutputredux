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
    /// Response curve sensitivity. 1.0 = linear (default).
    /// Greater than 1.0 = less sensitive near center (convex curve).
    /// Less than 1.0 = more sensitive near center (concave curve).
    /// Range: 0.1 to 5.0. Implemented as a power/gamma function.
    /// </summary>
    public double Sensitivity { get; set; } = 1.0;

    /// <summary>
    /// Transforms an input value according to this binding's settings.
    /// </summary>
    /// <param name="inputValue">The raw input value (0.0 - 1.0).</param>
    /// <param name="isAxisOutput">True if the output is an axis (centered at 0.5), false for triggers/buttons.</param>
    /// <returns>The transformed value (0.0 - 1.0).</returns>
    public double TransformValue(double inputValue, bool isAxisOutput = false)
    {
        // Apply min/max scaling
        double range = MaxValue - MinValue;
        double scaled = range > 0.0001
            ? (inputValue - MinValue) / range
            : 0.0;

        // Clamp to 0-1 range
        scaled = Math.Clamp(scaled, 0.0, 1.0);

        // Apply response curve (if not linear)
        if (Math.Abs(Sensitivity - 1.0) > 0.001)
        {
            scaled = ApplyResponseCurve(scaled, isAxisOutput);
        }

        // Apply inversion
        if (Invert)
        {
            scaled = 1.0 - scaled;
        }

        return scaled;
    }

    /// <summary>
    /// Applies a power/gamma response curve to the value.
    /// For axes: symmetric curve around center (0.5).
    /// For triggers: simple power curve over 0-1 range.
    /// </summary>
    private double ApplyResponseCurve(double value, bool isAxis)
    {
        if (isAxis)
        {
            // Symmetric power curve around center (0.5)
            double deflection = Math.Abs(value - 0.5) * 2.0; // normalize to 0-1 from center
            double curved = Math.Pow(deflection, Sensitivity);
            return 0.5 + Math.Sign(value - 0.5) * curved * 0.5;
        }
        else
        {
            // Simple power curve for triggers (0-1 range)
            return Math.Pow(value, Sensitivity);
        }
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
