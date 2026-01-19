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
}
