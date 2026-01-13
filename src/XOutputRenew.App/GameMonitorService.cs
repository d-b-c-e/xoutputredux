using System.Diagnostics;
using System.IO;
using XOutputRenew.Core.Games;

namespace XOutputRenew.App;

/// <summary>
/// Service that monitors running processes and detects when games start/stop.
/// </summary>
public class GameMonitorService : IDisposable
{
    private readonly GameAssociationManager _gameManager;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(3);
    private CancellationTokenSource? _monitorCts;
    private bool _disposed;
    private bool _isEnabled;

    /// <summary>
    /// Currently detected running game (if any).
    /// </summary>
    private string? _activeGameId;
    private int _activeGameProcessId;

    /// <summary>
    /// Raised when a game from our associations starts running.
    /// </summary>
    public event Action<GameAssociation>? GameStarted;

    /// <summary>
    /// Raised when a monitored game stops running.
    /// </summary>
    public event Action<GameAssociation>? GameStopped;

    /// <summary>
    /// Gets whether monitoring is currently enabled.
    /// </summary>
    public bool IsEnabled => _isEnabled;

    /// <summary>
    /// Gets the currently detected running game, if any.
    /// </summary>
    public GameAssociation? ActiveGame => _activeGameId != null ? _gameManager.GetById(_activeGameId) : null;

    public GameMonitorService(GameAssociationManager gameManager)
    {
        _gameManager = gameManager;
    }

    /// <summary>
    /// Starts monitoring for running games.
    /// </summary>
    public void StartMonitoring()
    {
        if (_isEnabled) return;

        _isEnabled = true;
        _monitorCts = new CancellationTokenSource();
        var token = _monitorCts.Token;

        AppLogger.Info("Game monitoring started");

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    CheckRunningProcesses();
                    await Task.Delay(_pollInterval, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    AppLogger.Error("Error in game monitor loop", ex);
                }
            }
        }, token);
    }

    /// <summary>
    /// Stops monitoring for running games.
    /// </summary>
    public void StopMonitoring()
    {
        if (!_isEnabled) return;

        _isEnabled = false;
        _monitorCts?.Cancel();
        _monitorCts?.Dispose();
        _monitorCts = null;

        // If a game was active, signal it stopped
        if (_activeGameId != null)
        {
            var game = _gameManager.GetById(_activeGameId);
            if (game != null)
            {
                AppLogger.Info($"Game monitor stopped while {game.Name} was running");
            }
            _activeGameId = null;
            _activeGameProcessId = 0;
        }

        AppLogger.Info("Game monitoring stopped");
    }

    private void CheckRunningProcesses()
    {
        // First check if currently tracked game is still running
        if (_activeGameId != null && _activeGameProcessId != 0)
        {
            bool gameStillRunning = false;
            try
            {
                // Try to get the process - throws if it no longer exists
                using var process = Process.GetProcessById(_activeGameProcessId);
                gameStillRunning = !process.HasExited;
            }
            catch (ArgumentException)
            {
                // Process no longer exists
                gameStillRunning = false;
            }
            catch (InvalidOperationException)
            {
                // Process has exited
                gameStillRunning = false;
            }

            if (!gameStillRunning)
            {
                var game = _gameManager.GetById(_activeGameId);
                if (game != null)
                {
                    AppLogger.Info($"Game exited: {game.Name}");
                    GameStopped?.Invoke(game);
                }
                _activeGameId = null;
                _activeGameProcessId = 0;
            }
            else
            {
                // Game is still running, no need to scan
                return;
            }
        }

        // Scan for any matching game
        var games = _gameManager.Games;
        if (games.Count == 0) return;

        // Build a lookup of executable names to game associations
        var exeToGame = new Dictionary<string, GameAssociation>(StringComparer.OrdinalIgnoreCase);
        foreach (var game in games)
        {
            var exeName = Path.GetFileName(game.ExecutablePath);
            if (!string.IsNullOrEmpty(exeName))
            {
                exeToGame[exeName] = game;
            }
        }

        // Check running processes
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                string? processPath = null;
                string? processName = process.ProcessName + ".exe";

                // Quick check by process name first
                if (exeToGame.TryGetValue(processName, out var matchedGame))
                {
                    // Verify by full path if possible
                    try
                    {
                        processPath = process.MainModule?.FileName;
                        if (processPath != null &&
                            processPath.Equals(matchedGame.ExecutablePath, StringComparison.OrdinalIgnoreCase))
                        {
                            // Exact match by path
                            OnGameDetected(matchedGame, process.Id);
                            return;
                        }
                        else if (processPath != null)
                        {
                            // Path didn't match, check if exe name still matches (different install location)
                            var actualExeName = Path.GetFileName(processPath);
                            if (actualExeName.Equals(Path.GetFileName(matchedGame.ExecutablePath), StringComparison.OrdinalIgnoreCase))
                            {
                                // Same exe name, probably the right game
                                AppLogger.Info($"Matched game by exe name: {matchedGame.Name} (path differs)");
                                OnGameDetected(matchedGame, process.Id);
                                return;
                            }
                        }
                    }
                    catch
                    {
                        // Can't get full path (access denied), match by process name only
                        OnGameDetected(matchedGame, process.Id);
                        return;
                    }
                }
            }
            catch
            {
                // Skip processes we can't access
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    private void OnGameDetected(GameAssociation game, int processId)
    {
        _activeGameId = game.Id;
        _activeGameProcessId = processId;
        AppLogger.Info($"Game detected: {game.Name} (PID: {processId})");
        GameStarted?.Invoke(game);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        StopMonitoring();
        GC.SuppressFinalize(this);
    }
}
