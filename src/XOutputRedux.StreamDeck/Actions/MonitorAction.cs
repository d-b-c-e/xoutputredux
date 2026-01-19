using BarRaider.SdTools;

namespace XOutputRedux.StreamDeck.Actions;

[PluginActionId("com.XOutputRedux.monitor")]
public class MonitorAction : KeypadBase
{
    private readonly string _baseIconPath;
    private bool _isMonitoring;
    private DateTime _lastStatusCheck = DateTime.MinValue;

    public MonitorAction(ISDConnection connection, InitialPayload payload)
        : base(connection, payload)
    {
        // Get base icon path
        var pluginDir = Path.GetDirectoryName(typeof(MonitorAction).Assembly.Location) ?? "";
        _baseIconPath = Path.Combine(pluginDir, "Images", "monitorAction@2x.png");

        // Set initial image
        _ = UpdateImage();
    }

    public override async void KeyPressed(KeyPayload payload)
    {
        try
        {
            bool success;
            if (_isMonitoring)
            {
                success = await XOutputClient.StopMonitoringAsync();
                if (success)
                {
                    _isMonitoring = false;
                    await UpdateImage();
                                    }
                else
                {
                    await Connection.ShowAlert();
                }
            }
            else
            {
                success = await XOutputClient.StartMonitoringAsync();
                if (success)
                {
                    _isMonitoring = true;
                    await UpdateImage();
                                    }
                else
                {
                    await Connection.ShowAlert();
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"MonitorAction error: {ex.Message}");
            await Connection.ShowAlert();
        }
    }

    public override void KeyReleased(KeyPayload payload) { }

    public override async void OnTick()
    {
        // Check status every 3 seconds
        if ((DateTime.Now - _lastStatusCheck).TotalSeconds >= 3)
        {
            _lastStatusCheck = DateTime.Now;
            await UpdateState();
        }
    }

    public override void Dispose() { }

    public override void ReceivedSettings(ReceivedSettingsPayload payload) { }

    public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

    private async Task UpdateState()
    {
        try
        {
            var status = await XOutputClient.GetStatusAsync();
            var wasMonitoring = _isMonitoring;

            _isMonitoring = status?.IsMonitoring == true;

            // Only update image if state changed
            if (_isMonitoring != wasMonitoring)
            {
                await UpdateImage();
            }
        }
        catch
        {
            if (_isMonitoring)
            {
                _isMonitoring = false;
                await UpdateImage();
            }
        }
    }

    private async Task UpdateImage()
    {
        var bottomText = _isMonitoring ? "On" : "Off";
        var imageBase64 = IconGenerator.GenerateButtonImage(_baseIconPath, "Game Monitor", bottomText);
        await Connection.SetImageAsync(imageBase64);
    }
}
