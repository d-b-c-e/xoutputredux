using System.Text.Json.Nodes;
using XOutputRedux.Core.Plugins;

namespace XOutputRedux.Moza.Plugin;

public class MozaPlugin : IXOutputPlugin
{
    public string Id => "moza";
    public string DisplayName => "Moza Wheel";

    /// <summary>
    /// Optional logging callback set by the host application.
    /// </summary>
    public static Action<string>? Log { get; set; }

    private MozaEditorTab? _editorTab;
    private MozaDevice? _device;

    public bool Initialize()
    {
        return true;
    }

    public object? CreateEditorTab(JsonObject? pluginData, bool readOnly)
    {
        // Use a short-lived SDK session to read current wheel values for slider defaults.
        MozaDevice? reader = null;
        try
        {
            reader = new MozaDevice();
            if (reader.Initialize())
            {
                Log?.Invoke("Moza editor: reading current wheel values");
            }
            else
            {
                reader = null;
                Log?.Invoke("Moza editor: SDK init failed, using defaults");
            }
        }
        catch
        {
            reader?.Dispose();
            reader = null;
        }

        _editorTab = new MozaEditorTab(pluginData, readOnly, reader);
        var tab = _editorTab.CreateTab();

        // Release the SDK session immediately
        if (reader != null)
        {
            reader.Dispose();
            Log?.Invoke("Moza editor: SDK session released");
        }

        return tab;
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

        Log?.Invoke("Moza OnProfileStart: applying settings...");

        try
        {
            // Keep the SDK session alive for the duration of the profile.
            _device?.Dispose();
            _device = new MozaDevice();
            if (!_device.Initialize())
            {
                Log?.Invoke("Moza: SDK init failed");
                _device = null;
                return;
            }

            // The SDK needs time after installMozaSDK() to connect to Pit House
            // and discover devices. Poll until a read succeeds or we timeout.
            Log?.Invoke("Moza: waiting for device discovery...");
            var deviceReady = false;
            for (var attempt = 1; attempt <= 10; attempt++)
            {
                Thread.Sleep(1000);
                try
                {
                    _device.GetWheelRotation();
                    deviceReady = true;
                    Log?.Invoke($"Moza: device ready after {attempt}s");
                    break;
                }
                catch
                {
                    Log?.Invoke($"Moza: attempt {attempt}/10 - device not ready");
                }
            }

            if (!deviceReady)
            {
                Log?.Invoke("Moza: device discovery timed out after 10s");
                _device.Dispose();
                _device = null;
                return;
            }

            // Apply all settings. The SDK sets the physical wheel stop (rotation limit)
            // and other wheel feel parameters. Axis scaling for DirectInput is handled
            // separately by the binding's Input Range (MinValue/MaxValue) in the profile.
            LogAndApply(pluginData, "wheelRotation", _device.SetWheelRotation);
            LogAndApply(pluginData, "ffbStrength", _device.SetFfbStrength);
            LogAndApply(pluginData, "maxTorque", _device.SetMaxTorque);

            var ffbReverse = pluginData["ffbReverse"]?.GetValue<bool>();
            if (ffbReverse.HasValue)
            {
                Log?.Invoke($"Moza: setting ffbReverse = {ffbReverse.Value}");
                _device.SetFfbReverse(ffbReverse.Value);
            }

            LogAndApply(pluginData, "damping", _device.SetDamping);
            LogAndApply(pluginData, "springStrength", _device.SetSpringStrength);
            LogAndApply(pluginData, "naturalInertia", _device.SetNaturalInertia);
            LogAndApply(pluginData, "speedDamping", _device.SetSpeedDamping);

            Log?.Invoke("Moza: settings applied (SDK session kept alive)");
        }
        catch (Exception ex)
        {
            Log?.Invoke($"Moza: failed to apply settings: {ex.Message}");
        }
    }

    private void LogAndApply(JsonObject data, string key, Action<int> setter)
    {
        var value = data[key]?.GetValue<int>();
        if (value.HasValue)
        {
            Log?.Invoke($"Moza: setting {key} = {value.Value}");
            setter(value.Value);
        }
    }

    public void OnProfileStop()
    {
        if (_device != null)
        {
            _device.Dispose();
            _device = null;
            Log?.Invoke("Moza OnProfileStop: SDK session released");
        }
    }

    public void Dispose()
    {
        _device?.Dispose();
        _device = null;
    }
}
