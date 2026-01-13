namespace XOutputRenew.Core.Mapping;

/// <summary>
/// Applies input mappings to produce Xbox controller output.
/// This is the core engine that converts physical input to emulated output.
/// </summary>
public class MappingEngine
{
    private MappingProfile? _activeProfile;
    private readonly Dictionary<(string DeviceId, int SourceIndex), double> _inputValues = new();
    private readonly object _lock = new();

    /// <summary>
    /// The currently active mapping profile.
    /// </summary>
    public MappingProfile? ActiveProfile
    {
        get => _activeProfile;
        set
        {
            _activeProfile = value;
            ProfileChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Event raised when the active profile changes.
    /// </summary>
    public event EventHandler? ProfileChanged;

    /// <summary>
    /// Updates an input value from a device.
    /// </summary>
    /// <param name="deviceId">The unique ID of the input device.</param>
    /// <param name="sourceIndex">The index of the input source on the device.</param>
    /// <param name="value">The new value (0.0 - 1.0).</param>
    public void UpdateInput(string deviceId, int sourceIndex, double value)
    {
        lock (_lock)
        {
            _inputValues[(deviceId, sourceIndex)] = value;
        }
    }

    /// <summary>
    /// Removes all input values from a specific device.
    /// </summary>
    public void RemoveDevice(string deviceId)
    {
        lock (_lock)
        {
            var keysToRemove = _inputValues.Keys
                .Where(k => k.DeviceId == deviceId)
                .ToList();
            foreach (var key in keysToRemove)
            {
                _inputValues.Remove(key);
            }
        }
    }

    /// <summary>
    /// Gets the current input value for a specific source.
    /// </summary>
    private double? GetInputValue(string deviceId, int sourceIndex)
    {
        lock (_lock)
        {
            return _inputValues.TryGetValue((deviceId, sourceIndex), out var value) ? value : null;
        }
    }

    /// <summary>
    /// Evaluates all mappings and produces the current Xbox controller state.
    /// </summary>
    public XboxControllerState Evaluate()
    {
        var state = new XboxControllerState();

        if (_activeProfile == null)
        {
            return state;
        }

        lock (_lock)
        {
            foreach (var kvp in _activeProfile.Mappings)
            {
                double value = kvp.Value.Evaluate(GetInputValue);
                ApplyToState(state, kvp.Key, value);
            }
        }

        return state;
    }

    private static void ApplyToState(XboxControllerState state, XboxOutput output, double value)
    {
        switch (output)
        {
            // Buttons
            case XboxOutput.A: state.A = value >= 0.5; break;
            case XboxOutput.B: state.B = value >= 0.5; break;
            case XboxOutput.X: state.X = value >= 0.5; break;
            case XboxOutput.Y: state.Y = value >= 0.5; break;
            case XboxOutput.LeftBumper: state.LeftBumper = value >= 0.5; break;
            case XboxOutput.RightBumper: state.RightBumper = value >= 0.5; break;
            case XboxOutput.Back: state.Back = value >= 0.5; break;
            case XboxOutput.Start: state.Start = value >= 0.5; break;
            case XboxOutput.Guide: state.Guide = value >= 0.5; break;
            case XboxOutput.LeftStickPress: state.LeftStick = value >= 0.5; break;
            case XboxOutput.RightStickPress: state.RightStick = value >= 0.5; break;
            case XboxOutput.DPadUp: state.DPadUp = value >= 0.5; break;
            case XboxOutput.DPadDown: state.DPadDown = value >= 0.5; break;
            case XboxOutput.DPadLeft: state.DPadLeft = value >= 0.5; break;
            case XboxOutput.DPadRight: state.DPadRight = value >= 0.5; break;

            // Axes
            case XboxOutput.LeftStickX: state.LeftStickX = value; break;
            case XboxOutput.LeftStickY: state.LeftStickY = value; break;
            case XboxOutput.RightStickX: state.RightStickX = value; break;
            case XboxOutput.RightStickY: state.RightStickY = value; break;

            // Triggers
            case XboxOutput.LeftTrigger: state.LeftTrigger = value; break;
            case XboxOutput.RightTrigger: state.RightTrigger = value; break;
        }
    }
}

/// <summary>
/// Represents the complete state of an Xbox controller.
/// </summary>
public class XboxControllerState
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
