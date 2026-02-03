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

        try
        {
            _device = new MozaDevice();
            if (!_device.Initialize())
            {
                _device = null;
                return;
            }

            // Primary settings
            ApplyIntSetting(pluginData, "wheelRotation", _device.SetWheelRotation);
            ApplyIntSetting(pluginData, "ffbStrength", _device.SetFfbStrength);
            ApplyIntSetting(pluginData, "maxTorque", _device.SetMaxTorque);

            var ffbReverse = pluginData["ffbReverse"]?.GetValue<bool>();
            if (ffbReverse.HasValue)
                _device.SetFfbReverse(ffbReverse.Value);

            // Wheel feel settings
            ApplyIntSetting(pluginData, "damping", _device.SetDamping);
            ApplyIntSetting(pluginData, "springStrength", _device.SetSpringStrength);
            ApplyIntSetting(pluginData, "naturalInertia", _device.SetNaturalInertia);
            ApplyIntSetting(pluginData, "speedDamping", _device.SetSpeedDamping);
        }
        catch
        {
            _device?.Dispose();
            _device = null;
        }
    }

    private static void ApplyIntSetting(JsonObject data, string key, Action<int> setter)
    {
        var value = data[key]?.GetValue<int>();
        if (value.HasValue)
            setter(value.Value);
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
