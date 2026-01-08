namespace XOutputRenew.Emulation;

/// <summary>
/// Represents an emulated Xbox 360 controller.
/// </summary>
public class XboxController : IDisposable
{
    private bool _disposed;
    private bool _connected;

    /// <summary>
    /// Whether the controller is currently connected/emulated.
    /// </summary>
    public bool IsConnected => _connected;

    /// <summary>
    /// Connects the emulated controller.
    /// </summary>
    public void Connect()
    {
        if (_connected) return;

        // TODO: Implement ViGEm connection
        _connected = true;
    }

    /// <summary>
    /// Disconnects the emulated controller.
    /// </summary>
    public void Disconnect()
    {
        if (!_connected) return;

        // TODO: Implement ViGEm disconnection
        _connected = false;
    }

    /// <summary>
    /// Sends input state to the emulated controller.
    /// </summary>
    public void SendInput(XboxInput input)
    {
        if (!_connected) return;

        // TODO: Implement ViGEm input sending
    }

    /// <summary>
    /// Event raised when force feedback is received from the game.
    /// </summary>
    public event EventHandler<ForceFeedbackEventArgs>? ForceFeedbackReceived;

    protected virtual void OnForceFeedbackReceived(ForceFeedbackEventArgs e)
    {
        ForceFeedbackReceived?.Invoke(this, e);
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
