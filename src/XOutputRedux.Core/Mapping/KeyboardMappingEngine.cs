namespace XOutputRedux.Core.Mapping;

/// <summary>
/// Evaluates keyboard bindings and produces key down/up transitions.
/// </summary>
/// <remarks>
/// Unlike <see cref="MappingEngine"/>, which is level-based (ViGEm is happy to receive the
/// full controller state every tick), keyboard output is edge-based: a key must be pressed
/// once on the rising edge and released once on the falling edge. This engine therefore
/// tracks which keys are currently held and reports only the difference.
/// <para>
/// Multiple bindings may target the same key; the key stays down while any of them is
/// active, matching the OR logic used elsewhere in the app.
/// </para>
/// </remarks>
public sealed class KeyboardMappingEngine
{
    private readonly KeyboardBinding[] _bindings;
    private readonly bool[] _bindingHeld;
    private readonly Dictionary<(string DeviceId, int SourceIndex), double> _inputValues = new();
    private readonly HashSet<KeyCode> _keysDown = new();
    private readonly HashSet<KeyCode> _desired = new();
    private readonly object _lock = new();

    public KeyboardMappingEngine(IEnumerable<KeyboardBinding> bindings)
    {
        _bindings = bindings.Where(b => b.Key != KeyCode.None).ToArray();
        _bindingHeld = new bool[_bindings.Length];
    }

    /// <summary>Number of active bindings.</summary>
    public int BindingCount => _bindings.Length;

    /// <summary>Keys currently held down by this engine.</summary>
    public IReadOnlyCollection<KeyCode> KeysDown
    {
        get { lock (_lock) { return _keysDown.ToArray(); } }
    }

    /// <summary>
    /// Updates an input value from a device.
    /// </summary>
    public void UpdateInput(string deviceId, int sourceIndex, double value)
    {
        lock (_lock)
        {
            _inputValues[(deviceId, sourceIndex)] = value;
        }
    }

    /// <summary>
    /// Recomputes held keys and writes the transitions into the caller's buffers.
    /// </summary>
    /// <remarks>
    /// Buffers are caller-owned so this stays allocation-free on the input hot path,
    /// which runs on every device input event.
    /// </remarks>
    /// <returns>True if either buffer received anything.</returns>
    public bool Evaluate(List<KeyCode> pressed, List<KeyCode> released)
    {
        pressed.Clear();
        released.Clear();

        lock (_lock)
        {
            _desired.Clear();

            for (int i = 0; i < _bindings.Length; i++)
            {
                var binding = _bindings[i];

                if (!_inputValues.TryGetValue((binding.DeviceId, binding.SourceIndex), out double value))
                {
                    continue;
                }

                if (binding.Invert)
                {
                    value = 1.0 - value;
                }

                // Schmitt trigger: press high, release low, so a source resting near the
                // threshold cannot chatter the key.
                if (_bindingHeld[i])
                {
                    if (value < binding.ReleaseThreshold) _bindingHeld[i] = false;
                }
                else
                {
                    if (value >= binding.PressThreshold) _bindingHeld[i] = true;
                }

                if (_bindingHeld[i])
                {
                    _desired.Add(binding.Key);
                }
            }

            foreach (var key in _desired)
            {
                if (!_keysDown.Contains(key)) pressed.Add(key);
            }

            foreach (var key in _keysDown)
            {
                if (!_desired.Contains(key)) released.Add(key);
            }

            foreach (var key in pressed) _keysDown.Add(key);
            foreach (var key in released) _keysDown.Remove(key);
        }

        return pressed.Count > 0 || released.Count > 0;
    }

    /// <summary>
    /// Clears all held state and reports every key that needs releasing.
    /// Must be called when the profile stops or output is suspended, otherwise a key
    /// that was down at that moment stays stuck down in the game.
    /// </summary>
    public void ReleaseAll(List<KeyCode> released)
    {
        released.Clear();

        lock (_lock)
        {
            released.AddRange(_keysDown);
            _keysDown.Clear();
            Array.Clear(_bindingHeld);
        }
    }
}
