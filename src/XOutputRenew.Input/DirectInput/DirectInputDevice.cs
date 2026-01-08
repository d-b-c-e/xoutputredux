using SharpDX.DirectInput;

namespace XOutputRenew.Input.DirectInput;

/// <summary>
/// DirectInput device implementation.
/// Adapted from XOutput.App.Devices.Input.DirectInput.DirectInputDevice
/// </summary>
public class DirectInputDevice : IInputDevice
{
    private const int PollingIntervalMs = 1;

    public string UniqueId { get; }
    public string Name { get; }
    public string? HardwareId { get; }
    public string? InterfacePath { get; }
    public InputMethod Method => InputMethod.DirectInput;
    public bool IsConnected => !_disposed;

    public IReadOnlyList<IInputSource> Sources => _sources;

    public event EventHandler<InputChangedEventArgs>? InputChanged;
    public event EventHandler? Disconnected;

    private readonly Joystick _joystick;
    private readonly DirectInputSource[] _sources;
    private readonly Thread? _pollThread;
    private readonly CancellationTokenSource _cts = new();

    private bool _running;
    private bool _disposed;

    public DirectInputDevice(Joystick joystick, string uniqueId, string name, string? hardwareId, string? interfacePath)
    {
        _joystick = joystick;
        UniqueId = uniqueId;
        Name = name;
        HardwareId = hardwareId;
        InterfacePath = interfacePath;

        // Enumerate sources
        var sources = new List<DirectInputSource>();

        // Buttons (up to 128)
        var buttons = joystick.GetObjects(DeviceObjectTypeFlags.Button)
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
        var sliders = joystick.GetObjects()
            .Where(o => o.ObjectType == ObjectGuid.Slider)
            .OrderBy(o => o.Usage)
            .Select((s, i) => DirectInputSource.FromSlider(s, i));
        sources.AddRange(sliders);

        // DPads
        if (joystick.Capabilities.PovCount > 0)
        {
            var dpads = Enumerable.Range(0, joystick.Capabilities.PovCount)
                .SelectMany(DirectInputSource.FromDPad);
            sources.AddRange(dpads);
        }

        _sources = sources.ToArray();

        // Acquire the joystick
        joystick.Acquire();

        // Create polling thread (not started yet)
        _pollThread = new Thread(PollLoop)
        {
            Name = $"DirectInput-{name}",
            IsBackground = true
        };
    }

    public void Start()
    {
        if (_running || _disposed) return;

        _running = true;
        _pollThread?.Start();
    }

    public void Stop()
    {
        if (!_running) return;

        _running = false;
        _cts.Cancel();
    }

    private void PollLoop()
    {
        try
        {
            while (_running && !_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var state = _joystick.GetCurrentState();
                    ProcessState(state);
                }
                catch (SharpDX.SharpDXException)
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
        var axes = _joystick.GetObjects(DeviceObjectTypeFlags.AbsoluteAxis)
            .Where(o => o.ObjectType != ObjectGuid.Slider)
            .ToArray();

        foreach (var axis in axes)
        {
            try
            {
                var properties = _joystick.GetObjectPropertiesById(axis.ObjectId);
                properties.Range = new InputRange(ushort.MinValue, ushort.MaxValue);
                properties.DeadZone = 0;
                properties.Saturation = 10000;
            }
            catch (SharpDX.SharpDXException)
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
        _cts.Dispose();

        try
        {
            _joystick.Unacquire();
            _joystick.Dispose();
        }
        catch
        {
            // Ignore disposal errors
        }

        GC.SuppressFinalize(this);
    }
}
