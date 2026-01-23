using System.Windows;
using System.Windows.Input;

namespace XOutputRedux.App;

/// <summary>
/// Dialog for capturing a new hotkey combination.
/// </summary>
public partial class HotkeyInputDialog : Window
{
    /// <summary>
    /// Gets the selected modifier keys.
    /// </summary>
    public uint Modifiers { get; private set; }

    /// <summary>
    /// Gets the selected virtual key code.
    /// </summary>
    public uint Key { get; private set; }

    public HotkeyInputDialog(uint currentModifiers, uint currentKey)
    {
        InitializeComponent();
        DarkModeHelper.EnableDarkTitleBar(this);

        Modifiers = currentModifiers;
        Key = currentKey;
        UpdateDisplay();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        // Get current modifier state
        uint mods = 0;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) mods |= GlobalHotkeyService.MOD_CONTROL;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) mods |= GlobalHotkeyService.MOD_ALT;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) mods |= GlobalHotkeyService.MOD_SHIFT;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) mods |= GlobalHotkeyService.MOD_WIN;

        // Get the actual key (handle system keys like Alt+X)
        var key = e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key;

        // Ignore if only modifier keys are pressed
        if (key == System.Windows.Input.Key.LeftCtrl || key == System.Windows.Input.Key.RightCtrl ||
            key == System.Windows.Input.Key.LeftAlt || key == System.Windows.Input.Key.RightAlt ||
            key == System.Windows.Input.Key.LeftShift || key == System.Windows.Input.Key.RightShift ||
            key == System.Windows.Input.Key.LWin || key == System.Windows.Input.Key.RWin)
        {
            return;
        }

        // Require at least one modifier
        if (mods == 0)
        {
            return;
        }

        Modifiers = mods;
        Key = (uint)KeyInterop.VirtualKeyFromKey(key);
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        HotkeyDisplayText.Text = GlobalHotkeyService.FormatHotkey(Modifiers, Key);
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
