using System.Diagnostics;
using System.Runtime.InteropServices;
using XOutputRedux.Core.Mapping;

namespace XOutputRedux.Emulation;

/// <summary>
/// Injects synthetic keystrokes via SendInput.
/// </summary>
/// <remarks>
/// Keys are sent as <b>scan codes</b> (KEYEVENTF_SCANCODE), not virtual key codes. This is
/// the whole point: games built on DirectInput or RawInput read the keyboard at the scan
/// code level and ignore virtual-key injection entirely. A VK-based implementation types
/// happily into Notepad and does nothing at all in the games this feature exists to serve.
/// </remarks>
public sealed class KeyboardSink : IDisposable
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventExtendedKey = 0x0001;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint KeyEventScanCode = 0x0008;

    private readonly int _inputSize = Marshal.SizeOf<INPUT>();
    private bool _disposed;

    // The guard now runs on every send, so cache the per-window answer. A window
    // handle cannot change owning process, so this is safe to memoise.
    private IntPtr _lastForegroundWindow = IntPtr.Zero;
    private bool _lastForegroundAllowed;

    /// <summary>
    /// When set, keystrokes are only emitted while a process of this name owns the
    /// foreground window. Without it, alt-tabbing out of the game sprays movement keys
    /// into whatever has focus. Case-insensitive, no ".exe" required.
    /// </summary>
    public string? TargetProcessName { get; set; }

    /// <summary>
    /// Whether output is currently permitted (i.e. the foreground window check passes).
    /// </summary>
    public bool CanSend()
    {
        if (string.IsNullOrWhiteSpace(TargetProcessName)) return true;

        try
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return false;
            if (hwnd == _lastForegroundWindow) return _lastForegroundAllowed;

            _lastForegroundWindow = hwnd;
            _lastForegroundAllowed = false;

            _ = GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0) return false;

            using var process = Process.GetProcessById((int)pid);
            string wanted = TargetProcessName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? TargetProcessName[..^4]
                : TargetProcessName;

            _lastForegroundAllowed = string.Equals(process.ProcessName, wanted, StringComparison.OrdinalIgnoreCase);
            return _lastForegroundAllowed;
        }
        catch
        {
            // Process may have exited between the handle lookup and the query.
            return false;
        }
    }

    /// <summary>
    /// Sends a batch of key presses and releases in a single SendInput call so the game
    /// observes them atomically within one frame.
    /// </summary>
    public void Send(IReadOnlyList<KeyCode> pressed, IReadOnlyList<KeyCode> released)
    {
        if (_disposed) return;

        // The guard is enforced HERE, not left to the caller. Key presses must never
        // escape into an unrelated window; releases are always allowed through, so a
        // held key can be lifted no matter where focus has gone.
        if (pressed.Count > 0 && !CanSend())
        {
            SendRaw(Array.Empty<KeyCode>(), released);
            return;
        }

        SendRaw(pressed, released);
    }

    /// <summary>Emits key events without consulting the foreground guard.</summary>
    private void SendRaw(IReadOnlyList<KeyCode> pressed, IReadOnlyList<KeyCode> released)
    {
        if (_disposed) return;

        int count = pressed.Count + released.Count;
        if (count == 0) return;

        var inputs = new INPUT[count];
        int index = 0;

        // Releases first: if a binding change swaps one key for another in the same tick,
        // the outgoing key should lift before the incoming one lands.
        foreach (var key in released) inputs[index++] = BuildInput(key, isKeyUp: true);
        foreach (var key in pressed) inputs[index++] = BuildInput(key, isKeyUp: false);

        uint sent = SendInput((uint)inputs.Length, inputs, _inputSize);
        if (sent != inputs.Length)
        {
            throw new InvalidOperationException(
                $"SendInput accepted {sent} of {inputs.Length} events " +
                $"(win32 error {Marshal.GetLastWin32Error()}).");
        }
    }

    /// <summary>Releases a set of keys, ignoring the foreground guard.</summary>
    public void ReleaseKeys(IReadOnlyList<KeyCode> keys)
    {
        if (keys.Count > 0) SendRaw(Array.Empty<KeyCode>(), keys);
    }

    private static INPUT BuildInput(KeyCode key, bool isKeyUp)
    {
        uint flags = KeyEventScanCode;
        if (isKeyUp) flags |= KeyEventKeyUp;
        if (key.IsExtended()) flags |= KeyEventExtendedKey;

        return new INPUT
        {
            type = InputKeyboard,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = key.ScanCode(),
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
    }

    public void Dispose()
    {
        _disposed = true;
    }

    #region Win32

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    // MOUSEINPUT is the largest member and must be present for INPUT to be sized
    // correctly (40 bytes on x64); SendInput rejects a mismatched cbSize.
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    #endregion
}
