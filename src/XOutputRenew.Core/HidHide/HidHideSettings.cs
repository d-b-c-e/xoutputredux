namespace XOutputRenew.Core.HidHide;

/// <summary>
/// HidHide settings for a mapping profile.
/// </summary>
public class HidHideSettings
{
    /// <summary>
    /// Whether to hide devices when the profile starts.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// List of device instance paths to hide when the profile starts.
    /// </summary>
    public List<string> DevicesToHide { get; set; } = new();

    /// <summary>
    /// Creates a deep clone of these settings.
    /// </summary>
    public HidHideSettings Clone()
    {
        return new HidHideSettings
        {
            Enabled = Enabled,
            DevicesToHide = new List<string>(DevicesToHide)
        };
    }
}

/// <summary>
/// Serialization data for HidHide settings.
/// </summary>
public class HidHideSettingsData
{
    public bool Enabled { get; set; }
    public List<string> DevicesToHide { get; set; } = new();

    public static HidHideSettingsData? FromSettings(HidHideSettings? settings)
    {
        if (settings == null) return null;
        return new HidHideSettingsData
        {
            Enabled = settings.Enabled,
            DevicesToHide = new List<string>(settings.DevicesToHide)
        };
    }

    public HidHideSettings ToSettings()
    {
        return new HidHideSettings
        {
            Enabled = Enabled,
            DevicesToHide = new List<string>(DevicesToHide)
        };
    }
}
