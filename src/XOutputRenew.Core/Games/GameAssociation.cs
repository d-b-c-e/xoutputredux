namespace XOutputRenew.Core.Games;

/// <summary>
/// Represents an association between a game executable and a controller profile.
/// </summary>
public class GameAssociation
{
    /// <summary>
    /// Unique identifier for this association.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Display name for the game (e.g., "Forza Horizon 5").
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Full path to the game executable.
    /// </summary>
    public string ExecutablePath { get; set; } = "";

    /// <summary>
    /// Name of the profile to start when launching this game.
    /// </summary>
    public string? ProfileName { get; set; }

    /// <summary>
    /// Delay in milliseconds after starting the profile before launching the game.
    /// Allows the virtual controller to be ready before the game starts.
    /// </summary>
    public int LaunchDelayMs { get; set; } = 2000;

    /// <summary>
    /// Gets the executable file name without path.
    /// </summary>
    public string ExecutableName => Path.GetFileName(ExecutablePath);

    /// <summary>
    /// Creates a deep copy of this association.
    /// </summary>
    public GameAssociation Clone()
    {
        return new GameAssociation
        {
            Id = Id,
            Name = Name,
            ExecutablePath = ExecutablePath,
            ProfileName = ProfileName,
            LaunchDelayMs = LaunchDelayMs
        };
    }
}
