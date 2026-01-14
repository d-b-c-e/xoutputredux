using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace XOutputRenew.App;

/// <summary>
/// Service for checking and downloading updates from GitHub Releases.
/// </summary>
public class UpdateService
{
    private const string GitHubApiUrl = "https://api.github.com/repos/d-b-c-e/xoutputrenew/releases";
    private const string UserAgent = "XOutputRenew-UpdateChecker";

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    static UpdateService()
    {
        HttpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);
    }

    /// <summary>
    /// Gets the current application version.
    /// </summary>
    public static SemanticVersion GetCurrentVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version ?? new Version(0, 0, 0);

        // Check for informational version which may contain pre-release tag
        var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (!string.IsNullOrEmpty(infoVersion))
        {
            // Parse informational version like "0.7.0-alpha" or "0.8.0-beta.2+build123"
            return SemanticVersion.Parse(infoVersion);
        }

        return new SemanticVersion(version.Major, version.Minor, version.Build, null);
    }

    /// <summary>
    /// Checks for available updates on GitHub.
    /// </summary>
    /// <returns>Release info if update available, null otherwise</returns>
    public async Task<ReleaseInfo?> CheckForUpdateAsync()
    {
        try
        {
            var response = await HttpClient.GetStringAsync(GitHubApiUrl);
            var releases = JsonSerializer.Deserialize<JsonElement[]>(response);

            if (releases == null || releases.Length == 0)
                return null;

            var currentVersion = GetCurrentVersion();
            ReleaseInfo? latestRelease = null;
            SemanticVersion? latestVersion = null;

            foreach (var release in releases)
            {
                // Skip drafts
                if (release.TryGetProperty("draft", out var draft) && draft.GetBoolean())
                    continue;

                var tagName = release.GetProperty("tag_name").GetString();
                if (string.IsNullOrEmpty(tagName))
                    continue;

                var releaseVersion = SemanticVersion.Parse(tagName);

                // Track the highest version we find
                if (latestVersion == null || releaseVersion > latestVersion)
                {
                    latestVersion = releaseVersion;

                    // Find the installer asset
                    string? downloadUrl = null;
                    string? assetName = null;
                    long assetSize = 0;

                    if (release.TryGetProperty("assets", out var assets))
                    {
                        foreach (var asset in assets.EnumerateArray())
                        {
                            var name = asset.GetProperty("name").GetString() ?? "";
                            // Look for Setup.exe installer
                            if (name.Contains("Setup", StringComparison.OrdinalIgnoreCase) &&
                                name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                            {
                                downloadUrl = asset.GetProperty("browser_download_url").GetString();
                                assetName = name;
                                assetSize = asset.GetProperty("size").GetInt64();
                                break;
                            }
                        }
                    }

                    if (downloadUrl != null)
                    {
                        latestRelease = new ReleaseInfo
                        {
                            Version = releaseVersion,
                            TagName = tagName,
                            Name = release.GetProperty("name").GetString() ?? tagName,
                            Body = release.GetProperty("body").GetString() ?? "",
                            IsPrerelease = release.TryGetProperty("prerelease", out var pre) && pre.GetBoolean(),
                            DownloadUrl = downloadUrl,
                            AssetName = assetName!,
                            AssetSize = assetSize,
                            PublishedAt = release.TryGetProperty("published_at", out var pub)
                                ? DateTime.Parse(pub.GetString() ?? DateTime.UtcNow.ToString())
                                : DateTime.UtcNow,
                            HtmlUrl = release.GetProperty("html_url").GetString() ?? ""
                        };
                    }
                }
            }

            // Return release only if it's newer than current
            if (latestRelease != null && latestVersion != null && latestVersion > currentVersion)
            {
                return latestRelease;
            }

            return null;
        }
        catch (Exception ex)
        {
            AppLogger.Warning($"Failed to check for updates: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Downloads the installer to a temp file.
    /// </summary>
    /// <param name="release">The release to download</param>
    /// <param name="progress">Progress callback (0-100)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Path to downloaded installer</returns>
    public async Task<string> DownloadInstallerAsync(
        ReleaseInfo release,
        Action<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), release.AssetName);

        using var response = await HttpClient.GetAsync(
            release.DownloadUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? release.AssetSize;
        var downloadedBytes = 0L;

        using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);

        var buffer = new byte[8192];
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            downloadedBytes += bytesRead;

            if (totalBytes > 0)
            {
                var percent = (int)(downloadedBytes * 100 / totalBytes);
                progress?.Invoke(Math.Min(percent, 100));
            }
        }

        progress?.Invoke(100);
        return tempPath;
    }

    /// <summary>
    /// Launches the installer and exits the application.
    /// </summary>
    public static void LaunchInstallerAndExit(string installerPath)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = installerPath,
                UseShellExecute = true
            });

            // Give the installer a moment to start
            Thread.Sleep(500);

            // Exit the application
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to launch installer", ex);
            throw;
        }
    }
}

/// <summary>
/// Information about a GitHub release.
/// </summary>
public class ReleaseInfo
{
    public required SemanticVersion Version { get; init; }
    public required string TagName { get; init; }
    public required string Name { get; init; }
    public required string Body { get; init; }
    public required bool IsPrerelease { get; init; }
    public required string DownloadUrl { get; init; }
    public required string AssetName { get; init; }
    public required long AssetSize { get; init; }
    public required DateTime PublishedAt { get; init; }
    public required string HtmlUrl { get; init; }

    public string FormattedSize => AssetSize switch
    {
        < 1024 => $"{AssetSize} B",
        < 1024 * 1024 => $"{AssetSize / 1024.0:F1} KB",
        _ => $"{AssetSize / (1024.0 * 1024.0):F1} MB"
    };
}

/// <summary>
/// Semantic version with pre-release support.
/// Handles versions like "0.7.0", "0.8.0-alpha", "0.8.0-beta.2"
/// </summary>
public class SemanticVersion : IComparable<SemanticVersion>
{
    public int Major { get; }
    public int Minor { get; }
    public int Patch { get; }
    public string? Prerelease { get; }

    public SemanticVersion(int major, int minor, int patch, string? prerelease)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        Prerelease = string.IsNullOrWhiteSpace(prerelease) ? null : prerelease.Trim();
    }

    public static SemanticVersion Parse(string version)
    {
        // Remove 'v' prefix if present
        version = version.TrimStart('v', 'V');

        // Remove build metadata (+build123)
        var plusIndex = version.IndexOf('+');
        if (plusIndex >= 0)
            version = version[..plusIndex];

        // Split on hyphen for prerelease
        string? prerelease = null;
        var hyphenIndex = version.IndexOf('-');
        if (hyphenIndex >= 0)
        {
            prerelease = version[(hyphenIndex + 1)..];
            version = version[..hyphenIndex];
        }

        // Parse major.minor.patch
        var parts = version.Split('.');
        var major = parts.Length > 0 ? int.Parse(parts[0]) : 0;
        var minor = parts.Length > 1 ? int.Parse(parts[1]) : 0;
        var patch = parts.Length > 2 ? int.Parse(parts[2]) : 0;

        return new SemanticVersion(major, minor, patch, prerelease);
    }

    public int CompareTo(SemanticVersion? other)
    {
        if (other == null) return 1;

        // Compare major.minor.patch
        var result = Major.CompareTo(other.Major);
        if (result != 0) return result;

        result = Minor.CompareTo(other.Minor);
        if (result != 0) return result;

        result = Patch.CompareTo(other.Patch);
        if (result != 0) return result;

        // Pre-release comparison:
        // - No prerelease > any prerelease (1.0.0 > 1.0.0-alpha)
        // - alpha < beta < rc
        if (Prerelease == null && other.Prerelease == null) return 0;
        if (Prerelease == null) return 1;  // This is release, other is prerelease
        if (other.Prerelease == null) return -1;  // This is prerelease, other is release

        return ComparePrerelease(Prerelease, other.Prerelease);
    }

    private static int ComparePrerelease(string a, string b)
    {
        // Extract type and number: "alpha" -> ("alpha", 0), "beta.2" -> ("beta", 2)
        var (typeA, numA) = ParsePrerelease(a);
        var (typeB, numB) = ParsePrerelease(b);

        // Compare types: alpha < beta < rc
        var typeOrder = new[] { "alpha", "beta", "rc" };
        var indexA = Array.FindIndex(typeOrder, t => typeA.StartsWith(t, StringComparison.OrdinalIgnoreCase));
        var indexB = Array.FindIndex(typeOrder, t => typeB.StartsWith(t, StringComparison.OrdinalIgnoreCase));

        if (indexA < 0) indexA = 999; // Unknown types sort last
        if (indexB < 0) indexB = 999;

        var result = indexA.CompareTo(indexB);
        if (result != 0) return result;

        // Same type, compare numbers
        return numA.CompareTo(numB);
    }

    private static (string type, int number) ParsePrerelease(string prerelease)
    {
        var match = Regex.Match(prerelease, @"^([a-zA-Z]+)\.?(\d*)$");
        if (match.Success)
        {
            var type = match.Groups[1].Value.ToLowerInvariant();
            var num = match.Groups[2].Success && int.TryParse(match.Groups[2].Value, out var n) ? n : 0;
            return (type, num);
        }
        return (prerelease.ToLowerInvariant(), 0);
    }

    public static bool operator >(SemanticVersion a, SemanticVersion b) => a.CompareTo(b) > 0;
    public static bool operator <(SemanticVersion a, SemanticVersion b) => a.CompareTo(b) < 0;
    public static bool operator >=(SemanticVersion a, SemanticVersion b) => a.CompareTo(b) >= 0;
    public static bool operator <=(SemanticVersion a, SemanticVersion b) => a.CompareTo(b) <= 0;

    public override string ToString()
    {
        var version = $"{Major}.{Minor}.{Patch}";
        if (Prerelease != null)
            version += $"-{Prerelease}";
        return version;
    }
}
