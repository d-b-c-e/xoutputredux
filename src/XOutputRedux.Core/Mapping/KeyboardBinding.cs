using System.Text.Json.Serialization;

namespace XOutputRedux.Core.Mapping;

/// <summary>
/// A keyboard key identified by its Set 1 scan code.
/// </summary>
/// <remarks>
/// The enum value IS the scan code, with bit 0x100 marking an extended (0xE0-prefixed)
/// key. This matters: games that read the keyboard through DirectInput or RawInput see
/// scan codes, not virtual key codes, and several keys collide unless the extended flag
/// is carried through — Up arrow (0xE048) and NumPad8 (0x48) are the same scan code
/// otherwise, as are Delete/NumPadDecimal and Insert/NumPad0.
/// </remarks>
public enum KeyCode
{
    None = 0,

    // Number row
    Escape = 0x01,
    D1 = 0x02, D2 = 0x03, D3 = 0x04, D4 = 0x05, D5 = 0x06,
    D6 = 0x07, D7 = 0x08, D8 = 0x09, D9 = 0x0A, D0 = 0x0B,
    Minus = 0x0C, Equals = 0x0D, Backspace = 0x0E,

    // Top letter row
    Tab = 0x0F,
    Q = 0x10, W = 0x11, E = 0x12, R = 0x13, T = 0x14,
    Y = 0x15, U = 0x16, I = 0x17, O = 0x18, P = 0x19,
    LeftBracket = 0x1A, RightBracket = 0x1B, Enter = 0x1C,

    // Home row
    LeftControl = 0x1D,
    A = 0x1E, S = 0x1F, D = 0x20, F = 0x21, G = 0x22,
    H = 0x23, J = 0x24, K = 0x25, L = 0x26,
    Semicolon = 0x27, Apostrophe = 0x28, Grave = 0x29,

    // Bottom letter row
    LeftShift = 0x2A, Backslash = 0x2B,
    Z = 0x2C, X = 0x2D, C = 0x2E, V = 0x2F, B = 0x30,
    N = 0x31, M = 0x32,
    Comma = 0x33, Period = 0x34, Slash = 0x35, RightShift = 0x36,

    // Modifiers and space
    NumPadMultiply = 0x37, LeftAlt = 0x38, Space = 0x39, CapsLock = 0x3A,

    // Function keys
    F1 = 0x3B, F2 = 0x3C, F3 = 0x3D, F4 = 0x3E, F5 = 0x3F,
    F6 = 0x40, F7 = 0x41, F8 = 0x42, F9 = 0x43, F10 = 0x44,
    F11 = 0x57, F12 = 0x58,

    NumLock = 0x45, ScrollLock = 0x46,

    // Numeric keypad (non-extended)
    NumPad7 = 0x47, NumPad8 = 0x48, NumPad9 = 0x49, NumPadMinus = 0x4A,
    NumPad4 = 0x4B, NumPad5 = 0x4C, NumPad6 = 0x4D, NumPadPlus = 0x4E,
    NumPad1 = 0x4F, NumPad2 = 0x50, NumPad3 = 0x51,
    NumPad0 = 0x52, NumPadDecimal = 0x53,

    // Extended keys (0xE0-prefixed) — flagged with 0x100
    NumPadEnter = 0x11C,
    RightControl = 0x11D,
    NumPadDivide = 0x135,
    RightAlt = 0x138,
    Home = 0x147, UpArrow = 0x148, PageUp = 0x149,
    LeftArrow = 0x14B, RightArrow = 0x14D,
    End = 0x14F, DownArrow = 0x150, PageDown = 0x151,
    Insert = 0x152, Delete = 0x153,
    LeftWindows = 0x15B, RightWindows = 0x15C, Menu = 0x15D
}

/// <summary>
/// Scan-code helpers for <see cref="KeyCode"/>.
/// </summary>
public static class KeyCodeExtensions
{
    private const int ExtendedFlag = 0x100;

    /// <summary>The Set 1 scan code, without the extended marker.</summary>
    public static ushort ScanCode(this KeyCode key) => (ushort)((int)key & 0xFF);

    /// <summary>Whether this key requires the 0xE0 extended prefix.</summary>
    public static bool IsExtended(this KeyCode key) => ((int)key & ExtendedFlag) != 0;
}

/// <summary>
/// Binds a physical input source to a keyboard key.
/// </summary>
/// <remarks>
/// Deliberately separate from <see cref="InputBinding"/> for now. The two collapse into a
/// single binding type once <c>XboxOutput</c> is generalised into an output-target union;
/// keeping them apart keeps this additive and revertible.
/// </remarks>
public class KeyboardBinding
{
    /// <summary>Unique ID of the input device.</summary>
    public required string DeviceId { get; init; }

    /// <summary>Index of the input source on the device.</summary>
    public required int SourceIndex { get; init; }

    /// <summary>The key to send while this input is active.</summary>
    public required KeyCode Key { get; init; }

    /// <summary>Display name for this binding (e.g. "Stick Up").</summary>
    public string? DisplayName { get; set; }

    /// <summary>Inverts the input before thresholding.</summary>
    public bool Invert { get; set; }

    /// <summary>
    /// Value at or above which the key goes down (0.0 - 1.0).
    /// </summary>
    public double PressThreshold { get; set; } = 0.5;

    /// <summary>
    /// Value below which the key comes back up (0.0 - 1.0). Kept lower than
    /// <see cref="PressThreshold"/> so an analog source hovering near the trip point
    /// does not machine-gun the key. Set equal to disable hysteresis.
    /// </summary>
    public double ReleaseThreshold { get; set; } = 0.35;
}

/// <summary>
/// Serializable form of <see cref="KeyboardBinding"/>.
/// </summary>
public class KeyboardBindingData
{
    public string DeviceId { get; set; } = "";
    public int SourceIndex { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public KeyCode Key { get; set; } = KeyCode.None;

    public string? DisplayName { get; set; }
    public bool Invert { get; set; }
    public double PressThreshold { get; set; } = 0.5;
    public double ReleaseThreshold { get; set; } = 0.35;

    public static KeyboardBindingData FromBinding(KeyboardBinding binding) => new()
    {
        DeviceId = binding.DeviceId,
        SourceIndex = binding.SourceIndex,
        Key = binding.Key,
        DisplayName = binding.DisplayName,
        Invert = binding.Invert,
        PressThreshold = binding.PressThreshold,
        ReleaseThreshold = binding.ReleaseThreshold
    };

    public KeyboardBinding ToBinding() => new()
    {
        DeviceId = DeviceId,
        SourceIndex = SourceIndex,
        Key = Key,
        DisplayName = DisplayName,
        Invert = Invert,
        PressThreshold = PressThreshold,
        ReleaseThreshold = ReleaseThreshold
    };
}
