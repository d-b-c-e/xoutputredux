using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XOutputRedux.App;

/// <summary>
/// Wrapper for device-settings.json with schema versioning.
/// </summary>
public class DeviceSettingsData
{
    /// <summary>
    /// Current schema version. Increment when making breaking changes.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// Schema version of this data. Used for migration.
    /// </summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>
    /// Device settings entries keyed by device unique ID.
    /// </summary>
    public Dictionary<string, DeviceSettingsEntry> Entries { get; set; } = new();

    /// <summary>
    /// Whether this data needs migration.
    /// </summary>
    [JsonIgnore]
    public bool NeedsMigration => SchemaVersion < CurrentSchemaVersion;

    /// <summary>
    /// Migrates the data to the current schema version.
    /// </summary>
    public void Migrate()
    {
        if (SchemaVersion < 1)
        {
            SchemaVersion = 1;
        }

        // Future migrations go here

        SchemaVersion = CurrentSchemaVersion;
    }
}

/// <summary>
/// Stores user-defined device settings (friendly names, etc).
/// </summary>
public class DeviceSettings
{
    private static string SettingsPath => AppPaths.DeviceSettings;

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
                DeviceSettingsData? data = null;
                bool needsResave = false;

                // Try new format first
                try
                {
                    data = JsonSerializer.Deserialize<DeviceSettingsData>(json);
                    if (data != null && json.Contains("\"schemaVersion\"", StringComparison.OrdinalIgnoreCase))
                    {
                        if (data.NeedsMigration)
                        {
                            data.Migrate();
                            needsResave = true;
                        }
                    }
                    else
                    {
                        data = null;
                    }
                }
                catch
                {
                    data = null;
                }

                // Fallback: legacy format (raw dictionary)
                if (data == null)
                {
                    var legacyEntries = JsonSerializer.Deserialize<Dictionary<string, DeviceSettingsEntry>>(json);
                    if (legacyEntries != null)
                    {
                        data = new DeviceSettingsData { Entries = legacyEntries };
                        needsResave = true;
                    }
                }

                if (data != null)
                {
                    _entries = data.Entries;
                    if (needsResave)
                    {
                        Save();
                    }
                }
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

            var data = new DeviceSettingsData { Entries = _entries };
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
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
