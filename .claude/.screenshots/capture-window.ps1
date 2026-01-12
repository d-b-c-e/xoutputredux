<#
.SYNOPSIS
    Captures a screenshot of a window by title and saves it as PNG.

.DESCRIPTION
    This script captures a screenshot of a specific window (default: XOutputRenew)
    and saves it to the .claude/.screenshots folder with a timestamp.

.PARAMETER WindowTitle
    Part of the window title to search for. Default is "XOutputRenew"

.PARAMETER OutputPath
    Path to save the screenshot. Default is the .screenshots folder with timestamp.

.EXAMPLE
    .\capture-window.ps1
    Captures XOutputRenew window

.EXAMPLE
    .\capture-window.ps1 -WindowTitle "Edit Profile"
    Captures a window with "Edit Profile" in the title
#>

param(
    [string]$WindowTitle = "XOutputRenew",
    [string]$OutputPath = ""
)

Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;

public class WindowCapture {
    [DllImport("user32.dll")]
    public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public static IntPtr FindWindowByTitle(string titlePart) {
        IntPtr foundWindow = IntPtr.Zero;

        EnumWindows((hWnd, lParam) => {
            if (IsWindowVisible(hWnd)) {
                var sb = new System.Text.StringBuilder(256);
                GetWindowText(hWnd, sb, 256);
                string title = sb.ToString();
                if (!string.IsNullOrEmpty(title) && title.IndexOf(titlePart, StringComparison.OrdinalIgnoreCase) >= 0) {
                    foundWindow = hWnd;
                    return false; // Stop enumeration
                }
            }
            return true; // Continue enumeration
        }, IntPtr.Zero);

        return foundWindow;
    }

    public static Bitmap CaptureWindow(IntPtr hWnd) {
        RECT rect;
        GetWindowRect(hWnd, out rect);

        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;

        if (width <= 0 || height <= 0) {
            throw new Exception("Invalid window dimensions");
        }

        Bitmap bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp)) {
            g.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
        }

        return bmp;
    }
}
"@ -ReferencedAssemblies System.Drawing

# Find the window
$hWnd = [WindowCapture]::FindWindowByTitle($WindowTitle)

if ($hWnd -eq [IntPtr]::Zero) {
    Write-Error "Window with title containing '$WindowTitle' not found"
    exit 1
}

# Bring window to foreground
[WindowCapture]::SetForegroundWindow($hWnd) | Out-Null
Start-Sleep -Milliseconds 200

# Determine output path
if ([string]::IsNullOrEmpty($OutputPath)) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $OutputPath = Join-Path $scriptDir "screenshot-$timestamp.png"
}

# Capture the window
try {
    $bitmap = [WindowCapture]::CaptureWindow($hWnd)
    $bitmap.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()

    Write-Host "Screenshot saved to: $OutputPath"
    Write-Output $OutputPath
} catch {
    Write-Error "Failed to capture window: $_"
    exit 1
}
