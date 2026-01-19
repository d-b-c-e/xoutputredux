using Vortice.DirectInput;
using XOutputRedux.Input.ForceFeedback;

namespace XOutputRedux.Input.DirectInput;

/// <summary>
/// DirectInput device implementation.
/// Adapted from XOutput.App.Devices.Input.DirectInput.DirectInputDevice
/// </summary>
public class DirectInputDevice : IInputDevice, IForceFeedbackDevice
{
    private const int PollingIntervalMs = 1;
    private const int FfbIntervalMs = 100; // 10 Hz update rate for force feedback

    public string UniqueId { get; }
    public string Name { get; }
    public string? HardwareId { get; }
    public string? InterfacePath { get; }
    public InputMethod Method => InputMethod.DirectInput;
    public bool IsConnected => !_disposed;

    public IReadOnlyList<IInputSource> Sources => _sources;

    // IForceFeedbackDevice implementation
    public bool SupportsForceFeedback => _ffbTargets.Length > 0;
    public IReadOnlyList<ForceFeedbackTarget> ForceFeedbackTargets => _ffbTargets;

    public event EventHandler<InputChangedEventArgs>? InputChanged;
    public event EventHandler? Disconnected;

    private readonly IDirectInputDevice8 _device;
    private readonly DirectInputSource[] _sources;

    // Force feedback fields
    private readonly ForceFeedbackTarget[] _ffbTargets;
    private readonly Dictionary<ForceFeedbackTarget, DirectDeviceForceFeedback> _ffbEffects;
    private readonly EffectInfo? _effectInfo;
    private Thread? _ffbThread;
    private CancellationTokenSource? _ffbCts;
    private double _pendingFfbValue;
    private readonly object _ffbLock = new();

    private Thread? _pollThread;
    private CancellationTokenSource? _cts;
    private bool _running;
    private bool _disposed;

    public DirectInputDevice(
        IDirectInputDevice8 device,
        string uniqueId,
        string name,
        string? hardwareId,
        string? interfacePath,
        bool hasForceFeedback = false,
        IntPtr windowHandle = default)
    {
        _device = device;
        UniqueId = uniqueId;
        Name = name;
        HardwareId = hardwareId;
        InterfacePath = interfacePath;

        // Initialize FFB collections (will be populated if FFB is available)
        _ffbTargets = Array.Empty<ForceFeedbackTarget>();
        _ffbEffects = new Dictionary<ForceFeedbackTarget, DirectDeviceForceFeedback>();

        // Set up force feedback if available
        if (hasForceFeedback && windowHandle != IntPtr.Zero)
        {
            try
            {
                // Set exclusive cooperative level (required for FFB output)
                device.SetCooperativeLevel(windowHandle,
                    CooperativeLevel.Background | CooperativeLevel.Exclusive);

                // Find ConstantForce effect (preferred) or any effect
                var effects = device.GetEffects().ToArray();
                _effectInfo = effects.FirstOrDefault(e => e.Guid == EffectGuid.ConstantForce)
                              ?? effects.FirstOrDefault();

                if (_effectInfo != null)
                {
                    // Get FFB actuator axes
                    var actuators = device.GetObjects()
                        .Where(obj => obj.ObjectId.Flags.HasFlag(DeviceObjectTypeFlags.ForceFeedbackActuator))
                        .ToArray();

                    if (actuators.Length > 0)
                    {
                        _ffbTargets = actuators
                            .Select(a => new ForceFeedbackTarget(a.Name, a.Offset))
                            .ToArray();

                        foreach (var target in _ffbTargets)
                        {
                            var actuator = actuators.First(a => a.Offset == target.Offset);
                            _ffbEffects[target] = new DirectDeviceForceFeedback(device, _effectInfo, actuator);
                        }
                    }
                }
            }
            catch
            {
                // FFB initialization failed - continue without FFB
                _ffbTargets = Array.Empty<ForceFeedbackTarget>();
                _ffbEffects.Clear();
            }
        }

        // Enumerate input sources
        var sources = new List<DirectInputSource>();

        // Buttons (up to 128)
        var buttons = device.GetObjects(DeviceObjectTypeFlags.Button)
            .Where(b => b.Usage > 0)
            .OrderBy(b => b.ObjectId.InstanceNumber)
            .Take(128)
            .Select((b, i) => DirectInputSource.FromButton(b, i));
        sources.AddRange(buttons);

        // Axes (up to 24)
        var axes = GetAxes()
            .OrderBy(a => a.Usage)
            .Take(24)
            .Select(DirectInputSource.FromAxis);
        sources.AddRange(axes);

        // Sliders
        var sliders = device.GetObjects()
            .Where(o => o.ObjectType == ObjectGuid.Slider)
            .OrderBy(o => o.Usage)
            .Select((s, i) => DirectInputSource.FromSlider(s, i));
        sources.AddRange(sliders);

        // DPads
        if (device.Capabilities.PovCount > 0)
        {
            var dpads = Enumerable.Range(0, device.Capabilities.PovCount)
                .SelectMany(DirectInputSource.FromDPad);
            sources.AddRange(dpads);
        }

        _sources = sources.ToArray();

        // Acquire the device
        device.Acquire();
    }

    public void Start()
    {
        if (_running || _disposed) return;

        _running = true;
        _cts = new CancellationTokenSource();
        _pollThread = new Thread(PollLoop)
        {
            Name = $"DirectInput-{Name}",
            IsBackground = true
        };
        _pollThread.Start();

        // Start FFB thread if supported
        if (SupportsForceFeedback)
        {
            _ffbCts = new CancellationTokenSource();
            _ffbThread = new Thread(ForceFeedbackLoop)
            {
                Name = $"DirectInput-FFB-{Name}",
                IsBackground = true
            };
            _ffbThread.Start();
        }
    }

    public void Stop()
    {
        if (!_running) return;

        _running = false;
        _cts?.Cancel();
        _ffbCts?.Cancel();

        // Wait for threads to finish (with timeout)
        _pollThread?.Join(500);
        _ffbThread?.Join(500);

        _cts?.Dispose();
        _ffbCts?.Dispose();
        _cts = null;
        _ffbCts = null;
        _pollThread = null;
        _ffbThread = null;

        StopForceFeedback();
    }

    // IForceFeedbackDevice implementation
    public void SendForceFeedback(double value)
    {
        if (!SupportsForceFeedback) return;

        lock (_ffbLock)
        {
            _pendingFfbValue = Math.Clamp(value, 0.0, 1.0);
        }
    }

    public void StopForceFeedback()
    {
        if (!SupportsForceFeedback) return;

        lock (_ffbLock)
        {
            _pendingFfbValue = 0;
        }

        foreach (var ffb in _ffbEffects.Values)
        {
            ffb.Stop();
        }
    }

    private void ForceFeedbackLoop()
    {
        try
        {
            while (_running && !_ffbCts!.Token.IsCancellationRequested)
            {
                double value;
                lock (_ffbLock)
                {
                    value = _pendingFfbValue;
                }

                foreach (var target in _ffbTargets)
                {
                    target.Value = value;
                    if (_ffbEffects.TryGetValue(target, out var ffb))
                    {
                        ffb.Value = value;
                    }
                }

                Thread.Sleep(FfbIntervalMs);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
    }

    private void PollLoop()
    {
        try
        {
            while (_running && _cts != null && !_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var state = new JoystickState();
                    _device.GetCurrentJoystickState(ref state);
                    ProcessState(state);
                }
                catch (SharpGen.Runtime.SharpGenException)
                {
                    // Device disconnected
                    _running = false;
                    Disconnected?.Invoke(this, EventArgs.Empty);
                    break;
                }

                Thread.Sleep(PollingIntervalMs);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
    }

    private void ProcessState(JoystickState state)
    {
        foreach (var source in _sources)
        {
            double oldValue = source.Value;
            if (source.Refresh(state))
            {
                InputChanged?.Invoke(this, new InputChangedEventArgs
                {
                    Source = source,
                    OldValue = oldValue,
                    NewValue = source.Value
                });
            }
        }
    }

    private IEnumerable<DeviceObjectInstance> GetAxes()
    {
        var axes = _device.GetObjects(DeviceObjectTypeFlags.AbsoluteAxis)
            .Where(o => o.ObjectType != ObjectGuid.Slider)
            .ToArray();

        foreach (var axis in axes)
        {
            try
            {
                var properties = _device.GetObjectPropertiesById(axis.ObjectId);
                properties.Range = new InputRange(ushort.MinValue, ushort.MaxValue);
                properties.DeadZone = 0;
                properties.Saturation = 10000;
            }
            catch (SharpGen.Runtime.SharpGenException)
            {
                // Some axes don't support property modification
            }
        }

        return axes;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();

        // Dispose FFB effects
        foreach (var ffb in _ffbEffects.Values)
        {
            try
            {
                ffb.Dispose();
            }
            catch
            {
                // Ignore disposal errors
            }
        }

        try
        {
            _device.Unacquire();
            _device.Dispose();
        }
        catch
        {
            // Ignore disposal errors
        }

        GC.SuppressFinalize(this);
    }
}
