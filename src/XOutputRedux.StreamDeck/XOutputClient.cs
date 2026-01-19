using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BarRaider.SdTools;

namespace XOutputRedux.StreamDeck;

/// <summary>
/// Client for communicating with XOutputRedux via named pipe IPC.
/// No longer spawns CLI processes - connects directly to the running GUI.
/// </summary>
public static class XOutputClient
{
    private const string PipeName = "XOutputRedux_IPC";
    private const string ExeName = "XOutputRedux.exe";
    private const int ConnectionTimeout = 1000;
    private const int StartupWaitMs = 3000;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    #region IPC Protocol Classes

    private class IpcCommand
    {
        [JsonPropertyName("Command")]
        public string? Command { get; set; }

        [JsonPropertyName("ProfileName")]
        public string? ProfileName { get; set; }
    }

    private class IpcResult
    {
        [JsonPropertyName("Success")]
        public bool Success { get; set; }

        [JsonPropertyName("Message")]
        public string? Message { get; set; }

        [JsonPropertyName("Status")]
        public IpcStatus? Status { get; set; }
    }

    private class IpcStatus
    {
        [JsonPropertyName("IsRunning")]
        public bool IsRunning { get; set; }

        [JsonPropertyName("ProfileName")]
        public string? ProfileName { get; set; }

        [JsonPropertyName("IsMonitoring")]
        public bool IsMonitoring { get; set; }
    }

    #endregion

    #region Public Data Classes

    /// <summary>
    /// Status returned from XOutputRedux.
    /// </summary>
    public class StatusResult
    {
        public bool IsRunning { get; set; }
        public string? ProfileName { get; set; }
        public bool IsMonitoring { get; set; }
    }

    /// <summary>
    /// Profile information.
    /// </summary>
    public class ProfileInfo
    {
        public string Name { get; set; } = "";
        public bool IsDefault { get; set; }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Checks if XOutputRedux GUI is running (IPC server available).
    /// </summary>
    public static bool IsGuiRunning()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut);
            client.Connect(ConnectionTimeout);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the current status from the running XOutputRedux instance.
    /// </summary>
    public static async Task<StatusResult?> GetStatusAsync()
    {
        try
        {
            var result = await SendCommandAsync(new IpcCommand { Command = "status" });
            if (result?.Success == true && result.Status != null)
            {
                return new StatusResult
                {
                    IsRunning = result.Status.IsRunning,
                    ProfileName = result.Status.ProfileName,
                    IsMonitoring = result.Status.IsMonitoring
                };
            }
            return null;
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.DEBUG, $"GetStatus: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gets the list of available profiles by reading from disk.
    /// Does not require the GUI to be running.
    /// </summary>
    public static Task<List<ProfileInfo>> GetProfilesAsync()
    {
        var profiles = new List<ProfileInfo>();

        try
        {
            var profilesDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "XOutputRedux",
                "Profiles");

            if (!Directory.Exists(profilesDir))
                return Task.FromResult(profiles);

            foreach (var file in Directory.GetFiles(profilesDir, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    var name = root.TryGetProperty("name", out var nameProp)
                        ? nameProp.GetString() ?? Path.GetFileNameWithoutExtension(file)
                        : Path.GetFileNameWithoutExtension(file);

                    var isDefault = root.TryGetProperty("isDefault", out var defaultProp)
                        && defaultProp.GetBoolean();

                    profiles.Add(new ProfileInfo { Name = name, IsDefault = isDefault });
                }
                catch
                {
                    // Skip invalid profile files
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"GetProfiles error: {ex.Message}");
        }

        return Task.FromResult(profiles);
    }

    /// <summary>
    /// Starts a profile. Launches GUI minimized if not running.
    /// </summary>
    public static async Task<bool> StartProfileAsync(string profileName)
    {
        try
        {
            await EnsureGuiRunningAsync();
            var result = await SendCommandAsync(new IpcCommand
            {
                Command = "start",
                ProfileName = profileName
            });
            return result?.Success == true;
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"StartProfile error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Stops the running profile.
    /// </summary>
    public static async Task<bool> StopProfileAsync()
    {
        try
        {
            if (!IsGuiRunning())
                return true; // Nothing to stop

            var result = await SendCommandAsync(new IpcCommand { Command = "stop" });
            return result?.Success == true;
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"StopProfile error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Starts game monitoring. Launches GUI minimized if not running.
    /// </summary>
    public static async Task<bool> StartMonitoringAsync()
    {
        try
        {
            await EnsureGuiRunningAsync();
            var result = await SendCommandAsync(new IpcCommand { Command = "monitor-on" });
            return result?.Success == true;
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"StartMonitoring error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Stops game monitoring.
    /// </summary>
    public static async Task<bool> StopMonitoringAsync()
    {
        try
        {
            if (!IsGuiRunning())
                return true; // Nothing to stop

            var result = await SendCommandAsync(new IpcCommand { Command = "monitor-off" });
            return result?.Success == true;
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"StopMonitoring error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Launches the XOutputRedux GUI.
    /// </summary>
    public static bool LaunchApp()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = ExeName,
                Arguments = "run",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process.Start(startInfo);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"LaunchApp error: {ex.Message}");
            return false;
        }
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Ensures the GUI is running, starting it minimized if necessary.
    /// </summary>
    private static async Task EnsureGuiRunningAsync()
    {
        if (IsGuiRunning())
            return;

        Logger.Instance.LogMessage(TracingLevel.INFO, "XOutputRedux not running, starting minimized...");

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = ExeName,
                Arguments = "--minimized",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process.Start(startInfo);

            // Wait for IPC server to become available
            var startTime = DateTime.Now;
            while ((DateTime.Now - startTime).TotalMilliseconds < StartupWaitMs)
            {
                await Task.Delay(200);
                if (IsGuiRunning())
                {
                    Logger.Instance.LogMessage(TracingLevel.INFO, "XOutputRedux started successfully");
                    return;
                }
            }

            Logger.Instance.LogMessage(TracingLevel.WARN, "XOutputRedux started but IPC not responding");
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"Failed to start XOutputRedux: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Sends a command to the running GUI via named pipe IPC.
    /// </summary>
    private static async Task<IpcResult?> SendCommandAsync(IpcCommand command)
    {
        using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut);

        await Task.Run(() => client.Connect(ConnectionTimeout));

        // Send command (length-prefixed JSON)
        var commandJson = JsonSerializer.Serialize(command, JsonOptions);
        var commandBytes = Encoding.UTF8.GetBytes(commandJson);
        var lengthBytes = BitConverter.GetBytes(commandBytes.Length);

        await client.WriteAsync(lengthBytes, 0, 4);
        await client.WriteAsync(commandBytes, 0, commandBytes.Length);
        await client.FlushAsync();

        // Read response (length-prefixed JSON)
        var responseLengthBytes = new byte[4];
        var bytesRead = await client.ReadAsync(responseLengthBytes, 0, 4);
        if (bytesRead != 4)
            return null;

        var responseLength = BitConverter.ToInt32(responseLengthBytes, 0);
        if (responseLength <= 0 || responseLength > 65536)
            return null;

        var responseBytes = new byte[responseLength];
        var totalRead = 0;
        while (totalRead < responseLength)
        {
            var read = await client.ReadAsync(responseBytes, totalRead, responseLength - totalRead);
            if (read == 0) break;
            totalRead += read;
        }

        var responseJson = Encoding.UTF8.GetString(responseBytes, 0, totalRead);
        return JsonSerializer.Deserialize<IpcResult>(responseJson, JsonOptions);
    }

    #endregion
}
