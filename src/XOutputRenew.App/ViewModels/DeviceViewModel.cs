using XOutputRenew.Input;

namespace XOutputRenew.App.ViewModels;

/// <summary>
/// View model for displaying an input device.
/// </summary>
public class DeviceViewModel
{
    public string UniqueId { get; }
    public string Name { get; }
    public string Method { get; }
    public string? HardwareId { get; }
    public string? InterfacePath { get; }
    public string SourcesSummary { get; }
    public IInputDevice Device { get; }

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
