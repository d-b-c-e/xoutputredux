using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace XOutputRedux.Moza.Plugin;

internal class MozaEditorTab
{
    private static readonly SolidColorBrush BackgroundBrush = new(Color.FromRgb(0x25, 0x25, 0x26));
    private static readonly SolidColorBrush ForegroundBrush = new(Color.FromRgb(0xE0, 0xE0, 0xE0));
    private static readonly SolidColorBrush SubtextBrush = new(Color.FromRgb(0x9E, 0x9E, 0x9E));
    private static readonly SolidColorBrush SeparatorBrush = new(Color.FromRgb(0x40, 0x40, 0x42));
    private static readonly SolidColorBrush TooltipBackgroundBrush = new(Color.FromRgb(0x3C, 0x3C, 0x3C));
    private static readonly SolidColorBrush TooltipBorderBrush = new(Color.FromRgb(0x55, 0x55, 0x55));
    private static readonly SolidColorBrush HelpTextBrush = new(Color.FromRgb(0x00, 0x78, 0xD4));

    private readonly JsonObject? _data;
    private readonly bool _readOnly;

    // Default values (hardcoded fallbacks, overridden by live wheel read)
    private int _defaultRotation = 900;
    private int _defaultFfb = 100;
    private int _defaultMaxTorque = 100;
    private bool _defaultFfbReverse = false;
    private int _defaultDamping = 0;
    private int _defaultSpring = 0;
    private int _defaultInertia = 100;
    private int _defaultSpeedDamping = 0;
    private int _defaultNaturalFriction = 0;
    private int _defaultSpeedDampingStartPoint = 0;
    private int _defaultHandsOffProtection = 0;

    private CheckBox? _enabledCheckBox;
    private Slider? _rotationSlider;
    private Slider? _ffbSlider;
    private Slider? _dampingSlider;
    private Slider? _springSlider;
    private Slider? _inertiaSlider;
    private Slider? _maxTorqueSlider;
    private Slider? _speedDampingSlider;
    private Slider? _naturalFrictionSlider;
    private Slider? _speedDampingStartPointSlider;
    private Slider? _handsOffProtectionSlider;
    private CheckBox? _ffbReverseCheckBox;
    private CheckBox? _ffbEnhancementCheckBox;
    private Slider? _ffbFrequencySlider;
    private CheckBox? _ambientEffectsCheckBox;
    private Slider? _ambientSpringSlider;
    private Slider? _ambientFrictionSlider;
    private Slider? _ambientDamperSlider;

    public MozaEditorTab(JsonObject? data, bool readOnly, MozaDevice? device = null)
    {
        _data = data;
        _readOnly = readOnly;

        // If no saved data (or disabled), try reading current values from the wheel
        bool hasEnabledData = data?["enabled"]?.GetValue<bool>() == true;
        if (!hasEnabledData && device != null)
        {
            ReadCurrentWheelSettings(device);
        }
    }

    private void ReadCurrentWheelSettings(MozaDevice device)
    {
        try
        {
            _defaultRotation = device.GetWheelRotation();
            _defaultFfb = device.GetFfbStrength();
            _defaultMaxTorque = device.GetMaxTorque();
            _defaultFfbReverse = device.GetFfbReverse();
            _defaultDamping = device.GetDamping();
            _defaultSpring = device.GetSpringStrength();
            _defaultInertia = device.GetNaturalInertia();
            _defaultSpeedDamping = device.GetSpeedDamping();
            _defaultNaturalFriction = device.GetNaturalFriction();
            _defaultSpeedDampingStartPoint = device.GetSpeedDampingStartPoint();
            _defaultHandsOffProtection = device.GetHandsOffProtection();
        }
        catch
        {
            // Pit House not running or wheel not connected — use hardcoded defaults
        }
    }

    public TabItem CreateTab()
    {
        var tab = new TabItem
        {
            Header = "Moza Wheel",
            Foreground = ForegroundBrush
        };

        var panel = new StackPanel
        {
            Margin = new Thickness(10),
            Background = BackgroundBrush
        };

        // Title
        panel.Children.Add(new TextBlock
        {
            Text = "Moza Wheel Settings",
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = ForegroundBrush,
            Margin = new Thickness(0, 0, 0, 5)
        });

        // Description
        panel.Children.Add(new TextBlock
        {
            Text = "Settings are sent to the Moza wheel when this profile starts. Requires Moza Pit House running.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = SubtextBrush,
            Margin = new Thickness(0, 0, 0, 15)
        });

        // Enabled checkbox
        bool enabled = _data?["enabled"]?.GetValue<bool>() ?? false;
        _enabledCheckBox = new CheckBox
        {
            Content = "Apply Moza settings on profile start",
            IsChecked = enabled,
            IsEnabled = !_readOnly,
            Foreground = ForegroundBrush,
            Margin = new Thickness(0, 0, 0, 15)
        };
        panel.Children.Add(_enabledCheckBox);

        // --- Primary Settings ---
        AddSectionHeader(panel, "Primary");

        int rotation = _data?["wheelRotation"]?.GetValue<int>() ?? _defaultRotation;
        AddSliderRow(panel, "Wheel Rotation", 90, 2700, rotation, 90,
            v => $"{v}\u00B0", out _rotationSlider,
            "Total steering angle lock-to-lock. Lower values (e.g. 540\u00B0) suit arcade racers, higher values (900\u00B0+) suit simulators.");

        int ffb = _data?["ffbStrength"]?.GetValue<int>() ?? _defaultFfb;
        AddSliderRow(panel, "FFB Strength", 0, 100, ffb, 5,
            v => $"{v}%", out _ffbSlider,
            "Overall force feedback intensity. Scales all FFB effects sent to the wheel.");

        int maxTorque = _data?["maxTorque"]?.GetValue<int>() ?? _defaultMaxTorque;
        AddSliderRow(panel, "Max Torque", 50, 100, maxTorque, 5,
            v => $"{v}%", out _maxTorqueSlider,
            "Caps the peak force the wheel can output. Lower this to prevent jarring jolts from crashes or wall hits.");

        bool ffbReverse = _data?["ffbReverse"]?.GetValue<bool>() ?? _defaultFfbReverse;
        var ffbReversePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
        _ffbReverseCheckBox = new CheckBox
        {
            Content = "Reverse FFB Direction",
            IsChecked = ffbReverse,
            IsEnabled = !_readOnly,
            Foreground = ForegroundBrush,
        };
        ffbReversePanel.Children.Add(_ffbReverseCheckBox);
        ffbReversePanel.Children.Add(CreateHelpBadge("Flip the FFB direction. Enable this if the wheel pulls into turns instead of resisting them."));
        panel.Children.Add(ffbReversePanel);

        // --- Feel Settings ---
        AddSectionHeader(panel, "Wheel Feel");

        int damping = _data?["damping"]?.GetValue<int>() ?? _defaultDamping;
        AddSliderRow(panel, "Damping", 0, 100, damping, 5,
            v => $"{v}%", out _dampingSlider,
            "Adds resistance that slows the wheel's movement. Smooths out twitchy steering in arcade racers.");

        int naturalFriction = _data?["naturalFriction"]?.GetValue<int>() ?? _defaultNaturalFriction;
        AddSliderRow(panel, "Natural Friction", 0, 100, naturalFriction, 5,
            v => $"{v}%", out _naturalFrictionSlider,
            "Constant grip resistance regardless of speed or direction. Makes the wheel feel connected to the road. Recommended for arcade racers that lack native wheel FFB.");

        int spring = _data?["springStrength"]?.GetValue<int>() ?? _defaultSpring;
        AddSliderRow(panel, "Center Spring", 0, 100, spring, 5,
            v => $"{v}%", out _springSlider,
            "Force that pulls the wheel back to center. Useful when games don't provide their own centering force.");

        int inertia = _data?["naturalInertia"]?.GetValue<int>() ?? _defaultInertia;
        AddSliderRow(panel, "Natural Inertia", 100, 500, inertia, 5,
            v => $"{v}%", out _inertiaSlider,
            "Simulates the weight of the steering wheel. Higher values make the wheel feel heavier and slower to turn.");

        int speedDamping = _data?["speedDamping"]?.GetValue<int>() ?? _defaultSpeedDamping;
        AddSliderRow(panel, "Speed Damping", 0, 100, speedDamping, 5,
            v => $"{v}%", out _speedDampingSlider,
            "Resistance that increases with turning speed. Prevents snapping the wheel quickly from lock to lock.");

        int speedDampingStartPoint = _data?["speedDampingStartPoint"]?.GetValue<int>() ?? _defaultSpeedDampingStartPoint;
        AddSliderRow(panel, "Speed Damping Threshold", 0, 100, speedDampingStartPoint, 5,
            v => $"{v}%", out _speedDampingStartPointSlider,
            "The turning speed at which Speed Damping begins to engage. Higher values let small corrections pass freely while still dampening fast movements.");

        // --- Safety ---
        AddSectionHeader(panel, "Safety");

        int handsOffProtection = _data?["handsOffProtection"]?.GetValue<int>() ?? _defaultHandsOffProtection;
        AddSliderRow(panel, "Hands-Off Protection", 0, 100, handsOffProtection, 5,
            v => $"{v}%", out _handsOffProtectionSlider,
            "Limits wheel torque when hands are detected as off the wheel. Prevents unexpected wheel spin from strong FFB effects.");

        // --- FFB Enhancement ---
        AddSectionHeader(panel, "FFB Enhancement");

        panel.Children.Add(new TextBlock
        {
            Text = "For games with Xbox rumble only (no native wheel FFB). Converts controller vibration into oscillating wheel effects instead of a static push.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = SubtextBrush,
            FontSize = 11,
            Margin = new Thickness(0, 0, 0, 10)
        });

        bool ffbEnhance = _data?["ffbEnhancement"]?.GetValue<bool>() ?? false;
        var ffbEnhancePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
        _ffbEnhancementCheckBox = new CheckBox
        {
            Content = "Use Moza vibration effects for rumble",
            IsChecked = ffbEnhance,
            IsEnabled = !_readOnly,
            Foreground = ForegroundBrush,
        };
        ffbEnhancePanel.Children.Add(_ffbEnhancementCheckBox);
        ffbEnhancePanel.Children.Add(CreateHelpBadge("Routes Xbox rumble through the Moza SDK as a sine-wave vibration effect instead of the default DirectInput constant force. Feels more like real rumble on a wheel."));
        panel.Children.Add(ffbEnhancePanel);

        int ffbFrequency = _data?["ffbFrequency"]?.GetValue<int>() ?? 50;
        AddSliderRow(panel, "Vibration Frequency", 10, 200, ffbFrequency, 10,
            v => $"{v} ms", out _ffbFrequencySlider,
            "Period of the vibration cycle in milliseconds. Lower values = faster/buzzier vibration (10ms). Higher values = slower/thuddy vibration (200ms). 50ms is a good starting point.");

        // --- Ambient Effects ---
        AddSectionHeader(panel, "Ambient Effects");

        panel.Children.Add(new TextBlock
        {
            Text = "Always-on background effects that give the wheel a baseline driving feel, even in games with no force feedback at all. These run alongside any game FFB.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = SubtextBrush,
            FontSize = 11,
            Margin = new Thickness(0, 0, 0, 10)
        });

        bool ambientEnabled = _data?["ambientEffects"]?.GetValue<bool>() ?? false;
        var ambientPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
        _ambientEffectsCheckBox = new CheckBox
        {
            Content = "Enable ambient wheel effects",
            IsChecked = ambientEnabled,
            IsEnabled = !_readOnly,
            Foreground = ForegroundBrush,
        };
        ambientPanel.Children.Add(_ambientEffectsCheckBox);
        ambientPanel.Children.Add(CreateHelpBadge("Creates persistent spring, friction, and damper effects on the wheel that run continuously while the profile is active."));
        panel.Children.Add(ambientPanel);

        int ambientSpring = _data?["ambientSpring"]?.GetValue<int>() ?? 30;
        AddSliderRow(panel, "Ambient Spring", 0, 100, ambientSpring, 5,
            v => $"{v}%", out _ambientSpringSlider,
            "Centering force that pulls the wheel back toward center. Creates a natural return-to-center feel for games that don't provide their own.");

        int ambientFriction = _data?["ambientFriction"]?.GetValue<int>() ?? 20;
        AddSliderRow(panel, "Ambient Friction", 0, 100, ambientFriction, 5,
            v => $"{v}%", out _ambientFrictionSlider,
            "Constant resistance in both directions. Simulates tire grip and prevents the wheel from spinning freely.");

        int ambientDamper = _data?["ambientDamper"]?.GetValue<int>() ?? 15;
        AddSliderRow(panel, "Ambient Damper", 0, 100, ambientDamper, 5,
            v => $"{v}%", out _ambientDamperSlider,
            "Speed-dependent resistance. Slows fast wheel movements to simulate hydraulic power steering. Works well with friction for a convincing driving feel.");

        // Wrap in ScrollViewer
        var scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = panel
        };

        tab.Content = scrollViewer;
        return tab;
    }

    private static void AddSectionHeader(StackPanel parent, string title)
    {
        parent.Children.Add(new Border
        {
            BorderBrush = SeparatorBrush,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Margin = new Thickness(0, 5, 0, 10),
            Child = new TextBlock
            {
                Text = title,
                Foreground = SubtextBrush,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 8, 0, 0)
            }
        });
    }

    private void AddSliderRow(StackPanel parent, string label, int min, int max,
        int value, int tickFrequency, Func<int, string> format, out Slider slider,
        string? tooltip = null)
    {
        // Label with optional help badge
        if (tooltip != null)
        {
            var labelPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 3) };
            labelPanel.Children.Add(new TextBlock
            {
                Text = label,
                Foreground = ForegroundBrush,
                FontWeight = FontWeights.SemiBold,
            });
            labelPanel.Children.Add(CreateHelpBadge(tooltip));
            parent.Children.Add(labelPanel);
        }
        else
        {
            parent.Children.Add(new TextBlock
            {
                Text = label,
                Foreground = ForegroundBrush,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 3),
            });
        }

        // Slider + value in a horizontal grid
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 15) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });

        slider = new Slider
        {
            Minimum = min,
            Maximum = max,
            Value = value,
            TickFrequency = tickFrequency,
            IsSnapToTickEnabled = true,
            IsEnabled = !_readOnly,
            VerticalAlignment = VerticalAlignment.Center
        };

        var valueText = new TextBlock
        {
            Text = format(value),
            Foreground = ForegroundBrush,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 60,
            TextAlignment = TextAlignment.Right
        };

        slider.ValueChanged += (_, e) =>
        {
            valueText.Text = format((int)e.NewValue);
        };

        Grid.SetColumn(slider, 0);
        Grid.SetColumn(valueText, 1);
        grid.Children.Add(slider);
        grid.Children.Add(valueText);

        // Range hint
        parent.Children.Add(grid);
        parent.Children.Add(new TextBlock
        {
            Text = $"Range: {format(min)} \u2013 {format(max)}",
            Foreground = SubtextBrush,
            FontSize = 11,
            Margin = new Thickness(0, -10, 0, 10)
        });
    }

    private FrameworkElement CreateHelpBadge(string tooltipText)
    {
        var helpText = new TextBlock
        {
            Text = "?",
            Foreground = HelpTextBrush,
            FontWeight = FontWeights.Bold,
            Cursor = System.Windows.Input.Cursors.Hand,
            Margin = new Thickness(5, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = CreateDarkToolTip(tooltipText),
        };
        ToolTipService.SetShowDuration(helpText, 15000);
        ToolTipService.SetInitialShowDelay(helpText, 200);

        return helpText;
    }

    private static ToolTip CreateDarkToolTip(string text)
    {
        return new ToolTip
        {
            Content = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 300,
                Foreground = ForegroundBrush,
                FontSize = 12,
            },
            Background = TooltipBackgroundBrush,
            Foreground = ForegroundBrush,
            BorderBrush = TooltipBorderBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 4, 8, 4),
        };
    }

    public JsonObject? GetData()
    {
        if (_enabledCheckBox?.IsChecked != true)
            return null;

        return new JsonObject
        {
            ["enabled"] = true,
            ["wheelRotation"] = (int)(_rotationSlider?.Value ?? 900),
            ["ffbStrength"] = (int)(_ffbSlider?.Value ?? 100),
            ["maxTorque"] = (int)(_maxTorqueSlider?.Value ?? 100),
            ["ffbReverse"] = _ffbReverseCheckBox?.IsChecked ?? false,
            ["damping"] = (int)(_dampingSlider?.Value ?? 0),
            ["naturalFriction"] = (int)(_naturalFrictionSlider?.Value ?? 0),
            ["springStrength"] = (int)(_springSlider?.Value ?? 0),
            ["naturalInertia"] = (int)(_inertiaSlider?.Value ?? 100),
            ["speedDamping"] = (int)(_speedDampingSlider?.Value ?? 0),
            ["speedDampingStartPoint"] = (int)(_speedDampingStartPointSlider?.Value ?? 0),
            ["handsOffProtection"] = (int)(_handsOffProtectionSlider?.Value ?? 0),
            ["ffbEnhancement"] = _ffbEnhancementCheckBox?.IsChecked ?? false,
            ["ffbFrequency"] = (int)(_ffbFrequencySlider?.Value ?? 50),
            ["ambientEffects"] = _ambientEffectsCheckBox?.IsChecked ?? false,
            ["ambientSpring"] = (int)(_ambientSpringSlider?.Value ?? 30),
            ["ambientFriction"] = (int)(_ambientFrictionSlider?.Value ?? 20),
            ["ambientDamper"] = (int)(_ambientDamperSlider?.Value ?? 15)
        };
    }
}
