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

    private readonly JsonObject? _data;
    private readonly bool _readOnly;

    private CheckBox? _enabledCheckBox;
    private Slider? _rotationSlider;
    private Slider? _ffbSlider;
    private Slider? _dampingSlider;
    private Slider? _springSlider;
    private Slider? _inertiaSlider;
    private Slider? _maxTorqueSlider;
    private Slider? _speedDampingSlider;
    private CheckBox? _ffbReverseCheckBox;

    public MozaEditorTab(JsonObject? data, bool readOnly)
    {
        _data = data;
        _readOnly = readOnly;
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

        int rotation = _data?["wheelRotation"]?.GetValue<int>() ?? 900;
        AddSliderRow(panel, "Wheel Rotation", 90, 2700, rotation, 10,
            v => $"{v}\u00B0", out _rotationSlider);

        int ffb = _data?["ffbStrength"]?.GetValue<int>() ?? 100;
        AddSliderRow(panel, "FFB Strength", 0, 100, ffb, 1,
            v => $"{v}%", out _ffbSlider);

        int maxTorque = _data?["maxTorque"]?.GetValue<int>() ?? 100;
        AddSliderRow(panel, "Max Torque", 50, 100, maxTorque, 1,
            v => $"{v}%", out _maxTorqueSlider);

        bool ffbReverse = _data?["ffbReverse"]?.GetValue<bool>() ?? false;
        _ffbReverseCheckBox = new CheckBox
        {
            Content = "Reverse FFB Direction",
            IsChecked = ffbReverse,
            IsEnabled = !_readOnly,
            Foreground = ForegroundBrush,
            Margin = new Thickness(0, 0, 0, 15)
        };
        panel.Children.Add(_ffbReverseCheckBox);

        // --- Feel Settings ---
        AddSectionHeader(panel, "Wheel Feel");

        int damping = _data?["damping"]?.GetValue<int>() ?? 0;
        AddSliderRow(panel, "Damping", 0, 100, damping, 1,
            v => $"{v}%", out _dampingSlider);

        int spring = _data?["springStrength"]?.GetValue<int>() ?? 0;
        AddSliderRow(panel, "Center Spring", 0, 100, spring, 1,
            v => $"{v}%", out _springSlider);

        int inertia = _data?["naturalInertia"]?.GetValue<int>() ?? 100;
        AddSliderRow(panel, "Natural Inertia", 100, 500, inertia, 5,
            v => $"{v}%", out _inertiaSlider);

        int speedDamping = _data?["speedDamping"]?.GetValue<int>() ?? 0;
        AddSliderRow(panel, "Speed Damping", 0, 100, speedDamping, 1,
            v => $"{v}%", out _speedDampingSlider);

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
        int value, int tickFrequency, Func<int, string> format, out Slider slider)
    {
        // Label
        parent.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = ForegroundBrush,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 3)
        });

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
            ["springStrength"] = (int)(_springSlider?.Value ?? 0),
            ["naturalInertia"] = (int)(_inertiaSlider?.Value ?? 100),
            ["speedDamping"] = (int)(_speedDampingSlider?.Value ?? 0)
        };
    }
}
