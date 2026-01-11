using System.Text.Json;

namespace XOutputRenew.Core.Mapping;

/// <summary>
/// Manages loading, saving, and organizing mapping profiles.
/// </summary>
public class ProfileManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _profilesDirectory;
    private readonly Dictionary<string, MappingProfile> _profiles = new();

    /// <summary>
    /// All loaded profiles.
    /// </summary>
    public IReadOnlyDictionary<string, MappingProfile> Profiles => _profiles;

    /// <summary>
    /// Event raised when profiles are loaded or changed.
    /// </summary>
    public event EventHandler? ProfilesChanged;

    public ProfileManager(string profilesDirectory)
    {
        _profilesDirectory = profilesDirectory;
    }

    /// <summary>
    /// Ensures the profiles directory exists.
    /// </summary>
    public void EnsureDirectoryExists()
    {
        if (!Directory.Exists(_profilesDirectory))
        {
            Directory.CreateDirectory(_profilesDirectory);
        }
    }

    /// <summary>
    /// Loads all profiles from the profiles directory.
    /// </summary>
    public void LoadProfiles()
    {
        EnsureDirectoryExists();
        _profiles.Clear();

        foreach (var file in Directory.GetFiles(_profilesDirectory, "*.json"))
        {
            try
            {
                var profile = LoadProfile(file);
                if (profile != null)
                {
                    string key = Path.GetFileNameWithoutExtension(file);
                    _profiles[key] = profile;
                }
            }
            catch
            {
                // Skip invalid profile files
            }
        }

        ProfilesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Loads a single profile from a file.
    /// </summary>
    public MappingProfile? LoadProfile(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        string json = File.ReadAllText(filePath);
        var data = JsonSerializer.Deserialize<MappingProfileData>(json, JsonOptions);
        return data?.ToProfile();
    }

    /// <summary>
    /// Saves a profile to the profiles directory.
    /// </summary>
    /// <param name="name">The filename (without extension) to save as.</param>
    /// <param name="profile">The profile to save.</param>
    public void SaveProfile(string name, MappingProfile profile)
    {
        EnsureDirectoryExists();

        string filePath = Path.Combine(_profilesDirectory, $"{name}.json");
        var data = MappingProfileData.FromProfile(profile);
        string json = JsonSerializer.Serialize(data, JsonOptions);
        File.WriteAllText(filePath, json);

        _profiles[name] = profile;
        ProfilesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Deletes a profile.
    /// </summary>
    public bool DeleteProfile(string name)
    {
        string filePath = Path.Combine(_profilesDirectory, $"{name}.json");

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        bool removed = _profiles.Remove(name);
        if (removed)
        {
            ProfilesChanged?.Invoke(this, EventArgs.Empty);
        }
        return removed;
    }

    /// <summary>
    /// Duplicates an existing profile with a new name.
    /// </summary>
    public MappingProfile? DuplicateProfile(string sourceName, string newName)
    {
        if (!_profiles.TryGetValue(sourceName, out var source))
            return null;

        var duplicate = source.Clone();
        duplicate.Name = newName;
        SaveProfile(newName, duplicate);
        return duplicate;
    }

    /// <summary>
    /// Renames a profile.
    /// </summary>
    /// <param name="oldName">Current profile key (filename without extension).</param>
    /// <param name="newName">New name for the profile.</param>
    /// <param name="error">Error message if rename fails.</param>
    /// <param name="overwrite">If true, overwrite existing profile with same name.</param>
    /// <returns>True if rename succeeded.</returns>
    public bool RenameProfile(string oldName, string newName, out string? error, bool overwrite = false)
    {
        error = null;

        if (!_profiles.TryGetValue(oldName, out var profile))
        {
            error = $"Profile key '{oldName}' not found. Available keys: {string.Join(", ", _profiles.Keys)}";
            return false;
        }

        if (_profiles.ContainsKey(newName) && !overwrite)
        {
            error = "PROFILE_EXISTS";
            return false;
        }

        string oldPath = Path.Combine(_profilesDirectory, $"{oldName}.json");
        string newPath = Path.Combine(_profilesDirectory, $"{newName}.json");

        // Update the profile name
        profile.Name = newName;

        // Save with new name and updated content
        var data = MappingProfileData.FromProfile(profile);
        string json = JsonSerializer.Serialize(data, JsonOptions);
        File.WriteAllText(newPath, json);

        // Delete old file (if different from new)
        if (oldPath != newPath && File.Exists(oldPath))
        {
            File.Delete(oldPath);
        }

        // Update dictionary
        if (oldName != newName)
        {
            _profiles.Remove(oldName);
        }
        _profiles[newName] = profile;

        ProfilesChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>
    /// Creates a new empty profile.
    /// </summary>
    public MappingProfile CreateProfile(string name)
    {
        var profile = new MappingProfile { Name = name };
        SaveProfile(name, profile);
        return profile;
    }

    /// <summary>
    /// Gets a profile by name, or null if not found.
    /// </summary>
    public MappingProfile? GetProfile(string name)
    {
        return _profiles.TryGetValue(name, out var profile) ? profile : null;
    }

    /// <summary>
    /// Gets the default profiles directory path.
    /// </summary>
    public static string GetDefaultProfilesDirectory()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "XOutputRenew", "Profiles");
    }
}
