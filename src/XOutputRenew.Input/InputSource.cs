namespace XOutputRenew.Input;

/// <summary>
/// Base class for input sources (buttons, axes, sliders, DPad).
/// Adapted from XOutput.App.Devices.Input.InputSource
/// </summary>
public abstract class InputSource : IInputSource
{
    public int Index { get; }
    public string Name { get; }
    public InputSourceType Type { get; }
    public double Deadzone { get; set; }

    protected double _value;

    public double Value => _value;

    protected InputSource(string name, InputSourceType type, int index)
    {
        Name = name;
        Type = type;
        Index = index;
    }

    /// <summary>
    /// Updates the value and returns true if it changed.
    /// </summary>
    protected bool RefreshValue(double newValue)
    {
        double calculatedValue = ApplyDeadzone(newValue);
        if (Math.Abs(calculatedValue - _value) > 0.0001)
        {
            _value = calculatedValue;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Applies deadzone based on source type.
    /// </summary>
    private double ApplyDeadzone(double newValue)
    {
        return Type switch
        {
            // Buttons and DPad: no deadzone
            InputSourceType.Button or InputSourceType.DPad => newValue,

            // Sliders: edge deadzone
            InputSourceType.Slider => newValue switch
            {
                _ when newValue < Deadzone => 0,
                _ when newValue > 1 - Deadzone => 1,
                _ => newValue
            },

            // Axes: center deadzone (0.5 is center)
            InputSourceType.Axis => Math.Abs(newValue - 0.5) < Deadzone ? 0.5 : newValue,

            _ => newValue
        };
    }

    public override string ToString() => Name;
}
