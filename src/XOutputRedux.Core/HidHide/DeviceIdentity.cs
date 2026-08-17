namespace XOutputRedux.Core.HidHide;

/// <summary>
/// Reduces a Windows device path to a stable HARDWARE identity — the part that
/// survives re-enumeration.
///
/// Device instance paths are not stable, which makes them a poor thing to store
/// in a saved profile:
///
/// <list type="bullet">
/// <item>The trailing instance segment (<c>d&amp;20e2af56&amp;0&amp;0000</c>) changes
///       with the USB port and enumeration order.</item>
/// <item>An XInput pad's <c>IG_xx</c> infix tracks its XInput slot, which moves
///       whenever the device is replugged or its controller mode is cycled — an
///       X-Arcade panel was observed shifting from <c>IG_00/01/02</c> to
///       <c>IG_04/05</c> within a single session.</item>
/// </list>
///
/// What is kept: <c>VID_</c>, <c>PID_</c>, and the <c>MI_</c>/<c>COL</c> interface
/// qualifiers, which identify a *function* of a device and do not move.
/// What is dropped: the instance segment, <c>IG_</c>, <c>REV_</c>, and the
/// interface class GUID.
///
/// This lives in Core rather than beside the HidHide wrapper so that saved
/// profiles can persist an identity without depending on HidHide being present.
/// </summary>
public static class DeviceIdentity
{
    /// <summary>
    /// Derives the hardware identity from any of the three path forms a profile
    /// may hold — <c>HID\...</c>, <c>USB\...</c>, or the
    /// <c>\\?\hid#...#{guid}</c> symbolic link.
    ///
    /// Returns null when the path carries no VID/PID at all (root-enumerated
    /// virtual devices such as vJoy), so callers fall back to exact matching for
    /// those rather than matching far too broadly.
    /// </summary>
    public static string? FromPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        // The symbolic-link form uses '#' where the instance path uses '\'.
        var segments = path.Replace('#', '\\')
                           .Split('\\', StringSplitOptions.RemoveEmptyEntries);

        var idSegment = segments.FirstOrDefault(
            s => s.Contains("VID_", StringComparison.OrdinalIgnoreCase));
        if (idSegment == null) return null;

        var tokens = idSegment
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.ToUpperInvariant())
            .Where(t => t.StartsWith("VID_", StringComparison.Ordinal)
                     || t.StartsWith("PID_", StringComparison.Ordinal)
                     || t.StartsWith("MI_", StringComparison.Ordinal)
                     || t.StartsWith("COL", StringComparison.Ordinal))
            .ToList();

        // Both VID and PID are required — a lone VID would match every device
        // from that vendor, which is far too broad to keep anything safe.
        if (!tokens.Any(t => t.StartsWith("VID_", StringComparison.Ordinal)) ||
            !tokens.Any(t => t.StartsWith("PID_", StringComparison.Ordinal)))
            return null;

        return string.Join("&", tokens);
    }

    /// <summary>
    /// Whether two identities refer to the same hardware function.
    /// </summary>
    public static bool Matches(string? a, string? b) =>
        !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b) &&
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
