namespace XOutputRedux.Core.Mapping;

/// <summary>
/// Represents a mapping from one or more physical inputs to an Xbox output.
/// Uses OR logic: any input can trigger the output.
/// </summary>
public class OutputMapping
{
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
    /// </summary>
    private double EvaluateAxis(Func<string, int, double?> getInputValue)
    {
        double maxDeflection = 0.0;
        double result = 0.5; // Default to center

        foreach (var binding in Bindings)
        {
            double? inputValue = getInputValue(binding.DeviceId, binding.SourceIndex);
            if (inputValue.HasValue)
            {
                double transformed = binding.TransformValue(inputValue.Value);
                double deflection = Math.Abs(transformed - 0.5);
                if (deflection > maxDeflection)
                {
                    maxDeflection = deflection;
                    result = transformed;
                }
            }
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
