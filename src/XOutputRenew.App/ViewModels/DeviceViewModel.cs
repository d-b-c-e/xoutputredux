using System.ComponentModel;
using XOutputRenew.Input;

namespace XOutputRenew.App.ViewModels;

/// <summary>
/// View model for displaying an input device.
/// </summary>
public class DeviceViewModel : INotifyPropertyChanged
{
    private bool _isActive;
    private string? _friendlyName;

    public string UniqueId { get; }
    public string Name { get; }
    public string Method { get; }
    public string? HardwareId { get; }
    public string? InterfacePath { get; }
    public string SourcesSummary { get; }
    public IInputDevice Device { get; }

    /// <summary>
    /// User-assigned friendly name for the device.
    /// </summary>
    public string? FriendlyName
    {
        get => _friendlyName;
        set
        {
            _friendlyName = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FriendlyName)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
        }
    }

    /// <summary>
    /// Display name (FriendlyName if set, otherwise Name).
    /// </summary>
    public string DisplayName => !string.IsNullOrEmpty(FriendlyName) ? FriendlyName : Name;

    /// <summary>
    /// Whether this device is currently receiving input.
    /// </summary>
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive != value)
            {
                _isActive = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public DeviceViewModel(IInputDevice device)
    {
        Device = device;
        UniqueId = device.UniqueId;
        Name = device.Name;
        Method = device.Method.ToString();
        HardwareId = device.HardwareId;
        InterfacePath = device.InterfacePath;
        SourcesSummary = BuildSourcesSummary(device.Sources);
    }

    /// <summary>
    /// Gets formatted device info for clipboard.
    /// </summary>
    public string GetDeviceInfo()
    {
        return $"""
            Device Name: {Name}
            Friendly Name: {FriendlyName ?? "(not set)"}
            Unique ID: {UniqueId}
            Hardware ID: {HardwareId ?? "(none)"}
            Interface Path: {InterfacePath ?? "(none)"}
            Input Method: {Method}
            Sources: {SourcesSummary}
            """;
    }

    private static string BuildSourcesSummary(IReadOnlyList<IInputSource> sources)
    {
        var buttons = sources.Count(s => s.Type == InputSourceType.Button);
        var axes = sources.Count(s => s.Type == InputSourceType.Axis);
        var sliders = sources.Count(s => s.Type == InputSourceType.Slider);
        var dpads = sources.Count(s => s.Type == InputSourceType.DPad);

        var parts = new List<string>();
        if (buttons > 0) parts.Add($"{buttons} btn");
        if (axes > 0) parts.Add($"{axes} axis");
        if (sliders > 0) parts.Add($"{sliders} slider");
        if (dpads > 0) parts.Add($"{dpads / 4} dpad");

        return string.Join(", ", parts);
    }
}
