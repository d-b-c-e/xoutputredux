namespace XOutputRedux.Core.Mapping;

/// <summary>
/// Represents a mapping from one or more physical inputs to an Xbox output.
/// Uses OR logic: any input can trigger the output.
/// </summary>
public class OutputMapping
{
    /// <summary>
    /// Enable diagnostic logging for axis evaluation.
    /// </summary>
    public static bool DiagnosticLogging { get; set; }
    internal static Action<string>? _diagnosticLog;

    /// <summary>
    /// Sets the diagnostic log callback.
    /// </summary>
    public static void SetDiagnosticLog(Action<string>? callback) => _diagnosticLog = callback;

    /// <summary>
    /// The Xbox output this mapping targets.
    /// </summary>
    public XboxOutput Output { get; }

    /// <summary>
    /// The input bindings for this output.
    /// Multiple bindings use OR logic for buttons, max-deflection for axes.
    /// </summary>
    public List<InputBinding> Bindings { get; } = new();

    public OutputMapping(XboxOutput output)
    {
        Output = output;
    }

    /// <summary>
    /// Evaluates all bindings and returns the output value.
    /// </summary>
    /// <param name="getInputValue">Function to get the current value of an input source.</param>
    /// <returns>The computed output value (0.0 - 1.0).</returns>
    public double Evaluate(Func<string, int, double?> getInputValue)
    {
        if (Bindings.Count == 0)
        {
            // No bindings - return default (center for axes, off for buttons)
            return Output.IsAxis() ? 0.5 : 0.0;
        }

        if (Output.IsButton())
        {
            // OR logic: any binding pressed = output pressed
            return EvaluateButton(getInputValue);
        }
        else if (Output.IsAxis())
        {
            // Max deflection from center
            return EvaluateAxis(getInputValue);
        }
        else
        {
            // Trigger: max value
            return EvaluateTrigger(getInputValue);
        }
    }

    /// <summary>
    /// Evaluates button output using OR logic.
    /// </summary>
    private double EvaluateButton(Func<string, int, double?> getInputValue)
    {
        foreach (var binding in Bindings)
        {
            double? inputValue = getInputValue(binding.DeviceId, binding.SourceIndex);
            if (inputValue.HasValue && binding.EvaluateAsButton(inputValue.Value))
            {
                return 1.0; // Button pressed
            }
        }
        return 0.0; // No binding triggered
    }

    /// <summary>
    /// Evaluates axis output using max deflection from center.
    /// Supports digital-to-axis bindings where buttons push the axis in a direction.
    /// </summary>
    private double EvaluateAxis(Func<string, int, double?> getInputValue)
    {
        double maxDeflection = 0.0;
        double result = 0.5; // Default to center

        // Track digital direction contributions separately
        bool hasDigitalBindings = false;
        double digitalSum = 0.0; // -1 to +1, summed from digital direction bindings

        foreach (var binding in Bindings)
        {
            double? inputValue = getInputValue(binding.DeviceId, binding.SourceIndex);
            if (!inputValue.HasValue) continue;

            if (binding.DigitalDirection != DigitalAxisDirection.None)
            {
                // Digital-to-axis: button pushes axis in a direction
                hasDigitalBindings = true;
                bool pressed = inputValue.Value >= binding.ButtonThreshold;
                if (pressed)
                {
                    digitalSum += binding.DigitalDirection == DigitalAxisDirection.Positive ? 1.0 : -1.0;
                }
            }
            else
            {
                // Analog binding: use transformed value with max deflection
                double transformed = binding.TransformValue(inputValue.Value, isAxisOutput: true);
                double deflection = Math.Abs(transformed - 0.5);
                if (deflection > maxDeflection)
                {
                    maxDeflection = deflection;
                    result = transformed;
                }
            }
        }

        // When digital bindings exist, they always take priority over analog bindings.
        // Digital bindings represent intentional button-to-axis mappings (e.g., HAT to stick),
        // and their center (0.5) when released must override any analog noise.
        if (hasDigitalBindings)
        {
            result = 0.5 + Math.Clamp(digitalSum, -1.0, 1.0) * 0.5;
        }

        // Diagnostic logging for axis outputs (throttled: only when result deviates from center)
        if (DiagnosticLogging && Math.Abs(result - 0.5) > 0.01)
        {
            var info = $"[EvalAxis] {Output}={result:F3} bindings={Bindings.Count} hasDigital={hasDigitalBindings} digitalSum={digitalSum:F1} maxAnalogDefl={maxDeflection:F3}";
            foreach (var b in Bindings)
            {
                var iv = getInputValue(b.DeviceId, b.SourceIndex);
                info += $"\n  srcIdx={b.SourceIndex} dir={b.DigitalDirection} input={iv?.ToString("F3") ?? "null"}";
            }
            _diagnosticLog?.Invoke(info);
        }

        return result;
    }

    /// <summary>
    /// Evaluates trigger output using max value.
    /// </summary>
    private double EvaluateTrigger(Func<string, int, double?> getInputValue)
    {
        double maxValue = 0.0;

        foreach (var binding in Bindings)
        {
            double? inputValue = getInputValue(binding.DeviceId, binding.SourceIndex);
            if (inputValue.HasValue)
            {
                double transformed = binding.TransformValue(inputValue.Value);
                if (transformed > maxValue)
                {
                    maxValue = transformed;
                }
            }
        }

        return maxValue;
    }

    /// <summary>
    /// Adds a binding to this output mapping.
    /// </summary>
    public void AddBinding(InputBinding binding)
    {
        Bindings.Add(binding);
    }

    /// <summary>
    /// Removes a binding from this output mapping.
    /// </summary>
    public bool RemoveBinding(InputBinding binding)
    {
        return Bindings.Remove(binding);
    }

    /// <summary>
    /// Clears all bindings from this output mapping.
    /// </summary>
    public void ClearBindings()
    {
        Bindings.Clear();
    }
}
