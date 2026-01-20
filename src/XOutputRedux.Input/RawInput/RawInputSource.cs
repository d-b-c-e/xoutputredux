using XOutputRedux.HidSharper.Reports;
using XOutputRedux.Input.DirectInput;

namespace XOutputRedux.Input.RawInput;

/// <summary>
/// Input source for RawInput/HID devices.
/// Adapted from XOutput.App.Devices.Input.RawInput.RawInputSource
/// </summary>
public class RawInputSource : InputSource
{
    private readonly Func<Dictionary<Usage, DataValue>, double?> _valueGetter;

    private RawInputSource(string name, InputSourceType type, int index, Func<Dictionary<Usage, DataValue>, double?> valueGetter)
        : base(name, type, index)
    {
        _valueGetter = valueGetter;
    }

    /// <summary>
    /// Creates input sources from a HID usage.
    /// </summary>
    public static RawInputSource[] FromUsage(Usage usage)
    {
        return usage switch
        {
            // X axes
            Usage.GenericDesktopX => [new RawInputSource("X Axis", InputSourceType.Axis, (int)usage,
                changes => GetScaledValue(changes, usage))],
            Usage.GenericDesktopRx => [new RawInputSource("Rx Axis", InputSourceType.Axis, (int)usage,
                changes => GetScaledValue(changes, usage))],

            // Y axes
            Usage.GenericDesktopY => [new RawInputSource("Y Axis", InputSourceType.Axis, (int)usage,
                changes => GetScaledValue(changes, usage))],
            Usage.GenericDesktopRy => [new RawInputSource("Ry Axis", InputSourceType.Axis, (int)usage,
                changes => GetScaledValue(changes, usage))],

            // Z axes
            Usage.GenericDesktopZ => [new RawInputSource("Z Axis", InputSourceType.Axis, (int)usage,
                changes => GetScaledValue(changes, usage))],
            Usage.GenericDesktopRz => [new RawInputSource("Rz Axis", InputSourceType.Axis, (int)usage,
                changes => GetScaledValue(changes, usage))],

            // Buttons
            >= Usage.Button1 and <= Usage.Button31 => [new RawInputSource(
                $"Button {usage - Usage.Button1 + 1}",
                InputSourceType.Button,
                (int)usage,
                changes => GetScaledValue(changes, usage))],

            // DPad (hat switch) - creates 4 direction sources
            Usage.GenericDesktopHatSwitch =>
            [
                new RawInputSource("DPad Up", InputSourceType.DPad, 100000,
                    changes => GetDPadDirection(changes, DPadDirection.Up)),
                new RawInputSource("DPad Down", InputSourceType.DPad, 100001,
                    changes => GetDPadDirection(changes, DPadDirection.Down)),
                new RawInputSource("DPad Left", InputSourceType.DPad, 100002,
                    changes => GetDPadDirection(changes, DPadDirection.Left)),
                new RawInputSource("DPad Right", InputSourceType.DPad, 100003,
                    changes => GetDPadDirection(changes, DPadDirection.Right)),
            ],

            // Sliders
            Usage.GenericDesktopSlider or Usage.GenericDesktopDial or Usage.GenericDesktopWheel =>
                [new RawInputSource(usage.ToString(), InputSourceType.Slider, (int)usage,
                    changes => GetScaledValue(changes, usage))],

            // Unknown usages - ignore
            _ => []
        };
    }

    /// <summary>
    /// Refreshes value from HID report changes. Returns true if changed.
    /// </summary>
    internal bool Refresh(Dictionary<Usage, DataValue> changes)
    {
        double? newValue = _valueGetter(changes);
        if (newValue.HasValue)
        {
            return RefreshValue(newValue.Value);
        }
        return false;
    }

    private static double? GetScaledValue(Dictionary<Usage, DataValue> changes, Usage usage)
    {
        if (changes.TryGetValue(usage, out var dataValue))
        {
            return dataValue.GetScaledValue(0, 1);
        }
        return null;
    }

    private static double? GetDPadDirection(Dictionary<Usage, DataValue> changes, DPadDirection directionCheck)
    {
        if (!changes.TryGetValue(Usage.GenericDesktopHatSwitch, out var dataValue))
        {
            return null;
        }

        var direction = GetDirection(dataValue);
        return direction.HasFlag(directionCheck) ? 1.0 : 0.0;
    }

    private static DPadDirection GetDirection(DataValue dataValue)
    {
        // HID hat switch uses logical values 0-8 (relative to minimum)
        int logicalMinimum = dataValue.DataItem?.LogicalMinimum ?? 0;
        int value = dataValue.GetLogicalValue() - logicalMinimum;

        return value switch
        {
            0 => DPadDirection.None,
            1 => DPadDirection.Up,
            2 => DPadDirection.Up | DPadDirection.Right,
            3 => DPadDirection.Right,
            4 => DPadDirection.Down | DPadDirection.Right,
            5 => DPadDirection.Down,
            6 => DPadDirection.Down | DPadDirection.Left,
            7 => DPadDirection.Left,
            8 => DPadDirection.Up | DPadDirection.Left,
            _ => DPadDirection.None
        };
    }
}
