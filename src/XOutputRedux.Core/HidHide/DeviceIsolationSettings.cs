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
    /// Match keep-list entries by HARDWARE IDENTITY (VID/PID plus the MI_/COL
    /// interface qualifier) in preference to the exact device instance path.
    ///
    /// Default true, because instance paths do not survive replugging and the
    /// common case is a fixed set of distinct controllers. With this on, a profile
    /// keeps working after devices are unplugged, moved to another port, or (for
    /// XInput pads) shuffled to a different slot.
    ///
    /// Turn it OFF when two devices share a VID/PID and must be told apart — two
    /// identical wheels, or the two interfaces an X-Arcade exposes. Identity
    /// cannot distinguish those, so identity matching would keep both. That is
    /// the safe direction (more devices visible, never fewer) but it is not
    /// always what was intended.
    /// </summary>
    public bool MatchByHardwareId { get; set; } = true;

    /// <summary>
    /// Creates a deep clone of these settings.
    /// </summary>
    public DeviceIsolationSettings Clone()
    {
        return new DeviceIsolationSettings
        {
            Enabled = Enabled,
            MatchByHardwareId = MatchByHardwareId,
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
    /// Informational only; matching uses <see cref="DeviceInstancePath"/> or
    /// <see cref="HardwareId"/>.
    /// </summary>
    public string? FriendlyName { get; set; }

    /// <summary>
    /// Stable hardware identity (e.g. <c>VID_346E&amp;PID_0006&amp;MI_02</c>),
    /// persisted so matching does not have to reconstruct it from a path that may
    /// since have gone stale.
    ///
    /// Null for devices with no VID/PID (root-enumerated virtual devices such as
    /// vJoy), which therefore always match by exact path.
    ///
    /// Populated on save, and backfilled on load for profiles written before this
    /// field existed — see <see cref="IsolationDeviceData"/>.
    /// </summary>
    public string? HardwareId { get; set; }

    /// <summary>
    /// Fills <see cref="HardwareId"/> from the stored path when it is missing.
    /// Safe to call repeatedly.
    /// </summary>
    public void EnsureHardwareId()
    {
        if (string.IsNullOrWhiteSpace(HardwareId))
        {
            HardwareId = DeviceIdentity.FromPath(DeviceInstancePath);
        }
    }

    public IsolationDevice Clone()
    {
        return new IsolationDevice
        {
            DeviceInstancePath = DeviceInstancePath,
            FriendlyName = FriendlyName,
            HardwareId = HardwareId
        };
    }
}

/// <summary>
/// Serialization data for device-isolation settings.
/// </summary>
public class DeviceIsolationSettingsData
{
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Nullable so an ABSENT value can be told apart from an explicit false.
    /// Profiles written before this field existed have no opinion, and should
    /// adopt the new identity-first default rather than being pinned to the old
    /// exact-path behaviour.
    /// </summary>
    public bool? MatchByHardwareId { get; set; }

    public List<IsolationDeviceData> KeepDevices { get; set; } = new();

    public static DeviceIsolationSettingsData? FromSettings(DeviceIsolationSettings? settings)
    {
        if (settings == null) return null;
        return new DeviceIsolationSettingsData
        {
            Enabled = settings.Enabled,
            MatchByHardwareId = settings.MatchByHardwareId,
            KeepDevices = settings.KeepDevices
                .Select(d => new IsolationDeviceData
                {
                    DeviceInstancePath = d.DeviceInstancePath,
                    FriendlyName = d.FriendlyName,
                    // Derive on save if it was never set, so a profile saved by
                    // this version always carries an identity.
                    HardwareId = d.HardwareId ?? DeviceIdentity.FromPath(d.DeviceInstancePath)
                })
                .ToList()
        };
    }

    public DeviceIsolationSettings ToSettings()
    {
        var settings = new DeviceIsolationSettings
        {
            Enabled = Enabled,
            MatchByHardwareId = MatchByHardwareId ?? true,
            KeepDevices = KeepDevices
                .Select(d => new IsolationDevice
                {
                    DeviceInstancePath = d.DeviceInstancePath,
                    FriendlyName = d.FriendlyName,
                    HardwareId = d.HardwareId
                })
                .ToList()
        };

        // Migration: profiles written before HardwareId existed carry only a
        // path. Backfill so matching never has to re-derive it later.
        foreach (var d in settings.KeepDevices) d.EnsureHardwareId();

        return settings;
    }
}

/// <summary>
/// Serialization data for a keep-visible device.
/// </summary>
public class IsolationDeviceData
{
    public string DeviceInstancePath { get; set; } = "";
    public string? FriendlyName { get; set; }

    /// <summary>
    /// Absent in profiles written before this field existed; backfilled from the
    /// path on load.
    /// </summary>
    public string? HardwareId { get; set; }
}
