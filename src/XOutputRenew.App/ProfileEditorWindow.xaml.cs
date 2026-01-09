using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using XOutputRenew.Core.Mapping;
using XOutputRenew.Input;

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
    private OutputViewModel? _selectedOutput;
    private BindingViewModel? _selectedBinding;
    private readonly DispatcherTimer _captureTimer;
    private DateTime _captureStartTime;
    private readonly Dictionary<string, double> _captureBaseline = new(); // device:sourceIndex -> baseline value
    private const int CaptureGracePeriodMs = 300; // Grace period to establish baseline before detecting

    public bool WasSaved { get; private set; }

    public ProfileEditorWindow(MappingProfile profile, InputDeviceManager deviceManager)
    {
        InitializeComponent();

        _originalProfile = profile;
        _profile = profile.Clone();
        _profile.Name = profile.Name; // Keep original name
        _deviceManager = deviceManager;

        ProfileNameText.Text = _profile.Name;
        ProfileDescText.Text = _profile.Description ?? "";

        OutputListView.ItemsSource = _outputs;
        InputMonitorList.ItemsSource = _inputMonitorItems;
        BindingListView.ItemsSource = _bindings;

        _captureTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _captureTimer.Tick += CaptureTimer_Tick;

        // Wire up input logging to app logger
        InputLogger.LogAction = msg => AppLogger.Info(msg);

        LoadOutputs();

        Closed += ProfileEditorWindow_Closed;
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

    private void StartMonitor_Click(object sender, RoutedEventArgs e)
    {
        if (_isMonitoring) return;

        _isMonitoring = true;
        StartMonitorButton.IsEnabled = false;
        StopMonitorButton.IsEnabled = true;
        _inputMonitorItems.Clear();

        foreach (var device in _deviceManager.Devices)
        {
            device.InputChanged += Device_InputChanged_Monitor;
            device.Start();
        }
    }

    private void StopMonitor_Click(object sender, RoutedEventArgs e)
    {
        StopMonitoring();
    }

    private void StopMonitoring()
    {
        if (!_isMonitoring) return;

        _isMonitoring = false;
        StartMonitorButton.IsEnabled = true;
        StopMonitorButton.IsEnabled = false;

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
            StartMonitor_Click(null!, null!);
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
