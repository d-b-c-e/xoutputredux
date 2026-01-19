using Vortice.DirectInput;

namespace XOutputRedux.Input.DirectInput;

/// <summary>
/// Input source for DirectInput devices.
/// Adapted from XOutput.App.Devices.Input.DirectInput.DirectInputSource
/// </summary>
public class DirectInputSource : InputSource
{
    private readonly Func<JoystickState, double> _valueGetter;

    private DirectInputSource(string name, InputSourceType type, int index, Func<JoystickState, double> valueGetter)
        : base(name, type, index)
    {
        _valueGetter = valueGetter;
    }

    /// <summary>
    /// Creates a button source.
    /// </summary>
    public static DirectInputSource FromButton(DeviceObjectInstance instance, int buttonIndex)
    {
        return new DirectInputSource(
            $"Button {instance.Usage}",
            InputSourceType.Button,
            instance.Offset,
            state => state.Buttons[buttonIndex] ? 1.0 : 0.0
        );
    }

    /// <summary>
    /// Creates axis source.
    /// </summary>
    public static DirectInputSource FromAxis(DeviceObjectInstance instance)
    {
        int axisIndex = instance.Usage >= 48 ? instance.Usage - 48 : instance.ObjectId.InstanceNumber;
        string name = instance.Name ?? $"Axis {axisIndex}";

        return new DirectInputSource(
            name,
            InputSourceType.Axis,
            instance.Offset,
            state => GetAxisValue(state, axisIndex) / (double)ushort.MaxValue
        );
    }

    /// <summary>
    /// Creates slider source.
    /// </summary>
    public static DirectInputSource FromSlider(DeviceObjectInstance instance, int sliderIndex)
    {
        string name = instance.Name ?? $"Slider {sliderIndex + 1}";

        return new DirectInputSource(
            name,
            InputSourceType.Slider,
            instance.Offset,
            state => state.Sliders[sliderIndex] / (double)ushort.MaxValue
        );
    }

    /// <summary>
    /// Creates four DPad direction sources from a single POV controller.
    /// </summary>
    public static DirectInputSource[] FromDPad(int povIndex)
    {
        int baseOffset = 100000 + povIndex * 4;
        string prefix = povIndex == 0 ? "DPad" : $"DPad{povIndex + 1}";

        return
        [
            new DirectInputSource($"{prefix} Up", InputSourceType.DPad, baseOffset,
                state => GetDPadDirection(state, povIndex).HasFlag(DPadDirection.Up) ? 1.0 : 0.0),
            new DirectInputSource($"{prefix} Down", InputSourceType.DPad, baseOffset + 1,
                state => GetDPadDirection(state, povIndex).HasFlag(DPadDirection.Down) ? 1.0 : 0.0),
            new DirectInputSource($"{prefix} Left", InputSourceType.DPad, baseOffset + 2,
                state => GetDPadDirection(state, povIndex).HasFlag(DPadDirection.Left) ? 1.0 : 0.0),
            new DirectInputSource($"{prefix} Right", InputSourceType.DPad, baseOffset + 3,
                state => GetDPadDirection(state, povIndex).HasFlag(DPadDirection.Right) ? 1.0 : 0.0),
        ];
    }

    /// <summary>
    /// Refreshes the value from joystick state. Returns true if changed.
    /// </summary>
    internal bool Refresh(JoystickState state)
    {
        double newValue = _valueGetter(state);
        return RefreshValue(newValue);
    }

    private static DPadDirection GetDPadDirection(JoystickState state, int povIndex)
    {
        int pov = state.PointOfViewControllers[povIndex];
        return pov switch
        {
            -1 => DPadDirection.None,
            0 => DPadDirection.Up,
            4500 => DPadDirection.Up | DPadDirection.Right,
            9000 => DPadDirection.Right,
            13500 => DPadDirection.Down | DPadDirection.Right,
            18000 => DPadDirection.Down,
            22500 => DPadDirection.Down | DPadDirection.Left,
            27000 => DPadDirection.Left,
            31500 => DPadDirection.Up | DPadDirection.Left,
            _ => DPadDirection.None
        };
    }

    private static int GetAxisValue(JoystickState state, int axisIndex)
    {
        return axisIndex switch
        {
            0 => state.X,
            1 => ushort.MaxValue - state.Y,  // Invert Y
            2 => state.Z,
            3 => state.RotationX,
            4 => ushort.MaxValue - state.RotationY,  // Invert RY
            5 => state.RotationZ,
            6 => state.AccelerationX,
            7 => ushort.MaxValue - state.AccelerationY,
            8 => state.AccelerationZ,
            9 => state.AngularAccelerationX,
            10 => ushort.MaxValue - state.AngularAccelerationY,
            11 => state.AngularAccelerationZ,
            12 => state.ForceX,
            13 => ushort.MaxValue - state.ForceY,
            14 => state.ForceZ,
            15 => state.TorqueX,
            16 => ushort.MaxValue - state.TorqueY,
            17 => state.TorqueZ,
            18 => state.VelocityX,
            19 => ushort.MaxValue - state.VelocityY,
            20 => state.VelocityZ,
            21 => state.AngularVelocityX,
            22 => ushort.MaxValue - state.AngularVelocityY,
            23 => state.AngularVelocityZ,
            _ => 0
        };
    }
}
