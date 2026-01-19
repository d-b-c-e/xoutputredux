using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace XOutputRedux.Input;

/// <summary>
/// Helper for generating device IDs.
/// Adapted from XOutput.App.Devices.Input.IdHelper
/// </summary>
public static partial class IdHelper
{
    [GeneratedRegex("(hid)#([^#]+)#[^#]+", RegexOptions.IgnoreCase)]
    private static partial Regex HidRegex();

    [GeneratedRegex("hid#(vid_[0-9a-f]{4}&pid_[0-9a-f]{4})", RegexOptions.IgnoreCase)]
    private static partial Regex VidPidRegex();

    /// <summary>
    /// Generates a unique ID from a device path using SHA256.
    /// </summary>
    public static string GetUniqueId(string path)
    {
        if (string.IsNullOrEmpty(path))
            return string.Empty;

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(path));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Extracts hardware ID (HID\VID_XXXX&PID_XXXX) from device path.
    /// </summary>
    public static string? GetHardwareId(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        // Try to extract VID/PID pattern
        var match = VidPidRegex().Match(path);
        if (match.Success)
        {
            return $"HID\\{match.Groups[1].Value}".ToUpperInvariant();
        }

        // Try HID pattern
        match = HidRegex().Match(path);
        if (match.Success)
        {
            return $"{match.Groups[1].Value}\\{match.Groups[2].Value}".ToUpperInvariant();
        }

        // Fallback: try to parse from instance path
        if (path.Contains("hid#", StringComparison.OrdinalIgnoreCase))
        {
            return GetHardwareIdFromInstancePath(path);
        }

        return null;
    }

    /// <summary>
    /// Gets the device instance path suitable for HidHide.
    /// </summary>
    public static string? GetDeviceInstancePath(string? interfacePath)
    {
        if (string.IsNullOrEmpty(interfacePath))
            return null;

        // Convert interface path to instance path format
        // Example: \\?\hid#vid_046d&pid_c294#7&... → HID\VID_046D&PID_C294\7&...
        var path = interfacePath;

        // Remove \\?\ prefix if present
        if (path.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase))
            path = path[4..];

        // Replace # with \ and uppercase
        path = path.Replace('#', '\\');

        // Find the GUID suffix and remove it
        int guidIndex = path.LastIndexOf('{');
        if (guidIndex > 0)
            path = path[..guidIndex].TrimEnd('\\');

        return path.ToUpperInvariant();
    }

    private static string? GetHardwareIdFromInstancePath(string path)
    {
        int hidIndex = path.IndexOf("hid#", StringComparison.OrdinalIgnoreCase);
        if (hidIndex < 0)
            return null;

        path = path[hidIndex..].Replace('#', '\\');

        int first = path.IndexOf('\\');
        if (first < 0)
            return path.ToUpperInvariant();

        int second = path.IndexOf('\\', first + 1);
        if (second > 0)
            return path[..second].ToUpperInvariant();

        return path.ToUpperInvariant();
    }
}
