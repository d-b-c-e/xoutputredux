namespace XOutputRedux.Core.Mapping;

/// <summary>
/// The kind of behavior a profile provides when started.
/// </summary>
public enum ProfileType
{
    /// <summary>
    /// Classic DInput-to-XInput translation: a virtual Xbox 360 controller is
    /// created and physical inputs are mapped onto it.
    /// </summary>
    Mapping = 0,

    /// <summary>
    /// Device isolation: no virtual controller is created. While the profile is
    /// active, every HID gaming device EXCEPT the configured keep-visible
    /// device(s) is hidden via HidHide, so games that auto-detect the first
    /// enumerated device find the right one. The kept device stays fully native,
    /// preserving DirectInput force feedback. All hiding is reverted on stop.
    /// </summary>
    DeviceIsolation = 1
}
