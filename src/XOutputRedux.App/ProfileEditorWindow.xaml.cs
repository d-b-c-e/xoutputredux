using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Text.Json.Nodes;
using XOutputRedux.Core.ForceFeedback;
using XOutputRedux.Core.HidHide;
using XOutputRedux.Core.Mapping;
using XOutputRedux.Core.Plugins;
using XOutputRedux.HidHide;
using XOutputRedux.Input;
using XOutputRedux.Input.ForceFeedback;

namespace XOutputRedux.App;

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
    private readonly Dictionary<(string deviceId, int sourceIndex), double> _latestInputValues = new();
    private const int CaptureGracePeriodMs = 300; // Grace period to establish baseline before detecting

    // Force feedback
    private readonly List<FfbDeviceItem> _ffbDevices = new();
    private bool _isLoadingFfbSettings;

    // HidHide
    private readonly HidHideService _hidHideService;
    private readonly DeviceSettings? _deviceSettings;
    private readonly ObservableCollection<HidHideDeviceViewModel> _hidHideDevices = new();
    private readonly ObservableCollection<WhitelistItem> _whitelistItems = new();
    private bool _isLoadingHidHideSettings;

    // Plugins
    private readonly IReadOnlyList<IXOutputPlugin> _plugins;

    public bool WasSaved { get; private set; }
    private readonly bool _isReadOnly;

    public ProfileEditorWindow(MappingProfile profile, InputDeviceManager deviceManager, HidHideService? hidHideService = null, DeviceSettings? deviceSettings = null, bool readOnly = false, IReadOnlyList<IXOutputPlugin>? plugins = null)
    {
        InitializeComponent();

        _originalProfile = profile;
        _profile = profile.Clone();
        _profile.Name = profile.Name; // Keep original name
        _deviceManager = deviceManager;
        _deviceSettings = deviceSettings;
        _isReadOnly = readOnly;
        _plugins = plugins ?? Array.Empty<IXOutputPlugin>();

        // Use passed service or create new one
        _hidHideService = hidHideService ?? new HidHideService();
        if (hidHideService == null)
        {
            _hidHideService.Initialize();
        }

        ProfileNameText.Text = _profile.Name;
        ProfileDescText.Text = _profile.Description ?? "";
        DefaultProfileCheckBox.IsChecked = _profile.IsDefault;

        OutputListView.ItemsSource = _outputs;
        InputMonitorList.ItemsSource = _inputMonitorItems;
        BindingListView.ItemsSource = _bindings;
        HidHideDeviceListBox.ItemsSource = _hidHideDevices;
        WhitelistListBox.ItemsSource = _whitelistItems;

        _captureTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _captureTimer.Tick += CaptureTimer_Tick;

        _outputHighlightTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _outputHighlightTimer.Tick += OutputHighlightTimer_Tick;

        // Wire up input logging to app logger
        InputLogger.LogAction = msg => AppLogger.Info(msg);

        LoadOutputs();
        LoadForceFeedbackSettings();
        LoadHidHideSettings();
        LoadPluginTabs();

        // Apply read-only mode
        if (_isReadOnly)
        {
            Title = "View Profile (Read Only)";
            SaveButton.IsEnabled = false;
            SaveButton.Content = "View Only";
            ReadOnlyBanner.Visibility = Visibility.Visible;
            CaptureButton.IsEnabled = false;
        }

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

        _latestInputValues.Clear();
    }

    private void DefaultProfile_Changed(object sender, RoutedEventArgs e)
    {
        _profile.IsDefault = DefaultProfileCheckBox.IsChecked == true;
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

        // Track latest values for axis capture
        _latestInputValues[(device.UniqueId, e.Source.Index)] = e.NewValue;

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

    private void OutputListView_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // Don't capture in read-only mode
        if (_isReadOnly) return;

        // Double-click triggers capture if an output is selected
        if (_selectedOutput != null && !_isCapturing)
        {
            StartCapturing();
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
            InvertCheckBox.IsEnabled = !_isReadOnly;
            ThresholdSlider.IsEnabled = !_isReadOnly && _selectedOutput?.Output.IsButton() == true;

            // Load current values
            InvertCheckBox.IsChecked = _selectedBinding.Binding.Invert;
            ThresholdSlider.Value = _selectedBinding.Binding.ButtonThreshold;

            // Show advanced settings for axes and triggers
            var isAxisOrTrigger = _selectedOutput?.Output.IsAxis() == true ||
                                  _selectedOutput?.Output.IsTrigger() == true;
            AdvancedSettingsExpander.Visibility = isAxisOrTrigger
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;

            if (isAxisOrTrigger)
            {
                MinValueTextBox.Text = _selectedBinding.Binding.MinValue.ToString("F4");
                MaxValueTextBox.Text = _selectedBinding.Binding.MaxValue.ToString("F4");
                MinValueTextBox.IsEnabled = !_isReadOnly;
                MaxValueTextBox.IsEnabled = !_isReadOnly;
                CaptureMinButton.IsEnabled = !_isReadOnly;
                CaptureMaxButton.IsEnabled = !_isReadOnly;
                ResetRangeButton.IsEnabled = !_isReadOnly;
                RangeHintText.Text = "";

                // Load sensitivity
                _updatingSensitivitySlider = true;
                SensitivitySlider.Value = _selectedBinding.Binding.Sensitivity;
                SensitivityValueText.Text = _selectedBinding.Binding.Sensitivity.ToString("F2");
                _updatingSensitivitySlider = false;
                SensitivitySlider.IsEnabled = !_isReadOnly;
                ResetSensitivityButton.IsEnabled = !_isReadOnly;
                UpdateCurvePreview();
            }

            AxisTuningPanel.Visibility = isAxisOrTrigger
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
        }
        else
        {
            InvertCheckBox.IsEnabled = false;
            ThresholdSlider.IsEnabled = false;
            AdvancedSettingsExpander.Visibility = System.Windows.Visibility.Collapsed;
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
        HelpDialog.Show(
            "Invert flips the input value:\n\n" +
            "- For axes: Left becomes right, up becomes down\n" +
            "- For triggers/buttons: Pressed becomes released\n\n" +
            "Select a binding from the list above to enable this option.",
            "Invert Help",
            this);
    }

    private void ThresholdHelp_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        HelpDialog.Show(
            "Threshold sets the activation point when mapping an analog input to a button.\n\n" +
            "For example, if you map a trigger (0-100%) to the A button:\n" +
            "- Threshold 0.5 = Button pressed when trigger is >50%\n" +
            "- Threshold 0.2 = Button pressed when trigger is >20%\n\n" +
            "This setting only applies when mapping to button outputs.\n" +
            "Select a binding from the list above to enable this option.",
            "Threshold Help",
            this);
    }

    private void MinValueTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_selectedBinding == null) return;
        if (double.TryParse(MinValueTextBox.Text, out double val))
        {
            _selectedBinding.Binding.MinValue = Math.Clamp(val, 0.0, 1.0);
        }
        MinValueTextBox.Text = _selectedBinding.Binding.MinValue.ToString("F4");
    }

    private void MaxValueTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_selectedBinding == null) return;
        if (double.TryParse(MaxValueTextBox.Text, out double val))
        {
            _selectedBinding.Binding.MaxValue = Math.Clamp(val, 0.0, 1.0);
        }
        MaxValueTextBox.Text = _selectedBinding.Binding.MaxValue.ToString("F4");
    }

    private void CaptureMinButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedBinding == null) return;

        var liveValue = GetLiveInputValue(_selectedBinding.Binding);
        if (liveValue.HasValue)
        {
            _selectedBinding.Binding.MinValue = liveValue.Value;
            MinValueTextBox.Text = liveValue.Value.ToString("F4");
            RangeHintText.Text = $"Captured min: {liveValue.Value:F4}";
            RangeHintText.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x4C, 0xAF, 0x50));
        }
        else
        {
            RangeHintText.Text = "Enable 'Listen for Input' first, then move axis to position";
            RangeHintText.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0xEF, 0x53, 0x50));
        }
    }

    private void CaptureMaxButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedBinding == null) return;

        var liveValue = GetLiveInputValue(_selectedBinding.Binding);
        if (liveValue.HasValue)
        {
            _selectedBinding.Binding.MaxValue = liveValue.Value;
            MaxValueTextBox.Text = liveValue.Value.ToString("F4");
            RangeHintText.Text = $"Captured max: {liveValue.Value:F4}";
            RangeHintText.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x4C, 0xAF, 0x50));
        }
        else
        {
            RangeHintText.Text = "Enable 'Listen for Input' first, then move axis to position";
            RangeHintText.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0xEF, 0x53, 0x50));
        }
    }

    private void ResetRangeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedBinding == null) return;
        _selectedBinding.Binding.MinValue = 0.0;
        _selectedBinding.Binding.MaxValue = 1.0;
        MinValueTextBox.Text = "0.0000";
        MaxValueTextBox.Text = "1.0000";
        RangeHintText.Text = "Range reset to defaults";
        RangeHintText.Foreground = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0x90, 0x90, 0x90));
    }

    private void InputRangeHelp_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        HelpDialog.Show(
            "Input Range remaps a portion of the physical axis to the full output range.\n\n" +
            "WHY USE IT?\n" +
            "If your wheel rotation is set to 270\u00b0 but the axis is calibrated for 1080\u00b0, " +
            "the axis only reaches ~25% at full lock. Setting Min and Max to the actual " +
            "range remaps it to the full 0-100% output.\n\n" +
            "HOW TO USE:\n" +
            "1. Check 'Listen for Input' in the Input Monitor above\n" +
            "2. Select the axis binding from the list\n" +
            "3. Move your axis to its minimum position and click 'Capture Min'\n" +
            "4. Move your axis to its maximum position and click 'Capture Max'\n\n" +
            "Or enter values manually (0.0 to 1.0).\n" +
            "Click 'Reset Range' to restore the default full range.",
            "Input Range Help",
            this);
    }

    private bool _updatingSensitivitySlider;

    private void SensitivitySlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingSensitivitySlider || _selectedBinding == null) return;
        _selectedBinding.Binding.Sensitivity = SensitivitySlider.Value;
        SensitivityValueText.Text = SensitivitySlider.Value.ToString("F2");
        UpdateCurvePreview();
    }

    private void ResetSensitivityButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedBinding == null) return;
        _selectedBinding.Binding.Sensitivity = 1.0;
        _updatingSensitivitySlider = true;
        SensitivitySlider.Value = 1.0;
        SensitivityValueText.Text = "1.00";
        _updatingSensitivitySlider = false;
        UpdateCurvePreview();
    }

    private void SensitivityHelp_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        HelpDialog.Show(
            "Sensitivity controls the response curve of the axis.\n\n" +
            "1.00 = Linear (default, no change)\n" +
            "> 1.00 = Less sensitive near center, more at extremes\n" +
            "< 1.00 = More sensitive near center, less at extremes\n\n" +
            "EXAMPLES:\n" +
            "\u2022 Steering wheel on a tight circuit: Set to 2.0\u20133.0 for more " +
            "precision near center, full lock still reaches the ends.\n" +
            "\u2022 Low-rotation wheel needing quick response: Set to 0.3\u20130.7 " +
            "for faster initial turn-in.\n\n" +
            "The curve preview shows how input (bottom) maps to output (left).",
            "Sensitivity Help",
            this);
    }

    private void CurvePreviewCanvas_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
    {
        UpdateCurvePreview();
    }

    private void UpdateCurvePreview()
    {
        CurvePreviewCanvas.Children.Clear();

        double width = CurvePreviewCanvas.ActualWidth;
        double height = CurvePreviewCanvas.ActualHeight;

        if (width < 10 || height < 10) return;

        double sensitivity = SensitivitySlider.Value;
        bool isAxis = _selectedOutput?.Output.IsAxis() == true;

        // Draw grid lines (subtle)
        var gridBrush = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0x3F, 0x3F, 0x46));
        for (int i = 1; i <= 3; i++)
        {
            double pos = i / 4.0;
            // Horizontal
            var hLine = new System.Windows.Shapes.Line
            {
                X1 = 0, Y1 = height * (1 - pos), X2 = width, Y2 = height * (1 - pos),
                Stroke = gridBrush, StrokeThickness = 0.5
            };
            CurvePreviewCanvas.Children.Add(hLine);
            // Vertical
            var vLine = new System.Windows.Shapes.Line
            {
                X1 = width * pos, Y1 = 0, X2 = width * pos, Y2 = height,
                Stroke = gridBrush, StrokeThickness = 0.5
            };
            CurvePreviewCanvas.Children.Add(vLine);
        }

        // Draw linear reference (dashed diagonal)
        var refLine = new System.Windows.Shapes.Line
        {
            X1 = 0, Y1 = height, X2 = width, Y2 = 0,
            Stroke = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x55, 0x55, 0x55)),
            StrokeThickness = 1,
            StrokeDashArray = new System.Windows.Media.DoubleCollection { 4, 3 }
        };
        CurvePreviewCanvas.Children.Add(refLine);

        // Draw response curve
        var points = new System.Windows.Media.PointCollection();
        int steps = (int)width;
        for (int i = 0; i <= steps; i++)
        {
            double input = (double)i / steps;
            double output;

            if (isAxis)
            {
                double deflection = Math.Abs(input - 0.5) * 2.0;
                double curved = Math.Pow(deflection, sensitivity);
                output = 0.5 + Math.Sign(input - 0.5) * curved * 0.5;
            }
            else
            {
                output = Math.Pow(input, sensitivity);
            }

            points.Add(new System.Windows.Point(i, height * (1 - output)));
        }

        var polyline = new System.Windows.Shapes.Polyline
        {
            Points = points,
            Stroke = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x00, 0x78, 0xD4)), // AccentBrush
            StrokeThickness = 2
        };
        CurvePreviewCanvas.Children.Add(polyline);
    }

    private double? GetLiveInputValue(InputBinding binding)
    {
        if (!_isMonitoring)
        {
            StartMonitoring();
            // Give devices a moment to report values
            System.Threading.Thread.Sleep(100);
        }

        if (_latestInputValues.TryGetValue((binding.DeviceId, binding.SourceIndex), out var value))
        {
            return value;
        }

        return null;
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
        HelpDialog.Show(
            "Xbox controllers have two rumble motors that games use differently:\n\n" +
            "LARGE MOTOR (Low Frequency)\n" +
            "- Heavy, deep rumble for impacts and collisions\n" +
            "- Used for: crashes, explosions, engine rumble\n" +
            "- Best for: steering wheels, bass shakers\n\n" +
            "SMALL MOTOR (High Frequency)\n" +
            "- Light, buzzy vibration for feedback\n" +
            "- Used for: road texture, gunfire, alerts\n" +
            "- Best for: subtle tactile feedback\n\n" +
            "MODE OPTIONS:\n\n" +
            "- Large Motor: Only use the large motor signal\n" +
            "  Best for wheels where you want strong, clear effects\n\n" +
            "- Small Motor: Only use the small motor signal\n" +
            "  Best for feeling subtle details like road texture\n\n" +
            "- Combined (Recommended): Use whichever motor is stronger\n" +
            "  Best overall experience, captures all game feedback\n\n" +
            "- Swap: Use small motor signal as primary\n" +
            "  For games that incorrectly use the small motor for main effects",
            "Motor Mode Help",
            this);
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
            _isLoadingHidHideSettings = false;
            return;
        }

        var version = _hidHideService.Version;
        HidHideStatusText.Text = version != null && version != "Installed"
            ? $"HidHide v{version} detected"
            : "HidHide detected";
        HidHideEnabledCheckBox.IsEnabled = true;

        // Populate device list from HidHide gaming devices (de-duplicated by VID/PID)
        _hidHideDevices.Clear();
        var gamingDevices = _hidHideService.GetGamingDevices().ToList();

        if (gamingDevices.Count == 0)
        {
            HidHideStatusText.Text += " - No devices found. If you just installed HidHide, a reboot may be required.";
        }
        else
        {
            var seenVidPids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var device in gamingDevices.Where(d => d.Present))
            {
                // De-duplicate by VID/PID (same as HardwareId deduplication in main device list)
                var vidPid = ExtractVidPid(device.DeviceInstancePath ?? "");
                if (!string.IsNullOrEmpty(vidPid))
                {
                    if (seenVidPids.Contains(vidPid))
                        continue;
                    seenVidPids.Add(vidPid);
                }

                // Try to find a friendly name from DeviceSettings
                string? friendlyName = TryGetFriendlyNameForHidHideDevice(device);

                var displayName = friendlyName
                    ?? (!string.IsNullOrEmpty(device.Product)
                        ? $"{device.Product} ({device.Vendor})"
                        : device.DeviceInstancePath ?? "Unknown Device");

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

        // Load the global application whitelist
        LoadWhitelist();

        _isLoadingHidHideSettings = false;
    }

    private void LoadWhitelist()
    {
        _whitelistItems.Clear();

        if (!_hidHideService.IsAvailable) return;

        try
        {
            var apps = _hidHideService.GetWhitelistedApplications()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            foreach (var appPath in apps)
            {
                _whitelistItems.Add(new WhitelistItem(appPath));
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Failed to load whitelist: {ex.Message}");
        }
    }

    private void AddWhitelistApp_Click(object sender, RoutedEventArgs e)
    {
        if (!_hidHideService.IsAvailable)
        {
            MessageBox.Show("HidHide is not available.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select Application to Whitelist",
            Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            var path = dialog.FileName;

            // Check if already whitelisted
            if (_whitelistItems.Any(w => w.FullPath.Equals(path, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("This application is already whitelisted.", "Already Whitelisted",
                    MessageBoxButton.OK, MessageBoxImage.None);
                return;
            }

            if (_hidHideService.WhitelistApplication(path))
            {
                _whitelistItems.Add(new WhitelistItem(path));
                AppLogger.Info($"Added to HidHide whitelist: {path}");
            }
            else
            {
                MessageBox.Show("Failed to add application to whitelist.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void RemoveWhitelistApp_Click(object sender, RoutedEventArgs e)
    {
        if (!_hidHideService.IsAvailable)
        {
            MessageBox.Show("HidHide is not available.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var selected = WhitelistListBox.SelectedItem as WhitelistItem;
        if (selected == null)
        {
            MessageBox.Show("Please select an application to remove.", "No Selection",
                MessageBoxButton.OK, MessageBoxImage.None);
            return;
        }

        // Warn if removing XOutputRedux itself
        var currentExe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
        if (currentExe != null && selected.FullPath.Equals(currentExe, StringComparison.OrdinalIgnoreCase))
        {
            var result = MessageBox.Show(
                "You are about to remove XOutputRedux from the whitelist. This will prevent XOutputRedux from seeing hidden devices.\n\nAre you sure?",
                "Warning",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;
        }

        // Remove ALL occurrences (HidHide may have duplicates with different casing/formatting)
        int removeCount = 0;
        const int maxAttempts = 5;
        for (int i = 0; i < maxAttempts; i++)
        {
            if (_hidHideService.UnwhitelistApplication(selected.FullPath))
            {
                removeCount++;
            }
            else
            {
                break; // No more to remove
            }
        }

        if (removeCount > 0)
        {
            _whitelistItems.Remove(selected);
            AppLogger.Info($"Removed from HidHide whitelist ({removeCount} occurrence(s)): {selected.FullPath}");
        }
        else
        {
            MessageBox.Show("Failed to remove application from whitelist.", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AddWhitelistProcess_Click(object sender, RoutedEventArgs e)
    {
        if (!_hidHideService.IsAvailable)
        {
            MessageBox.Show("HidHide is not available.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // System paths to exclude
        var systemPaths = new[]
        {
            @"C:\Windows\",
            @"C:\Program Files\WindowsApps\",
            Environment.GetFolderPath(Environment.SpecialFolder.Windows)
        };

        // Common system process names to exclude
        var systemProcessNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "svchost", "csrss", "wininit", "winlogon", "services", "lsass", "smss",
            "dwm", "sihost", "taskhostw", "explorer", "RuntimeBroker", "SearchHost",
            "StartMenuExperienceHost", "ShellExperienceHost", "ctfmon", "conhost",
            "dllhost", "fontdrvhost", "WmiPrvSE", "spoolsv", "SearchIndexer",
            "SecurityHealthService", "SgrmBroker", "NisSrv", "MsMpEng", "Registry",
            "Memory Compression", "System", "Idle", "audiodg", "TextInputHost",
            "SystemSettings", "ApplicationFrameHost", "backgroundTaskHost", "LockApp",
            "WidgetService", "Widgets", "CompPkgSrv", "UserOOBEBroker"
        };

        // Get running processes with main modules (excludes system processes we can't access)
        var processes = new List<ProcessInfo>();
        foreach (var proc in System.Diagnostics.Process.GetProcesses())
        {
            try
            {
                if (proc.MainModule?.FileName != null)
                {
                    var path = proc.MainModule.FileName;
                    var name = proc.ProcessName;

                    // Skip system processes
                    if (systemProcessNames.Contains(name))
                        continue;

                    // Skip processes in system paths
                    if (systemPaths.Any(sp => path.StartsWith(sp, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    processes.Add(new ProcessInfo
                    {
                        Name = name,
                        Path = path
                    });
                }
            }
            catch
            {
                // Skip processes we can't access
            }
        }

        // Remove duplicates and sort
        var uniqueProcesses = processes
            .GroupBy(p => p.Path.ToLowerInvariant())
            .Select(g => g.First())
            .OrderBy(p => p.Name)
            .ToList();

        // Show process picker dialog
        var dialog = new ProcessPickerDialog(uniqueProcesses) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.SelectedProcess != null)
        {
            var path = dialog.SelectedProcess.Path;

            // Check if already whitelisted
            if (_whitelistItems.Any(w => w.FullPath.Equals(path, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("This application is already whitelisted.", "Already Whitelisted",
                    MessageBoxButton.OK, MessageBoxImage.None);
                return;
            }

            if (_hidHideService.WhitelistApplication(path))
            {
                _whitelistItems.Add(new WhitelistItem(path));
                AppLogger.Info($"Added to HidHide whitelist: {path}");
            }
            else
            {
                MessageBox.Show("Failed to add application to whitelist.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void UpdateHidHideControlsEnabled(bool enabled)
    {
        // Checkboxes bind IsEnabled to HidHideEnabledCheckBox via XAML.
        // No additional code needed here.
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

    private void HidHideHelp_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        HelpDialog.Show(
            "HidHide prevents games from seeing your physical controllers, so they only see " +
            "the virtual Xbox controller created by XOutputRedux.\n\n" +
            "WHY USE IT?\n" +
            "Without HidHide, games may see both your physical device AND the virtual controller, " +
            "causing double input or conflicts.\n\n" +
            "HOW IT WORKS:\n" +
            "1. Select devices to hide from this list\n" +
            "2. When you start the profile, those devices become invisible to games\n" +
            "3. When you stop the profile, devices become visible again\n" +
            "4. XOutputRedux is automatically whitelisted so it can still read your devices\n\n" +
            "REQUIREMENTS:\n" +
            "- HidHide driver must be installed (nefarius.at/HidHide)\n" +
            "- No admin rights required after installation",
            "Device Hiding Help",
            this);
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

    /// <summary>
    /// Tries to find a friendly name for a HidHide device by matching VID/PID to input devices.
    /// </summary>
    private string? TryGetFriendlyNameForHidHideDevice(HidHideDevice hidHideDevice)
    {
        if (_deviceSettings == null || string.IsNullOrEmpty(hidHideDevice.DeviceInstancePath))
            return null;

        // Extract VID/PID from HidHide device instance path
        // Format: "HID\VID_346E&PID_0006\..." or "USB\VID_046D&PID_C294\..."
        var vidPid = ExtractVidPid(hidHideDevice.DeviceInstancePath);
        if (string.IsNullOrEmpty(vidPid))
            return null;

        // Find matching input device by HardwareId
        foreach (var device in _deviceManager.Devices)
        {
            if (!string.IsNullOrEmpty(device.HardwareId) &&
                device.HardwareId.Contains(vidPid, StringComparison.OrdinalIgnoreCase))
            {
                var friendlyName = _deviceSettings.GetFriendlyName(device.UniqueId);
                if (!string.IsNullOrEmpty(friendlyName))
                    return friendlyName;
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts VID_XXXX&PID_XXXX from a device path.
    /// </summary>
    private static string? ExtractVidPid(string devicePath)
    {
        // Look for VID_XXXX&PID_XXXX pattern
        var match = System.Text.RegularExpressions.Regex.Match(
            devicePath,
            @"VID_[0-9A-Fa-f]{4}&PID_[0-9A-Fa-f]{4}",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return match.Success ? match.Value.ToUpperInvariant() : null;
    }

    #endregion

    #region Plugins

    private void LoadPluginTabs()
    {
        foreach (var plugin in _plugins)
        {
            try
            {
                var data = _profile.PluginData?.GetValueOrDefault(plugin.Id);
                var tab = plugin.CreateEditorTab(data, _isReadOnly);
                if (tab is TabItem tabItem)
                {
                    MainTabControl.Items.Add(tabItem);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Plugin {plugin.Id} failed to create editor tab", ex);
            }
        }
    }

    private void SavePluginData()
    {
        Dictionary<string, JsonObject>? pluginData = null;

        foreach (var plugin in _plugins)
        {
            try
            {
                var data = plugin.GetEditorData();
                if (data != null)
                {
                    pluginData ??= new Dictionary<string, JsonObject>();
                    pluginData[plugin.Id] = data;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Plugin {plugin.Id} failed to get editor data", ex);
            }
        }

        _originalProfile.PluginData = pluginData;
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
                    ButtonThreshold = binding.ButtonThreshold,
                    Sensitivity = binding.Sensitivity
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

        // Copy plugin data
        SavePluginData();

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

/// <summary>
/// Represents a whitelisted application in HidHide.
/// </summary>
public class WhitelistItem
{
    public string FullPath { get; }
    public string DisplayName { get; }

    public WhitelistItem(string fullPath)
    {
        FullPath = fullPath;
        DisplayName = System.IO.Path.GetFileName(fullPath);
    }
}

/// <summary>
/// Represents a running process for the picker dialog.
/// </summary>
public class ProcessInfo
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string DisplayName => $"{Name} ({System.IO.Path.GetFileName(Path)})";
}

