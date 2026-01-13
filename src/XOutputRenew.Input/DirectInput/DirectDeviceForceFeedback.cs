using SharpDX;
using SharpDX.DirectInput;

namespace XOutputRenew.Input.DirectInput;

/// <summary>
/// Manages force feedback effects for a DirectInput device.
/// Ported from XOutput.App.Devices.Input.DirectInput.DirectDeviceForceFeedback
/// </summary>
public class DirectDeviceForceFeedback : IDisposable
{
    private readonly Joystick _joystick;
    private readonly EffectInfo _effectInfo;
    private readonly int[] _axes;
    private readonly int[] _directions;
    private readonly int _gain;
    private readonly int _samplePeriod;

    private Effect? _effect;
    private double _value;
    private bool _disposed;

    /// <summary>
    /// Whether an error occurred preventing FFB from working.
    /// </summary>
    public bool HasError { get; private set; }

    /// <summary>
    /// Current force feedback value (0.0 - 1.0).
    /// </summary>
    public double Value
    {
        get => _value;
        set
        {
            if (Math.Abs(value - _value) < 0.001)
                return;

            if (!HasError)
            {
                _effect = CreateAndStartEffect(_effect, value);
            }
            _value = value;
        }
    }

    public DirectDeviceForceFeedback(
        Joystick joystick,
        EffectInfo effectInfo,
        DeviceObjectInstance actuator)
    {
        _joystick = joystick;
        _effectInfo = effectInfo;
        _gain = joystick.Properties.ForceFeedbackGain;
        _samplePeriod = joystick.Capabilities.ForceFeedbackSamplePeriod;
        _axes = new[] { (int)actuator.ObjectId };
        _directions = new[] { 0 };
    }

    /// <summary>
    /// Stops the current force feedback effect.
    /// </summary>
    public void Stop()
    {
        try
        {
            _effect?.Stop();
        }
        catch
        {
            // Ignore errors when stopping
        }
        _value = 0;
    }

    private Effect? CreateAndStartEffect(Effect? oldEffect, double value)
    {
        var effectParams = new EffectParameters
        {
            Flags = EffectFlags.Cartesian | EffectFlags.ObjectIds,
            StartDelay = 0,
            SamplePeriod = _samplePeriod,
            Duration = int.MaxValue,
            TriggerButton = -1,
            TriggerRepeatInterval = int.MaxValue,
            Gain = _gain
        };
        effectParams.SetAxes(_axes, _directions);

        var constantForce = new ConstantForce
        {
            Magnitude = CalculateMagnitude(value)
        };
        effectParams.Parameters = constantForce;

        try
        {
            var newEffect = new Effect(_joystick, _effectInfo.Guid, effectParams);
            oldEffect?.Dispose();
            newEffect.Start();
            return newEffect;
        }
        catch (SharpDXException ex)
        {
            if (ex.Message.Contains("E_NOTIMPL"))
            {
                HasError = true;
                // Log but don't throw - graceful degradation
            }
            return null;
        }
        catch
        {
            // Silently fail for other errors
            return null;
        }
    }

    private int CalculateMagnitude(double value)
    {
        // Magnitude range is typically -10000 to 10000
        // Using positive values for rumble
        return (int)(_gain * value);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _effect?.Dispose();
        }
        catch
        {
            // Ignore disposal errors
        }

        GC.SuppressFinalize(this);
    }
}
