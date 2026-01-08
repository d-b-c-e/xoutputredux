using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using XOutputRenew.App.ViewModels;
using XOutputRenew.Core.Mapping;
using XOutputRenew.Emulation;
using XOutputRenew.HidHide;
using XOutputRenew.Input;

namespace XOutputRenew.App;

/// <summary>
/// Main application window.
/// </summary>
public partial class MainWindow : Window
{
    private readonly InputDeviceManager _deviceManager;
    private readonly ProfileManager _profileManager;
    private readonly ViGEmService _vigemService;
    private readonly HidHideService _hidHideService;

    private readonly ObservableCollection<DeviceViewModel> _devices = new();
    private readonly ObservableCollection<ProfileViewModel> _profiles = new();

    private XboxController? _activeController;
    private MappingEngine? _activeMappingEngine;
    private ProfileViewModel? _runningProfile;
    private bool _isExiting;

    public MainWindow()
    {
        InitializeComponent();

        // Initialize services
        _deviceManager = new InputDeviceManager();
        _profileManager = new ProfileManager(ProfileManager.GetDefaultProfilesDirectory());
        _vigemService = new ViGEmService();
        _hidHideService = new HidHideService();

        // Bind collections
        DeviceListView.ItemsSource = _devices;
        ProfileListView.ItemsSource = _profiles;

        // Load data
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshDevices();
        RefreshProfiles();
        CheckDriverStatus();

        // Check for startup profile
        if (Application.Current.Properties["StartProfile"] is string startProfile && !string.IsNullOrEmpty(startProfile))
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
            WindowState = WindowState.Minimized;
            Hide();
        }
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
        }
        else
        {
            ViGEmStatusText.Text = "Not Installed";
            ViGEmStatusText.Foreground = new SolidColorBrush(Colors.Red);
            ViGEmInfoText.Text = "Install ViGEmBus from https://github.com/nefarius/ViGEmBus/releases";
        }

        // Check HidHide
        _hidHideService.Initialize();
        bool hidHideInstalled = _hidHideService.IsAvailable;
        if (hidHideInstalled)
        {
            HidHideStatusText.Text = "Installed";
            HidHideStatusText.Foreground = new SolidColorBrush(Colors.Green);
            HidHideInfoText.Text = "Device hiding available.";
        }
        else
        {
            HidHideStatusText.Text = "Not Installed";
            HidHideStatusText.Foreground = new SolidColorBrush(Colors.Orange);
            HidHideInfoText.Text = "Optional: Install HidHide for device hiding.";
        }
    }

    private void RefreshDevices()
    {
        _devices.Clear();
        _deviceManager.RefreshDevices();

        foreach (var device in _deviceManager.Devices)
        {
            _devices.Add(new DeviceViewModel(device));
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

        // Restore selection
        if (selectedFileName != null)
        {
            ProfileListView.SelectedItem = _profiles.FirstOrDefault(p => p.FileName == selectedFileName);
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

    #endregion

    #region Profile Events

    private void ProfileListView_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        bool hasSelection = ProfileListView.SelectedItem != null;
        EditButton.IsEnabled = hasSelection;
        DuplicateButton.IsEnabled = hasSelection;
        DeleteButton.IsEnabled = hasSelection;
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

        if (selected.IsRunning)
        {
            MessageBox.Show("Cannot edit a running profile. Stop it first.", "Edit Profile",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var editor = new ProfileEditorWindow(selected.Profile, _deviceManager);
        editor.Owner = this;
        editor.ShowDialog();

        if (editor.WasSaved)
        {
            // Save the profile to disk
            _profileManager.SaveProfile(selected.FileName, selected.Profile);
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

            profile.IsRunning = true;
            _runningProfile = profile;
            UpdateStartStopButton();
            ActiveProfileText.Text = $"Running: {profile.Name}";
            StatusText.Text = $"Started profile: {profile.Name}";
            TrayIcon.ToolTipText = $"XOutputRenew - {profile.Name}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to start profile: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            StopProfile();
        }
    }

    private void StopProfile()
    {
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
            _runningProfile.IsRunning = false;
            StatusText.Text = $"Stopped profile: {_runningProfile.Name}";
            _runningProfile = null;
        }

        UpdateStartStopButton();
        ActiveProfileText.Text = "No profile running";
        TrayIcon.ToolTipText = "XOutputRenew";
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

    #endregion

    #region System Tray

    private void TrayIcon_Show_Click(object sender, RoutedEventArgs e)
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
        if (!_isExiting)
        {
            // Minimize to tray instead of closing
            e.Cancel = true;
            WindowState = WindowState.Minimized;
            return;
        }

        // Actually closing - cleanup
        StopProfile();
        TrayIcon.Dispose();
        _deviceManager.Dispose();
        _vigemService.Dispose();
    }

    #endregion
}
