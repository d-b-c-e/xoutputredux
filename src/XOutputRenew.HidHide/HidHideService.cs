using System.Diagnostics;
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
        try
        {
            // Check registry for installation
            using var key = Registry.ClassesRoot.OpenSubKey(
                @"Installer\Dependencies\NSS.Drivers.HidHide.x64");

            if (key?.GetValue("Version") is string version)
            {
                Version = version;
                _cliPath = FindCliPath();
                IsAvailable = _cliPath != null;
                return IsAvailable;
            }
        }
        catch
        {
            // Registry access failed
        }

        IsAvailable = false;
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
        var (success, output) = ExecuteCommandWithOutput("--dev-gaming");
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
            return [];
        }
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
        ];

        foreach (var path in searchPaths)
        {
            if (File.Exists(path))
                return path;
        }

        // Try to find via registry
        try
        {
            using var key = Registry.ClassesRoot.OpenSubKey(
                @"SOFTWARE\Nefarius Software Solutions e.U.\Nefarius Software Solutions e.U. HidHide");

            if (key?.GetValue("Path") is string installPath)
            {
                var cliPath = Path.Combine(installPath, "x64", "HidHideCLI.exe");
                if (File.Exists(cliPath))
                    return cliPath;
            }
        }
        catch
        {
            // Registry access failed
        }

        // Fallback: assume it's in PATH
        return "HidHideCLI.exe";
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
