namespace XOutputRedux.Core.HidHide;

/// <summary>
/// Settings for a device-isolation profile: the device(s) that must remain
/// visible while the profile is active. Every other HID gaming device is
/// hidden via HidHide for the duration of the profile.
/// </summary>
public class DeviceIsolationSettings
{
    /// <summary>
    /// Whether isolation is applied when the profile starts.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Devices to KEEP visible while the profile is active.
    /// All other HID gaming devices are hidden.
    /// </summary>
    public List<IsolationDevice> KeepDevices { get; set; } = new();

    /// <summary>
    /// Creates a deep clone of these settings.
    /// </summary>
    public DeviceIsolationSettings Clone()
    {
        return new DeviceIsolationSettings
        {
            Enabled = Enabled,
            KeepDevices = KeepDevices.Select(d => d.Clone()).ToList()
        };
    }
}

/// <summary>
/// A device referenced by a device-isolation profile.
/// </summary>
public class IsolationDevice
{
    /// <summary>
    /// Stable HidHide device identifier. This is the base container device
    /// instance path when available (hides the whole composite device),
    /// otherwise the plain device instance path — as reported by
    /// HidHideCLI --dev-gaming.
    /// </summary>
    public string DeviceInstancePath { get; set; } = "";

    /// <summary>
    /// Human-readable name for UI display (e.g. "Gudsen MOZA R12 Base").
    /// Informational only; matching uses <see cref="DeviceInstancePath"/>.
    /// </summary>
    public string? FriendlyName { get; set; }

    public IsolationDevice Clone()
    {
        return new IsolationDevice
        {
            DeviceInstancePath = DeviceInstancePath,
            FriendlyName = FriendlyName
        };
    }
}

/// <summary>
/// Serialization data for device-isolation settings.
/// </summary>
public class DeviceIsolationSettingsData
{
    public bool Enabled { get; set; } = true;
    public List<IsolationDeviceData> KeepDevices { get; set; } = new();

    public static DeviceIsolationSettingsData? FromSettings(DeviceIsolationSettings? settings)
    {
        if (settings == null) return null;
        return new DeviceIsolationSettingsData
        {
            Enabled = settings.Enabled,
            KeepDevices = settings.KeepDevices
                .Select(d => new IsolationDeviceData
                {
                    DeviceInstancePath = d.DeviceInstancePath,
                    FriendlyName = d.FriendlyName
                })
                .ToList()
        };
    }

    public DeviceIsolationSettings ToSettings()
    {
        return new DeviceIsolationSettings
        {
            Enabled = Enabled,
            KeepDevices = KeepDevices
                .Select(d => new IsolationDevice
                {
                    DeviceInstancePath = d.DeviceInstancePath,
                    FriendlyName = d.FriendlyName
                })
                .ToList()
        };
    }
}

/// <summary>
/// Serialization data for a keep-visible device.
/// </summary>
public class IsolationDeviceData
{
    public string DeviceInstancePath { get; set; } = "";
    public string? FriendlyName { get; set; }
}
