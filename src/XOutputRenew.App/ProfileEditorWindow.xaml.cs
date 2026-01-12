using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using XOutputRenew.Core.ForceFeedback;
using XOutputRenew.Core.HidHide;
using XOutputRenew.Core.Mapping;
using XOutputRenew.HidHide;
using XOutputRenew.Input;
using XOutputRenew.Input.ForceFeedback;

namespace XOutputRenew.App;

/// <summary>
/// Profile editor window with interactive mapping.
/// </summary>
public partial class ProfileEditorWindow : Window
{
    private readonly MappingProfile _profile;
    private readonly MappingProfile _originalProfile;
    private readonly InputDeviceManager _deviceManager;
    private readonly ObservableCollection<OutputViewModel> _outputs = new();
    private readonly ObservableCollection<string> _inputMonitorItems = new();
    private readonly ObservableCollection<BindingViewModel> _bindings = new();

    private bool _isMonitoring;
    private bool _isCapturing;
    private bool _isListeningForInput;
    private OutputViewModel? _selectedOutput;
    private BindingViewModel? _selectedBinding;
    private readonly DispatcherTimer _captureTimer;
    private readonly DispatcherTimer _outputHighlightTimer;
    private readonly Dictionary<XboxOutput, DateTime> _outputLastActive = new();
    private DateTime _captureStartTime;
    private readonly Dictionary<string, double> _captureBaseline = new(); // device:sourceIndex -> baseline value
    private const int CaptureGracePeriodMs = 300; // Grace period to establish baseline before detecting

    // Force feedback
    private readonly List<FfbDeviceItem> _ffbDevices = new();
    private bool _isLoadingFfbSettings;

    // HidHide
    private readonly HidHideService _hidHideService;
    private readonly ObservableCollection<HidHideDeviceViewModel> _hidHideDevices = new();
    private bool _isLoadingHidHideSettings;

    public bool WasSaved { get; private set; }

    public ProfileEditorWindow(MappingProfile profile, InputDeviceManager deviceManager, HidHideService? hidHideService = null)
    {
        InitializeComponent();

        _originalProfile = profile;
        _profile = profile.Clone();
        _profile.Name = profile.Name; // Keep original name
        _deviceManager = deviceManager;

        // Use passed service or create new one
        _hidHideService = hidHideService ?? new HidHideService();
        if (hidHideService == null)
        {
            _hidHideService.Initialize();
        }

        ProfileNameText.Text = _profile.Name;
        ProfileDescText.Text = _profile.Description ?? "";

        OutputListView.ItemsSource = _outputs;
        InputMonitorList.ItemsSource = _inputMonitorItems;
        BindingListView.ItemsSource = _bindings;
        HidHideDeviceListBox.ItemsSource = _hidHideDevices;

        _captureTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _captureTimer.Tick += CaptureTimer_Tick;

        _outputHighlightTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _outputHighlightTimer.Tick += OutputHighlightTimer_Tick;

        // Wire up input logging to app logger
        InputLogger.LogAction = msg => AppLogger.Info(msg);

        LoadOutputs();
        LoadForceFeedbackSettings();
        LoadHidHideSettings();

        Closed += ProfileEditorWindow_Closed;
        Loaded += (s, e) => DarkModeHelper.EnableDarkTitleBar(this);
    }

    private void LoadOutputs()
    {
        _outputs.Clear();
        foreach (XboxOutput output in Enum.GetValues<XboxOutput>())
        {
            var mapping = _profile.GetMapping(output);
            _outputs.Add(new OutputViewModel(output, mapping));
        }
    }

    private void RefreshOutputs()
    {
        foreach (var vm in _outputs)
        {
            vm.RefreshBindings();
        }
    }

    #region Input Monitoring

    private void StartMonitoring()
    {
        if (_isMonitoring) return;

        _isMonitoring = true;
        _inputMonitorItems.Clear();

        foreach (var device in _deviceManager.Devices)
        {
            device.InputChanged += Device_InputChanged_Monitor;
            device.Start();
        }
    }

    private void StopMonitoring()
    {
        if (!_isMonitoring) return;

        _isMonitoring = false;

        foreach (var device in _deviceManager.Devices)
        {
            device.InputChanged -= Device_InputChanged_Monitor;
            device.Stop();
        }
    }

    private void VerboseLogging_Changed(object sender, RoutedEventArgs e)
    {
        InputLogger.VerboseEnabled = VerboseLoggingCheckBox.IsChecked == true;
        if (InputLogger.VerboseEnabled)
        {
            AppLogger.Info($"Verbose input logging ENABLED - check log file: {AppLogger.GetLogPath()}");
        }
        else
        {
            AppLogger.Info("Verbose input logging disabled");
        }
    }

    private void ListenForInput_Changed(object sender, RoutedEventArgs e)
    {
        _isListeningForInput = ListenForInputCheckBox.IsChecked == true;

        if (_isListeningForInput)
        {
            // Start listening on all devices
            foreach (var device in _deviceManager.Devices)
            {
                device.InputChanged += Device_InputChanged_Listen;
                device.InputChanged += Device_InputChanged_Monitor;
                device.Start();
            }
            _outputHighlightTimer.Start();
            _isMonitoring = true;
            _inputMonitorItems.Clear();
        }
        else
        {
            // Stop listening
            foreach (var device in _deviceManager.Devices)
            {
                device.InputChanged -= Device_InputChanged_Listen;
                device.InputChanged -= Device_InputChanged_Monitor;
                device.Stop();
            }
            _outputHighlightTimer.Stop();
            _outputLastActive.Clear();
            _isMonitoring = false;

            // Clear highlights
            foreach (var vm in _outputs)
            {
                vm.IsActive = false;
            }
        }
    }

    private void Device_InputChanged_Listen(object? sender, InputChangedEventArgs e)
    {
        if (sender is not IInputDevice device) return;

        Dispatcher.BeginInvoke(() =>
        {
            // Check which outputs this input triggers based on current mappings
            foreach (var outputVm in _outputs)
            {
                // Check if this device/source is mapped to this output
                var mapping = outputVm.Mapping;
                var binding = mapping.Bindings.FirstOrDefault(b =>
                    b.DeviceId == device.UniqueId && b.SourceIndex == e.Source.Index);

                if (binding != null)
                {
                    // Determine if the input is "active" based on output type
                    bool isActive = false;
                    double value = binding.Invert ? 1.0 - e.NewValue : e.NewValue;

                    if (outputVm.Output.IsButton())
                    {
                        isActive = value >= binding.ButtonThreshold;
                    }
                    else if (outputVm.Output.IsTrigger())
                    {
                        isActive = value > 0.1; // Trigger is active if pressed at all
                    }
                    else if (outputVm.Output.IsAxis())
                    {
                        // Axis is active if it deviates from center (0.5)
                        isActive = Math.Abs(value - 0.5) > 0.15;
                    }

                    if (isActive)
                    {
                        outputVm.IsActive = true;
                        _outputLastActive[outputVm.Output] = DateTime.Now;
                    }
                }
            }
        });
    }

    private void OutputHighlightTimer_Tick(object? sender, EventArgs e)
    {
        var now = DateTime.Now;
        var timeout = TimeSpan.FromMilliseconds(300);

        foreach (var vm in _outputs)
        {
            if (_outputLastActive.TryGetValue(vm.Output, out var lastActive))
            {
                if (now - lastActive > timeout)
                {
                    vm.IsActive = false;
                }
            }
        }
    }

    private void Device_InputChanged_Monitor(object? sender, InputChangedEventArgs e)
    {
        if (sender is not IInputDevice device) return;

        Dispatcher.BeginInvoke(() =>
        {
            var line = $"{device.Name}: {e.Source.Name} = {e.NewValue:F2}";
            _inputMonitorItems.Insert(0, line);

            // Keep list manageable
            while (_inputMonitorItems.Count > 50)
            {
                _inputMonitorItems.RemoveAt(_inputMonitorItems.Count - 1);
            }

            // If capturing, check for significant input
            if (_isCapturing && _selectedOutput != null)
            {
                bool isSignificant = IsSignificantInput(device, e, _selectedOutput.Output);
                if (isSignificant)
                {
                    CaptureBinding(device, e.Source);
                }
            }
        });
    }

    private bool IsSignificantInput(IInputDevice device, InputChangedEventArgs e, XboxOutput output)
    {
        var key = $"{device.UniqueId}:{e.Source.Index}";
        var elapsed = DateTime.Now - _captureStartTime;

        // During grace period, just update baselines but don't trigger
        // This allows initial device values to settle before detection
        if (elapsed.TotalMilliseconds < CaptureGracePeriodMs)
        {
            _captureBaseline[key] = e.NewValue;
            return false;
        }

        // For buttons: value crosses threshold (high)
        if (output.IsButton())
        {
            return e.NewValue > 0.7;
        }

        // For axes/triggers: check for significant CHANGE from baseline
        // This prevents jittery axes from triggering capture
        if (!_captureBaseline.TryGetValue(key, out var baseline))
        {
            // First reading after grace period - record as baseline, don't trigger
            _captureBaseline[key] = e.NewValue;
            return false;
        }

        double delta = Math.Abs(e.NewValue - baseline);

        if (output.IsAxis())
        {
            // Axis needs to move significantly from where it was when capture started
            return delta > 0.4;
        }

        if (output.IsTrigger())
        {
            // Trigger needs to be pressed significantly from its baseline
            return delta > 0.4 && e.NewValue > 0.5;
        }

        return false;
    }

    #endregion

    #region Capture Binding

    private void CaptureInput_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedOutput == null) return;

        if (_isCapturing)
        {
            StopCapturing();
            return;
        }

        StartCapturing();
    }

    private void StartCapturing()
    {
        _isCapturing = true;
        _captureStartTime = DateTime.Now;
        _captureBaseline.Clear(); // Will be populated during grace period

        CaptureButton.Content = "Cancel Capture";
        CaptureButton.Background = new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36));
        CaptureHintText.Text = "Press a button or move an axis on your controller...";
        CaptureHintText.Foreground = new SolidColorBrush(Colors.Blue);

        _captureTimer.Start();

        // Start monitoring if not already
        if (!_isMonitoring)
        {
            StartMonitoring();
        }
    }

    private void StopCapturing()
    {
        _isCapturing = false;
        _captureTimer.Stop();
        CaptureButton.Content = "Capture Input";
        CaptureButton.Background = new SolidColorBrush(Color.FromRgb(0x21, 0x96, 0xF3));
        CaptureHintText.Text = "";
        CaptureHintText.Foreground = new SolidColorBrush(Colors.Gray);
    }

    private void CaptureTimer_Tick(object? sender, EventArgs e)
    {
        var elapsed = DateTime.Now - _captureStartTime;
        if (elapsed.TotalSeconds > 10)
        {
            StopCapturing();
            CaptureHintText.Text = "Capture timed out. Try again.";
            CaptureHintText.Foreground = new SolidColorBrush(Colors.Red);
        }
        else
        {
            var remaining = 10 - (int)elapsed.TotalSeconds;
            CaptureButton.Content = $"Cancel ({remaining}s)";
        }
    }

    private void CaptureBinding(IInputDevice device, IInputSource source)
    {
        if (_selectedOutput == null) return;

        StopCapturing();

        // Check if binding already exists
        var existingBinding = _selectedOutput.Mapping.Bindings
            .FirstOrDefault(b => b.DeviceId == device.UniqueId && b.SourceIndex == source.Index);

        if (existingBinding != null)
        {
            CaptureHintText.Text = "This input is already mapped to this output.";
            CaptureHintText.Foreground = new SolidColorBrush(Colors.Orange);
            return;
        }

        // Add the binding
        var binding = new InputBinding
        {
            DeviceId = device.UniqueId,
            SourceIndex = source.Index,
            DisplayName = $"{device.Name}: {source.Name}"
        };

        _profile.AddBinding(_selectedOutput.Output, binding);
        _selectedOutput.RefreshBindings();
        RefreshBindingsList();

        CaptureHintText.Text = $"Mapped: {binding.DisplayName}";
        CaptureHintText.Foreground = new SolidColorBrush(Colors.Green);
    }

    #endregion

    #region Output Selection

    private void OutputListView_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        _selectedOutput = OutputListView.SelectedItem as OutputViewModel;

        // Clear the capture hint text when switching outputs
        CaptureHintText.Text = "";
        CaptureHintText.Foreground = new SolidColorBrush(Colors.Gray);

        if (_selectedOutput != null)
        {
            CaptureButton.IsEnabled = true;
            CaptureStatusText.Text = $"Ready to capture for: {_selectedOutput.OutputName}";
            RefreshBindingsList();
        }
        else
        {
            CaptureButton.IsEnabled = false;
            CaptureStatusText.Text = "Select an output from the list";
            _bindings.Clear();
        }

        if (_isCapturing)
        {
            StopCapturing();
        }
    }

    private void ClearBindings_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedOutput == null) return;

        // Clear all bindings for the selected output
        var bindingsToRemove = _selectedOutput.Mapping.Bindings.ToList();
        foreach (var binding in bindingsToRemove)
        {
            _profile.RemoveBinding(_selectedOutput.Output, binding);
        }

        _selectedOutput.RefreshBindings();
        RefreshBindingsList();
    }

    private void RefreshBindingsList()
    {
        _bindings.Clear();
        if (_selectedOutput == null) return;

        foreach (var binding in _selectedOutput.Mapping.Bindings)
        {
            _bindings.Add(new BindingViewModel(binding, _selectedOutput.Output));
        }
    }

    #endregion

    #region Binding Editing

    private void BindingListView_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        _selectedBinding = BindingListView.SelectedItem as BindingViewModel;

        if (_selectedBinding != null)
        {
            InvertCheckBox.IsEnabled = true;
            ThresholdSlider.IsEnabled = _selectedOutput?.Output.IsButton() == true;

            // Load current values
            InvertCheckBox.IsChecked = _selectedBinding.Binding.Invert;
            ThresholdSlider.Value = _selectedBinding.Binding.ButtonThreshold;
        }
        else
        {
            InvertCheckBox.IsEnabled = false;
            ThresholdSlider.IsEnabled = false;
        }
    }

    private void InvertCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_selectedBinding != null)
        {
            _selectedBinding.Binding.Invert = InvertCheckBox.IsChecked == true;
        }
    }

    private void ThresholdSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_selectedBinding != null)
        {
            _selectedBinding.Binding.ButtonThreshold = ThresholdSlider.Value;
        }
    }

    private void InvertHelp_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        MessageBox.Show(
            "Invert flips the input value:\n\n" +
            "- For axes: Left becomes right, up becomes down\n" +
            "- For triggers/buttons: Pressed becomes released\n\n" +
            "Select a binding from the list above to enable this option.",
            "Invert Help",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void ThresholdHelp_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        MessageBox.Show(
            "Threshold sets the activation point when mapping an analog input to a button.\n\n" +
            "For example, if you map a trigger (0-100%) to the A button:\n" +
            "- Threshold 0.5 = Button pressed when trigger is >50%\n" +
            "- Threshold 0.2 = Button pressed when trigger is >20%\n\n" +
            "This setting only applies when mapping to button outputs.\n" +
            "Select a binding from the list above to enable this option.",
            "Threshold Help",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void RemoveBinding_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button) return;
        if (button.Tag is not BindingViewModel bindingVm) return;
        if (_selectedOutput == null) return;

        _profile.RemoveBinding(_selectedOutput.Output, bindingVm.Binding);
        _selectedOutput.RefreshBindings();
        RefreshBindingsList();
    }

    #endregion

    #region Force Feedback Settings

    private void LoadForceFeedbackSettings()
    {
        _isLoadingFfbSettings = true;

        // Populate FFB device combo with devices that support FFB
        _ffbDevices.Clear();
        FfbDeviceComboBox.Items.Clear();

        // Add "None" option
        var noneItem = new FfbDeviceItem { DeviceId = null, DisplayName = "(None)" };
        _ffbDevices.Add(noneItem);
        FfbDeviceComboBox.Items.Add(new ComboBoxItem { Content = noneItem.DisplayName, Tag = noneItem });

        foreach (var device in _deviceManager.Devices)
        {
            if (device is IForceFeedbackDevice ffbDevice && ffbDevice.SupportsForceFeedback)
            {
                var item = new FfbDeviceItem { DeviceId = device.UniqueId, DisplayName = device.Name };
                _ffbDevices.Add(item);
                FfbDeviceComboBox.Items.Add(new ComboBoxItem { Content = item.DisplayName, Tag = item });
            }
        }

        // Load current settings
        var settings = _profile.ForceFeedbackSettings;
        if (settings != null)
        {
            FfbEnabledCheckBox.IsChecked = settings.Enabled;
            UpdateFfbControlsEnabled(settings.Enabled);

            // Select the target device
            for (int i = 0; i < _ffbDevices.Count; i++)
            {
                if (_ffbDevices[i].DeviceId == settings.TargetDeviceId)
                {
                    FfbDeviceComboBox.SelectedIndex = i;
                    break;
                }
            }

            // Select the mode
            string modeTag = settings.Mode.ToString();
            for (int i = 0; i < FfbModeComboBox.Items.Count; i++)
            {
                if (FfbModeComboBox.Items[i] is ComboBoxItem item &&
                    item.Tag?.ToString() == modeTag)
                {
                    FfbModeComboBox.SelectedIndex = i;
                    break;
                }
            }

            FfbGainSlider.Value = settings.Gain;
            FfbGainText.Text = $"{(int)(settings.Gain * 100)}%";
        }
        else
        {
            // Defaults
            FfbEnabledCheckBox.IsChecked = false;
            UpdateFfbControlsEnabled(false);
            FfbDeviceComboBox.SelectedIndex = 0;
            FfbModeComboBox.SelectedIndex = 2; // Combined
            FfbGainSlider.Value = 1.0;
            FfbGainText.Text = "100%";
        }

        _isLoadingFfbSettings = false;
    }

    private void UpdateFfbControlsEnabled(bool enabled)
    {
        FfbDeviceComboBox.IsEnabled = enabled;
        FfbModeComboBox.IsEnabled = enabled;
        FfbGainSlider.IsEnabled = enabled;
    }

    private void FfbEnabled_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingFfbSettings) return;

        bool enabled = FfbEnabledCheckBox.IsChecked == true;
        UpdateFfbControlsEnabled(enabled);
        UpdateFfbSettings();
    }

    private void FfbDevice_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingFfbSettings) return;
        UpdateFfbSettings();
    }

    private void FfbMode_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingFfbSettings) return;
        UpdateFfbSettings();
    }

    private void FfbGain_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // FfbGainText may be null during XAML initialization
        if (_isLoadingFfbSettings || FfbGainText == null) return;

        FfbGainText.Text = $"{(int)(FfbGainSlider.Value * 100)}%";
        UpdateFfbSettings();
    }

    private void MotorModeHelp_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        MessageBox.Show(
            "Xbox controllers have two rumble motors that games use differently:\n\n" +
            "LARGE MOTOR (Low Frequency)\n" +
            "• Heavy, deep rumble for impacts and collisions\n" +
            "• Used for: crashes, explosions, engine rumble\n" +
            "• Best for: steering wheels, bass shakers\n\n" +
            "SMALL MOTOR (High Frequency)\n" +
            "• Light, buzzy vibration for feedback\n" +
            "• Used for: road texture, gunfire, alerts\n" +
            "• Best for: subtle tactile feedback\n\n" +
            "MODE OPTIONS:\n\n" +
            "• Large Motor - Only use the large motor signal\n" +
            "  Best for wheels where you want strong, clear effects\n\n" +
            "• Small Motor - Only use the small motor signal\n" +
            "  Best for feeling subtle details like road texture\n\n" +
            "• Combined (Recommended) - Use whichever motor is stronger\n" +
            "  Best overall experience, captures all game feedback\n\n" +
            "• Swap - Use small motor signal as primary\n" +
            "  For games that incorrectly use the small motor for main effects",
            "Motor Mode Help",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void UpdateFfbSettings()
    {
        if (_profile.ForceFeedbackSettings == null)
        {
            _profile.ForceFeedbackSettings = new ForceFeedbackSettings();
        }

        var settings = _profile.ForceFeedbackSettings;
        settings.Enabled = FfbEnabledCheckBox.IsChecked == true;

        // Get selected device
        if (FfbDeviceComboBox.SelectedItem is ComboBoxItem deviceItem &&
            deviceItem.Tag is FfbDeviceItem ffbDevice)
        {
            settings.TargetDeviceId = ffbDevice.DeviceId;
        }

        // Get selected mode
        if (FfbModeComboBox.SelectedItem is ComboBoxItem modeItem &&
            modeItem.Tag is string modeTag)
        {
            settings.Mode = Enum.Parse<ForceFeedbackMode>(modeTag);
        }

        settings.Gain = FfbGainSlider.Value;
    }

    #endregion

    #region HidHide Settings

    private void LoadHidHideSettings()
    {
        _isLoadingHidHideSettings = true;

        // Check if HidHide is available
        if (!_hidHideService.IsAvailable)
        {
            HidHideStatusText.Text = "HidHide is not installed. Install from nefarius.at/HidHide";
            HidHideEnabledCheckBox.IsEnabled = false;
            HidHideDeviceListBox.IsEnabled = false;
            _isLoadingHidHideSettings = false;
            return;
        }

        var version = _hidHideService.Version;
        HidHideStatusText.Text = version != null && version != "Installed"
            ? $"HidHide v{version} detected"
            : "HidHide detected";
        HidHideEnabledCheckBox.IsEnabled = true;

        // Populate device list from HidHide gaming devices
        _hidHideDevices.Clear();
        var gamingDevices = _hidHideService.GetGamingDevices().ToList();

        if (gamingDevices.Count == 0)
        {
            HidHideStatusText.Text += " - No devices found. If you just installed HidHide, a reboot may be required.";
        }
        else
        {
            foreach (var device in gamingDevices.Where(d => d.Present))
            {
                var displayName = !string.IsNullOrEmpty(device.Product)
                    ? $"{device.Product} ({device.Vendor})"
                    : device.DeviceInstancePath ?? "Unknown Device";

                _hidHideDevices.Add(new HidHideDeviceViewModel
                {
                    DeviceInstancePath = device.DeviceInstancePath ?? "",
                    DisplayName = displayName,
                    IsSelected = false
                });
            }
        }

        // Load current settings
        var settings = _profile.HidHideSettings;
        if (settings != null)
        {
            HidHideEnabledCheckBox.IsChecked = settings.Enabled;
            UpdateHidHideControlsEnabled(settings.Enabled);

            // Mark devices that are in the profile's hide list
            foreach (var deviceVm in _hidHideDevices)
            {
                deviceVm.IsSelected = settings.DevicesToHide.Contains(deviceVm.DeviceInstancePath);
            }
        }
        else
        {
            HidHideEnabledCheckBox.IsChecked = false;
            UpdateHidHideControlsEnabled(false);
        }

        _isLoadingHidHideSettings = false;
    }

    private void UpdateHidHideControlsEnabled(bool enabled)
    {
        HidHideDeviceListBox.IsEnabled = enabled;
    }

    private void HidHideEnabled_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingHidHideSettings) return;

        bool enabled = HidHideEnabledCheckBox.IsChecked == true;
        UpdateHidHideControlsEnabled(enabled);
        UpdateHidHideSettings();
    }

    private void HidHideDevice_CheckChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoadingHidHideSettings) return;
        UpdateHidHideSettings();
    }

    private void HidHideHelp_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        MessageBox.Show(
            "HidHide prevents games from seeing your physical controllers, so they only see " +
            "the virtual Xbox controller created by XOutputRenew.\n\n" +
            "WHY USE IT?\n" +
            "Without HidHide, games may see both your physical device AND the virtual controller, " +
            "causing double input or conflicts.\n\n" +
            "HOW IT WORKS:\n" +
            "1. Select devices to hide from this list\n" +
            "2. When you start the profile, those devices become invisible to games\n" +
            "3. When you stop the profile, devices become visible again\n" +
            "4. XOutputRenew is automatically whitelisted so it can still read your devices\n\n" +
            "REQUIREMENTS:\n" +
            "• HidHide driver must be installed (nefarius.at/HidHide)\n" +
            "• No admin rights required after installation",
            "Device Hiding Help",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void UpdateHidHideSettings()
    {
        if (_profile.HidHideSettings == null)
        {
            _profile.HidHideSettings = new HidHideSettings();
        }

        var settings = _profile.HidHideSettings;
        settings.Enabled = HidHideEnabledCheckBox.IsChecked == true;

        // Get selected devices
        settings.DevicesToHide.Clear();
        foreach (var deviceVm in _hidHideDevices.Where(d => d.IsSelected))
        {
            settings.DevicesToHide.Add(deviceVm.DeviceInstancePath);
        }
    }

    #endregion

    #region Save/Cancel

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        // Copy bindings from edited profile back to original
        _originalProfile.ClearAllBindings();
        foreach (var output in Enum.GetValues<XboxOutput>())
        {
            foreach (var binding in _profile.GetMapping(output).Bindings)
            {
                _originalProfile.AddBinding(output, new InputBinding
                {
                    DeviceId = binding.DeviceId,
                    SourceIndex = binding.SourceIndex,
                    DisplayName = binding.DisplayName,
                    Invert = binding.Invert,
                    MinValue = binding.MinValue,
                    MaxValue = binding.MaxValue,
                    ButtonThreshold = binding.ButtonThreshold
                });
            }
        }

        // Copy force feedback settings
        if (_profile.ForceFeedbackSettings != null)
        {
            _originalProfile.ForceFeedbackSettings = _profile.ForceFeedbackSettings.Clone();
        }
        else
        {
            _originalProfile.ForceFeedbackSettings = null;
        }

        // Copy HidHide settings
        if (_profile.HidHideSettings != null)
        {
            _originalProfile.HidHideSettings = _profile.HidHideSettings.Clone();
        }
        else
        {
            _originalProfile.HidHideSettings = null;
        }

        WasSaved = true;
        StopMonitoring();
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        WasSaved = false;
        StopMonitoring();
        Close();
    }

    private void ProfileEditorWindow_Closed(object? sender, EventArgs e)
    {
        StopMonitoring();
        StopCapturing();
    }

    #endregion
}

/// <summary>
/// View model for Xbox output in the editor.
/// </summary>
public class OutputViewModel : System.ComponentModel.INotifyPropertyChanged
{
    public XboxOutput Output { get; }
    public OutputMapping Mapping { get; }

    public string OutputName => Output.ToString();
    public string OutputType => Output.IsButton() ? "Button" : Output.IsAxis() ? "Axis" : "Trigger";

    private string _bindingsSummary = "";
    public string BindingsSummary
    {
        get => _bindingsSummary;
        private set
        {
            _bindingsSummary = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(BindingsSummary)));
        }
    }

    private bool _isActive;
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive != value)
            {
                _isActive = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsActive)));
            }
        }
    }

    public OutputViewModel(XboxOutput output, OutputMapping mapping)
    {
        Output = output;
        Mapping = mapping;
        RefreshBindings();
    }

    public void RefreshBindings()
    {
        if (Mapping.Bindings.Count == 0)
        {
            BindingsSummary = "(not mapped)";
        }
        else if (Mapping.Bindings.Count == 1)
        {
            BindingsSummary = Mapping.Bindings[0].DisplayName ?? "1 binding";
        }
        else
        {
            BindingsSummary = $"{Mapping.Bindings.Count} bindings (OR)";
        }
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// View model for a binding in the editor.
/// </summary>
public class BindingViewModel
{
    public InputBinding Binding { get; }
    public XboxOutput Output { get; }

    public string DisplayName => Binding.DisplayName ?? $"Source {Binding.SourceIndex}";

    public BindingViewModel(InputBinding binding, XboxOutput output)
    {
        Binding = binding;
        Output = output;
    }
}

/// <summary>
/// Helper class for FFB device combo items.
/// </summary>
public class FfbDeviceItem
{
    public string? DeviceId { get; set; }
    public string DisplayName { get; set; } = "";
}

/// <summary>
/// View model for HidHide device selection.
/// </summary>
public class HidHideDeviceViewModel : INotifyPropertyChanged
{
    public string DeviceInstancePath { get; set; } = "";
    public string DisplayName { get; set; } = "";

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

