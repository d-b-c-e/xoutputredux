using System.Text.Json;
using System.Text.Json.Serialization;

namespace XOutputRedux.Core.Games;

/// <summary>
/// Wrapper for games.json with schema versioning.
/// </summary>
public class GamesData
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
    /// List of game associations.
    /// </summary>
    public List<GameAssociation> Games { get; set; } = new();

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
        // Migration from version 0 (no version/raw list) to version 1
        if (SchemaVersion < 1)
        {
            SchemaVersion = 1;
        }

        // Future migrations go here

        SchemaVersion = CurrentSchemaVersion;
    }
}

/// <summary>
/// Manages game-profile associations, including persistence to disk.
/// </summary>
public class GameAssociationManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly List<GameAssociation> _games = new();

    /// <summary>
    /// Gets the list of game associations.
    /// </summary>
    public IReadOnlyList<GameAssociation> Games => _games;

    /// <summary>
    /// Creates a new manager with the specified storage path.
    /// </summary>
    public GameAssociationManager(string filePath)
    {
        _filePath = filePath;
    }

    /// <summary>
    /// Gets the default storage path for games.json.
    /// </summary>
    public static string GetDefaultFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "XOutputRedux", "games.json");
    }

    /// <summary>
    /// Loads game associations from disk.
    /// </summary>
    public void Load()
    {
        _games.Clear();

        if (!File.Exists(_filePath))
            return;

        try
        {
            var json = File.ReadAllText(_filePath);

            // Try to load as new format (GamesData with schema version)
            GamesData? data = null;
            bool needsResave = false;

            try
            {
                data = JsonSerializer.Deserialize<GamesData>(json, JsonOptions);

                // Check if it's actually the new format (has SchemaVersion property)
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
                    // Old format - try to parse as raw list
                    data = null;
                }
            }
            catch
            {
                data = null;
            }

            // Fallback: try legacy format (raw list)
            if (data == null)
            {
                var legacyGames = JsonSerializer.Deserialize<List<GameAssociation>>(json, JsonOptions);
                if (legacyGames != null)
                {
                    data = new GamesData { Games = legacyGames };
                    needsResave = true; // Convert to new format
                }
            }

            if (data != null)
            {
                _games.AddRange(data.Games);

                // Re-save if migrated or converted from legacy format
                if (needsResave)
                {
                    Save();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load games: {ex.Message}");
        }
    }

    /// <summary>
    /// Saves game associations to disk.
    /// </summary>
    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var data = new GamesData { Games = _games.ToList() };
            var json = JsonSerializer.Serialize(data, JsonOptions);
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save games: {ex.Message}");
        }
    }

    /// <summary>
    /// Adds a new game association.
    /// </summary>
    public void Add(GameAssociation game)
    {
        _games.Add(game);
        Save();
    }

    /// <summary>
    /// Removes a game association by ID.
    /// </summary>
    public bool Remove(string id)
    {
        var game = _games.FirstOrDefault(g => g.Id == id);
        if (game != null)
        {
            _games.Remove(game);
            Save();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Updates an existing game association.
    /// </summary>
    public bool Update(GameAssociation game)
    {
        var index = _games.FindIndex(g => g.Id == game.Id);
        if (index >= 0)
        {
            _games[index] = game;
            Save();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets a game association by ID.
    /// </summary>
    public GameAssociation? GetById(string id)
    {
        return _games.FirstOrDefault(g => g.Id == id);
    }

    /// <summary>
    /// Gets a game association by executable path.
    /// </summary>
    public GameAssociation? GetByExecutablePath(string path)
    {
        return _games.FirstOrDefault(g =>
            g.ExecutablePath.Equals(path, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets all game associations for a specific profile.
    /// </summary>
    public IEnumerable<GameAssociation> GetByProfile(string profileName)
    {
        return _games.Where(g =>
            g.ProfileName?.Equals(profileName, StringComparison.OrdinalIgnoreCase) == true);
    }
}
