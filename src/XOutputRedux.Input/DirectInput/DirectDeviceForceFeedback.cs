using Vortice.DirectInput;

namespace XOutputRedux.Input.DirectInput;

/// <summary>
/// Manages force feedback effects for a DirectInput device.
/// Ported from XOutput.App.Devices.Input.DirectInput.DirectDeviceForceFeedback
/// </summary>
public class DirectDeviceForceFeedback : IDisposable
{
    private readonly IDirectInputDevice8 _device;
    private readonly EffectInfo _effectInfo;
    private readonly int[] _axes;
    private readonly int[] _directions;
    private readonly int _gain;
    private readonly int _samplePeriod;

    private IDirectInputEffect? _effect;
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
        IDirectInputDevice8 device,
        EffectInfo effectInfo,
        DeviceObjectInstance actuator)
    {
        _device = device;
        _effectInfo = effectInfo;
        _gain = device.Properties.ForceFeedbackGain;
        _samplePeriod = device.Capabilities.ForceFeedbackSamplePeriod;
        _axes = [(int)actuator.ObjectId];
        _directions = [0];
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

    private IDirectInputEffect? CreateAndStartEffect(IDirectInputEffect? oldEffect, double value)
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
            var newEffect = _device.CreateEffect(_effectInfo.Guid, effectParams);
            oldEffect?.Dispose();
            newEffect.Start();
            return newEffect;
        }
        catch (SharpGen.Runtime.SharpGenException ex)
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
