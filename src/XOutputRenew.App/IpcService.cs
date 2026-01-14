using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace XOutputRenew.App;

/// <summary>
/// Inter-process communication service using named pipes.
/// Allows a running instance to receive commands from other instances.
/// </summary>
public class IpcService : IDisposable
{
    private const string PipeName = "XOutputRenew_IPC";
    private const int ConnectionTimeout = 1000; // ms

    private readonly CancellationTokenSource _cts = new();
    private NamedPipeServerStream? _serverPipe;
    private Task? _serverTask;
    private bool _disposed;

    /// <summary>
    /// Event raised when a start profile command is received.
    /// </summary>
    public event Action<string>? StartProfileRequested;

    /// <summary>
    /// Event raised when a stop command is received.
    /// </summary>
    public event Action? StopRequested;

    /// <summary>
    /// Event raised when monitoring should be enabled.
    /// </summary>
    public event Action? MonitoringEnableRequested;

    /// <summary>
    /// Event raised when monitoring should be disabled.
    /// </summary>
    public event Action? MonitoringDisableRequested;

    /// <summary>
    /// Delegate to get the current status.
    /// </summary>
    public Func<IpcStatus>? GetStatus { get; set; }

    /// <summary>
    /// Starts the IPC server to listen for incoming commands.
    /// </summary>
    public void StartServer()
    {
        _serverTask = Task.Run(ServerLoop);
        AppLogger.Info("IPC server started");
    }

    /// <summary>
    /// Checks if another instance is already running.
    /// </summary>
    public static bool IsAnotherInstanceRunning()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut);
            client.Connect(ConnectionTimeout);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Sends a start profile command to the running instance.
    /// </summary>
    public static IpcResult SendStartCommand(string profileName)
    {
        return SendCommand(new IpcCommand { Command = "start", ProfileName = profileName });
    }

    /// <summary>
    /// Sends a stop command to the running instance.
    /// </summary>
    public static IpcResult SendStopCommand()
    {
        return SendCommand(new IpcCommand { Command = "stop" });
    }

    /// <summary>
    /// Sends a status command to the running instance and returns the status.
    /// </summary>
    public static IpcResult SendStatusCommand()
    {
        return SendCommand(new IpcCommand { Command = "status" });
    }

    /// <summary>
    /// Sends a command to enable game monitoring.
    /// </summary>
    public static IpcResult SendMonitoringOnCommand()
    {
        return SendCommand(new IpcCommand { Command = "monitor-on" });
    }

    /// <summary>
    /// Sends a command to disable game monitoring.
    /// </summary>
    public static IpcResult SendMonitoringOffCommand()
    {
        return SendCommand(new IpcCommand { Command = "monitor-off" });
    }

    private static IpcResult SendCommand(IpcCommand command)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut);
            client.Connect(ConnectionTimeout);

            // Send command
            var commandJson = JsonSerializer.Serialize(command);
            var commandBytes = Encoding.UTF8.GetBytes(commandJson);
            var lengthBytes = BitConverter.GetBytes(commandBytes.Length);
            client.Write(lengthBytes, 0, 4);
            client.Write(commandBytes, 0, commandBytes.Length);
            client.Flush();

            // Read response
            var responseLengthBytes = new byte[4];
            if (client.Read(responseLengthBytes, 0, 4) != 4)
                return new IpcResult { Success = false, Message = "Failed to read response length" };

            var responseLength = BitConverter.ToInt32(responseLengthBytes, 0);
            var responseBytes = new byte[responseLength];
            var totalRead = 0;
            while (totalRead < responseLength)
            {
                var read = client.Read(responseBytes, totalRead, responseLength - totalRead);
                if (read == 0) break;
                totalRead += read;
            }

            var responseJson = Encoding.UTF8.GetString(responseBytes, 0, totalRead);
            return JsonSerializer.Deserialize<IpcResult>(responseJson) ?? new IpcResult { Success = false, Message = "Invalid response" };
        }
        catch (TimeoutException)
        {
            return new IpcResult { Success = false, Message = "No running instance found" };
        }
        catch (Exception ex)
        {
            return new IpcResult { Success = false, Message = $"IPC error: {ex.Message}" };
        }
    }

    private async Task ServerLoop()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                _serverPipe = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await _serverPipe.WaitForConnectionAsync(_cts.Token);
                await HandleClientAsync(_serverPipe);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                AppLogger.Warning($"IPC server error: {ex.Message}");
            }
            finally
            {
                _serverPipe?.Dispose();
                _serverPipe = null;
            }
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream pipe)
    {
        try
        {
            // Read command length
            var lengthBytes = new byte[4];
            if (await pipe.ReadAsync(lengthBytes, 0, 4, _cts.Token) != 4)
                return;

            var length = BitConverter.ToInt32(lengthBytes, 0);
            if (length <= 0 || length > 65536) return;

            // Read command
            var commandBytes = new byte[length];
            var totalRead = 0;
            while (totalRead < length)
            {
                var read = await pipe.ReadAsync(commandBytes, totalRead, length - totalRead, _cts.Token);
                if (read == 0) break;
                totalRead += read;
            }

            var commandJson = Encoding.UTF8.GetString(commandBytes, 0, totalRead);
            var command = JsonSerializer.Deserialize<IpcCommand>(commandJson);

            if (command == null)
            {
                await SendResponseAsync(pipe, new IpcResult { Success = false, Message = "Invalid command" });
                return;
            }

            // Process command
            var result = ProcessCommand(command);

            // Send response
            await SendResponseAsync(pipe, result);
        }
        catch (Exception ex)
        {
            AppLogger.Warning($"IPC client handler error: {ex.Message}");
        }
    }

    private IpcResult ProcessCommand(IpcCommand command)
    {
        AppLogger.Info($"IPC received command: {command.Command}");

        switch (command.Command?.ToLowerInvariant())
        {
            case "start":
                if (string.IsNullOrEmpty(command.ProfileName))
                    return new IpcResult { Success = false, Message = "Profile name required" };

                StartProfileRequested?.Invoke(command.ProfileName);
                return new IpcResult { Success = true, Message = $"Starting profile: {command.ProfileName}" };

            case "stop":
                StopRequested?.Invoke();
                return new IpcResult { Success = true, Message = "Stop command sent" };

            case "status":
                var status = GetStatus?.Invoke() ?? new IpcStatus();
                return new IpcResult
                {
                    Success = true,
                    Message = status.IsRunning ? $"Running: {status.ProfileName}" : "No profile running",
                    Status = status
                };

            case "monitor-on":
                MonitoringEnableRequested?.Invoke();
                return new IpcResult { Success = true, Message = "Game monitoring enabled" };

            case "monitor-off":
                MonitoringDisableRequested?.Invoke();
                return new IpcResult { Success = true, Message = "Game monitoring disabled" };

            default:
                return new IpcResult { Success = false, Message = $"Unknown command: {command.Command}" };
        }
    }

    private async Task SendResponseAsync(NamedPipeServerStream pipe, IpcResult result)
    {
        var responseJson = JsonSerializer.Serialize(result);
        var responseBytes = Encoding.UTF8.GetBytes(responseJson);
        var lengthBytes = BitConverter.GetBytes(responseBytes.Length);
        await pipe.WriteAsync(lengthBytes, 0, 4, _cts.Token);
        await pipe.WriteAsync(responseBytes, 0, responseBytes.Length, _cts.Token);
        await pipe.FlushAsync(_cts.Token);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts.Cancel();
        _serverPipe?.Dispose();

        try
        {
            _serverTask?.Wait(1000);
        }
        catch { }

        _cts.Dispose();
        AppLogger.Info("IPC server stopped");
    }
}

/// <summary>
/// Command sent via IPC.
/// </summary>
public class IpcCommand
{
    public string? Command { get; set; }
    public string? ProfileName { get; set; }
}

/// <summary>
/// Result returned from IPC command.
/// </summary>
public class IpcResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public IpcStatus? Status { get; set; }
}

/// <summary>
/// Status information returned from status command.
/// </summary>
public class IpcStatus
{
    public bool IsRunning { get; set; }
    public string? ProfileName { get; set; }
    public string? ViGEmStatus { get; set; }
    public string? HidHideStatus { get; set; }
}
