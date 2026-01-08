using SharpDX.DirectInput;

namespace XOutputRenew.Input.DirectInput;

/// <summary>
/// Provider for DirectInput devices.
/// Adapted from XOutput.App.Devices.Input.DirectInput.DirectInputDeviceProvider
/// </summary>
public class DirectInputDeviceProvider : IDisposable
{
    // Emulated SCP device GUID to ignore
    private const string EmulatedScpId = "028e045e-0000-0000-0000-504944564944";

    private readonly SharpDX.DirectInput.DirectInput _directInput;
    private readonly Dictionary<string, DirectInputDevice> _devices = new();
    private readonly object _lock = new();
    private bool _disposed;

    public event EventHandler<DeviceEventArgs>? DeviceConnected;
    public event EventHandler<DeviceEventArgs>? DeviceDisconnected;

    public DirectInputDeviceProvider()
    {
        _directInput = new SharpDX.DirectInput.DirectInput();
    }

    /// <summary>
    /// Refreshes the device list, discovering new devices and removing disconnected ones.
    /// </summary>
    public void RefreshDevices()
    {
        if (_disposed) return;

        lock (_lock)
        {
            var foundIds = new HashSet<string>();

            // Get all joystick/gamepad devices
            var instances = _directInput.GetDevices()
                .Where(d => d.Type == DeviceType.Joystick ||
                           d.Type == DeviceType.Gamepad ||
                           d.Type == DeviceType.FirstPerson)
                .Where(d => d.ProductGuid.ToString() != EmulatedScpId);

            foreach (var instance in instances)
            {
                try
                {
                    var device = CreateOrGetDevice(instance);
                    if (device != null)
                    {
                        foundIds.Add(device.UniqueId);
                    }
                }
                catch (Exception)
                {
                    // Failed to create device, skip it
                }
            }

            // Remove disconnected devices
            var disconnected = _devices.Keys.Except(foundIds).ToList();
            foreach (var id in disconnected)
            {
                if (_devices.TryGetValue(id, out var device))
                {
                    _devices.Remove(id);
                    device.Dispose();
                    DeviceDisconnected?.Invoke(this, new DeviceEventArgs { Device = device });
                }
            }
        }
    }

    /// <summary>
    /// Gets all currently known devices.
    /// </summary>
    public IReadOnlyList<DirectInputDevice> GetDevices()
    {
        lock (_lock)
        {
            return _devices.Values.ToList();
        }
    }

    /// <summary>
    /// Gets a device by unique ID.
    /// </summary>
    public DirectInputDevice? GetDevice(string uniqueId)
    {
        lock (_lock)
        {
            return _devices.GetValueOrDefault(uniqueId);
        }
    }

    private DirectInputDevice? CreateOrGetDevice(DeviceInstance instance)
    {
        if (!_directInput.IsDeviceAttached(instance.InstanceGuid))
            return null;

        // Create joystick to get interface path
        var joystick = new Joystick(_directInput, instance.InstanceGuid);

        try
        {
            // Check if device has any useful inputs
            if (joystick.Capabilities.AxeCount < 1 && joystick.Capabilities.ButtonCount < 1)
            {
                joystick.Dispose();
                return null;
            }

            // Get unique ID based on interface path or GUID
            string uniqueIdBase;
            string? interfacePath = null;
            string? hardwareId = null;

            if (instance.IsHumanInterfaceDevice)
            {
                interfacePath = joystick.Properties.InterfacePath;
                uniqueIdBase = interfacePath;
                hardwareId = IdHelper.GetHardwareId(interfacePath);
            }
            else
            {
                uniqueIdBase = $"{instance.ProductGuid}:{instance.InstanceGuid}";
            }

            string uniqueId = IdHelper.GetUniqueId(uniqueIdBase);

            // Check if we already have this device
            if (_devices.ContainsKey(uniqueId))
            {
                joystick.Dispose();
                return _devices[uniqueId];
            }

            // Set buffer size for event handling
            joystick.Properties.BufferSize = 128;

            var device = new DirectInputDevice(
                joystick,
                uniqueId,
                instance.ProductName,
                hardwareId,
                interfacePath
            );

            _devices[uniqueId] = device;
            DeviceConnected?.Invoke(this, new DeviceEventArgs { Device = device });

            return device;
        }
        catch
        {
            joystick.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_lock)
        {
            foreach (var device in _devices.Values)
            {
                device.Dispose();
            }
            _devices.Clear();
        }

        _directInput.Dispose();
        GC.SuppressFinalize(this);
    }
}
