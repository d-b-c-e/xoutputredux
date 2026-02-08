using XOutputRedux.Core.Mapping;

namespace XOutputRedux.Tests;

[TestClass]
public class MappingTests
{
    [TestMethod]
    public void OutputMapping_Button_OrLogic_AnyPressedReturnsPressed()
    {
        // Arrange
        var mapping = new OutputMapping(XboxOutput.A);
        mapping.AddBinding(new InputBinding { DeviceId = "dev1", SourceIndex = 0 });
        mapping.AddBinding(new InputBinding { DeviceId = "dev1", SourceIndex = 1 });

        var inputs = new Dictionary<(string, int), double>
        {
            [("dev1", 0)] = 0.0, // Not pressed
            [("dev1", 1)] = 1.0  // Pressed
        };

        double? GetValue(string d, int i) => inputs.TryGetValue((d, i), out var v) ? v : null;

        // Act
        double result = mapping.Evaluate(GetValue);

        // Assert - should be pressed (OR logic)
        Assert.AreEqual(1.0, result);
    }

    [TestMethod]
    public void OutputMapping_Button_OrLogic_NonePressed_ReturnsNotPressed()
    {
        // Arrange
        var mapping = new OutputMapping(XboxOutput.A);
        mapping.AddBinding(new InputBinding { DeviceId = "dev1", SourceIndex = 0 });
        mapping.AddBinding(new InputBinding { DeviceId = "dev1", SourceIndex = 1 });

        var inputs = new Dictionary<(string, int), double>
        {
            [("dev1", 0)] = 0.0, // Not pressed
            [("dev1", 1)] = 0.0  // Not pressed
        };

        double? GetValue(string d, int i) => inputs.TryGetValue((d, i), out var v) ? v : null;

        // Act
        double result = mapping.Evaluate(GetValue);

        // Assert - should not be pressed
        Assert.AreEqual(0.0, result);
    }

    [TestMethod]
    public void OutputMapping_Axis_MaxDeflection_TakesLargestDeflection()
    {
        // Arrange
        var mapping = new OutputMapping(XboxOutput.LeftStickX);
        mapping.AddBinding(new InputBinding { DeviceId = "dev1", SourceIndex = 0 });
        mapping.AddBinding(new InputBinding { DeviceId = "dev1", SourceIndex = 1 });

        var inputs = new Dictionary<(string, int), double>
        {
            [("dev1", 0)] = 0.6, // Small deflection right
            [("dev1", 1)] = 0.9  // Large deflection right
        };

        double? GetValue(string d, int i) => inputs.TryGetValue((d, i), out var v) ? v : null;

        // Act
        double result = mapping.Evaluate(GetValue);

        // Assert - should take the one with largest deflection from center
        Assert.AreEqual(0.9, result, 0.001);
    }

    [TestMethod]
    public void OutputMapping_Trigger_MaxValue_TakesHighestValue()
    {
        // Arrange
        var mapping = new OutputMapping(XboxOutput.LeftTrigger);
        mapping.AddBinding(new InputBinding { DeviceId = "dev1", SourceIndex = 0 });
        mapping.AddBinding(new InputBinding { DeviceId = "dev1", SourceIndex = 1 });

        var inputs = new Dictionary<(string, int), double>
        {
            [("dev1", 0)] = 0.3, // Partial press
            [("dev1", 1)] = 0.8  // More pressed
        };

        double? GetValue(string d, int i) => inputs.TryGetValue((d, i), out var v) ? v : null;

        // Act
        double result = mapping.Evaluate(GetValue);

        // Assert - should take max value
        Assert.AreEqual(0.8, result, 0.001);
    }

    [TestMethod]
    public void InputBinding_Invert_FlipsValue()
    {
        // Arrange
        var binding = new InputBinding
        {
            DeviceId = "dev1",
            SourceIndex = 0,
            Invert = true
        };

        // Act
        double result = binding.TransformValue(0.8);

        // Assert
        Assert.AreEqual(0.2, result, 0.001);
    }

    [TestMethod]
    public void InputBinding_MinMax_ScalesValue()
    {
        // Arrange - input range is 0.2 to 0.8
        var binding = new InputBinding
        {
            DeviceId = "dev1",
            SourceIndex = 0,
            MinValue = 0.2,
            MaxValue = 0.8
        };

        // Act - 0.5 is halfway through the range
        double result = binding.TransformValue(0.5);

        // Assert - should be 0.5 (halfway through output range)
        Assert.AreEqual(0.5, result, 0.001);
    }

    [TestMethod]
    public void InputBinding_EvaluateAsButton_UseThreshold()
    {
        // Arrange
        var binding = new InputBinding
        {
            DeviceId = "dev1",
            SourceIndex = 0,
            ButtonThreshold = 0.7
        };

        // Act & Assert
        Assert.IsFalse(binding.EvaluateAsButton(0.5)); // Below threshold
        Assert.IsTrue(binding.EvaluateAsButton(0.8));  // Above threshold
    }

    [TestMethod]
    public void MappingProfile_CanAddAndRemoveBindings()
    {
        // Arrange
        var profile = new MappingProfile { Name = "Test" };
        var binding = new InputBinding { DeviceId = "dev1", SourceIndex = 0 };

        // Act
        profile.AddBinding(XboxOutput.A, binding);

        // Assert
        Assert.AreEqual(1, profile.GetMapping(XboxOutput.A).Bindings.Count);
        Assert.AreEqual(1, profile.TotalBindings);

        // Act - remove
        profile.RemoveBinding(XboxOutput.A, binding);

        // Assert
        Assert.AreEqual(0, profile.GetMapping(XboxOutput.A).Bindings.Count);
    }

    [TestMethod]
    public void MappingProfile_Clone_CreatesCopy()
    {
        // Arrange
        var profile = new MappingProfile { Name = "Original", Description = "Test" };
        profile.AddBinding(XboxOutput.A, new InputBinding { DeviceId = "dev1", SourceIndex = 0 });
        profile.AddBinding(XboxOutput.B, new InputBinding { DeviceId = "dev1", SourceIndex = 1 });

        // Act
        var clone = profile.Clone();

        // Assert
        Assert.AreEqual("Original (Copy)", clone.Name);
        Assert.AreEqual("Test", clone.Description);
        Assert.AreEqual(2, clone.TotalBindings);
    }

    [TestMethod]
    public void MappingEngine_Evaluate_AppliesAllMappings()
    {
        // Arrange
        var engine = new MappingEngine();
        var profile = new MappingProfile { Name = "Test" };

        profile.AddBinding(XboxOutput.A, new InputBinding { DeviceId = "dev1", SourceIndex = 0 });
        profile.AddBinding(XboxOutput.LeftStickX, new InputBinding { DeviceId = "dev1", SourceIndex = 1 });

        engine.ActiveProfile = profile;
        engine.UpdateInput("dev1", 0, 1.0); // A pressed
        engine.UpdateInput("dev1", 1, 0.75); // Stick right

        // Act
        var state = engine.Evaluate();

        // Assert
        Assert.IsTrue(state.A);
        Assert.AreEqual(0.75, state.LeftStickX, 0.001);
    }

    [TestMethod]
    public void MappingEngine_NoProfile_ReturnsDefaultState()
    {
        // Arrange
        var engine = new MappingEngine();

        // Act
        var state = engine.Evaluate();

        // Assert
        Assert.IsFalse(state.A);
        Assert.AreEqual(0.5, state.LeftStickX); // Centered
        Assert.AreEqual(0.0, state.LeftTrigger); // Released
    }

    [TestMethod]
    public void XboxOutput_IsButton_CorrectForButtons()
    {
        Assert.IsTrue(XboxOutput.A.IsButton());
        Assert.IsTrue(XboxOutput.DPadUp.IsButton());
        Assert.IsFalse(XboxOutput.LeftStickX.IsButton());
        Assert.IsFalse(XboxOutput.LeftTrigger.IsButton());
    }

    [TestMethod]
    public void XboxOutput_IsAxis_CorrectForAxes()
    {
        Assert.IsTrue(XboxOutput.LeftStickX.IsAxis());
        Assert.IsTrue(XboxOutput.RightStickY.IsAxis());
        Assert.IsFalse(XboxOutput.A.IsAxis());
        Assert.IsFalse(XboxOutput.LeftTrigger.IsAxis());
    }

    [TestMethod]
    public void XboxOutput_IsTrigger_CorrectForTriggers()
    {
        Assert.IsTrue(XboxOutput.LeftTrigger.IsTrigger());
        Assert.IsTrue(XboxOutput.RightTrigger.IsTrigger());
        Assert.IsFalse(XboxOutput.A.IsTrigger());
        Assert.IsFalse(XboxOutput.LeftStickX.IsTrigger());
    }

    #region Response Curve Tests

    [TestMethod]
    public void InputBinding_Sensitivity_Linear_NoChange()
    {
        var binding = new InputBinding { DeviceId = "dev1", SourceIndex = 0, Sensitivity = 1.0 };

        Assert.AreEqual(0.0, binding.TransformValue(0.0), 0.001);
        Assert.AreEqual(0.25, binding.TransformValue(0.25), 0.001);
        Assert.AreEqual(0.5, binding.TransformValue(0.5), 0.001);
        Assert.AreEqual(0.75, binding.TransformValue(0.75), 0.001);
        Assert.AreEqual(1.0, binding.TransformValue(1.0), 0.001);
    }

    [TestMethod]
    public void InputBinding_Sensitivity_Axis_CenterPreserved()
    {
        var binding = new InputBinding { DeviceId = "dev1", SourceIndex = 0, Sensitivity = 2.0 };
        Assert.AreEqual(0.5, binding.TransformValue(0.5, isAxisOutput: true), 0.001);

        binding.Sensitivity = 0.5;
        Assert.AreEqual(0.5, binding.TransformValue(0.5, isAxisOutput: true), 0.001);

        binding.Sensitivity = 3.0;
        Assert.AreEqual(0.5, binding.TransformValue(0.5, isAxisOutput: true), 0.001);
    }

    [TestMethod]
    public void InputBinding_Sensitivity_Axis_ExtremesPreserved()
    {
        var binding = new InputBinding { DeviceId = "dev1", SourceIndex = 0, Sensitivity = 3.0 };

        Assert.AreEqual(0.0, binding.TransformValue(0.0, isAxisOutput: true), 0.001);
        Assert.AreEqual(1.0, binding.TransformValue(1.0, isAxisOutput: true), 0.001);
    }

    [TestMethod]
    public void InputBinding_Sensitivity_Axis_HigherThanOne_LessSensitiveNearCenter()
    {
        var binding = new InputBinding { DeviceId = "dev1", SourceIndex = 0, Sensitivity = 2.0 };

        // Small deflection right (0.75 = 50% deflection from center)
        double result = binding.TransformValue(0.75, isAxisOutput: true);
        // With sensitivity 2.0: deflection 0.5^2 = 0.25, output = 0.5 + 0.25/2 = 0.625
        Assert.AreEqual(0.625, result, 0.001);
        Assert.IsTrue(result < 0.75, "Higher sensitivity should reduce output near center");
    }

    [TestMethod]
    public void InputBinding_Sensitivity_Axis_LowerThanOne_MoreSensitiveNearCenter()
    {
        var binding = new InputBinding { DeviceId = "dev1", SourceIndex = 0, Sensitivity = 0.5 };

        double result = binding.TransformValue(0.75, isAxisOutput: true);
        Assert.IsTrue(result > 0.75, "Lower sensitivity should increase output near center");
    }

    [TestMethod]
    public void InputBinding_Sensitivity_Axis_Symmetric()
    {
        var binding = new InputBinding { DeviceId = "dev1", SourceIndex = 0, Sensitivity = 2.0 };

        double left = binding.TransformValue(0.25, isAxisOutput: true);
        double right = binding.TransformValue(0.75, isAxisOutput: true);

        // Symmetric around center: left should equal 1.0 - right
        Assert.AreEqual(1.0 - right, left, 0.001);
    }

    [TestMethod]
    public void InputBinding_Sensitivity_Trigger_PowerCurve()
    {
        var binding = new InputBinding { DeviceId = "dev1", SourceIndex = 0, Sensitivity = 2.0 };

        // Trigger: simple power curve. 0.5^2 = 0.25
        double result = binding.TransformValue(0.5, isAxisOutput: false);
        Assert.AreEqual(0.25, result, 0.001);

        // 0.25^0.5 = 0.5
        binding.Sensitivity = 0.5;
        result = binding.TransformValue(0.25, isAxisOutput: false);
        Assert.AreEqual(0.5, result, 0.001);
    }

    [TestMethod]
    public void InputBinding_Sensitivity_Trigger_ExtremesPreserved()
    {
        var binding = new InputBinding { DeviceId = "dev1", SourceIndex = 0, Sensitivity = 3.0 };

        Assert.AreEqual(0.0, binding.TransformValue(0.0, isAxisOutput: false), 0.001);
        Assert.AreEqual(1.0, binding.TransformValue(1.0, isAxisOutput: false), 0.001);
    }

    [TestMethod]
    public void InputBinding_Sensitivity_DefaultValue_NoTransformation()
    {
        // Default sensitivity (1.0) should produce identical results to no curve
        var withCurve = new InputBinding { DeviceId = "dev1", SourceIndex = 0, Sensitivity = 1.0 };
        var without = new InputBinding { DeviceId = "dev1", SourceIndex = 0 };

        double[] testValues = { 0.0, 0.1, 0.25, 0.5, 0.75, 0.9, 1.0 };
        foreach (var v in testValues)
        {
            Assert.AreEqual(
                without.TransformValue(v, isAxisOutput: true),
                withCurve.TransformValue(v, isAxisOutput: true),
                0.001,
                $"Value {v} should be identical with default sensitivity");
        }
    }

    [TestMethod]
    public void MappingProfile_Clone_CopiesSensitivity()
    {
        var profile = new MappingProfile { Name = "Test" };
        profile.AddBinding(XboxOutput.LeftStickX, new InputBinding
        {
            DeviceId = "dev1",
            SourceIndex = 0,
            Sensitivity = 2.5
        });

        var clone = profile.Clone();

        var binding = clone.GetMapping(XboxOutput.LeftStickX).Bindings[0];
        Assert.AreEqual(2.5, binding.Sensitivity, 0.001);
    }

    [TestMethod]
    public void MappingProfile_RoundTrip_PreservesSensitivity()
    {
        var original = new MappingProfile { Name = "Test" };
        original.AddBinding(XboxOutput.LeftStickX, new InputBinding
        {
            DeviceId = "dev1",
            SourceIndex = 0,
            Sensitivity = 3.0
        });

        var data = MappingProfileData.FromProfile(original);
        var restored = data.ToProfile();

        var binding = restored.GetMapping(XboxOutput.LeftStickX).Bindings[0];
        Assert.AreEqual(3.0, binding.Sensitivity, 0.001);
    }

    #endregion
}
