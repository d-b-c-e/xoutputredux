using System.Text.Json;
using XOutputRedux.Core.HidHide;

namespace XOutputRedux.Tests;

/// <summary>
/// Tests for persisting hardware identity in isolation profiles (1.3.0).
///
/// 1.2.0 derived identity at match time as a fallback, which meant it was
/// reconstructed from a path that may already have gone stale. 1.3.0 stores the
/// identity with the profile and treats it as authoritative, so a profile keeps
/// working across replugging without being edited.
///
/// The migration cases matter most: profiles written before the field existed
/// must adopt the new behaviour rather than silently keeping the old one.
/// </summary>
[TestClass]
public class IsolationIdentityPersistenceTests
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [TestMethod]
    public void SavingDerivesHardwareIdWhenAbsent()
    {
        var settings = new DeviceIsolationSettings
        {
            KeepDevices = new List<IsolationDevice>
            {
                new() { DeviceInstancePath = @"HID\VID_346E&PID_0006&MI_02\8&32e6eb88&0&0000",
                        FriendlyName = "MOZA R12 Base" }
            }
        };

        var data = DeviceIsolationSettingsData.FromSettings(settings);

        Assert.IsNotNull(data);
        Assert.AreEqual("VID_346E&PID_0006&MI_02", data!.KeepDevices[0].HardwareId);
    }

    [TestMethod]
    public void LoadingBackfillsHardwareIdForLegacyProfiles()
    {
        // A profile as written by 1.2.0 and earlier: path only, no identity.
        var legacy = new DeviceIsolationSettingsData
        {
            Enabled = true,
            KeepDevices = new List<IsolationDeviceData>
            {
                new() { DeviceInstancePath = @"HID\VID_346E&PID_0006&MI_02\8&32e6eb88&0&0000" }
            }
        };

        var settings = legacy.ToSettings();

        Assert.AreEqual("VID_346E&PID_0006&MI_02", settings.KeepDevices[0].HardwareId);
    }

    [TestMethod]
    public void LegacyProfilesAdoptIdentityFirstMatching()
    {
        // No MatchByHardwareId in the JSON at all. Such a profile has no opinion,
        // and should get the new default rather than being pinned to the old
        // exact-path behaviour it never chose.
        var legacy = new DeviceIsolationSettingsData { Enabled = true };

        Assert.IsNull(legacy.MatchByHardwareId, "absent in the persisted form");
        Assert.IsTrue(legacy.ToSettings().MatchByHardwareId, "adopts the new default");
    }

    [TestMethod]
    public void ExplicitFalseIsPreservedAcrossRoundTrip()
    {
        // Someone with two identical devices turns identity matching off. That
        // decision must survive a save/load cycle - it is exactly the case a
        // nullable-with-default could quietly discard.
        var settings = new DeviceIsolationSettings { MatchByHardwareId = false };

        var json = JsonSerializer.Serialize(DeviceIsolationSettingsData.FromSettings(settings), Json);
        var back = JsonSerializer.Deserialize<DeviceIsolationSettingsData>(json, Json);

        Assert.IsNotNull(back);
        Assert.IsFalse(back!.MatchByHardwareId);
        Assert.IsFalse(back.ToSettings().MatchByHardwareId);
    }

    [TestMethod]
    public void HardwareIdSurvivesRoundTrip()
    {
        var settings = new DeviceIsolationSettings
        {
            KeepDevices = new List<IsolationDevice>
            {
                new() { DeviceInstancePath = @"HID\VID_0483&PID_0531\a&509e5f3&0&0000",
                        FriendlyName = "DS-8X Shifter" }
            }
        };

        var json = JsonSerializer.Serialize(DeviceIsolationSettingsData.FromSettings(settings), Json);
        var back = JsonSerializer.Deserialize<DeviceIsolationSettingsData>(json, Json)!.ToSettings();

        Assert.AreEqual("VID_0483&PID_0531", back.KeepDevices[0].HardwareId);
        Assert.AreEqual(@"HID\VID_0483&PID_0531\a&509e5f3&0&0000", back.KeepDevices[0].DeviceInstancePath);
    }

    [TestMethod]
    public void VirtualDevicesGetNoHardwareId()
    {
        // Root-enumerated devices (vJoy, ViGEm pads) have no VID/PID, so they must
        // keep matching by exact path rather than by an identity that would match
        // far too broadly.
        var settings = new DeviceIsolationSettings
        {
            KeepDevices = new List<IsolationDevice>
            {
                new() { DeviceInstancePath = @"ROOT\HIDCLASS\0000", FriendlyName = "vJoy Device" }
            }
        };

        var data = DeviceIsolationSettingsData.FromSettings(settings);

        Assert.IsNull(data!.KeepDevices[0].HardwareId);
    }

    [TestMethod]
    public void EnsureHardwareIdDoesNotOverwriteAnExistingValue()
    {
        // If a profile already carries an identity, a stale path must not be
        // allowed to redefine it.
        var device = new IsolationDevice
        {
            DeviceInstancePath = @"HID\VID_045E&PID_028E&IG_05\d&1e0810ad&0&0000",
            HardwareId = "VID_346E&PID_0006&MI_02"
        };

        device.EnsureHardwareId();

        Assert.AreEqual("VID_346E&PID_0006&MI_02", device.HardwareId);
    }

    [TestMethod]
    public void CloneCarriesHardwareIdAndPreference()
    {
        var settings = new DeviceIsolationSettings
        {
            MatchByHardwareId = false,
            KeepDevices = new List<IsolationDevice>
            {
                new() { DeviceInstancePath = @"HID\VID_346E&PID_0006&MI_02\8&x&0&0000",
                        HardwareId = "VID_346E&PID_0006&MI_02" }
            }
        };

        var clone = settings.Clone();

        Assert.IsFalse(clone.MatchByHardwareId);
        Assert.AreEqual("VID_346E&PID_0006&MI_02", clone.KeepDevices[0].HardwareId);
    }

    [TestMethod]
    public void IdentityIsUnchangedByPortAndXInputSlotMoves()
    {
        // The whole point: the same physical device across a replug and an XInput
        // slot change must resolve to one identity.
        var a = DeviceIdentity.FromPath(@"HID\VID_045E&PID_028E&IG_00\d&af8d77e&0&0000");
        var b = DeviceIdentity.FromPath(@"HID\VID_045E&PID_028E&IG_04\d&20e2af56&0&0000");

        Assert.AreEqual(a, b);
        Assert.IsTrue(DeviceIdentity.Matches(a, b));
    }

    [TestMethod]
    public void MatchesRejectsNullAndEmpty()
    {
        Assert.IsFalse(DeviceIdentity.Matches(null, "VID_346E&PID_0006"));
        Assert.IsFalse(DeviceIdentity.Matches("VID_346E&PID_0006", null));
        Assert.IsFalse(DeviceIdentity.Matches("", ""));
    }
}
