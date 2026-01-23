using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace XOutputRedux.App;

/// <summary>
/// Service for registering and handling global hotkeys.
/// </summary>
public class GlobalHotkeyService : IDisposable
{
    // Win32 constants
    private const int WM_HOTKEY = 0x0312;
    private const int HOTKEY_ID_ADD_GAME = 1;

    // Modifier key constants (must match Win32 values)
    public const uint MOD_NONE = 0x0000;
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;

    // Process access rights
    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    #region P/Invoke Declarations

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(IntPtr hProcess, uint dwFlags, StringBuilder lpExeName, ref uint lpdwSize);

    #endregion

    private IntPtr _windowHandle;
    private HwndSource? _hwndSource;
    private bool _isRegistered;
    private bool _disposed;

    /// <summary>
    /// Event fired when the add game hotkey is pressed.
    /// </summary>
    public event Action? AddGameHotkeyPressed;

    /// <summary>
    /// Gets the currently registered modifier keys.
    /// </summary>
    public uint CurrentModifiers { get; private set; }

    /// <summary>
    /// Gets the currently registered virtual key code.
    /// </summary>
    public uint CurrentKey { get; private set; }

    /// <summary>
    /// Gets whether a hotkey is currently registered and active.
    /// </summary>
    public bool IsEnabled => _isRegistered;

    /// <summary>
    /// Initializes the hotkey service and registers the hotkey.
    /// </summary>
    /// <param name="window">The WPF window to hook for messages.</param>
    /// <param name="modifiers">Modifier keys (MOD_CONTROL, MOD_SHIFT, etc.).</param>
    /// <param name="virtualKey">Virtual key code.</param>
    public void Initialize(Window window, uint modifiers, uint virtualKey)
    {
        _windowHandle = new WindowInteropHelper(window).Handle;
        if (_windowHandle == IntPtr.Zero)
        {
            AppLogger.Warning("GlobalHotkeyService: Window handle is zero, cannot register hotkey");
            return;
        }

        _hwndSource = HwndSource.FromHwnd(_windowHandle);
        _hwndSource?.AddHook(WndProc);

        Register(modifiers, virtualKey);
    }

    /// <summary>
    /// Registers or re-registers the hotkey with new key combination.
    /// </summary>
    /// <param name="modifiers">Modifier keys.</param>
    /// <param name="virtualKey">Virtual key code.</param>
    /// <returns>True if registration succeeded.</returns>
    public bool Register(uint modifiers, uint virtualKey)
    {
        if (_windowHandle == IntPtr.Zero)
        {
            AppLogger.Warning("GlobalHotkeyService: Cannot register hotkey - no window handle");
            return false;
        }

        // Unregister existing hotkey first
        if (_isRegistered)
        {
            UnregisterHotKey(_windowHandle, HOTKEY_ID_ADD_GAME);
            _isRegistered = false;
        }

        _isRegistered = RegisterHotKey(_windowHandle, HOTKEY_ID_ADD_GAME, modifiers, virtualKey);

        if (_isRegistered)
        {
            CurrentModifiers = modifiers;
            CurrentKey = virtualKey;
            AppLogger.Info($"GlobalHotkeyService: Registered hotkey {FormatHotkey(modifiers, virtualKey)}");
        }
        else
        {
            var error = Marshal.GetLastWin32Error();
            AppLogger.Warning($"GlobalHotkeyService: Failed to register hotkey (error {error}) - may be in use by another application");
        }

        return _isRegistered;
    }

    /// <summary>
    /// Unregisters the current hotkey.
    /// </summary>
    public void Unregister()
    {
        if (_isRegistered && _windowHandle != IntPtr.Zero)
        {
            UnregisterHotKey(_windowHandle, HOTKEY_ID_ADD_GAME);
            _isRegistered = false;
            AppLogger.Info("GlobalHotkeyService: Unregistered hotkey");
        }
    }

    /// <summary>
    /// Window procedure hook to catch WM_HOTKEY messages.
    /// </summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID_ADD_GAME)
        {
            AddGameHotkeyPressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    /// <summary>
    /// Gets the executable path of the foreground window's process.
    /// </summary>
    /// <returns>The full path to the executable, or null if it cannot be determined.</returns>
    public static string? GetForegroundWindowExecutablePath()
    {
        try
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
            {
                return null;
            }

            GetWindowThreadProcessId(hwnd, out uint processId);
            if (processId == 0)
            {
                return null;
            }

            IntPtr hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
            if (hProcess == IntPtr.Zero)
            {
                // Access denied - likely an admin process or UWP app
                return null;
            }

            try
            {
                var sb = new StringBuilder(1024);
                uint size = (uint)sb.Capacity;
                if (QueryFullProcessImageName(hProcess, 0, sb, ref size))
                {
                    return sb.ToString();
                }
            }
            finally
            {
                CloseHandle(hProcess);
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warning($"GlobalHotkeyService: Failed to get foreground window executable: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Gets the process ID of the current application.
    /// </summary>
    public static uint GetCurrentProcessId() => (uint)Environment.ProcessId;

    /// <summary>
    /// Gets the process ID of the foreground window.
    /// </summary>
    public static uint GetForegroundWindowProcessId()
    {
        IntPtr hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return 0;
        }
        GetWindowThreadProcessId(hwnd, out uint processId);
        return processId;
    }

    /// <summary>
    /// Formats a hotkey combination as a display string.
    /// </summary>
    public static string FormatHotkey(uint modifiers, uint key)
    {
        var parts = new List<string>();

        if ((modifiers & MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((modifiers & MOD_ALT) != 0) parts.Add("Alt");
        if ((modifiers & MOD_SHIFT) != 0) parts.Add("Shift");
        if ((modifiers & MOD_WIN) != 0) parts.Add("Win");

        // Convert virtual key to display name
        var wpfKey = KeyInterop.KeyFromVirtualKey((int)key);
        parts.Add(wpfKey.ToString());

        return string.Join(" + ", parts);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Unregister();
        _hwndSource?.RemoveHook(WndProc);
    }
}
