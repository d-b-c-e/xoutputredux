namespace XOutputRedux.Input.DirectInput;

/// <summary>
/// DPad direction flags.
/// </summary>
[Flags]
public enum DPadDirection
{
    None = 0,
    Up = 1,
    Down = 2,
    Left = 4,
    Right = 8
}
