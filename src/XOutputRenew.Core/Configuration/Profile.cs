namespace XOutputRenew.Core.Configuration;

/// <summary>
/// Represents a mapping profile that defines how input devices map to Xbox controller outputs.
/// </summary>
public class Profile
{
    /// <summary>
    /// Unique name for the profile.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Optional description of the profile.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// List of input devices used by this profile.
    /// </summary>
    public List<DeviceReference> InputDevices { get; set; } = [];

    /// <summary>
    /// Button mappings - key is Xbox button name, value is list of input sources that trigger it.
    /// </summary>
    public Dictionary<string, List<InputBinding>> ButtonMappings { get; set; } = [];

    /// <summary>
    /// Axis mappings - key is Xbox axis name, value is the axis configuration.
    /// </summary>
    public Dictionary<string, AxisMapping> AxisMappings { get; set; } = [];

    /// <summary>
    /// HidHide configuration for this profile.
    /// </summary>
    public HidHideConfig? HidHide { get; set; }
}

/// <summary>
/// Reference to an input device.
/// </summary>
public class DeviceReference
{
    /// <summary>
    /// Unique identifier for the device (SHA256 hash of device path).
    /// </summary>
    public required string UniqueId { get; set; }

    /// <summary>
    /// Friendly name for display.
    /// </summary>
    public string? FriendlyName { get; set; }

    /// <summary>
    /// Input method (DirectInput, RawInput).
    /// </summary>
    public required string InputMethod { get; set; }
}

/// <summary>
/// Binding of an input source to an Xbox button.
/// </summary>
public class InputBinding
{
    /// <summary>
    /// Device unique ID.
    /// </summary>
    public required string DeviceId { get; set; }

    /// <summary>
    /// Source index on the device.
    /// </summary>
    public required int SourceIndex { get; set; }

    /// <summary>
    /// Friendly name for the source.
    /// </summary>
    public string? SourceName { get; set; }
}

/// <summary>
/// Axis mapping configuration.
/// </summary>
public class AxisMapping
{
    /// <summary>
    /// Device unique ID.
    /// </summary>
    public required string DeviceId { get; set; }

    /// <summary>
    /// Source index on the device.
    /// </summary>
    public required int SourceIndex { get; set; }

    /// <summary>
    /// Friendly name for the source.
    /// </summary>
    public string? SourceName { get; set; }

    /// <summary>
    /// Whether to invert the axis.
    /// </summary>
    public bool Invert { get; set; }

    /// <summary>
    /// Deadzone value (0.0 - 1.0).
    /// </summary>
    public double Deadzone { get; set; }
}

/// <summary>
/// HidHide configuration.
/// </summary>
public class HidHideConfig
{
    /// <summary>
    /// Whether to hide devices when profile starts.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Hardware IDs of devices to hide.
    /// </summary>
    public List<string> DeviceIds { get; set; } = [];
}
