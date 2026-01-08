namespace XOutputRenew.Core.Mapping;

/// <summary>
/// Represents an Xbox 360 controller output target.
/// </summary>
public enum XboxOutput
{
    // Buttons
    A,
    B,
    X,
    Y,
    LeftBumper,
    RightBumper,
    Back,
    Start,
    Guide,
    LeftStick,
    RightStick,
    DPadUp,
    DPadDown,
    DPadLeft,
    DPadRight,

    // Axes
    LeftStickX,
    LeftStickY,
    RightStickX,
    RightStickY,

    // Triggers
    LeftTrigger,
    RightTrigger
}

/// <summary>
/// Helper methods for XboxOutput.
/// </summary>
public static class XboxOutputExtensions
{
    /// <summary>
    /// Returns true if this output is a button (binary on/off).
    /// </summary>
    public static bool IsButton(this XboxOutput output)
    {
        return output switch
        {
            XboxOutput.A or XboxOutput.B or XboxOutput.X or XboxOutput.Y or
            XboxOutput.LeftBumper or XboxOutput.RightBumper or
            XboxOutput.Back or XboxOutput.Start or XboxOutput.Guide or
            XboxOutput.LeftStick or XboxOutput.RightStick or
            XboxOutput.DPadUp or XboxOutput.DPadDown or XboxOutput.DPadLeft or XboxOutput.DPadRight
                => true,
            _ => false
        };
    }

    /// <summary>
    /// Returns true if this output is an axis (continuous value, centered at 0.5).
    /// </summary>
    public static bool IsAxis(this XboxOutput output)
    {
        return output switch
        {
            XboxOutput.LeftStickX or XboxOutput.LeftStickY or
            XboxOutput.RightStickX or XboxOutput.RightStickY
                => true,
            _ => false
        };
    }

    /// <summary>
    /// Returns true if this output is a trigger (continuous value, 0.0 to 1.0).
    /// </summary>
    public static bool IsTrigger(this XboxOutput output)
    {
        return output == XboxOutput.LeftTrigger || output == XboxOutput.RightTrigger;
    }
}
