namespace XOutputRedux.Core.Mapping;

/// <summary>
/// Direction a digital input pushes an axis when used as a button-to-axis binding.
/// </summary>
public enum DigitalAxisDirection
{
    /// <summary>No direction — use the input's analog value (default behavior).</summary>
    None,
    /// <summary>When pressed, push the axis toward 1.0 (right/down).</summary>
    Positive,
    /// <summary>When pressed, push the axis toward 0.0 (left/up).</summary>
    Negative
}

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
    /// Inner deadzone (0.0 - 0.49). Input values below this threshold
    /// (after min/max scaling) are treated as zero (triggers) or center (axes).
    /// </summary>
    public double InnerDeadzone { get; set; } = 0.0;

    /// <summary>
    /// Outer deadzone (0.0 - 0.49). Input values above (1.0 - OuterDeadzone)
    /// (after min/max scaling) are treated as full deflection.
    /// </summary>
    public double OuterDeadzone { get; set; } = 0.0;

    /// <summary>
    /// For button-to-axis mappings: which direction this button pushes the axis.
    /// None = use analog value (default). Positive = push to 1.0. Negative = push to 0.0.
    /// </summary>
    public DigitalAxisDirection DigitalDirection { get; set; } = DigitalAxisDirection.None;

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

        // Apply deadzones (remap effective range to 0-1)
        if (InnerDeadzone > 0.001 || OuterDeadzone > 0.001)
        {
            scaled = ApplyDeadzones(scaled, isAxisOutput);
        }

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
    /// Applies inner and outer deadzones.
    /// For triggers: values below inner → 0, above (1-outer) → 1, rest remapped linearly.
    /// For axes: symmetric deadzones around center (0.5).
    /// </summary>
    private double ApplyDeadzones(double value, bool isAxis)
    {
        if (isAxis)
        {
            // Work in deflection space (0-1 from center)
            double deflection = Math.Abs(value - 0.5) * 2.0;
            double sign = Math.Sign(value - 0.5);

            double effectiveRange = 1.0 - InnerDeadzone - OuterDeadzone;
            if (effectiveRange <= 0.001) return 0.5; // deadzones consume entire range

            if (deflection <= InnerDeadzone)
                deflection = 0.0;
            else if (deflection >= 1.0 - OuterDeadzone)
                deflection = 1.0;
            else
                deflection = (deflection - InnerDeadzone) / effectiveRange;

            return 0.5 + sign * deflection * 0.5;
        }
        else
        {
            // Trigger: simple linear remap
            double effectiveRange = 1.0 - InnerDeadzone - OuterDeadzone;
            if (effectiveRange <= 0.001) return 0.0;

            if (value <= InnerDeadzone)
                return 0.0;
            if (value >= 1.0 - OuterDeadzone)
                return 1.0;

            return (value - InnerDeadzone) / effectiveRange;
        }
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
