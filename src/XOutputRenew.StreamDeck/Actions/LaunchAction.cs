using BarRaider.SdTools;

namespace XOutputRenew.StreamDeck.Actions;

[PluginActionId("com.xoutputrenew.launch")]
public class LaunchAction : KeypadBase
{
    private readonly string _baseIconPath;

    public LaunchAction(ISDConnection connection, InitialPayload payload)
        : base(connection, payload)
    {
        // Get base icon path
        var pluginDir = Path.GetDirectoryName(typeof(LaunchAction).Assembly.Location) ?? "";
        _baseIconPath = Path.Combine(pluginDir, "Images", "launchAppAction@2x.png");

        // Set initial image
        _ = UpdateImage();
    }

    public override async void KeyPressed(KeyPayload payload)
    {
        try
        {
            var success = XOutputClient.LaunchApp();
            if (success)
            {
                            }
            else
            {
                await Connection.ShowAlert();
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"LaunchAction error: {ex.Message}");
            await Connection.ShowAlert();
        }
    }

    public override void KeyReleased(KeyPayload payload) { }

    public override void OnTick() { }

    public override void Dispose() { }

    public override void ReceivedSettings(ReceivedSettingsPayload payload) { }

    public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

    private async Task UpdateImage()
    {
        var imageBase64 = IconGenerator.GenerateButtonImage(_baseIconPath, "Launch", "App");
        await Connection.SetImageAsync(imageBase64);
    }
}
