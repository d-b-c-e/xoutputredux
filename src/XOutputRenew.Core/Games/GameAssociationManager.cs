using System.Text.Json;

namespace XOutputRenew.Core.Games;

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
        return Path.Combine(appData, "XOutputRenew", "games.json");
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
            var games = JsonSerializer.Deserialize<List<GameAssociation>>(json, JsonOptions);
            if (games != null)
            {
                _games.AddRange(games);
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

            var json = JsonSerializer.Serialize(_games, JsonOptions);
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
