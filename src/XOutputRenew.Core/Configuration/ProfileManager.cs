using System.Text.Json;

namespace XOutputRenew.Core.Configuration;

/// <summary>
/// Manages loading, saving, and organizing profiles.
/// </summary>
public class ProfileManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _profilesDirectory;

    public ProfileManager()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _profilesDirectory = Path.Combine(appData, "XOutputRenew", "profiles");
        Directory.CreateDirectory(_profilesDirectory);
    }

    /// <summary>
    /// Gets all available profile names.
    /// </summary>
    public IEnumerable<string> GetProfileNames()
    {
        if (!Directory.Exists(_profilesDirectory))
            return [];

        return Directory.GetFiles(_profilesDirectory, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name != null)
            .Cast<string>();
    }

    /// <summary>
    /// Loads a profile by name.
    /// </summary>
    public Profile? LoadProfile(string name)
    {
        var path = GetProfilePath(name);
        if (!File.Exists(path))
            return null;

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<Profile>(json, JsonOptions);
    }

    /// <summary>
    /// Saves a profile.
    /// </summary>
    public void SaveProfile(Profile profile)
    {
        var path = GetProfilePath(profile.Name);
        var json = JsonSerializer.Serialize(profile, JsonOptions);
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Duplicates an existing profile with a new name.
    /// </summary>
    public Profile? DuplicateProfile(string existingName, string newName)
    {
        var existing = LoadProfile(existingName);
        if (existing == null)
            return null;

        existing.Name = newName;
        SaveProfile(existing);
        return existing;
    }

    /// <summary>
    /// Deletes a profile.
    /// </summary>
    public bool DeleteProfile(string name)
    {
        var path = GetProfilePath(name);
        if (!File.Exists(path))
            return false;

        File.Delete(path);
        return true;
    }

    private string GetProfilePath(string name)
    {
        return Path.Combine(_profilesDirectory, $"{name}.json");
    }
}
