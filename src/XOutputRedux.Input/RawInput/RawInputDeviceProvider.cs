using XOutputRedux.HidSharper;

namespace XOutputRedux.Input.RawInput;

/// <summary>
/// Provider for RawInput/HID devices using HidSharper.
/// Adapted from XOutput.App.Devices.Input.RawInput.RawInputDeviceProvider
/// </summary>
public class RawInputDeviceProvider : IDisposable
{
    // Gaming device usage pages
    private const int GenericDesktopPage = 0x01;
    private const int GameControlsPage = 0x05;

    // Gaming device usages
    private const int GamepadUsage = 0x05;
    private const int JoystickUsage = 0x04;
    private const int MultiAxisControllerUsage = 0x08;

    private readonly Dictionary<string, RawInputDevice> _devices = new();
    private readonly object _lock = new();
    private bool _disposed;

    public event EventHandler<DeviceEventArgs>? DeviceConnected;
    public event EventHandler<DeviceEventArgs>? DeviceDisconnected;

    /// <summary>
    /// Refreshes the device list, discovering new devices and removing disconnected ones.
    /// </summary>
    public void RefreshDevices()
    {
        if (_disposed) return;

        lock (_lock)
        {
            var foundIds = new HashSet<string>();

            // Get all HID devices that look like game controllers
            var hidDevices = DeviceList.Local.GetHidDevices()
                .Where(IsGamingDevice)
                .ToList();

            foreach (var hidDevice in hidDevices)
            {
                try
                {
                    var device = CreateOrGetDevice(hidDevice);
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
    public IReadOnlyList<RawInputDevice> GetDevices()
    {
        lock (_lock)
        {
            return _devices.Values.ToList();
        }
    }

    /// <summary>
    /// Gets a device by unique ID.
    /// </summary>
    public RawInputDevice? GetDevice(string uniqueId)
    {
        lock (_lock)
        {
            return _devices.GetValueOrDefault(uniqueId);
        }
    }

    private static bool IsGamingDevice(HidDevice device)
    {
        try
        {
            // Check primary usage
            var usage = device.GetReportDescriptor().DeviceItems
                .FirstOrDefault()?.Usages.GetAllValues().FirstOrDefault();

            if (usage == null) return false;

            uint usageValue = usage.Value;
            int usagePage = (int)(usageValue >> 16);
            int usageId = (int)(usageValue & 0xFFFF);

            // Check for gaming device usages
            if (usagePage == GenericDesktopPage)
            {
                return usageId == GamepadUsage ||
                       usageId == JoystickUsage ||
                       usageId == MultiAxisControllerUsage;
            }

            if (usagePage == GameControlsPage)
            {
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private RawInputDevice? CreateOrGetDevice(HidDevice hidDevice)
    {
        // Get unique ID based on VID/PID (hardware ID) for port-independent identification
        string? hardwareId = IdHelper.GetHardwareId(hidDevice.DevicePath);
        // Use hardware ID (VID/PID) for stable identification across USB ports
        string uniqueIdBase = hardwareId ?? hidDevice.DevicePath;
        string uniqueId = IdHelper.GetUniqueId(uniqueIdBase);

        // Check if we already have this device
        if (_devices.TryGetValue(uniqueId, out var existingDevice))
        {
            return existingDevice;
        }

        // Try to open the device
        if (!hidDevice.TryOpen(out var hidStream))
        {
            return null;
        }

        try
        {
            var reportDescriptor = hidDevice.GetReportDescriptor();
            var deviceItem = reportDescriptor.DeviceItems.FirstOrDefault();

            if (deviceItem == null)
            {
                hidStream!.Dispose();
                return null;
            }

            var device = new RawInputDevice(
                hidDevice,
                hidStream!,
                reportDescriptor,
                deviceItem,
                uniqueId,
                hardwareId
            );

            _devices[uniqueId] = device;
            DeviceConnected?.Invoke(this, new DeviceEventArgs { Device = device });

            return device;
        }
        catch
        {
            hidStream!.Dispose();
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

        GC.SuppressFinalize(this);
    }
}
