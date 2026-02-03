using System.Text.Json.Nodes;
using XOutputRedux.Core.Plugins;

namespace XOutputRedux.Moza.Plugin;

public class MozaPlugin : IXOutputPlugin
{
    public string Id => "moza";
    public string DisplayName => "Moza Wheel";

    private MozaDevice? _device;
    private MozaEditorTab? _editorTab;

    public bool Initialize()
    {
        // Don't initialize the SDK here — only when a profile actually starts.
        // The SDK requires Moza Pit House to be running.
        return true;
    }

    public object? CreateEditorTab(JsonObject? pluginData, bool readOnly)
    {
        _editorTab = new MozaEditorTab(pluginData, readOnly);
        return _editorTab.CreateTab();
    }

    public JsonObject? GetEditorData()
    {
        return _editorTab?.GetData();
    }

    public void OnProfileStart(JsonObject? pluginData)
    {
        if (pluginData == null)
            return;

        var enabled = pluginData["enabled"]?.GetValue<bool>() ?? false;
        if (!enabled)
            return;

        var wheelRotation = pluginData["wheelRotation"]?.GetValue<int>();
        var ffbStrength = pluginData["ffbStrength"]?.GetValue<int>();

        if (wheelRotation == null && ffbStrength == null)
            return;

        try
        {
            _device = new MozaDevice();
            if (!_device.Initialize())
            {
                _device = null;
                return;
            }

            if (wheelRotation.HasValue)
                _device.SetWheelRotation(wheelRotation.Value);

            if (ffbStrength.HasValue)
                _device.SetFfbStrength(ffbStrength.Value);
        }
        catch
        {
            _device?.Dispose();
            _device = null;
        }
    }

    public void OnProfileStop()
    {
        _device?.Dispose();
        _device = null;
    }

    public void Dispose()
    {
        _device?.Dispose();
        _device = null;
    }
}
