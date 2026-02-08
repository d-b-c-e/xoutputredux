using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using XOutputRedux.Core.Mapping;

namespace XOutputRedux.App;

public partial class XboxControllerTestView : UserControl
{
    private static readonly SolidColorBrush ReleasedBrush = new(Color.FromRgb(0xCC, 0xCC, 0xCC));
    private static readonly SolidColorBrush PressedBrush = new(Color.FromRgb(0x4C, 0xAF, 0x50));
    private static readonly SolidColorBrush GrayBrush = new(Color.FromRgb(0x88, 0x88, 0x88));
    private static readonly SolidColorBrush GreenBrush = new(Color.FromRgb(0x4C, 0xAF, 0x50));

    // Stick dot base positions (Canvas.Left/Top from XAML)
    private const double LeftStickDotBaseX = 82;
    private const double LeftStickDotBaseY = 157;
    private const double RightStickDotBaseX = 252;
    private const double RightStickDotBaseY = 247;
    private const double StickRange = 20;
    private const double TriggerMaxHeight = 50;

    public XboxControllerTestView()
    {
        InitializeComponent();
    }

    public void UpdateState(XboxControllerState state)
    {
        // Update button visuals
        ButtonA.Fill = state.A ? PressedBrush : ReleasedBrush;
        ButtonB.Fill = state.B ? PressedBrush : ReleasedBrush;
        ButtonX.Fill = state.X ? PressedBrush : ReleasedBrush;
        ButtonY.Fill = state.Y ? PressedBrush : ReleasedBrush;
        ButtonLB.Fill = state.LeftBumper ? PressedBrush : ReleasedBrush;
        ButtonRB.Fill = state.RightBumper ? PressedBrush : ReleasedBrush;
        ButtonBack.Fill = state.Back ? PressedBrush : ReleasedBrush;
        ButtonStart.Fill = state.Start ? PressedBrush : ReleasedBrush;
        ButtonGuide.Fill = state.Guide ? PressedBrush : ReleasedBrush;
        ButtonLS.Fill = state.LeftStick ? PressedBrush : ReleasedBrush;
        ButtonRS.Fill = state.RightStick ? PressedBrush : ReleasedBrush;
        DPadUp.Fill = state.DPadUp ? PressedBrush : ReleasedBrush;
        DPadDown.Fill = state.DPadDown ? PressedBrush : ReleasedBrush;
        DPadLeft.Fill = state.DPadLeft ? PressedBrush : ReleasedBrush;
        DPadRight.Fill = state.DPadRight ? PressedBrush : ReleasedBrush;

        // Update trigger fills (height based on value)
        LeftTriggerFill.Height = state.LeftTrigger * TriggerMaxHeight;
        RightTriggerFill.Height = state.RightTrigger * TriggerMaxHeight;

        // Update stick dot positions
        Canvas.SetLeft(LeftStickDot,
            LeftStickDotBaseX + (state.LeftStickX - 0.5) * 2 * StickRange);
        Canvas.SetTop(LeftStickDot,
            LeftStickDotBaseY + (state.LeftStickY - 0.5) * 2 * StickRange);
        Canvas.SetLeft(RightStickDot,
            RightStickDotBaseX + (state.RightStickX - 0.5) * 2 * StickRange);
        Canvas.SetTop(RightStickDot,
            RightStickDotBaseY + (state.RightStickY - 0.5) * 2 * StickRange);

        // Update data panel - buttons
        TextA.Text = $"A: {(state.A ? "Pressed" : "-")}";
        TextB.Text = $"B: {(state.B ? "Pressed" : "-")}";
        TextX.Text = $"X: {(state.X ? "Pressed" : "-")}";
        TextY.Text = $"Y: {(state.Y ? "Pressed" : "-")}";
        TextLB.Text = $"LB: {(state.LeftBumper ? "Pressed" : "-")}";
        TextRB.Text = $"RB: {(state.RightBumper ? "Pressed" : "-")}";
        TextBack.Text = $"Back: {(state.Back ? "Pressed" : "-")}";
        TextStart.Text = $"Start: {(state.Start ? "Pressed" : "-")}";
        TextGuide.Text = $"Guide: {(state.Guide ? "Pressed" : "-")}";
        TextLS.Text = $"LS: {(state.LeftStick ? "Pressed" : "-")}";
        TextRS.Text = $"RS: {(state.RightStick ? "Pressed" : "-")}";
        TextDPadUp.Text = $"Up: {(state.DPadUp ? "Pressed" : "-")}";
        TextDPadDown.Text = $"Down: {(state.DPadDown ? "Pressed" : "-")}";
        TextDPadLeft.Text = $"Left: {(state.DPadLeft ? "Pressed" : "-")}";
        TextDPadRight.Text = $"Right: {(state.DPadRight ? "Pressed" : "-")}";

        // Update data panel - triggers
        TextLT.Text = $"{state.LeftTrigger:F2}";
        TextRT.Text = $"{state.RightTrigger:F2}";
        BarLT.Value = state.LeftTrigger * 100;
        BarRT.Value = state.RightTrigger * 100;

        // Update data panel - axes
        TextLSX.Text = $"{state.LeftStickX:F2}";
        TextLSY.Text = $"{state.LeftStickY:F2}";
        TextRSX.Text = $"{state.RightStickX:F2}";
        TextRSY.Text = $"{state.RightStickY:F2}";
        BarLSX.Value = state.LeftStickX * 100;
        BarLSY.Value = state.LeftStickY * 100;
        BarRSX.Value = state.RightStickX * 100;
        BarRSY.Value = state.RightStickY * 100;
    }

    public void Reset()
    {
        UpdateState(new XboxControllerState());
    }

    public void ShowOverlay(string? message = null)
    {
        OverlayText.Text = message ?? " — Start a profile to see controller output";
        OverlayText.Visibility = Visibility.Visible;
    }

    public void HideOverlay()
    {
        OverlayText.Visibility = Visibility.Collapsed;
    }

    public void SetProfileStatus(string text, bool isRunning)
    {
        ProfileStatusText.Text = text;
        ProfileStatusText.Foreground = isRunning ? GreenBrush : GrayBrush;
    }
}
