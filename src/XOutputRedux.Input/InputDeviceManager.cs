using XOutputRedux.Input.DirectInput;
using XOutputRedux.Input.RawInput;

namespace XOutputRedux.Input;

/// <summary>
/// Manages discovery and lifecycle of input devices.
/// </summary>
public class InputDeviceManager : IDisposable
{
    private readonly DirectInputDeviceProvider _directInputProvider;
    private readonly RawInputDeviceProvider _rawInputProvider;
    private readonly Timer _refreshTimer;
    private readonly object _lock = new();
    private bool _disposed;

    /// <summary>
    /// All currently known devices (de-duplicated by HardwareId, preferring DirectInput).
    /// </summary>
    public IReadOnlyList<IInputDevice> Devices
    {
        get
        {
            lock (_lock)
            {
                var devices = new List<IInputDevice>();
                var seenHardwareIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Add DirectInput devices first (preferred)
                foreach (var device in _directInputProvider.GetDevices())
                {
                    devices.Add(device);
                    if (!string.IsNullOrEmpty(device.HardwareId))
                    {
                        seenHardwareIds.Add(device.HardwareId);
                    }
                }

                // Add RawInput devices only if not already seen
                foreach (var device in _rawInputProvider.GetDevices())
                {
                    if (string.IsNullOrEmpty(device.HardwareId) || !seenHardwareIds.Contains(device.HardwareId))
                    {
                        devices.Add(device);
                        if (!string.IsNullOrEmpty(device.HardwareId))
                        {
                            seenHardwareIds.Add(device.HardwareId);
                        }
                    }
                }

                return devices;
            }
        }
    }

    /// <summary>
    /// Event raised when a device is connected.
    /// </summary>
    public event EventHandler<DeviceEventArgs>? DeviceConnected;

    /// <summary>
    /// Event raised when a device is disconnected.
    /// </summary>
    public event EventHandler<DeviceEventArgs>? DeviceDisconnected;

    public InputDeviceManager()
    {
        _directInputProvider = new DirectInputDeviceProvider();
        _directInputProvider.DeviceConnected += OnDeviceConnected;
        _directInputProvider.DeviceDisconnected += OnDeviceDisconnected;

        _rawInputProvider = new RawInputDeviceProvider();
        _rawInputProvider.DeviceConnected += OnDeviceConnected;
        _rawInputProvider.DeviceDisconnected += OnDeviceDisconnected;

        // Refresh every 5 seconds
        _refreshTimer = new Timer(_ => RefreshDevices(), null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// Sets the window handle used for DirectInput exclusive cooperative level (required for FFB).
    /// </summary>
    public void SetWindowHandle(IntPtr handle)
    {
        _directInputProvider.SetWindowHandle(handle);
    }

    /// <summary>
    /// Manually triggers a device refresh.
    /// </summary>
    public void RefreshDevices()
    {
        if (_disposed) return;

        lock (_lock)
        {
            _directInputProvider.RefreshDevices();
            _rawInputProvider.RefreshDevices();
        }
    }

    /// <summary>
    /// Disposes and recreates all DirectInput devices.
    /// Used after hardware state changes (e.g., Moza wheel config)
    /// to get fresh device handles with correct FFB state.
    /// </summary>
    public void RecreateDirectInputDevices()
    {
        lock (_lock)
        {
            _directInputProvider.RecreateDevices();
        }
    }

    /// <summary>
    /// Gets a device by its unique ID.
    /// </summary>
    public IInputDevice? GetDevice(string uniqueId)
    {
        lock (_lock)
        {
            return _directInputProvider.GetDevice(uniqueId)
                ?? (IInputDevice?)_rawInputProvider.GetDevice(uniqueId);
        }
    }

    /// <summary>
    /// Starts polling a device.
    /// </summary>
    public void StartDevice(IInputDevice device)
    {
        device.Start();
    }

    /// <summary>
    /// Stops polling a device.
    /// </summary>
    public void StopDevice(IInputDevice device)
    {
        device.Stop();
    }

    private void OnDeviceConnected(object? sender, DeviceEventArgs e)
    {
        DeviceConnected?.Invoke(this, e);
    }

    private void OnDeviceDisconnected(object? sender, DeviceEventArgs e)
    {
        DeviceDisconnected?.Invoke(this, e);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _refreshTimer.Dispose();

        _directInputProvider.DeviceConnected -= OnDeviceConnected;
        _directInputProvider.DeviceDisconnected -= OnDeviceDisconnected;
        _directInputProvider.Dispose();

        _rawInputProvider.DeviceConnected -= OnDeviceConnected;
        _rawInputProvider.DeviceDisconnected -= OnDeviceDisconnected;
        _rawInputProvider.Dispose();

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Event args for device events.
/// </summary>
public class DeviceEventArgs : EventArgs
{
    public required IInputDevice Device { get; init; }
}
