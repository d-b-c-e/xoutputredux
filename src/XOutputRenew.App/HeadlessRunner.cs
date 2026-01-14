using XOutputRenew.Core.Games;
using XOutputRenew.Core.Mapping;
using XOutputRenew.Emulation;
using XOutputRenew.HidHide;
using XOutputRenew.Input;

namespace XOutputRenew.App;

/// <summary>
/// Runs XOutputRenew in headless mode without GUI.
/// Useful for scripting, services, and gaming frontend integration.
/// </summary>
public class HeadlessRunner : IDisposable
{
    private readonly InputDeviceManager _deviceManager;
    private readonly ViGEmService _vigemService;
    private readonly HidHideService _hidHideService;
    private readonly ProfileManager _profileManager;
    private readonly GameAssociationManager _gameManager;
    private readonly GameMonitorService _gameMonitorService;
    private readonly IpcService _ipcService;
    private readonly ForceFeedbackService _ffbService;

    private XboxController? _activeController;
    private MappingEngine? _activeMappingEngine;
    private MappingProfile? _runningProfile;
    private readonly List<string> _hiddenDevices = new();
    private readonly ManualResetEventSlim _shutdownEvent = new(false);
    private bool _disposed;
    private bool _monitoringEnabled;

    public HeadlessRunner()
    {
        _deviceManager = new InputDeviceManager();
        _vigemService = new ViGEmService();
        _hidHideService = new HidHideService();
        _profileManager = new ProfileManager(ProfileManager.GetDefaultProfilesDirectory());
        _gameManager = new GameAssociationManager(GameAssociationManager.GetDefaultFilePath());
        _gameMonitorService = new GameMonitorService(_gameManager);
        _ipcService = new IpcService();
        _ffbService = new ForceFeedbackService(_deviceManager);

        // Wire up IPC handlers
        _ipcService.StartProfileRequested += IpcService_StartProfileRequested;
        _ipcService.StopRequested += IpcService_StopRequested;
        _ipcService.MonitoringEnableRequested += IpcService_MonitoringEnableRequested;
        _ipcService.MonitoringDisableRequested += IpcService_MonitoringDisableRequested;
        _ipcService.GetStatus = GetIpcStatus;

        // Wire up game monitoring handlers
        _gameMonitorService.GameStarted += GameMonitorService_GameStarted;
        _gameMonitorService.GameStopped += GameMonitorService_GameStopped;
    }

    /// <summary>
    /// Runs in headless mode with the specified profile and/or monitoring.
    /// Blocks until shutdown is requested via IPC or Ctrl+C.
    /// </summary>
    /// <param name="profileName">Profile to start (optional if monitoring is enabled)</param>
    /// <param name="enableMonitoring">Whether to enable game monitoring</param>
    public int Run(string? profileName, bool enableMonitoring = false)
    {
        AppLogger.Info($"Starting headless mode (profile: {profileName ?? "none"}, monitoring: {enableMonitoring})");
        Console.WriteLine($"XOutputRenew Headless Mode");
        Console.WriteLine($"==========================");

        // Load app settings for toast notifications
        var appSettings = AppSettings.Load();
        ToastNotificationService.Enabled = appSettings.ToastNotificationsEnabled;

        // Initialize services
        if (!_vigemService.Initialize())
        {
            Console.Error.WriteLine("Error: ViGEmBus driver is not installed.");
            Console.Error.WriteLine("Install from: https://github.com/nefarius/ViGEmBus/releases");
            return Program.ExitError;
        }
        Console.WriteLine("ViGEm: OK");

        _hidHideService.Initialize();
        if (_hidHideService.IsAvailable)
        {
            Console.WriteLine("HidHide: OK");
            _hidHideService.WhitelistSelf();
        }
        else
        {
            Console.WriteLine("HidHide: Not installed (optional)");
        }

        // Load profiles
        _profileManager.LoadProfiles();

        // Load game associations
        _gameManager.Load();
        Console.WriteLine($"Games configured: {_gameManager.Games.Count}");

        // Refresh devices
        Console.WriteLine("Discovering devices...");
        Thread.Sleep(500);
        _deviceManager.RefreshDevices();
        Console.WriteLine($"Found {_deviceManager.Devices.Count} input device(s)");

        // Start IPC server
        _ipcService.StartServer();
        Console.WriteLine("IPC server started");

        // Start profile if specified
        if (!string.IsNullOrEmpty(profileName))
        {
            var profile = FindProfile(profileName);
            if (profile == null)
            {
                Console.Error.WriteLine($"Error: Profile '{profileName}' not found.");
                Console.Error.WriteLine("Available profiles:");
                foreach (var p in _profileManager.Profiles.Values)
                {
                    Console.Error.WriteLine($"  {p.Name}");
                }
                return Program.ExitProfileNotFound;
            }

            if (!StartProfile(profile))
            {
                return Program.ExitError;
            }
            Console.WriteLine($"Profile '{profile.Name}' is now running.");
        }

        // Start game monitoring if requested
        if (enableMonitoring)
        {
            StartMonitoring();
        }

        Console.WriteLine();
        if (_runningProfile != null)
        {
            Console.WriteLine($"Profile: {_runningProfile.Name}");
        }
        if (_monitoringEnabled)
        {
            Console.WriteLine($"Monitoring: Enabled (watching {_gameManager.Games.Count} game(s))");
        }
        Console.WriteLine("Press Ctrl+C to stop, or use 'XOutputRenew stop' from another terminal.");
        Console.WriteLine();

        // Handle Ctrl+C
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            Console.WriteLine("\nShutdown requested...");
            _shutdownEvent.Set();
        };

        // Wait for shutdown
        _shutdownEvent.Wait();

        // Stop monitoring and profile
        StopMonitoring();
        StopProfile();

        Console.WriteLine("Headless mode stopped.");
        return Program.ExitSuccess;
    }

    private MappingProfile? FindProfile(string name)
    {
        // Try exact match first
        var profile = _profileManager.GetProfile(name);
        if (profile != null) return profile;

        // Try case-insensitive match
        var match = _profileManager.Profiles.FirstOrDefault(p =>
            p.Key.Equals(name, StringComparison.OrdinalIgnoreCase) ||
            p.Value.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        return match.Value;
    }

    private bool StartProfile(MappingProfile profile)
    {
        StopProfile();

        try
        {
            // Create controller
            _activeController = _vigemService.CreateXboxController();
            _activeController.Connect();

            // Set up mapping engine
            _activeMappingEngine = new MappingEngine();
            _activeMappingEngine.ActiveProfile = profile;

            // Subscribe to device input changes and start devices
            foreach (var device in _deviceManager.Devices)
            {
                device.InputChanged += Device_InputChanged;
                device.Start();
            }

            // Attach force feedback service
            _ffbService.Attach(_activeController, profile);

            _runningProfile = profile;

            // Hide devices if HidHide is enabled for this profile
            HideProfileDevices(profile);

            // Show toast notification
            ToastNotificationService.ShowProfileStarted(profile.Name);

            Console.WriteLine($"Started profile: {profile.Name}");
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to start profile: {ex.Message}");
            AppLogger.Error("Failed to start profile in headless mode", ex);
            StopProfile();
            return false;
        }
    }

    private void StopProfile()
    {
        // Unhide any devices we hid
        UnhideProfileDevices();

        // Detach force feedback service
        _ffbService.Detach();

        // Stop devices
        foreach (var device in _deviceManager.Devices)
        {
            device.InputChanged -= Device_InputChanged;
            device.Stop();
        }

        // Disconnect controller
        _activeController?.Dispose();
        _activeController = null;
        _activeMappingEngine = null;

        if (_runningProfile != null)
        {
            var stoppedProfileName = _runningProfile.Name;
            _runningProfile = null;

            // Show toast notification
            ToastNotificationService.ShowProfileStopped(stoppedProfileName);
            Console.WriteLine($"Stopped profile: {stoppedProfileName}");
        }
    }

    private void HideProfileDevices(MappingProfile profile)
    {
        _hiddenDevices.Clear();

        var hidHideSettings = profile.HidHideSettings;
        if (hidHideSettings == null || !hidHideSettings.Enabled)
            return;

        if (!_hidHideService.IsAvailable)
        {
            AppLogger.Warning("HidHide is not available - cannot hide devices");
            return;
        }

        // Enable cloaking if not already enabled
        _hidHideService.EnableCloaking();

        foreach (var devicePath in hidHideSettings.DevicesToHide)
        {
            if (_hidHideService.HideDevice(devicePath))
            {
                _hiddenDevices.Add(devicePath);
                AppLogger.Info($"Hidden device: {devicePath}");
            }
        }
    }

    private void UnhideProfileDevices()
    {
        if (!_hidHideService.IsAvailable || _hiddenDevices.Count == 0)
            return;

        foreach (var devicePath in _hiddenDevices)
        {
            _hidHideService.UnhideDevice(devicePath);
            AppLogger.Info($"Unhidden device: {devicePath}");
        }

        _hiddenDevices.Clear();
    }

    private void Device_InputChanged(object? sender, InputChangedEventArgs e)
    {
        if (_activeMappingEngine == null || _activeController == null) return;

        if (sender is IInputDevice device)
        {
            _activeMappingEngine.UpdateInput(device.UniqueId, e.Source.Index, e.NewValue);

            // Evaluate and send to controller
            var state = _activeMappingEngine.Evaluate();
            _activeController.SendInput(new XboxInput
            {
                A = state.A,
                B = state.B,
                X = state.X,
                Y = state.Y,
                LeftBumper = state.LeftBumper,
                RightBumper = state.RightBumper,
                Back = state.Back,
                Start = state.Start,
                Guide = state.Guide,
                LeftStick = state.LeftStick,
                RightStick = state.RightStick,
                DPadUp = state.DPadUp,
                DPadDown = state.DPadDown,
                DPadLeft = state.DPadLeft,
                DPadRight = state.DPadRight,
                LeftStickX = state.LeftStickX,
                LeftStickY = state.LeftStickY,
                RightStickX = state.RightStickX,
                RightStickY = state.RightStickY,
                LeftTrigger = state.LeftTrigger,
                RightTrigger = state.RightTrigger
            });
        }
    }

    private void IpcService_StartProfileRequested(string profileName)
    {
        AppLogger.Info($"IPC: Starting profile '{profileName}'");

        var profile = FindProfile(profileName);
        if (profile != null)
        {
            StartProfile(profile);
        }
        else
        {
            AppLogger.Warning($"IPC: Profile not found: {profileName}");
        }
    }

    private void IpcService_StopRequested()
    {
        AppLogger.Info("IPC: Stop requested");
        StopMonitoring();
        StopProfile();
        _shutdownEvent.Set();
    }

    private void IpcService_MonitoringEnableRequested()
    {
        AppLogger.Info("IPC: Monitoring enable requested");
        StartMonitoring();
    }

    private void IpcService_MonitoringDisableRequested()
    {
        AppLogger.Info("IPC: Monitoring disable requested");
        StopMonitoring();
    }

    private void StartMonitoring()
    {
        if (_monitoringEnabled) return;

        _gameMonitorService.StartMonitoring();
        _monitoringEnabled = true;
        Console.WriteLine($"Game monitoring enabled - watching {_gameManager.Games.Count} game(s)");
        ToastNotificationService.ShowMonitoringStarted(_gameManager.Games.Count);
    }

    private void StopMonitoring()
    {
        if (!_monitoringEnabled) return;

        _gameMonitorService.StopMonitoring();
        _monitoringEnabled = false;
        Console.WriteLine("Game monitoring disabled");
        ToastNotificationService.ShowMonitoringStopped();
    }

    private void GameMonitorService_GameStarted(GameAssociation game)
    {
        AppLogger.Info($"Game detected: {game.Name}");
        Console.WriteLine($"Game started: {game.Name}");

        if (string.IsNullOrEmpty(game.ProfileName))
        {
            AppLogger.Warning($"No profile configured for game: {game.Name}");
            Console.WriteLine($"Warning: No profile configured for game '{game.Name}'");
            return;
        }

        var profile = FindProfile(game.ProfileName);
        if (profile != null)
        {
            StartProfile(profile);
            Console.WriteLine($"Started profile: {profile.Name}");
            ToastNotificationService.ShowGameLaunched(game.Name, profile.Name);
        }
        else
        {
            AppLogger.Warning($"Profile not found for game: {game.ProfileName}");
            Console.WriteLine($"Warning: Profile '{game.ProfileName}' not found for game '{game.Name}'");
        }
    }

    private void GameMonitorService_GameStopped(GameAssociation game)
    {
        AppLogger.Info($"Game exited: {game.Name}");
        Console.WriteLine($"Game exited: {game.Name}");

        if (_runningProfile != null && _runningProfile.Name == game.ProfileName)
        {
            StopProfile();
            ToastNotificationService.ShowGameExited(game.Name);
        }
    }

    private IpcStatus GetIpcStatus()
    {
        return new IpcStatus
        {
            IsRunning = _runningProfile != null,
            ProfileName = _runningProfile?.Name,
            ViGEmStatus = _vigemService.IsAvailable ? "Available" : "Not installed",
            HidHideStatus = _hidHideService.IsAvailable ? "Available" : "Not installed"
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        StopMonitoring();
        StopProfile();

        _ipcService.StartProfileRequested -= IpcService_StartProfileRequested;
        _ipcService.StopRequested -= IpcService_StopRequested;
        _ipcService.MonitoringEnableRequested -= IpcService_MonitoringEnableRequested;
        _ipcService.MonitoringDisableRequested -= IpcService_MonitoringDisableRequested;
        _ipcService.Dispose();

        _gameMonitorService.GameStarted -= GameMonitorService_GameStarted;
        _gameMonitorService.GameStopped -= GameMonitorService_GameStopped;
        _gameMonitorService.Dispose();

        _ffbService.Dispose();
        _deviceManager.Dispose();
        _vigemService.Dispose();
        _hidHideService.Dispose();
        _shutdownEvent.Dispose();

        GC.SuppressFinalize(this);
    }
}
