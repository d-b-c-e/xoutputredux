using System.IO;
using System.Text.Json;

namespace XOutputRenew.App;

/// <summary>
/// Stores user-defined device settings (friendly names, etc).
/// </summary>
public class DeviceSettings
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "XOutputRenew",
        "device-settings.json");

    private Dictionary<string, DeviceSettingsEntry> _entries = new();

    /// <summary>
    /// Gets the friendly name for a device.
    /// </summary>
    public string? GetFriendlyName(string uniqueId)
    {
        return _entries.TryGetValue(uniqueId, out var entry) ? entry.FriendlyName : null;
    }

    /// <summary>
    /// Sets the friendly name for a device.
    /// </summary>
    public void SetFriendlyName(string uniqueId, string? friendlyName)
    {
        if (string.IsNullOrEmpty(friendlyName))
        {
            _entries.Remove(uniqueId);
        }
        else
        {
            if (!_entries.TryGetValue(uniqueId, out var entry))
            {
                entry = new DeviceSettingsEntry();
                _entries[uniqueId] = entry;
            }
            entry.FriendlyName = friendlyName;
        }
        Save();
    }

    /// <summary>
    /// Loads settings from disk.
    /// </summary>
    public void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                _entries = JsonSerializer.Deserialize<Dictionary<string, DeviceSettingsEntry>>(json) ?? new();
            }
        }
        catch
        {
            _entries = new();
        }
    }

    /// <summary>
    /// Saves settings to disk.
    /// </summary>
    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }
}

/// <summary>
/// Settings for a single device.
/// </summary>
public class DeviceSettingsEntry
{
    public string? FriendlyName { get; set; }
}
