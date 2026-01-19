using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace XOutputRedux.Emulation;

/// <summary>
/// Represents an emulated Xbox 360 controller using ViGEm.
/// Adapted from XOutput.Emulation.ViGEm.ViGEmXboxDevice
/// </summary>
public class XboxController : IDisposable
{
    private readonly IXbox360Controller _controller;
    private bool _disposed;
    private bool _connected;

    /// <summary>
    /// Whether the controller is currently connected/emulated.
    /// </summary>
    public bool IsConnected => _connected;

    /// <summary>
    /// Event raised when force feedback is received from the game.
    /// </summary>
    public event EventHandler<ForceFeedbackEventArgs>? ForceFeedbackReceived;

    internal XboxController(IXbox360Controller controller)
    {
        _controller = controller;
        _controller.AutoSubmitReport = false;
    }

    /// <summary>
    /// Connects the emulated controller.
    /// </summary>
    public void Connect()
    {
        if (_connected) return;

        _controller.Connect();
        _controller.FeedbackReceived += OnFeedbackReceived;
        _connected = true;

        // Initialize with centered sticks
        SendInput(new XboxInput());
    }

    /// <summary>
    /// Disconnects the emulated controller.
    /// </summary>
    public void Disconnect()
    {
        if (!_connected) return;

        _connected = false;
        _controller.FeedbackReceived -= OnFeedbackReceived;
        _controller.Disconnect();
    }

    /// <summary>
    /// Sends input state to the emulated controller.
    /// </summary>
    public void SendInput(XboxInput input)
    {
        if (!_connected) return;

        // Buttons
        _controller.SetButtonState(Xbox360Button.A, input.A);
        _controller.SetButtonState(Xbox360Button.B, input.B);
        _controller.SetButtonState(Xbox360Button.X, input.X);
        _controller.SetButtonState(Xbox360Button.Y, input.Y);
        _controller.SetButtonState(Xbox360Button.LeftShoulder, input.LeftBumper);
        _controller.SetButtonState(Xbox360Button.RightShoulder, input.RightBumper);
        _controller.SetButtonState(Xbox360Button.Back, input.Back);
        _controller.SetButtonState(Xbox360Button.Start, input.Start);
        _controller.SetButtonState(Xbox360Button.Guide, input.Guide);
        _controller.SetButtonState(Xbox360Button.LeftThumb, input.LeftStick);
        _controller.SetButtonState(Xbox360Button.RightThumb, input.RightStick);

        // D-Pad
        _controller.SetButtonState(Xbox360Button.Up, input.DPadUp);
        _controller.SetButtonState(Xbox360Button.Down, input.DPadDown);
        _controller.SetButtonState(Xbox360Button.Left, input.DPadLeft);
        _controller.SetButtonState(Xbox360Button.Right, input.DPadRight);

        // Axes (convert from 0.0-1.0 range to short range)
        _controller.SetAxisValue(Xbox360Axis.LeftThumbX, ConvertAxis(input.LeftStickX));
        _controller.SetAxisValue(Xbox360Axis.LeftThumbY, ConvertAxis(input.LeftStickY));
        _controller.SetAxisValue(Xbox360Axis.RightThumbX, ConvertAxis(input.RightStickX));
        _controller.SetAxisValue(Xbox360Axis.RightThumbY, ConvertAxis(input.RightStickY));

        // Triggers (convert from 0.0-1.0 range to byte range)
        _controller.SetSliderValue(Xbox360Slider.LeftTrigger, ConvertTrigger(input.LeftTrigger));
        _controller.SetSliderValue(Xbox360Slider.RightTrigger, ConvertTrigger(input.RightTrigger));

        _controller.SubmitReport();
    }

    private void OnFeedbackReceived(object? sender, Xbox360FeedbackReceivedEventArgs e)
    {
        ForceFeedbackReceived?.Invoke(this, new ForceFeedbackEventArgs
        {
            LargeMotor = (double)e.LargeMotor / byte.MaxValue,
            SmallMotor = (double)e.SmallMotor / byte.MaxValue
        });
    }

    /// <summary>
    /// Converts axis value from 0.0-1.0 range (center at 0.5) to short range.
    /// </summary>
    private static short ConvertAxis(double value)
    {
        // Convert from 0.0-1.0 (center 0.5) to -32768 to 32767
        return (short)((value - 0.5) * 2 * short.MaxValue);
    }

    /// <summary>
    /// Converts trigger value from 0.0-1.0 range to byte range.
    /// </summary>
    private static byte ConvertTrigger(double value)
    {
        return (byte)(value * byte.MaxValue);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Disconnect();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Xbox controller input state.
/// </summary>
public class XboxInput
{
    // Buttons
    public bool A { get; set; }
    public bool B { get; set; }
    public bool X { get; set; }
    public bool Y { get; set; }
    public bool LeftBumper { get; set; }
    public bool RightBumper { get; set; }
    public bool Back { get; set; }
    public bool Start { get; set; }
    public bool Guide { get; set; }
    public bool LeftStick { get; set; }
    public bool RightStick { get; set; }

    // D-Pad
    public bool DPadUp { get; set; }
    public bool DPadDown { get; set; }
    public bool DPadLeft { get; set; }
    public bool DPadRight { get; set; }

    // Axes (0.0 - 1.0, center at 0.5)
    public double LeftStickX { get; set; } = 0.5;
    public double LeftStickY { get; set; } = 0.5;
    public double RightStickX { get; set; } = 0.5;
    public double RightStickY { get; set; } = 0.5;

    // Triggers (0.0 - 1.0)
    public double LeftTrigger { get; set; }
    public double RightTrigger { get; set; }
}

/// <summary>
/// Force feedback event args.
/// </summary>
public class ForceFeedbackEventArgs : EventArgs
{
    /// <summary>
    /// Large motor intensity (0.0 - 1.0).
    /// </summary>
    public double LargeMotor { get; init; }

    /// <summary>
    /// Small motor intensity (0.0 - 1.0).
    /// </summary>
    public double SmallMotor { get; init; }
}
