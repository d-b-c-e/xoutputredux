using XOutputRedux.Core.Mapping;

namespace XOutputRedux.Tests;

[TestClass]
public class KeyboardOutputTests
{
    private readonly List<KeyCode> _pressed = new();
    private readonly List<KeyCode> _released = new();

    private static KeyboardBinding Bind(int sourceIndex, KeyCode key) => new()
    {
        DeviceId = "dev1",
        SourceIndex = sourceIndex,
        Key = key
    };

    [TestMethod]
    public void ScanCode_SeparatesExtendedKeysThatShareACode()
    {
        // The reason scan codes must carry the extended flag: these two keys are
        // indistinguishable by scan code alone.
        Assert.AreEqual(0x48, KeyCodeExtensions.ScanCode(KeyCode.UpArrow));
        Assert.AreEqual(0x48, KeyCodeExtensions.ScanCode(KeyCode.NumPad8));
        Assert.IsTrue(KeyCode.UpArrow.IsExtended());
        Assert.IsFalse(KeyCode.NumPad8.IsExtended());

        Assert.AreEqual(0x53, KeyCodeExtensions.ScanCode(KeyCode.Delete));
        Assert.AreEqual(0x53, KeyCodeExtensions.ScanCode(KeyCode.NumPadDecimal));
        Assert.IsTrue(KeyCode.Delete.IsExtended());
        Assert.IsFalse(KeyCode.NumPadDecimal.IsExtended());
    }

    [TestMethod]
    public void KeyCode_HasNoDuplicateValues()
    {
        // Duplicate enum values would break name-based JSON round-tripping.
        var values = Enum.GetValues<KeyCode>().Select(v => (int)v).ToList();
        Assert.AreEqual(values.Count, values.Distinct().Count(), "KeyCode contains duplicate scan codes");
    }

    [TestMethod]
    public void Engine_EmitsPressOnRisingEdgeOnly()
    {
        var engine = new KeyboardMappingEngine(new[] { Bind(0, KeyCode.W) });

        engine.UpdateInput("dev1", 0, 1.0);
        Assert.IsTrue(engine.Evaluate(_pressed, _released));
        CollectionAssert.AreEqual(new[] { KeyCode.W }, _pressed);
        Assert.AreEqual(0, _released.Count);

        // Still held — no further events.
        engine.UpdateInput("dev1", 0, 1.0);
        Assert.IsFalse(engine.Evaluate(_pressed, _released));
        Assert.AreEqual(0, _pressed.Count);
        Assert.AreEqual(0, _released.Count);
    }

    [TestMethod]
    public void Engine_EmitsReleaseOnFallingEdge()
    {
        var engine = new KeyboardMappingEngine(new[] { Bind(0, KeyCode.W) });

        engine.UpdateInput("dev1", 0, 1.0);
        engine.Evaluate(_pressed, _released);

        engine.UpdateInput("dev1", 0, 0.0);
        Assert.IsTrue(engine.Evaluate(_pressed, _released));
        Assert.AreEqual(0, _pressed.Count);
        CollectionAssert.AreEqual(new[] { KeyCode.W }, _released);
    }

    [TestMethod]
    public void Engine_HysteresisPreventsChatterNearThreshold()
    {
        var binding = Bind(0, KeyCode.W);
        binding.PressThreshold = 0.5;
        binding.ReleaseThreshold = 0.35;
        var engine = new KeyboardMappingEngine(new[] { binding });

        engine.UpdateInput("dev1", 0, 0.55);
        engine.Evaluate(_pressed, _released);
        CollectionAssert.AreEqual(new[] { KeyCode.W }, _pressed);

        // Dipping into the dead band must NOT release — this is the case that would
        // machine-gun the key on an analog stick resting near the trip point.
        engine.UpdateInput("dev1", 0, 0.45);
        Assert.IsFalse(engine.Evaluate(_pressed, _released));

        engine.UpdateInput("dev1", 0, 0.40);
        Assert.IsFalse(engine.Evaluate(_pressed, _released));

        // Below the release threshold it finally lifts.
        engine.UpdateInput("dev1", 0, 0.30);
        Assert.IsTrue(engine.Evaluate(_pressed, _released));
        CollectionAssert.AreEqual(new[] { KeyCode.W }, _released);
    }

    [TestMethod]
    public void Engine_TwoBindingsOnSameKey_UseOrLogic()
    {
        var engine = new KeyboardMappingEngine(new[] { Bind(0, KeyCode.Space), Bind(1, KeyCode.Space) });

        engine.UpdateInput("dev1", 0, 1.0);
        engine.Evaluate(_pressed, _released);
        CollectionAssert.AreEqual(new[] { KeyCode.Space }, _pressed);

        // Second source also goes down — key is already down, nothing to emit.
        engine.UpdateInput("dev1", 1, 1.0);
        Assert.IsFalse(engine.Evaluate(_pressed, _released));

        // First releases, second still held — key must STAY down.
        engine.UpdateInput("dev1", 0, 0.0);
        Assert.IsFalse(engine.Evaluate(_pressed, _released));

        // Both released — now it lifts.
        engine.UpdateInput("dev1", 1, 0.0);
        Assert.IsTrue(engine.Evaluate(_pressed, _released));
        CollectionAssert.AreEqual(new[] { KeyCode.Space }, _released);
    }

    [TestMethod]
    public void Engine_Invert_TripsOnLowValues()
    {
        var binding = Bind(0, KeyCode.LeftArrow);
        binding.Invert = true;
        var engine = new KeyboardMappingEngine(new[] { binding });

        engine.UpdateInput("dev1", 0, 0.0);
        Assert.IsTrue(engine.Evaluate(_pressed, _released));
        CollectionAssert.AreEqual(new[] { KeyCode.LeftArrow }, _pressed);
    }

    [TestMethod]
    public void Engine_ReleaseAll_ReportsHeldKeysAndClearsState()
    {
        var engine = new KeyboardMappingEngine(new[] { Bind(0, KeyCode.W), Bind(1, KeyCode.A) });

        engine.UpdateInput("dev1", 0, 1.0);
        engine.UpdateInput("dev1", 1, 1.0);
        engine.Evaluate(_pressed, _released);
        Assert.AreEqual(2, _pressed.Count);

        engine.ReleaseAll(_released);
        CollectionAssert.AreEquivalent(new[] { KeyCode.W, KeyCode.A }, _released);
        Assert.AreEqual(0, engine.KeysDown.Count);

        // After ReleaseAll the input is still physically high; it must re-press rather
        // than stay silent, otherwise regaining focus would leave the key dead.
        engine.UpdateInput("dev1", 0, 1.0);
        Assert.IsTrue(engine.Evaluate(_pressed, _released));
        CollectionAssert.Contains(_pressed, KeyCode.W);
    }

    [TestMethod]
    public void Engine_IgnoresUnmappedAndNoneKeyBindings()
    {
        var engine = new KeyboardMappingEngine(new[] { Bind(0, KeyCode.None), Bind(1, KeyCode.W) });
        Assert.AreEqual(1, engine.BindingCount);

        // Source with no value reported yet must not trip anything.
        Assert.IsFalse(engine.Evaluate(_pressed, _released));
    }

    [TestMethod]
    public void Profile_KeyboardMappings_RoundTripThroughJson()
    {
        var profile = new MappingProfile { Name = "KbTest", KeyboardTargetProcess = "doom2" };
        profile.KeyboardMappings.Add(new KeyboardBinding
        {
            DeviceId = "abc123",
            SourceIndex = 4,
            Key = KeyCode.UpArrow,
            DisplayName = "Stick Up",
            PressThreshold = 0.6,
            ReleaseThreshold = 0.4
        });

        var data = MappingProfileData.FromProfile(profile);
        string json = System.Text.Json.JsonSerializer.Serialize(data);

        // Keys must serialize by name so profiles stay hand-editable.
        StringAssert.Contains(json, "UpArrow");

        var restored = System.Text.Json.JsonSerializer
            .Deserialize<MappingProfileData>(json)!.ToProfile();

        Assert.AreEqual(1, restored.KeyboardMappings.Count);
        Assert.AreEqual(KeyCode.UpArrow, restored.KeyboardMappings[0].Key);
        Assert.AreEqual(4, restored.KeyboardMappings[0].SourceIndex);
        Assert.AreEqual(0.6, restored.KeyboardMappings[0].PressThreshold);
        Assert.AreEqual("doom2", restored.KeyboardTargetProcess);
        Assert.IsTrue(restored.HasKeyboardOutput);
    }

    [TestMethod]
    public void Profile_WithoutKeyboardMappings_OmitsThemFromJson()
    {
        var profile = new MappingProfile { Name = "Plain" };
        var data = MappingProfileData.FromProfile(profile);

        Assert.IsNull(data.KeyboardMappings);
        Assert.IsFalse(profile.HasKeyboardOutput);

        // An existing profile with no keyboard section must still load cleanly.
        string legacy = """
            {"schemaVersion":5,"name":"Legacy","mappings":[]}
            """;
        var loaded = System.Text.Json.JsonSerializer.Deserialize<MappingProfileData>(
            legacy,
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            })!;

        var restored = loaded.ToProfile();
        Assert.AreEqual(0, restored.KeyboardMappings.Count);
        Assert.IsFalse(restored.HasKeyboardOutput);
    }

    [TestMethod]
    public void Profile_Clone_CopiesKeyboardMappings()
    {
        var profile = new MappingProfile { KeyboardTargetProcess = "game" };
        profile.KeyboardMappings.Add(new KeyboardBinding
        {
            DeviceId = "d", SourceIndex = 1, Key = KeyCode.Space
        });

        var clone = profile.Clone();
        Assert.AreEqual(1, clone.KeyboardMappings.Count);
        Assert.AreEqual(KeyCode.Space, clone.KeyboardMappings[0].Key);
        Assert.AreEqual("game", clone.KeyboardTargetProcess);

        // Deep copy — mutating the clone must not touch the original.
        clone.KeyboardMappings.Clear();
        Assert.AreEqual(1, profile.KeyboardMappings.Count);
    }
}
