namespace XOutputRedux.Input.ForceFeedback;

/// <summary>
/// Represents a force feedback actuator axis on a device.
/// </summary>
public class ForceFeedbackTarget
{
    /// <summary>
    /// Display name of the actuator.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Offset of the actuator object in DirectInput.
    /// </summary>
    public int Offset { get; }

    /// <summary>
    /// Current target value (0.0 - 1.0).
    /// </summary>
    public double Value { get; set; }

    public ForceFeedbackTarget(string name, int offset)
    {
        Name = name;
        Offset = offset;
    }
}
