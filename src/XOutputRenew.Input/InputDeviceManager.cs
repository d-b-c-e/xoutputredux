namespace XOutputRenew.Input;

/// <summary>
/// Manages discovery and lifecycle of input devices.
/// </summary>
public class InputDeviceManager : IDisposable
{
    private readonly List<IInputDevice> _devices = [];
    private readonly object _lock = new();
    private bool _disposed;

    /// <summary>
    /// All currently known devices.
    /// </summary>
    public IReadOnlyList<IInputDevice> Devices
    {
        get
        {
            lock (_lock)
            {
                return _devices.ToList();
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

    /// <summary>
    /// Refreshes the list of available devices.
    /// </summary>
    public void RefreshDevices()
    {
        // TODO: Implement device discovery using DirectInput and RawInput providers
    }

    /// <summary>
    /// Gets a device by its unique ID.
    /// </summary>
    public IInputDevice? GetDevice(string uniqueId)
    {
        lock (_lock)
        {
            return _devices.FirstOrDefault(d => d.UniqueId == uniqueId);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_lock)
        {
            foreach (var device in _devices)
            {
                device.Dispose();
            }
            _devices.Clear();
        }

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
