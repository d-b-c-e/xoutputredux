using XOutputRenew.Core.ForceFeedback;
using XOutputRenew.Core.Mapping;
using XOutputRenew.Emulation;
using XOutputRenew.Input;
using XOutputRenew.Input.ForceFeedback;

namespace XOutputRenew.App;

/// <summary>
/// Orchestrates force feedback from ViGEm (games) to physical devices.
/// </summary>
public class ForceFeedbackService : IDisposable
{
    private readonly InputDeviceManager _deviceManager;
    private XboxController? _controller;
    private MappingProfile? _profile;
    private IForceFeedbackDevice? _targetDevice;
    private bool _disposed;

    public ForceFeedbackService(InputDeviceManager deviceManager)
    {
        _deviceManager = deviceManager;
    }

    /// <summary>
    /// Attaches to an Xbox controller and profile to route force feedback.
    /// </summary>
    public void Attach(XboxController controller, MappingProfile profile)
    {
        Detach();

        _controller = controller;
        _profile = profile;

        var settings = profile.ForceFeedbackSettings;
        if (settings?.Enabled != true)
        {
            AppLogger.Info("Force feedback disabled in profile");
            return;
        }

        if (string.IsNullOrEmpty(settings.TargetDeviceId))
        {
            AppLogger.Warning("Force feedback enabled but no target device configured");
            return;
        }

        // Find target device
        var device = _deviceManager.GetDevice(settings.TargetDeviceId);
        if (device == null)
        {
            AppLogger.Warning($"Force feedback target device not found: {settings.TargetDeviceId}");
            return;
        }

        if (device is not IForceFeedbackDevice ffbDevice || !ffbDevice.SupportsForceFeedback)
        {
            AppLogger.Warning($"Target device '{device.Name}' does not support force feedback");
            return;
        }

        _targetDevice = ffbDevice;
        _controller.ForceFeedbackReceived += OnForceFeedbackReceived;

        AppLogger.Info($"Force feedback attached to: {device.Name}");
    }

    /// <summary>
    /// Detaches from the current controller and stops force feedback.
    /// </summary>
    public void Detach()
    {
        if (_controller != null)
        {
            _controller.ForceFeedbackReceived -= OnForceFeedbackReceived;
        }

        _targetDevice?.StopForceFeedback();

        _controller = null;
        _profile = null;
        _targetDevice = null;
    }

    private void OnForceFeedbackReceived(object? sender, ForceFeedbackEventArgs e)
    {
        if (_targetDevice == null || _profile?.ForceFeedbackSettings == null)
            return;

        var settings = _profile.ForceFeedbackSettings;

        // Calculate final value based on motor mode
        double value = settings.Mode switch
        {
            ForceFeedbackMode.Large => e.LargeMotor,
            ForceFeedbackMode.Small => e.SmallMotor,
            ForceFeedbackMode.Combined => Math.Max(e.LargeMotor, e.SmallMotor),
            ForceFeedbackMode.Swap => e.SmallMotor, // Small becomes the primary
            _ => Math.Max(e.LargeMotor, e.SmallMotor)
        };

        // Apply gain multiplier
        value = Math.Clamp(value * settings.Gain, 0.0, 1.0);

        _targetDevice.SendForceFeedback(value);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Detach();
        GC.SuppressFinalize(this);
    }
}
