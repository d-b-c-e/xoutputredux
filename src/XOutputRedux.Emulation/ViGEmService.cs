using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Exceptions;

namespace XOutputRedux.Emulation;

/// <summary>
/// Service for managing ViGEm emulation.
/// Adapted from XOutput.Emulation.ViGEm.ViGEmEmulator
/// </summary>
public class ViGEmService : IDisposable
{
    private ViGEmClient? _client;
    private bool _disposed;
    private bool _initialized;

    /// <summary>
    /// Whether ViGEm is installed and available.
    /// </summary>
    public bool IsAvailable { get; private set; }

    /// <summary>
    /// Initializes the ViGEm client.
    /// </summary>
    public bool Initialize()
    {
        if (_initialized) return IsAvailable;

        try
        {
            _client = new ViGEmClient();
            IsAvailable = true;
            _initialized = true;
        }
        catch (VigemBusNotFoundException)
        {
            IsAvailable = false;
            _initialized = true;
        }
        catch (DllNotFoundException)
        {
            IsAvailable = false;
            _initialized = true;
        }
        catch
        {
            IsAvailable = false;
            _initialized = true;
        }

        return IsAvailable;
    }

    /// <summary>
    /// Creates a new Xbox controller.
    /// </summary>
    public XboxController CreateXboxController()
    {
        if (!IsAvailable || _client == null)
            throw new InvalidOperationException("ViGEm is not available. Call Initialize() first and check IsAvailable.");

        var controller = _client.CreateXbox360Controller();
        return new XboxController(controller);
    }

    /// <summary>
    /// Downloads and installs ViGEmBus from GitHub releases.
    /// </summary>
    /// <param name="progress">Optional progress callback (0-100)</param>
    /// <returns>True if installation was successful</returns>
    public async Task<(bool Success, string Message)> DownloadAndInstallAsync(Action<int>? progress = null)
    {
        const string releasesApiUrl = "https://api.github.com/repos/nefarius/ViGEmBus/releases/latest";
        const string fallbackDownloadUrl = "https://github.com/nefarius/ViGEmBus/releases/download/v1.22.0/ViGEmBus_1.22.0_x64_x86_arm64.exe";

        string downloadUrl = fallbackDownloadUrl;
        string installerPath = Path.Combine(Path.GetTempPath(), "ViGEmBus_Installer.exe");

        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "XOutputRedux");

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
                            // Look for the combined installer exe
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

            // Reset initialized state so we can re-check
            _initialized = false;
            _client?.Dispose();
            _client = null;
            IsAvailable = false;

            // Re-initialize to pick up the new installation
            Initialize();

            if (IsAvailable)
            {
                return (true, "ViGEmBus installed successfully.");
            }
            else if (process.ExitCode == 0)
            {
                return (true, "Installer completed. A system restart may be required for ViGEmBus to be detected.");
            }
            else
            {
                return (false, $"Installer exited with code {process.ExitCode}. ViGEmBus may not be installed.");
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

        _client?.Dispose();
        _client = null;
        IsAvailable = false;

        GC.SuppressFinalize(this);
    }
}
