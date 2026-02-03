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
}
