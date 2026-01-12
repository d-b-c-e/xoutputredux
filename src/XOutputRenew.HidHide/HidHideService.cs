using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Win32;

namespace XOutputRenew.HidHide;

/// <summary>
/// Service for managing HidHide device hiding via CLI.
/// </summary>
public class HidHideService : IDisposable
{
    private bool _disposed;
    private string? _cliPath;

    /// <summary>
    /// Whether HidHide is installed and available.
    /// </summary>
    public bool IsAvailable { get; private set; }

    /// <summary>
    /// HidHide version if installed.
    /// </summary>
    public string? Version { get; private set; }

    /// <summary>
    /// Initializes the HidHide service by checking installation.
    /// </summary>
    public bool Initialize()
    {
        IsAvailable = false;
        Version = null;
        _cliPath = null;

        // First, try to find the CLI executable
        _cliPath = FindCliPath();
        if (_cliPath == null)
        {
            return false;
        }

        // Verify the CLI actually works by running a simple command
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _cliPath,
                    Arguments = "--cloak-state",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit(5000);

            // If the CLI runs without error, HidHide is installed
            if (process.ExitCode == 0)
            {
                IsAvailable = true;

                // Try to get version from registry
                try
                {
                    using var key = Registry.ClassesRoot.OpenSubKey(
                        @"Installer\Dependencies\NSS.Drivers.HidHide.x64");
                    Version = key?.GetValue("Version") as string;
                }
                catch { }

                // Fallback version detection
                if (string.IsNullOrEmpty(Version))
                {
                    Version = "Installed";
                }

                return true;
            }
        }
        catch
        {
            // CLI execution failed
        }

        _cliPath = null;
        return false;
    }

    /// <summary>
    /// Enables device hiding (cloaking).
    /// </summary>
    public bool EnableCloaking()
    {
        return ExecuteCommand("--cloak-on");
    }

    /// <summary>
    /// Disables device hiding (cloaking).
    /// </summary>
    public bool DisableCloaking()
    {
        return ExecuteCommand("--cloak-off");
    }

    /// <summary>
    /// Gets whether cloaking is currently enabled.
    /// </summary>
    public bool? IsCloakingEnabled()
    {
        var (success, output) = ExecuteCommandWithOutput("--cloak-state");
        if (!success) return null;

        return output?.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) == true;
    }

    /// <summary>
    /// Hides a device by its device instance path.
    /// </summary>
    public bool HideDevice(string deviceInstancePath)
    {
        if (string.IsNullOrWhiteSpace(deviceInstancePath)) return false;
        return ExecuteCommand("--dev-hide", $"\"{deviceInstancePath}\"");
    }

    /// <summary>
    /// Unhides a device by its device instance path.
    /// </summary>
    public bool UnhideDevice(string deviceInstancePath)
    {
        if (string.IsNullOrWhiteSpace(deviceInstancePath)) return false;
        return ExecuteCommand("--dev-unhide", $"\"{deviceInstancePath}\"");
    }

    /// <summary>
    /// Gets list of currently hidden device instance paths.
    /// </summary>
    public IEnumerable<string> GetHiddenDevices()
    {
        var (success, output) = ExecuteCommandWithOutput("--dev-list");
        if (!success || string.IsNullOrWhiteSpace(output))
            return [];

        // Output is one device per line
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s));
    }

    /// <summary>
    /// Gets list of all gaming devices (joysticks, gamepads, etc.)
    /// </summary>
    public IEnumerable<HidHideDevice> GetGamingDevices()
    {
        // Try --dev-gaming first, fall back to --dev-all
        var (success, output) = ExecuteCommandWithOutput("--dev-gaming");

        if (!success || string.IsNullOrWhiteSpace(output))
        {
            // Try alternative command
            (success, output) = ExecuteCommandWithOutput("--dev-all");
        }

        if (!success || string.IsNullOrWhiteSpace(output))
            return [];

        try
        {
            var devices = JsonSerializer.Deserialize<List<HidHideDevice>>(output,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return devices ?? [];
        }
        catch
        {
            // JSON parsing failed - try to parse as simple list
            return [];
        }
    }

    /// <summary>
    /// Gets the raw output from a HidHide CLI command (for debugging).
    /// </summary>
    public string? GetCommandOutput(string command)
    {
        var (success, output) = ExecuteCommandWithOutput(command);
        return output;
    }

    /// <summary>
    /// Adds an application to the whitelist (can see hidden devices).
    /// </summary>
    public bool WhitelistApplication(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath)) return false;
        if (!File.Exists(executablePath)) return false;

        return ExecuteCommand("--app-reg", $"\"{executablePath}\"");
    }

    /// <summary>
    /// Removes an application from the whitelist.
    /// </summary>
    public bool UnwhitelistApplication(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath)) return false;
        return ExecuteCommand("--app-unreg", $"\"{executablePath}\"");
    }

    /// <summary>
    /// Gets list of whitelisted application paths.
    /// </summary>
    public IEnumerable<string> GetWhitelistedApplications()
    {
        var (success, output) = ExecuteCommandWithOutput("--app-list");
        if (!success || string.IsNullOrWhiteSpace(output))
            return [];

        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s));
    }

    /// <summary>
    /// Whitelists the current application.
    /// </summary>
    public bool WhitelistSelf()
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath)) return false;
        return WhitelistApplication(exePath);
    }

    private string? FindCliPath()
    {
        // Check common installation paths
        string[] searchPaths =
        [
            @"C:\Program Files\Nefarius Software Solutions e.U.\HidHideCLI\HidHideCLI.exe",
            @"C:\Program Files\Nefarius Software Solutions\HidHide\x64\HidHideCLI.exe",
            @"C:\Program Files\Nefarius Software Solutions e.U.\HidHide\x64\HidHideCLI.exe",
            @"C:\Program Files\Nefarius Software Solutions e.U.\HidHide\HidHideCLI.exe",
        ];

        foreach (var path in searchPaths)
        {
            if (File.Exists(path))
                return path;
        }

        // Try to find via registry (HKLM\SOFTWARE)
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Nefarius Software Solutions e.U.\HidHide");

            if (key?.GetValue("Path") is string installPath)
            {
                // Try both with and without x64 subfolder
                var cliPath = Path.Combine(installPath, "x64", "HidHideCLI.exe");
                if (File.Exists(cliPath))
                    return cliPath;

                cliPath = Path.Combine(installPath, "HidHideCLI.exe");
                if (File.Exists(cliPath))
                    return cliPath;
            }
        }
        catch
        {
            // Registry access failed
        }

        // Not found
        return null;
    }

    private bool ExecuteCommand(string command, string args = "")
    {
        if (!IsAvailable || _cliPath == null) return false;

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _cliPath,
                    Arguments = $"{command} {args}".Trim(),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit(10000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private (bool Success, string? Output) ExecuteCommandWithOutput(string command, string args = "")
    {
        if (!IsAvailable || _cliPath == null)
            return (false, null);

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _cliPath,
                    Arguments = $"{command} {args}".Trim(),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(10000);

            return (process.ExitCode == 0, output);
        }
        catch
        {
            return (false, null);
        }
    }

    /// <summary>
    /// Downloads and installs HidHide from GitHub releases.
    /// </summary>
    /// <param name="progress">Optional progress callback (0-100)</param>
    /// <returns>True if installation was successful</returns>
    public async Task<(bool Success, string Message)> DownloadAndInstallAsync(Action<int>? progress = null)
    {
        const string releasesApiUrl = "https://api.github.com/repos/nefarius/HidHide/releases/latest";
        const string fallbackDownloadUrl = "https://github.com/nefarius/HidHide/releases/download/v1.5.230.0/HidHide_1.5.230_x64.exe";

        string downloadUrl = fallbackDownloadUrl;
        string installerPath = Path.Combine(Path.GetTempPath(), "HidHide_Installer.exe");

        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "XOutputRenew");

            // Try to get latest release info from GitHub API
            progress?.Invoke(5);
            try
            {
                var releaseJson = await httpClient.GetStringAsync(releasesApiUrl);
                var release = JsonSerializer.Deserialize<JsonElement>(releaseJson);

                if (release.TryGetProperty("assets", out var assets))
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        if (asset.TryGetProperty("name", out var name) &&
                            asset.TryGetProperty("browser_download_url", out var url))
                        {
                            var fileName = name.GetString() ?? "";
                            if (fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                                fileName.Contains("x64", StringComparison.OrdinalIgnoreCase))
                            {
                                downloadUrl = url.GetString() ?? fallbackDownloadUrl;
                                break;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Use fallback URL if API fails
            }

            // Download the installer
            progress?.Invoke(10);
            using (var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                var downloadedBytes = 0L;

                using (var contentStream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(installerPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var buffer = new byte[8192];
                    int bytesRead;
                    while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                        downloadedBytes += bytesRead;

                        if (totalBytes > 0)
                        {
                            var percent = (int)(10 + (downloadedBytes * 80 / totalBytes));
                            progress?.Invoke(Math.Min(percent, 90));
                        }
                    }
                }
            }

            progress?.Invoke(90);

            // Run the installer (no silent mode - let user see the installer UI)
            var processInfo = new ProcessStartInfo
            {
                FileName = installerPath,
                UseShellExecute = true,
                Verb = "runas" // Request admin elevation
            };

            var process = Process.Start(processInfo);
            if (process == null)
            {
                return (false, "Failed to start installer");
            }

            await process.WaitForExitAsync();
            progress?.Invoke(100);

            // Re-initialize to pick up the new installation (regardless of exit code, user may have installed)
            Initialize();

            if (IsAvailable)
            {
                return (true, "HidHide installed successfully. A system restart may be required.");
            }
            else if (process.ExitCode == 0)
            {
                return (true, "Installer completed. A system restart may be required for HidHide to be detected.");
            }
            else
            {
                return (false, $"Installer exited with code {process.ExitCode}. HidHide may not be installed.");
            }
        }
        catch (HttpRequestException ex)
        {
            return (false, $"Download failed: {ex.Message}");
        }
        catch (Exception ex) when (ex.Message.Contains("canceled") || ex.Message.Contains("elevation"))
        {
            return (false, "Installation was cancelled or elevation was denied.");
        }
        catch (Exception ex)
        {
            return (false, $"Installation failed: {ex.Message}");
        }
        finally
        {
            // Clean up installer file
            try
            {
                if (File.Exists(installerPath))
                {
                    File.Delete(installerPath);
                }
            }
            catch { }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Represents a HID device as returned by HidHide.
/// </summary>
public class HidHideDevice
{
    public bool Present { get; set; }
    public bool GamingDevice { get; set; }
    public string? SymbolicLink { get; set; }
    public string? Vendor { get; set; }
    public string? Product { get; set; }
    public string? SerialNumber { get; set; }
    public string? Usage { get; set; }
    public string? Description { get; set; }
    public string? DeviceInstancePath { get; set; }
    public string? BaseContainerDeviceInstancePath { get; set; }
}
