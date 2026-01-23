using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using Microsoft.Win32;
using System.Windows.Media;
using System.Windows.Threading;
using XOutputRedux.App.ViewModels;
using XOutputRedux.Core.Games;
using XOutputRedux.Core.Mapping;
using XOutputRedux.Emulation;
using XOutputRedux.HidHide;
using XOutputRedux.Input;

namespace XOutputRedux.App;

/// <summary>
/// Main application window.
/// </summary>
public partial class MainWindow : Window
{
    private readonly InputDeviceManager _deviceManager;
    private readonly ProfileManager _profileManager;
    private readonly ViGEmService _vigemService;
    private readonly HidHideService _hidHideService;
    private readonly DeviceSettings _deviceSettings;
    private readonly AppSettings _appSettings;

    private readonly ObservableCollection<DeviceViewModel> _devices = new();
    private readonly ObservableCollection<ProfileViewModel> _profiles = new();

    private XboxController? _activeController;
    private MappingEngine? _activeMappingEngine;
    private ForceFeedbackService? _ffbService;
    private ProfileViewModel? _runningProfile;
    private List<string> _hiddenDevices = new();
    private bool _isExiting;
    private bool _isListeningForInput;
    private readonly Dictionary<string, DateTime> _deviceLastInput = new();
    private readonly DispatcherTimer _inputHighlightTimer;
    private readonly IpcService _ipcService;
    private readonly GameAssociationManager _gameManager;
    private readonly GameMonitorService _gameMonitorService;
    private GlobalHotkeyService? _hotkeyService;

    // Test tab brushes
    private static readonly SolidColorBrush ReleasedBrush = new(Color.FromRgb(0xCC, 0xCC, 0xCC));
    private static readonly SolidColorBrush PressedBrush = new(Color.FromRgb(0x4C, 0xAF, 0x50));

    // Stick dot base positions (Canvas.Left/Top from XAML)
    private const double LeftStickDotBaseX = 82;
    private const double LeftStickDotBaseY = 157;
    private const double RightStickDotBaseX = 252;
    private const double RightStickDotBaseY = 247;
    private const double StickRange = 20; // pixels from center
    private const double TriggerMaxHeight = 50; // trigger container height

    public MainWindow()
    {
        InitializeComponent();

        // Initialize logging early so device enumeration is logged
        AppLogger.Initialize();
        InputLogger.LogAction = msg => AppLogger.Info(msg);

        // Initialize services
        _deviceManager = new InputDeviceManager();
        _profileManager = new ProfileManager(AppPaths.Profiles);
        _vigemService = new ViGEmService();
        _hidHideService = new HidHideService();
        _deviceSettings = new DeviceSettings();
        _deviceSettings.Load();
        _appSettings = AppSettings.Load();

        // Timer to clear input highlights after inactivity
        _inputHighlightTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _inputHighlightTimer.Tick += InputHighlightTimer_Tick;

        // Initialize IPC service for remote control
        _ipcService = new IpcService();
        _ipcService.StartProfileRequested += IpcService_StartProfileRequested;
        _ipcService.StopRequested += IpcService_StopRequested;
        _ipcService.MonitoringEnableRequested += IpcService_MonitoringEnableRequested;
        _ipcService.MonitoringDisableRequested += IpcService_MonitoringDisableRequested;
        _ipcService.GetStatus = GetIpcStatus;
        _ipcService.StartServer();

        // Initialize game manager and monitor service
        _gameManager = new GameAssociationManager(AppPaths.Games);
        _gameManager.Load();
        _gameMonitorService = new GameMonitorService(_gameManager);
        _gameMonitorService.GameStarted += GameMonitorService_GameStarted;
        _gameMonitorService.GameStopped += GameMonitorService_GameStopped;

        // Bind collections
        DeviceListView.ItemsSource = _devices;
        ProfileListView.ItemsSource = _profiles;
        GamesListView.ItemsSource = _gameManager.Games;

        // Load data
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        AppLogger.Info("MainWindow loaded");

        // Enable dark title bar
        DarkModeHelper.EnableDarkTitleBar(this);

        // Set window handle for DirectInput force feedback (required for exclusive mode)
        var windowHandle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        _deviceManager.SetWindowHandle(windowHandle);

        // Initialize force feedback service
        _ffbService = new ForceFeedbackService(_deviceManager);

        RefreshDevices();
        RefreshProfiles();
        CheckDriverStatus();
        InitializeOptions();
        InitializeGlobalHotkey();

        // Show portable mode indicators if applicable
        if (AppPaths.IsPortable)
        {
            PortableModeWarning.Visibility = Visibility.Visible;
            PortableModeText.Visibility = Visibility.Visible;
        }

        // Set version in About tab
        var version = System.Reflection.Assembly.GetExecutingAssembly()
            .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? "Unknown";
        VersionText.Text = $"Version {version}";

        // Check for startup profile (command line takes precedence, then saved setting)
        string? startProfile = Application.Current.Properties["StartProfile"] as string;
        if (string.IsNullOrEmpty(startProfile))
        {
            startProfile = _appSettings.StartupProfile;
        }

        if (!string.IsNullOrEmpty(startProfile))
        {
            var profile = _profiles.FirstOrDefault(p => p.Name == startProfile || p.FileName == startProfile);
            if (profile != null)
            {
                ProfileListView.SelectedItem = profile;
                StartProfile(profile);
            }
        }

        // Check for minimized startup
        if (Application.Current.Properties["Minimized"] is true)
        {
            // Must call Show() first to ensure tray icon initializes properly
            // Window_StateChanged will call Hide() when we set Minimized state
            Show();
            WindowState = WindowState.Minimized;
        }

        // Restore game monitoring if it was enabled
        if (_appSettings.GameMonitoringEnabled && _gameManager.Games.Count > 0)
        {
            StartGameMonitoring(saveToSettings: false, showToast: false);
        }

        // Check for updates on startup (async, doesn't block)
        _ = CheckForUpdatesOnStartupAsync();
    }

    private void CheckDriverStatus()
    {
        // Check ViGEm
        bool vigemInstalled = _vigemService.Initialize();
        if (vigemInstalled)
        {
            ViGEmStatusText.Text = "Installed";
            ViGEmStatusText.Foreground = new SolidColorBrush(Colors.Green);
            ViGEmInfoText.Text = "Virtual Xbox controller emulation available.";
            InstallViGEmButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            ViGEmStatusText.Text = "Not Installed";
            ViGEmStatusText.Foreground = new SolidColorBrush(Colors.Red);
            ViGEmInfoText.Text = "Required: Install ViGEmBus for Xbox controller emulation.";
            InstallViGEmButton.Visibility = Visibility.Visible;

            // Prompt to install if user hasn't declined before
            if (!_appSettings.ViGEmBusPromptDeclined)
            {
                PromptViGEmBusInstall();
            }
        }

        // Check HidHide
        _hidHideService.Initialize();
        bool hidHideInstalled = _hidHideService.IsAvailable;
        if (hidHideInstalled)
        {
            HidHideStatusText.Text = "Installed";
            HidHideStatusText.Foreground = new SolidColorBrush(Colors.Green);
            HidHideInfoText.Text = "Device hiding available.";

            // Whitelist our application so we can still see hidden devices
            if (_hidHideService.WhitelistSelf())
            {
                AppLogger.Info("Whitelisted XOutputRedux in HidHide");
            }
        }
        else
        {
            HidHideStatusText.Text = "Not Installed";
            HidHideStatusText.Foreground = new SolidColorBrush(Colors.Orange);
            HidHideInfoText.Text = "Optional: Install HidHide for device hiding.";
            InstallHidHideButton.Visibility = Visibility.Visible;

            // Prompt to install if user hasn't declined before
            if (!_appSettings.HidHidePromptDeclined)
            {
                PromptHidHideInstall();
            }
        }

        // Report driver status for crash reporting
        App.SetDriverStatus(_vigemService.IsAvailable, _hidHideService.IsAvailable);
    }

    private async void PromptHidHideInstall()
    {
        var result = MessageBox.Show(
            "HidHide is not installed. This optional driver allows XOutputRedux to hide your physical controllers " +
            "from games, preventing 'double input' issues.\n\n" +
            "Would you like to download and install HidHide now?\n\n" +
            "(You can always install it later from the Status tab)",
            "Install HidHide?",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            await InstallHidHide();
        }
        else
        {
            // Remember that user declined
            _appSettings.HidHidePromptDeclined = true;
            _appSettings.Save();
        }
    }

    private async Task InstallHidHide()
    {
        StatusText.Text = "Downloading HidHide...";
        HidHideInfoText.Text = "Downloading and installing...";

        var (success, message) = await _hidHideService.DownloadAndInstallAsync(progress =>
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = $"Installing HidHide... {progress}%";
            });
        });

        if (success)
        {
            HidHideStatusText.Text = "Installed";
            HidHideStatusText.Foreground = new SolidColorBrush(Colors.Green);
            HidHideInfoText.Text = "Device hiding available.";
            StatusText.Text = "HidHide installed successfully";

            // Whitelist ourselves
            _hidHideService.WhitelistSelf();

            MessageBox.Show(
                message + "\n\nA system restart is required for HidHide to function properly.",
                "Installation Complete",
                MessageBoxButton.OK,
                MessageBoxImage.None);
        }
        else
        {
            HidHideInfoText.Text = "Installation failed. Click to try again.";
            StatusText.Text = $"HidHide installation failed: {message}";

            MessageBox.Show(
                $"Failed to install HidHide:\n\n{message}\n\n" +
                "You can try installing manually from:\nhttps://github.com/nefarius/HidHide/releases",
                "Installation Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void InstallHidHide_Click(object sender, RoutedEventArgs e)
    {
        InstallHidHideButton.IsEnabled = false;
        InstallHidHideButton.Content = "Installing...";

        await InstallHidHide();

        // Update button state based on result
        if (_hidHideService.IsAvailable)
        {
            InstallHidHideButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            InstallHidHideButton.IsEnabled = true;
            InstallHidHideButton.Content = "Retry Install";
        }
    }

    private async void PromptViGEmBusInstall()
    {
        var result = MessageBox.Show(
            "ViGEmBus driver is not installed. This driver is REQUIRED for XOutputRedux to create " +
            "virtual Xbox controllers.\n\n" +
            "Without ViGEmBus, XOutputRedux cannot emulate controllers and will not function.\n\n" +
            "Would you like to download and install ViGEmBus now?\n\n" +
            "(You can always install it later from the Status tab)",
            "Install ViGEmBus?",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            await InstallViGEm();
        }
        else
        {
            // Remember that user declined
            _appSettings.ViGEmBusPromptDeclined = true;
            _appSettings.Save();
        }
    }

    private async Task InstallViGEm()
    {
        StatusText.Text = "Downloading ViGEmBus...";
        ViGEmInfoText.Text = "Downloading and installing...";

        var (success, message) = await _vigemService.DownloadAndInstallAsync(progress =>
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = $"Installing ViGEmBus... {progress}%";
            });
        });

        if (success)
        {
            ViGEmStatusText.Text = "Installed";
            ViGEmStatusText.Foreground = new SolidColorBrush(Colors.Green);
            ViGEmInfoText.Text = "Virtual Xbox controller emulation available.";
            StatusText.Text = "ViGEmBus installed successfully";
            InstallViGEmButton.Visibility = Visibility.Collapsed;

            MessageBox.Show(
                message + "\n\nYou may need to restart XOutputRedux for the driver to be fully available.",
                "Installation Complete",
                MessageBoxButton.OK,
                MessageBoxImage.None);
        }
        else
        {
            ViGEmInfoText.Text = "Installation failed. Click to try again.";
            StatusText.Text = $"ViGEmBus installation failed: {message}";

            MessageBox.Show(
                $"Failed to install ViGEmBus:\n\n{message}\n\n" +
                "You can try installing manually from:\nhttps://github.com/nefarius/ViGEmBus/releases",
                "Installation Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void InstallViGEm_Click(object sender, RoutedEventArgs e)
    {
        InstallViGEmButton.IsEnabled = false;
        InstallViGEmButton.Content = "Installing...";

        await InstallViGEm();

        // Update button state based on result
        if (_vigemService.IsAvailable)
        {
            InstallViGEmButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            InstallViGEmButton.IsEnabled = true;
            InstallViGEmButton.Content = "Retry Install";
        }
    }

    private void RefreshDevices()
    {
        _devices.Clear();
        _deviceManager.RefreshDevices();

        foreach (var device in _deviceManager.Devices)
        {
            var vm = new DeviceViewModel(device);
            // Apply saved friendly name
            vm.FriendlyName = _deviceSettings.GetFriendlyName(device.UniqueId);
            _devices.Add(vm);
        }

        DeviceCountText.Text = $"{_devices.Count} device(s) detected";
        StatusText.Text = $"Found {_devices.Count} input device(s)";
    }

    private void RefreshProfiles()
    {
        var selectedFileName = (_profiles.FirstOrDefault(p => p == ProfileListView.SelectedItem) as ProfileViewModel)?.FileName;

        _profiles.Clear();
        _profileManager.LoadProfiles();

        foreach (var kvp in _profileManager.Profiles)
        {
            var vm = new ProfileViewModel(kvp.Key, kvp.Value);
            if (_runningProfile?.FileName == kvp.Key)
            {
                vm.IsRunning = true;
                _runningProfile = vm;
            }
            _profiles.Add(vm);
        }

        // Restore selection, or auto-select first profile if none selected
        if (selectedFileName != null)
        {
            ProfileListView.SelectedItem = _profiles.FirstOrDefault(p => p.FileName == selectedFileName);
        }

        // Auto-select first profile if nothing selected (makes Start button immediately usable)
        if (ProfileListView.SelectedItem == null && _profiles.Count > 0)
        {
            ProfileListView.SelectedItem = _profiles[0];
        }

        // Update startup profile dropdown if it exists
        if (StartupProfileComboBox != null)
        {
            RefreshStartupProfileComboBox();
        }
    }

    #region Device Events

    private void RefreshDevices_Click(object sender, RoutedEventArgs e)
    {
        RefreshDevices();
    }

    private void DeviceListView_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        // Could show device details panel in the future
    }

    private void ShowDeviceInfo_Click(object sender, RoutedEventArgs e)
    {
        if (DeviceListView.SelectedItem is not DeviceViewModel selected) return;

        var text = selected.GetDeviceInfo();
        var dialog = new TextDisplayDialog("Device Info", text);
        dialog.Owner = this;
        dialog.ShowDialog();
    }

    private void RenameDevice_Click(object sender, RoutedEventArgs e)
    {
        if (DeviceListView.SelectedItem is not DeviceViewModel selected) return;

        var dialog = new InputDialog("Rename Device", "Enter a friendly name for this device:",
            selected.FriendlyName ?? selected.Name);

        if (dialog.ShowDialog() == true)
        {
            var newName = string.IsNullOrWhiteSpace(dialog.InputText) ? null : dialog.InputText.Trim();
            selected.FriendlyName = newName;
            _deviceSettings.SetFriendlyName(selected.UniqueId, newName);
            StatusText.Text = newName != null
                ? $"Renamed device to: {newName}"
                : $"Cleared friendly name for: {selected.Name}";
        }
    }

    private void ListenForInput_Changed(object sender, RoutedEventArgs e)
    {
        _isListeningForInput = ListenForInputCheckBox.IsChecked == true;

        if (_isListeningForInput)
        {
            // Start listening on all devices
            foreach (var vm in _devices)
            {
                vm.Device.InputChanged += Device_InputChanged_Listen;
                vm.Device.Start();
            }
            _inputHighlightTimer.Start();
            StatusText.Text = "Listening for input - press buttons to identify devices";
        }
        else
        {
            // Stop listening
            foreach (var vm in _devices)
            {
                vm.Device.InputChanged -= Device_InputChanged_Listen;
                vm.Device.Stop();
                vm.IsActive = false;
            }
            _inputHighlightTimer.Stop();
            _deviceLastInput.Clear();
            StatusText.Text = "Stopped listening for input";
        }
    }

    private void VerboseLogging_Changed(object sender, RoutedEventArgs e)
    {
        InputLogger.VerboseEnabled = VerboseLoggingCheckBox.IsChecked == true;
        if (InputLogger.VerboseEnabled)
        {
            AppLogger.Info($"Verbose input logging ENABLED - check log file: {AppLogger.GetLogPath()}");
            StatusText.Text = $"Verbose logging enabled - log: {AppLogger.GetLogPath()}";
        }
        else
        {
            AppLogger.Info("Verbose input logging disabled");
            StatusText.Text = "Verbose logging disabled";
        }
    }

    private void Device_InputChanged_Listen(object? sender, InputChangedEventArgs e)
    {
        if (sender is not IInputDevice device) return;

        // Only highlight on significant input changes
        bool isSignificant = e.NewValue > 0.5 || Math.Abs(e.NewValue - 0.5) > 0.3;
        if (!isSignificant) return;

        Dispatcher.BeginInvoke(() =>
        {
            _deviceLastInput[device.UniqueId] = DateTime.Now;

            var vm = _devices.FirstOrDefault(d => d.UniqueId == device.UniqueId);
            if (vm != null)
            {
                vm.IsActive = true;
            }
        });
    }

    private void InputHighlightTimer_Tick(object? sender, EventArgs e)
    {
        var now = DateTime.Now;
        var timeout = TimeSpan.FromMilliseconds(500);

        foreach (var vm in _devices)
        {
            if (_deviceLastInput.TryGetValue(vm.UniqueId, out var lastInput))
            {
                if (now - lastInput > timeout)
                {
                    vm.IsActive = false;
                }
            }
        }
    }

    #endregion

    #region Profile Events

    private void ProfileListView_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        bool hasSelection = ProfileListView.SelectedItem != null;
        StartStopButton.IsEnabled = hasSelection;

        UpdateStartStopButton();
    }

    private void ProfileListView_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        EditProfile_Click(sender, e);
    }

    private void EditProfile_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileListView.SelectedItem is not ProfileViewModel selected) return;

        // Open in read-only mode if profile is running
        bool readOnly = selected.IsRunning;

        var editor = new ProfileEditorWindow(selected.Profile, _deviceManager, _hidHideService, _deviceSettings, readOnly);
        editor.Owner = this;
        editor.ShowDialog();

        if (editor.WasSaved && !readOnly)
        {
            // If this profile is now the default, clear default from others
            if (selected.Profile.IsDefault)
            {
                _profileManager.SetDefaultProfile(selected.FileName);
            }
            else
            {
                // Just save this profile
                _profileManager.SaveProfile(selected.FileName, selected.Profile);
            }
            RefreshProfiles();
            StatusText.Text = $"Saved profile: {selected.Name}";
        }
    }

    private void UpdateStartStopButton()
    {
        var selected = ProfileListView.SelectedItem as ProfileViewModel;
        if (selected == null)
        {
            StartStopButton.IsEnabled = false;
            return;
        }

        StartStopButton.IsEnabled = true;
        if (selected.IsRunning)
        {
            StartStopButton.Content = "Stop";
            StartStopButton.Background = new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)); // Red
        }
        else
        {
            StartStopButton.Content = "Start";
            StartStopButton.Background = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)); // Green
        }
    }

    private void NewProfile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new InputDialog("New Profile", "Enter profile name:");
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputText))
        {
            var profile = _profileManager.CreateProfile(dialog.InputText);
            RefreshProfiles();
            ProfileListView.SelectedItem = _profiles.FirstOrDefault(p => p.FileName == dialog.InputText);
            StatusText.Text = $"Created profile: {dialog.InputText}";
        }
    }

    private void DuplicateProfile_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileListView.SelectedItem is not ProfileViewModel selected) return;

        var dialog = new InputDialog("Duplicate Profile", "Enter new profile name:", selected.Name + " (Copy)");
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputText))
        {
            _profileManager.DuplicateProfile(selected.FileName, dialog.InputText);
            RefreshProfiles();
            ProfileListView.SelectedItem = _profiles.FirstOrDefault(p => p.FileName == dialog.InputText);
            StatusText.Text = $"Duplicated profile as: {dialog.InputText}";
        }
    }

    private void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileListView.SelectedItem is not ProfileViewModel selected) return;

        if (selected.IsRunning)
        {
            MessageBox.Show("Cannot delete a running profile. Stop it first.", "Delete Profile",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = MessageBox.Show($"Delete profile '{selected.Name}'?", "Delete Profile",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _profileManager.DeleteProfile(selected.FileName);
            RefreshProfiles();
            StatusText.Text = $"Deleted profile: {selected.Name}";
        }
    }

    private void RenameProfile_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileListView.SelectedItem is not ProfileViewModel selected) return;

        if (selected.IsRunning)
        {
            MessageBox.Show("Cannot rename a running profile. Stop it first.", "Rename Profile",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new InputDialog("Rename Profile", "Enter new name:", selected.Name);
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputText))
        {
            var newName = dialog.InputText.Trim();
            if (newName == selected.Name) return;

            bool success = _profileManager.RenameProfile(selected.FileName, newName, out string? error);

            // If profile exists, ask to overwrite
            if (!success && error == "PROFILE_EXISTS")
            {
                var result = MessageBox.Show(
                    $"A profile named '{newName}' already exists. Overwrite it?",
                    "Profile Exists",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    success = _profileManager.RenameProfile(selected.FileName, newName, out error, overwrite: true);
                }
            }

            if (success)
            {
                AppLogger.Info($"Renamed profile '{selected.FileName}' to '{newName}'");
                RefreshProfiles();
                ProfileListView.SelectedItem = _profiles.FirstOrDefault(p => p.Name == newName);
                StatusText.Text = $"Renamed profile to: {newName}";
            }
            else if (error != "PROFILE_EXISTS")
            {
                AppLogger.Error($"Failed to rename profile '{selected.FileName}' to '{newName}': {error}");
                MessageBox.Show($"Failed to rename profile.\n\nError: {error}",
                    "Rename Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void StartStopProfile_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileListView.SelectedItem is not ProfileViewModel selected) return;

        if (selected.IsRunning)
        {
            StopProfile();
        }
        else
        {
            StartProfile(selected);
        }
    }

    private void OpenGameControllers_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "joy.cpl",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open Game Controllers: {ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void StartProfile(ProfileViewModel profile)
    {
        // Stop any running profile first
        StopProfile();

        if (!_vigemService.IsAvailable)
        {
            MessageBox.Show("ViGEm is not installed. Cannot start emulation.", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        try
        {
            // Create controller
            _activeController = _vigemService.CreateXboxController();
            _activeController.Connect();

            // Set up mapping engine
            _activeMappingEngine = new MappingEngine();
            _activeMappingEngine.ActiveProfile = profile.Profile;

            // Subscribe to device input changes
            foreach (var device in _deviceManager.Devices)
            {
                device.InputChanged += Device_InputChanged;
                device.Start();
            }

            // Attach force feedback service
            _ffbService?.Attach(_activeController, profile.Profile);

            profile.IsRunning = true;
            _runningProfile = profile;
            UpdateStartStopButton();
            ActiveProfileText.Text = $"Running: {profile.Name}";
            StatusText.Text = $"Started profile: {profile.Name}";
            TrayIcon.ToolTipText = $"XOutputRedux - {profile.Name}";

            // Show test tab controller
            TestOverlay.Visibility = Visibility.Collapsed;
            TestProfileStatus.Text = $"Profile: {profile.Name}";
            TestProfileStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)); // Green
            ResetTestTab();

            // Hide devices if HidHide is enabled for this profile
            HideProfileDevices(profile.Profile);

            // Show toast notification
            ToastNotificationService.ShowProfileStarted(profile.Name);

            // Track active profile for crash reporting
            App.SetActiveProfile(profile.Name);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to start profile: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            StopProfile();
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
            else
            {
                AppLogger.Warning($"Failed to hide device: {devicePath}");
            }
        }

        if (_hiddenDevices.Count > 0)
        {
            StatusText.Text = $"Started profile: {profile.Name} ({_hiddenDevices.Count} device(s) hidden)";
        }
    }

    private void UnhideProfileDevices()
    {
        if (_hiddenDevices.Count == 0)
            return;

        if (!_hidHideService.IsAvailable)
            return;

        foreach (var devicePath in _hiddenDevices)
        {
            if (_hidHideService.UnhideDevice(devicePath))
            {
                AppLogger.Info($"Unhidden device: {devicePath}");
            }
            else
            {
                AppLogger.Warning($"Failed to unhide device: {devicePath}");
            }
        }

        _hiddenDevices.Clear();
    }

    private void StopProfile()
    {
        // Unhide any devices we hid when starting the profile
        UnhideProfileDevices();

        // Detach force feedback service first
        _ffbService?.Detach();

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
            _runningProfile.IsRunning = false;
            StatusText.Text = $"Stopped profile: {stoppedProfileName}";
            _runningProfile = null;

            // Show toast notification
            ToastNotificationService.ShowProfileStopped(stoppedProfileName);
        }

        // Clear active profile for crash reporting
        App.SetActiveProfile(null);

        UpdateStartStopButton();
        ActiveProfileText.Text = "No profile running";
        TrayIcon.ToolTipText = "XOutputRedux";

        // Hide test tab controller
        TestOverlay.Visibility = Visibility.Visible;
        TestProfileStatus.Text = "No Profile Running";
        TestProfileStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)); // Gray
        ResetTestTab();
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

            // Update test tab
            Dispatcher.BeginInvoke(() => UpdateTestTab(state));
        }
    }

    #endregion

    #region System Tray

    private void TrayIcon_Show_Click(object sender, RoutedEventArgs e)
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void TrayIcon_DoubleClick(object sender, RoutedEventArgs e)
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void TrayIcon_Exit_Click(object sender, RoutedEventArgs e)
    {
        _isExiting = true;
        Close();
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Hide();
        }
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (!_isExiting && _appSettings.MinimizeToTrayOnClose)
        {
            // Minimize to tray instead of closing
            e.Cancel = true;
            WindowState = WindowState.Minimized;
            return;
        }

        // Actually closing - cleanup
        _isExiting = true;
        StopProfile();
        ToastNotificationService.Cleanup();

        // Stop input listening if active
        if (_isListeningForInput)
        {
            ListenForInputCheckBox.IsChecked = false;
        }
        _inputHighlightTimer.Stop();

        _ffbService?.Dispose();
        _hotkeyService?.Dispose();
        _gameMonitorService.Dispose();
        _ipcService.Dispose();
        AppLogger.Shutdown();
        TrayIcon.Dispose();
        _deviceManager.Dispose();
        _vigemService.Dispose();
    }

    #endregion

    #region IPC Handlers

    private void IpcService_StartProfileRequested(string profileName)
    {
        // Must run on UI thread
        Dispatcher.Invoke(() =>
        {
            var profile = _profiles.FirstOrDefault(p =>
                p.Name.Equals(profileName, StringComparison.OrdinalIgnoreCase) ||
                p.FileName.Equals(profileName, StringComparison.OrdinalIgnoreCase) ||
                p.FileName.Equals(profileName + ".json", StringComparison.OrdinalIgnoreCase));

            if (profile != null)
            {
                AppLogger.Info($"IPC: Starting profile '{profile.Name}'");
                StartProfile(profile);
            }
            else
            {
                AppLogger.Warning($"IPC: Profile not found: {profileName}");
            }
        });
    }

    private void IpcService_StopRequested()
    {
        // Must run on UI thread
        Dispatcher.Invoke(() =>
        {
            AppLogger.Info("IPC: Stopping profile");
            StopProfile();
        });
    }

    private void IpcService_MonitoringEnableRequested()
    {
        // Must run on UI thread
        Dispatcher.Invoke(() =>
        {
            AppLogger.Info("IPC: Enabling game monitoring");
            if (!_gameMonitorService.IsEnabled)
            {
                StartGameMonitoring();
            }
        });
    }

    private void IpcService_MonitoringDisableRequested()
    {
        // Must run on UI thread
        Dispatcher.Invoke(() =>
        {
            AppLogger.Info("IPC: Disabling game monitoring");
            if (_gameMonitorService.IsEnabled)
            {
                StopGameMonitoring();
            }
        });
    }

    private IpcStatus GetIpcStatus()
    {
        return new IpcStatus
        {
            IsRunning = _runningProfile != null,
            ProfileName = _runningProfile?.Name,
            IsMonitoring = _gameMonitorService?.IsEnabled ?? false,
            ViGEmStatus = _vigemService.IsAvailable ? "Available" : "Not installed",
            HidHideStatus = _hidHideService.IsAvailable ? "Available" : "Not installed"
        };
    }

    #endregion

    #region Games

    private void GamesListView_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        var hasSelection = GamesListView.SelectedItem != null;
        EditGameButton.IsEnabled = hasSelection;
        RemoveGameButton.IsEnabled = hasSelection;
    }

    private void GamesListView_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (GamesListView.SelectedItem is GameAssociation game)
        {
            EditGame(game);
        }
    }

    private void AddGame_Click(object sender, RoutedEventArgs e)
    {
        var profileInfos = _profiles.Select(p => new GameEditorDialog.ProfileInfo
        {
            Name = p.Name,
            FileName = p.FileName
        });

        var dialog = new GameEditorDialog(profileInfos)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true && dialog.Result != null)
        {
            _gameManager.Add(dialog.Result);
            RefreshGamesListView();
            StatusText.Text = $"Added game: {dialog.Result.Name}";
        }
    }

    private void EditGame_Click(object sender, RoutedEventArgs e)
    {
        if (GamesListView.SelectedItem is GameAssociation game)
        {
            EditGame(game);
        }
    }

    private void EditGame(GameAssociation game)
    {
        var profileInfos = _profiles.Select(p => new GameEditorDialog.ProfileInfo
        {
            Name = p.Name,
            FileName = p.FileName
        });

        var dialog = new GameEditorDialog(profileInfos, game)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true && dialog.Result != null)
        {
            // Preserve the ID from the original
            dialog.Result.Id = game.Id;
            _gameManager.Update(dialog.Result);
            RefreshGamesListView();
            StatusText.Text = $"Updated game: {dialog.Result.Name}";
        }
    }

    private void RemoveGame_Click(object sender, RoutedEventArgs e)
    {
        if (GamesListView.SelectedItem is not GameAssociation game) return;

        var result = MessageBox.Show($"Remove game '{game.Name}' from the list?", "Remove Game",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _gameManager.Remove(game.Id);
            RefreshGamesListView();
            StatusText.Text = $"Removed game: {game.Name}";
        }
    }

    private void ToggleGameMonitoring_Click(object sender, RoutedEventArgs e)
    {
        if (_gameMonitorService.IsEnabled)
        {
            StopGameMonitoring();
        }
        else
        {
            StartGameMonitoring();
        }
    }

    private void StartGameMonitoring(bool saveToSettings = true, bool showToast = true)
    {
        if (_gameManager.Games.Count == 0)
        {
            MessageBox.Show("No games configured. Add a game first.",
                "No Games", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _gameMonitorService.StartMonitoring();
        UpdateMonitoringUI();
        StatusText.Text = "Game monitoring enabled - watching for configured games";
        AppLogger.Info("Game monitoring started");

        if (showToast)
        {
            ToastNotificationService.ShowMonitoringStarted(_gameManager.Games.Count);
        }

        if (saveToSettings)
        {
            _appSettings.GameMonitoringEnabled = true;
            _appSettings.Save();
        }
    }

    private void StopGameMonitoring(bool saveToSettings = true, bool showToast = true)
    {
        _gameMonitorService.StopMonitoring();
        UpdateMonitoringUI();
        StatusText.Text = "Game monitoring disabled";
        AppLogger.Info("Game monitoring stopped");

        if (showToast)
        {
            ToastNotificationService.ShowMonitoringStopped();
        }

        if (saveToSettings)
        {
            _appSettings.GameMonitoringEnabled = false;
            _appSettings.Save();
        }
    }

    private void UpdateMonitoringUI()
    {
        if (_gameMonitorService.IsEnabled)
        {
            GameMonitorButton.Content = "Disable Monitoring";
            GameMonitorButton.Background = new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)); // Red
            MonitorStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)); // Green
            MonitorStatusText.Text = _gameMonitorService.ActiveGame != null
                ? $"Running: {_gameMonitorService.ActiveGame.Name}"
                : $"Monitoring {_gameManager.Games.Count} game(s)...";
            MonitorStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
        }
        else
        {
            GameMonitorButton.Content = "Enable Monitoring";
            GameMonitorButton.Background = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)); // Green
            MonitorStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)); // Gray
            MonitorStatusText.Text = "Monitoring disabled";
            MonitorStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E));
        }
    }

    private void GameMonitorService_GameStarted(GameAssociation game)
    {
        Dispatcher.Invoke(() =>
        {
            AppLogger.Info($"Game detected: {game.Name}, starting profile: {game.ProfileName}");

            // Find the associated profile
            var profile = _profiles.FirstOrDefault(p =>
                p.Name.Equals(game.ProfileName, StringComparison.OrdinalIgnoreCase));

            if (profile == null)
            {
                AppLogger.Warning($"Profile '{game.ProfileName}' not found for game '{game.Name}'");
                StatusText.Text = $"Game detected but profile '{game.ProfileName}' not found";
                return;
            }

            // Start the profile
            StartProfile(profile);
            UpdateMonitoringUI();
            StatusText.Text = $"Game detected: {game.Name} - Profile started";
            ToastNotificationService.ShowGameLaunched(game.Name, profile.Name);
        });
    }

    private void GameMonitorService_GameStopped(GameAssociation game)
    {
        Dispatcher.Invoke(() =>
        {
            AppLogger.Info($"Game exited: {game.Name}, stopping profile");
            StopProfile();
            UpdateMonitoringUI();
            StatusText.Text = $"Game exited: {game.Name}";
            ToastNotificationService.ShowGameExited(game.Name);
        });
    }

    private void RefreshGamesListView()
    {
        // Force refresh by re-binding
        GamesListView.ItemsSource = null;
        GamesListView.ItemsSource = _gameManager.Games;
    }

    #endregion

    #region Options

    private void InitializeOptions()
    {
        // Load settings into UI
        MinimizeToTrayCheckBox.IsChecked = _appSettings.MinimizeToTrayOnClose;
        ToastNotificationsCheckBox.IsChecked = _appSettings.ToastNotificationsEnabled;
        ToastNotificationService.Enabled = _appSettings.ToastNotificationsEnabled;
        CrashReportingCheckBox.IsChecked = _appSettings.CrashReportingEnabled;
        IncludeProfileInCrashReportCheckBox.IsChecked = _appSettings.IncludeProfileInCrashReport;
        IncludeProfileInCrashReportCheckBox.IsEnabled = _appSettings.CrashReportingEnabled;
        StartWithWindowsCheckBox.IsChecked = AppSettings.GetStartWithWindows();

        // Populate startup profile dropdown
        RefreshStartupProfileComboBox();

        // Admin status
        bool isAdmin = AppSettings.IsRunningAsAdmin();
        AdminStatusText.Text = isAdmin ? "Yes" : "No";
        AdminStatusText.Foreground = isAdmin
            ? new SolidColorBrush(Colors.Green)
            : new SolidColorBrush(Colors.Gray);
        RestartAsAdminButton.IsEnabled = !isAdmin;

        // PATH checkbox - show current state
        AddToPathCheckBox.IsChecked = IsInSystemPath();

        // Updates checkbox
        CheckForUpdatesCheckBox.IsChecked = _appSettings.CheckForUpdatesOnStartup;

        // Hotkey settings
        AddGameHotkeyEnabledCheckBox.IsChecked = _appSettings.AddGameHotkeyEnabled;
        UpdateHotkeyDisplayText();
        UpdateHotkeyStatus(_hotkeyService?.IsEnabled ?? false);
    }

    private void RefreshStartupProfileComboBox()
    {
        var items = new List<string> { "(None)" };
        items.AddRange(_profiles.Select(p => p.Name));
        StartupProfileComboBox.ItemsSource = items;

        if (string.IsNullOrEmpty(_appSettings.StartupProfile))
        {
            StartupProfileComboBox.SelectedIndex = 0;
        }
        else
        {
            var index = items.IndexOf(_appSettings.StartupProfile);
            StartupProfileComboBox.SelectedIndex = index >= 0 ? index : 0;
        }
    }

    private void MinimizeToTray_Changed(object sender, RoutedEventArgs e)
    {
        _appSettings.MinimizeToTrayOnClose = MinimizeToTrayCheckBox.IsChecked == true;
        _appSettings.Save();
    }

    private void ToastNotifications_Changed(object sender, RoutedEventArgs e)
    {
        _appSettings.ToastNotificationsEnabled = ToastNotificationsCheckBox.IsChecked == true;
        ToastNotificationService.Enabled = _appSettings.ToastNotificationsEnabled;
        _appSettings.Save();
    }

    private void CrashReporting_Changed(object sender, RoutedEventArgs e)
    {
        _appSettings.CrashReportingEnabled = CrashReportingCheckBox.IsChecked == true;
        IncludeProfileInCrashReportCheckBox.IsEnabled = _appSettings.CrashReportingEnabled;
        _appSettings.Save();
    }

    private void IncludeProfileInCrashReport_Changed(object sender, RoutedEventArgs e)
    {
        _appSettings.IncludeProfileInCrashReport = IncludeProfileInCrashReportCheckBox.IsChecked == true;
        _appSettings.Save();
    }

    private void StartWithWindows_Changed(object sender, RoutedEventArgs e)
    {
        AppSettings.SetStartWithWindows(StartWithWindowsCheckBox.IsChecked == true);
    }

    private void StartupProfile_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (StartupProfileComboBox.SelectedIndex <= 0)
        {
            _appSettings.StartupProfile = null;
        }
        else
        {
            _appSettings.StartupProfile = StartupProfileComboBox.SelectedItem as string;
        }
        _appSettings.Save();
    }

    private void RestartAsAdmin_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "The application will restart with administrator privileges. Continue?",
            "Restart as Administrator",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            AppSettings.RestartAsAdmin();
        }
    }

    private void PathHelp_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        HelpDialog.Show(
            "When enabled, adds XOutputRedux to the system PATH environment variable.\n\n" +
            "This allows you to run CLI commands from any directory:\n" +
            "  XOutputRedux.App status\n" +
            "  XOutputRedux.App start \"My Profile\"\n" +
            "  XOutputRedux.App stop\n\n" +
            "Requires administrator privileges to modify the system PATH.",
            "Add to System PATH",
            this);
    }

    private void AddToPath_Changed(object sender, RoutedEventArgs e)
    {
        bool shouldAdd = AddToPathCheckBox.IsChecked == true;
        bool isCurrentlyInPath = IsInSystemPath();

        if (shouldAdd == isCurrentlyInPath)
            return; // No change needed

        if (!AppSettings.IsRunningAsAdmin())
        {
            MessageBox.Show(
                "Administrator privileges are required to modify the system PATH.\n\nPlease restart as administrator first.",
                "Administrator Required",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            // Revert checkbox
            AddToPathCheckBox.IsChecked = isCurrentlyInPath;
            return;
        }

        try
        {
            if (shouldAdd)
            {
                AddToSystemPath();
                MessageBox.Show(
                    "XOutputRedux has been added to the system PATH.\n\nYou may need to restart your terminal for the change to take effect.",
                    "PATH Updated",
                    MessageBoxButton.OK,
                    MessageBoxImage.None);
            }
            else
            {
                RemoveFromSystemPath();
                MessageBox.Show(
                    "XOutputRedux has been removed from the system PATH.",
                    "PATH Updated",
                    MessageBoxButton.OK,
                    MessageBoxImage.None);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to modify system PATH: {ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            // Revert checkbox
            AddToPathCheckBox.IsChecked = isCurrentlyInPath;
        }
    }

    private bool IsInSystemPath()
    {
        try
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
            var systemPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? "";
            var paths = systemPath.Split(';', StringSplitOptions.RemoveEmptyEntries);
            return paths.Any(p => p.TrimEnd('\\').Equals(appDir, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    private void AddToSystemPath()
    {
        var appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
        var systemPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? "";

        if (!systemPath.Split(';').Any(p => p.TrimEnd('\\').Equals(appDir, StringComparison.OrdinalIgnoreCase)))
        {
            var newPath = string.IsNullOrEmpty(systemPath) ? appDir : $"{systemPath};{appDir}";
            Environment.SetEnvironmentVariable("PATH", newPath, EnvironmentVariableTarget.Machine);
            AppLogger.Info($"Added to system PATH: {appDir}");
        }
    }

    private void RemoveFromSystemPath()
    {
        var appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
        var systemPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? "";
        var paths = systemPath.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !p.TrimEnd('\\').Equals(appDir, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var newPath = string.Join(";", paths);
        Environment.SetEnvironmentVariable("PATH", newPath, EnvironmentVariableTarget.Machine);
        AppLogger.Info($"Removed from system PATH: {appDir}");
    }

    private void CheckForUpdates_Changed(object sender, RoutedEventArgs e)
    {
        _appSettings.CheckForUpdatesOnStartup = CheckForUpdatesCheckBox.IsChecked == true;
        _appSettings.Save();
    }

    private async void CheckForUpdatesNow_Click(object sender, RoutedEventArgs e)
    {
        CheckForUpdatesButton.IsEnabled = false;
        CheckForUpdatesButton.Content = "Checking...";

        try
        {
            var updateService = new UpdateService();
            var result = await updateService.CheckForUpdateAsync();

            if (!result.Success)
            {
                MessageBox.Show(
                    result.ErrorMessage ?? "Unknown error occurred.",
                    "Update Check Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            else if (result.UpdateAvailable && result.Release != null)
            {
                var dialog = new UpdateDialog(result.Release) { Owner = this };
                dialog.ShowDialog();
            }
            else
            {
                MessageBox.Show(
                    $"You're running the latest version ({UpdateService.GetCurrentVersion()}).",
                    "No Updates Available",
                    MessageBoxButton.OK,
                    MessageBoxImage.None);
            }

            _appSettings.RecordUpdateCheck();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to check for updates: {ex.Message}",
                "Update Check Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            CheckForUpdatesButton.IsEnabled = true;
            CheckForUpdatesButton.Content = "Check Now";
        }
    }

    #endregion

    #region Global Hotkey

    private void InitializeGlobalHotkey()
    {
        if (!_appSettings.AddGameHotkeyEnabled)
        {
            UpdateHotkeyStatus(false);
            return;
        }

        _hotkeyService = new GlobalHotkeyService();
        _hotkeyService.AddGameHotkeyPressed += OnAddGameHotkeyPressed;
        _hotkeyService.Initialize(this, _appSettings.AddGameHotkeyModifiers, _appSettings.AddGameHotkeyKey);

        UpdateHotkeyStatus(_hotkeyService.IsEnabled);
    }

    private void OnAddGameHotkeyPressed()
    {
        Dispatcher.Invoke(() =>
        {
            // Skip if XOutputRedux itself is focused
            if (GlobalHotkeyService.GetForegroundWindowProcessId() == GlobalHotkeyService.GetCurrentProcessId())
            {
                return;
            }

            // Check if profile is running
            if (_runningProfile == null)
            {
                ToastNotificationService.ShowHotkeyError("No profile running - start a profile first");
                return;
            }

            // Get foreground window executable path
            var exePath = GlobalHotkeyService.GetForegroundWindowExecutablePath();
            if (string.IsNullOrEmpty(exePath))
            {
                ToastNotificationService.ShowHotkeyError("Could not detect focused application");
                AppLogger.Warning("Add game hotkey: failed to get foreground window executable path");
                return;
            }

            // Check if game already exists
            var existingGame = _gameManager.GetByExecutablePath(exePath);
            if (existingGame != null)
            {
                ToastNotificationService.ShowHotkeyError($"'{existingGame.Name}' is already configured");
                return;
            }

            // Create new game association
            var gameName = Path.GetFileNameWithoutExtension(exePath);
            var game = new GameAssociation
            {
                Name = gameName,
                ExecutablePath = exePath,
                ProfileName = _runningProfile.Name,
                LaunchDelayMs = 2000 // Default delay
            };

            _gameManager.Add(game);
            RefreshGamesListView();

            ToastNotificationService.ShowGameAddedViaHotkey(gameName, _runningProfile.Name);
            AppLogger.Info($"Added game via hotkey: {gameName} -> {_runningProfile.Name}");
            StatusText.Text = $"Added game: {gameName}";
        });
    }

    private void AddGameHotkeyEnabled_Changed(object sender, RoutedEventArgs e)
    {
        _appSettings.AddGameHotkeyEnabled = AddGameHotkeyEnabledCheckBox.IsChecked == true;
        _appSettings.Save();

        if (_appSettings.AddGameHotkeyEnabled)
        {
            if (_hotkeyService == null)
            {
                InitializeGlobalHotkey();
            }
            else
            {
                _hotkeyService.Register(_appSettings.AddGameHotkeyModifiers, _appSettings.AddGameHotkeyKey);
                UpdateHotkeyStatus(_hotkeyService.IsEnabled);
            }
        }
        else
        {
            _hotkeyService?.Unregister();
            UpdateHotkeyStatus(false);
        }
    }

    private void ChangeHotkey_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new HotkeyInputDialog(_appSettings.AddGameHotkeyModifiers, _appSettings.AddGameHotkeyKey)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            _appSettings.AddGameHotkeyModifiers = dialog.Modifiers;
            _appSettings.AddGameHotkeyKey = dialog.Key;
            _appSettings.Save();

            if (_appSettings.AddGameHotkeyEnabled && _hotkeyService != null)
            {
                bool success = _hotkeyService.Register(_appSettings.AddGameHotkeyModifiers, _appSettings.AddGameHotkeyKey);
                UpdateHotkeyStatus(success);
            }

            UpdateHotkeyDisplayText();
        }
    }

    private void UpdateHotkeyDisplayText()
    {
        HotkeyText.Text = GlobalHotkeyService.FormatHotkey(_appSettings.AddGameHotkeyModifiers, _appSettings.AddGameHotkeyKey);
    }

    private void UpdateHotkeyStatus(bool registered)
    {
        if (registered)
        {
            HotkeyStatusText.Text = "Active";
            HotkeyStatusText.Foreground = new SolidColorBrush(Colors.Green);
        }
        else
        {
            HotkeyStatusText.Text = "Inactive";
            HotkeyStatusText.Foreground = new SolidColorBrush(Colors.Gray);
        }
    }

    #endregion

    #region Stream Deck

    private void InstallStreamDeckPlugin_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Look for the bundled plugin file next to the executable
            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            var pluginPath = Path.Combine(exeDir, "XOutputRedux.streamDeckPlugin");

            if (!File.Exists(pluginPath))
            {
                MessageBox.Show(
                    "Stream Deck plugin file not found.\n\n" +
                    "The plugin should be included with the application. " +
                    "Please reinstall or download the plugin from the GitHub releases page.",
                    "Plugin Not Found",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // Open the plugin file - this will launch Stream Deck's installer
            var startInfo = new ProcessStartInfo
            {
                FileName = pluginPath,
                UseShellExecute = true
            };
            Process.Start(startInfo);

            AppLogger.Info($"Launched Stream Deck plugin installer: {pluginPath}");
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to install Stream Deck plugin", ex);
            MessageBox.Show(
                $"Failed to install Stream Deck plugin: {ex.Message}",
                "Installation Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void BackupSettings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "XOutputRedux Backup|*.xorbackup",
            FileName = BackupRestoreService.GetDefaultBackupFilename(),
            DefaultExt = ".xorbackup",
            Title = "Save Backup"
        };

        if (dialog.ShowDialog() == true)
        {
            BackupSettingsButton.IsEnabled = false;
            RestoreSettingsButton.IsEnabled = false;
            StatusText.Text = "Creating backup...";

            try
            {
                var (success, message) = await BackupRestoreService.CreateBackupAsync(dialog.FileName);
                StatusText.Text = message;

                if (success)
                {
                    ToastNotificationService.ShowBackupCreated(Path.GetFileName(dialog.FileName));
                }
                else
                {
                    MessageBox.Show(message, "Backup Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                BackupSettingsButton.IsEnabled = true;
                RestoreSettingsButton.IsEnabled = true;
            }
        }
    }

    private async void RestoreSettings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "XOutputRedux Backup|*.xorbackup",
            DefaultExt = ".xorbackup",
            Title = "Select Backup to Restore"
        };

        if (dialog.ShowDialog() == true)
        {
            // Confirm with user
            var result = MessageBox.Show(
                "This will replace all current settings and profiles with the backup.\n\n" +
                "Any profiles with the same name will be overwritten.\n\n" +
                "Continue?",
                "Restore Settings",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                BackupSettingsButton.IsEnabled = false;
                RestoreSettingsButton.IsEnabled = false;
                StatusText.Text = "Restoring settings...";

                try
                {
                    var (success, message) = await BackupRestoreService.RestoreBackupAsync(dialog.FileName);
                    StatusText.Text = message;

                    if (success)
                    {
                        ToastNotificationService.ShowBackupRestored();

                        // Reload profiles
                        RefreshProfiles();

                        MessageBox.Show(
                            "Settings restored successfully.\n\n" +
                            "Please restart the application for all settings to take effect.",
                            "Restore Complete",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show(message, "Restore Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                finally
                {
                    BackupSettingsButton.IsEnabled = true;
                    RestoreSettingsButton.IsEnabled = true;
                }
            }
        }
    }

    private async Task CheckForUpdatesOnStartupAsync()
    {
#if DEBUG
        // Skip automatic update checks in debug builds
        AppLogger.Info("Skipping automatic update check (debug build)");
        await Task.CompletedTask; // Suppress warning
        return;
#else
        if (!_appSettings.ShouldCheckForUpdates())
            return;

        try
        {
            var updateService = new UpdateService();
            var result = await updateService.CheckForUpdateAsync();

            if (result.Success && result.UpdateAvailable && result.Release != null)
            {
                // Show update dialog
                var dialog = new UpdateDialog(result.Release) { Owner = this };
                dialog.ShowDialog();
            }
            // Silently ignore errors on startup - don't bother user

            _appSettings.RecordUpdateCheck();
        }
        catch (Exception ex)
        {
            AppLogger.Warning($"Startup update check failed: {ex.Message}");
        }
#endif
    }

    #endregion

    #region Test Tab

    private void UpdateTestTab(XboxControllerState state)
    {
        // Update button visuals
        ButtonA.Fill = state.A ? PressedBrush : ReleasedBrush;
        ButtonB.Fill = state.B ? PressedBrush : ReleasedBrush;
        ButtonX.Fill = state.X ? PressedBrush : ReleasedBrush;
        ButtonY.Fill = state.Y ? PressedBrush : ReleasedBrush;
        ButtonLB.Fill = state.LeftBumper ? PressedBrush : ReleasedBrush;
        ButtonRB.Fill = state.RightBumper ? PressedBrush : ReleasedBrush;
        ButtonBack.Fill = state.Back ? PressedBrush : ReleasedBrush;
        ButtonStart.Fill = state.Start ? PressedBrush : ReleasedBrush;
        ButtonGuide.Fill = state.Guide ? PressedBrush : ReleasedBrush;
        ButtonLS.Fill = state.LeftStick ? PressedBrush : ReleasedBrush;
        ButtonRS.Fill = state.RightStick ? PressedBrush : ReleasedBrush;
        DPadUp.Fill = state.DPadUp ? PressedBrush : ReleasedBrush;
        DPadDown.Fill = state.DPadDown ? PressedBrush : ReleasedBrush;
        DPadLeft.Fill = state.DPadLeft ? PressedBrush : ReleasedBrush;
        DPadRight.Fill = state.DPadRight ? PressedBrush : ReleasedBrush;

        // Update trigger fills (height based on value)
        LeftTriggerFill.Height = state.LeftTrigger * TriggerMaxHeight;
        RightTriggerFill.Height = state.RightTrigger * TriggerMaxHeight;

        // Update stick dot positions
        System.Windows.Controls.Canvas.SetLeft(LeftStickDot,
            LeftStickDotBaseX + (state.LeftStickX - 0.5) * 2 * StickRange);
        System.Windows.Controls.Canvas.SetTop(LeftStickDot,
            LeftStickDotBaseY + (state.LeftStickY - 0.5) * 2 * StickRange);
        System.Windows.Controls.Canvas.SetLeft(RightStickDot,
            RightStickDotBaseX + (state.RightStickX - 0.5) * 2 * StickRange);
        System.Windows.Controls.Canvas.SetTop(RightStickDot,
            RightStickDotBaseY + (state.RightStickY - 0.5) * 2 * StickRange);

        // Update data panel - buttons
        TextA.Text = $"A: {(state.A ? "Pressed" : "-")}";
        TextB.Text = $"B: {(state.B ? "Pressed" : "-")}";
        TextX.Text = $"X: {(state.X ? "Pressed" : "-")}";
        TextY.Text = $"Y: {(state.Y ? "Pressed" : "-")}";
        TextLB.Text = $"LB: {(state.LeftBumper ? "Pressed" : "-")}";
        TextRB.Text = $"RB: {(state.RightBumper ? "Pressed" : "-")}";
        TextBack.Text = $"Back: {(state.Back ? "Pressed" : "-")}";
        TextStart.Text = $"Start: {(state.Start ? "Pressed" : "-")}";
        TextGuide.Text = $"Guide: {(state.Guide ? "Pressed" : "-")}";
        TextLS.Text = $"LS: {(state.LeftStick ? "Pressed" : "-")}";
        TextRS.Text = $"RS: {(state.RightStick ? "Pressed" : "-")}";
        TextDPadUp.Text = $"Up: {(state.DPadUp ? "Pressed" : "-")}";
        TextDPadDown.Text = $"Down: {(state.DPadDown ? "Pressed" : "-")}";
        TextDPadLeft.Text = $"Left: {(state.DPadLeft ? "Pressed" : "-")}";
        TextDPadRight.Text = $"Right: {(state.DPadRight ? "Pressed" : "-")}";

        // Update data panel - triggers
        TextLT.Text = $"{state.LeftTrigger:F2}";
        TextRT.Text = $"{state.RightTrigger:F2}";
        BarLT.Value = state.LeftTrigger * 100;
        BarRT.Value = state.RightTrigger * 100;

        // Update data panel - axes
        TextLSX.Text = $"{state.LeftStickX:F2}";
        TextLSY.Text = $"{state.LeftStickY:F2}";
        TextRSX.Text = $"{state.RightStickX:F2}";
        TextRSY.Text = $"{state.RightStickY:F2}";
        BarLSX.Value = state.LeftStickX * 100;
        BarLSY.Value = state.LeftStickY * 100;
        BarRSX.Value = state.RightStickX * 100;
        BarRSY.Value = state.RightStickY * 100;
    }

    private void ResetTestTab()
    {
        var defaultState = new XboxControllerState();
        UpdateTestTab(defaultState);
    }

    #endregion

    #region About Tab

    private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            AppLogger.Warning($"Failed to open URL: {ex.Message}");
        }
        e.Handled = true;
    }

    #endregion
}
