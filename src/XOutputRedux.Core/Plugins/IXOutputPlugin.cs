using System.Text.Json.Nodes;

namespace XOutputRedux.Core.Plugins;

/// <summary>
/// Interface for XOutputRedux plugins. Plugins can provide UI tabs in the
/// profile editor and react to profile start/stop lifecycle events.
/// </summary>
public interface IXOutputPlugin : IDisposable
{
    /// <summary>
    /// Unique plugin identifier used as the key in the profile's pluginData dictionary.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Display name shown in UI (e.g., tab header).
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Called once when the plugin is loaded.
    /// Return false to indicate initialization failure (plugin will be skipped).
    /// </summary>
    bool Initialize();

    /// <summary>
    /// Creates a WPF TabItem for the profile editor.
    /// Called each time the profile editor opens.
    /// Return null if this plugin has no editor tab.
    /// The returned object must be a System.Windows.Controls.TabItem.
    /// </summary>
    object? CreateEditorTab(JsonObject? pluginData, bool readOnly);

    /// <summary>
    /// Called when the profile editor is saving.
    /// Return the plugin data to persist in the profile, or null to remove plugin data.
    /// </summary>
    JsonObject? GetEditorData();

    /// <summary>
    /// Called when a profile starts running.
    /// </summary>
    void OnProfileStart(JsonObject? pluginData);

    /// <summary>
    /// Called when a profile stops running.
    /// </summary>
    void OnProfileStop();

    /// <summary>
    /// After OnProfileStart, returns axis input range adjustments.
    /// The runtime applies these to matching bindings before the mapping engine starts,
    /// overriding MinValue/MaxValue in memory only (not persisted to disk).
    /// Returns null if no adjustments needed.
    /// </summary>
    IReadOnlyList<AxisRangeOverride>? GetAxisRangeOverrides() => null;

    /// <summary>
    /// After OnProfileStart, returns a force feedback handler that replaces
    /// the default DirectInput FFB routing. When a handler is provided, the
    /// ForceFeedbackService routes rumble data through the plugin instead of
    /// sending ConstantForce effects via DirectInput.
    /// Returns null to use the default DirectInput FFB pipeline.
    /// </summary>
    IForceFeedbackHandler? GetForceFeedbackHandler() => null;
}

/// <summary>
/// Describes an axis input range override applied by a plugin at profile start.
/// </summary>
/// <param name="DeviceHardwareId">VID/PID substring to match (e.g., "VID_346E&amp;PID_0006").</param>
/// <param name="SourceIndex">Axis source index on the device (e.g., 0 for X axis).</param>
/// <param name="MinValue">Normalized min value (0.0-1.0) at the physical axis limit.</param>
/// <param name="MaxValue">Normalized max value (0.0-1.0) at the physical axis limit.</param>
public record AxisRangeOverride(string DeviceHardwareId, int SourceIndex, double MinValue, double MaxValue);
