using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace XOutputRedux.App;

/// <summary>
/// Helper class to enable dark mode title bar on Windows 10/11.
/// </summary>
public static class DarkModeHelper
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    /// <summary>
    /// Enables dark mode title bar for the specified window.
    /// Call this after the window is loaded (e.g., in Loaded event).
    /// </summary>
    public static void EnableDarkTitleBar(Window window)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            int useImmersiveDarkMode = 1;

            // Try the newer attribute first (Windows 10 20H1+), fall back to older one
            if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useImmersiveDarkMode, sizeof(int)) != 0)
            {
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref useImmersiveDarkMode, sizeof(int));
            }
        }
        catch
        {
            // Ignore errors - dark title bar is a nice-to-have, not critical
        }
    }
}
