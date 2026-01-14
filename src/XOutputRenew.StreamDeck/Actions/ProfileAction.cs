using BarRaider.SdTools;
using BarRaider.SdTools.Events;
using BarRaider.SdTools.Wrappers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace XOutputRenew.StreamDeck.Actions;

[PluginActionId("com.xoutputrenew.profile")]
public class ProfileAction : KeypadBase
{
    private class PluginSettings
    {
        public static PluginSettings CreateDefaultSettings() => new();

        [JsonProperty(PropertyName = "profileName")]
        public string ProfileName { get; set; } = "";
    }

    private readonly string _baseIconPath;
    private PluginSettings _settings;
    private bool _isRunning;
    private DateTime _lastStatusCheck = DateTime.MinValue;

    public ProfileAction(ISDConnection connection, InitialPayload payload)
        : base(connection, payload)
    {
        // Get base icon path
        var pluginDir = Path.GetDirectoryName(typeof(ProfileAction).Assembly.Location) ?? "";
        _baseIconPath = Path.Combine(pluginDir, "Images", "profileAction@2x.png");

        if (payload.Settings == null || payload.Settings.Count == 0)
        {
            _settings = PluginSettings.CreateDefaultSettings();
            _ = SaveSettings();
        }
        else
        {
            _settings = payload.Settings.ToObject<PluginSettings>() ?? PluginSettings.CreateDefaultSettings();
        }

        // Subscribe to Property Inspector messages
        Connection.OnSendToPlugin += OnSendToPlugin;

        // Set initial image
        _ = UpdateImage();
    }

    private async void OnSendToPlugin(object? sender, SDEventReceivedEventArgs<SendToPlugin> e)
    {
        // Handle profile list request from Property Inspector
        if (e.Event.Payload.TryGetValue("action", out var actionToken) && actionToken.ToString() == "getProfiles")
        {
            try
            {
                var profiles = await XOutputClient.GetProfilesAsync();
                var profileList = profiles.Select(p => new { name = p.Name, isDefault = p.IsDefault }).ToList();

                await Connection.SendToPropertyInspectorAsync(JObject.FromObject(new
                {
                    profiles = profileList
                }));
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"Failed to get profiles: {ex.Message}");
            }
        }
    }

    public override async void KeyPressed(KeyPayload payload)
    {
        if (string.IsNullOrEmpty(_settings.ProfileName))
        {
            await Connection.ShowAlert();
            return;
        }

        try
        {
            bool success;
            if (_isRunning)
            {
                success = await XOutputClient.StopProfileAsync();
                if (success)
                {
                    _isRunning = false;
                    await UpdateImage();
                                    }
                else
                {
                    await Connection.ShowAlert();
                }
            }
            else
            {
                success = await XOutputClient.StartProfileAsync(_settings.ProfileName);
                if (success)
                {
                    _isRunning = true;
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
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"ProfileAction error: {ex.Message}");
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

    public override void Dispose()
    {
        Connection.OnSendToPlugin -= OnSendToPlugin;
    }

    public override void ReceivedSettings(ReceivedSettingsPayload payload)
    {
        Tools.AutoPopulateSettings(_settings, payload.Settings);
        _ = SaveSettings();
        _ = UpdateImage();
    }

    public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

    private async Task UpdateState()
    {
        try
        {
            var status = await XOutputClient.GetStatusAsync();
            var wasRunning = _isRunning;

            _isRunning = status?.IsRunning == true &&
                         string.Equals(status.ProfileName, _settings.ProfileName, StringComparison.OrdinalIgnoreCase);

            // Only update image if state changed
            if (_isRunning != wasRunning)
            {
                await UpdateImage();
            }
        }
        catch
        {
            if (_isRunning)
            {
                _isRunning = false;
                await UpdateImage();
            }
        }
    }

    private async Task UpdateImage()
    {
        string topText;
        string bottomText;

        if (string.IsNullOrEmpty(_settings.ProfileName))
        {
            topText = "Start/Stop";
            bottomText = "Profile";
        }
        else
        {
            topText = _settings.ProfileName;
            bottomText = _isRunning ? "On" : "Off";
        }

        var imageBase64 = IconGenerator.GenerateButtonImage(_baseIconPath, topText, bottomText);
        await Connection.SetImageAsync(imageBase64);
    }

    private Task SaveSettings()
    {
        return Connection.SetSettingsAsync(JObject.FromObject(_settings));
    }
}
