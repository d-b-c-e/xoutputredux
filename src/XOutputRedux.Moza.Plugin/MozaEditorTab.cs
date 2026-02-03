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

    private readonly JsonObject? _data;
    private readonly bool _readOnly;

    private CheckBox? _enabledCheckBox;
    private Slider? _rotationSlider;
    private Slider? _ffbSlider;
    private TextBlock? _rotationValueText;
    private TextBlock? _ffbValueText;

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
            Margin = new Thickness(0, 0, 0, 20)
        };
        panel.Children.Add(_enabledCheckBox);

        // Wheel Rotation
        int rotation = _data?["wheelRotation"]?.GetValue<int>() ?? 900;
        AddSliderRow(panel, "Wheel Rotation", 90, 2700, rotation, 10,
            v => $"{v}\u00B0",
            out _rotationSlider, out _rotationValueText);

        // FFB Strength
        int ffb = _data?["ffbStrength"]?.GetValue<int>() ?? 100;
        AddSliderRow(panel, "FFB Strength", 0, 100, ffb, 1,
            v => $"{v}%",
            out _ffbSlider, out _ffbValueText);

        tab.Content = panel;
        return tab;
    }

    private void AddSliderRow(StackPanel parent, string label, int min, int max,
        int value, int tickFrequency, Func<int, string> format,
        out Slider slider, out TextBlock valueText)
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

        valueText = new TextBlock
        {
            Text = format(value),
            Foreground = ForegroundBrush,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 60,
            TextAlignment = TextAlignment.Right
        };

        var capturedValueText = valueText;
        slider.ValueChanged += (_, e) =>
        {
            capturedValueText.Text = format((int)e.NewValue);
        };

        Grid.SetColumn(slider, 0);
        Grid.SetColumn(valueText, 1);
        grid.Children.Add(slider);
        grid.Children.Add(valueText);

        // Range hint
        var rangeText = new TextBlock
        {
            Text = $"Range: {format(min)} \u2013 {format(max)}",
            Foreground = SubtextBrush,
            FontSize = 11,
            Margin = new Thickness(0, -10, 0, 10)
        };

        parent.Children.Add(grid);
        parent.Children.Add(rangeText);
    }

    public JsonObject? GetData()
    {
        if (_enabledCheckBox?.IsChecked != true)
            return null;

        return new JsonObject
        {
            ["enabled"] = true,
            ["wheelRotation"] = (int)(_rotationSlider?.Value ?? 900),
            ["ffbStrength"] = (int)(_ffbSlider?.Value ?? 100)
        };
    }
}
