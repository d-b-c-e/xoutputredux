using System.Text.Json;
using XOutputRedux.Core.HidHide;
using XOutputRedux.Core.Mapping;
using XOutputRedux.HidHide;

namespace XOutputRedux.Tests;

[TestClass]
public class DeviceIsolationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static MappingProfile CreateIsolationProfile()
    {
        return new MappingProfile
        {
            Name = "VR Isolation",
            ProfileType = ProfileType.DeviceIsolation,
            DeviceIsolation = new DeviceIsolationSettings
            {
                Enabled = true,
                KeepDevices = new List<IsolationDevice>
                {
                    new()
                    {
                        DeviceInstancePath = @"HID\VID_346E&PID_0005\7&2f2a54b2&0&0000",
                        FriendlyName = "Gudsen MOZA R12 Base"
                    }
                }
            }
        };
    }

    [TestMethod]
    public void DefaultProfile_IsMappingType()
    {
        var profile = new MappingProfile();

        Assert.AreEqual(ProfileType.Mapping, profile.ProfileType);
        Assert.IsNull(profile.DeviceIsolation);
    }

    [TestMethod]
    public void IsolationProfile_RoundTrip_PreservesTypeAndKeepDevices()
    {
        var original = CreateIsolationProfile();

        var data = MappingProfileData.FromProfile(original);
        var restored = data.ToProfile();

        Assert.AreEqual(ProfileType.DeviceIsolation, restored.ProfileType);
        Assert.IsNotNull(restored.DeviceIsolation);
        Assert.IsTrue(restored.DeviceIsolation.Enabled);
        Assert.AreEqual(1, restored.DeviceIsolation.KeepDevices.Count);
        Assert.AreEqual(@"HID\VID_346E&PID_0005\7&2f2a54b2&0&0000",
            restored.DeviceIsolation.KeepDevices[0].DeviceInstancePath);
        Assert.AreEqual("Gudsen MOZA R12 Base",
            restored.DeviceIsolation.KeepDevices[0].FriendlyName);
    }

    [TestMethod]
    public void IsolationProfile_JsonRoundTrip_PreservesTypeAndKeepDevices()
    {
        var data = MappingProfileData.FromProfile(CreateIsolationProfile());

        string json = JsonSerializer.Serialize(data, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<MappingProfileData>(json, JsonOptions);

        // ProfileType serializes as a readable string, not an integer
        StringAssert.Contains(json, "\"DeviceIsolation\"");

        Assert.IsNotNull(deserialized);
        Assert.AreEqual(ProfileType.DeviceIsolation, deserialized.ProfileType);
        Assert.IsNotNull(deserialized.DeviceIsolation);
        Assert.AreEqual(1, deserialized.DeviceIsolation.KeepDevices.Count);
        Assert.AreEqual("Gudsen MOZA R12 Base",
            deserialized.DeviceIsolation.KeepDevices[0].FriendlyName);
    }

    [TestMethod]
    public void V4Profile_WithoutProfileType_DefaultsToMappingAndMigrates()
    {
        // A pre-isolation (v4) profile has no profileType / deviceIsolation fields
        string v4Json = """
            {
                "schemaVersion": 4,
                "name": "Old Mapping Profile",
                "mappings": []
            }
            """;

        var data = JsonSerializer.Deserialize<MappingProfileData>(v4Json, JsonOptions);

        Assert.IsNotNull(data);
        Assert.AreEqual(ProfileType.Mapping, data.ProfileType);
        Assert.IsNull(data.DeviceIsolation);
        Assert.IsTrue(data.NeedsMigration);

        data.Migrate();

        Assert.AreEqual(MappingProfileData.CurrentSchemaVersion, data.SchemaVersion);
        Assert.AreEqual(ProfileType.Mapping, data.ProfileType);
    }

    [TestMethod]
    public void IsolationProfile_Clone_DeepCopiesIsolationSettings()
    {
        var original = CreateIsolationProfile();

        var clone = original.Clone();

        Assert.AreEqual(ProfileType.DeviceIsolation, clone.ProfileType);
        Assert.IsNotNull(clone.DeviceIsolation);
        Assert.AreNotSame(original.DeviceIsolation, clone.DeviceIsolation);
        Assert.AreNotSame(original.DeviceIsolation!.KeepDevices[0], clone.DeviceIsolation.KeepDevices[0]);
        Assert.AreEqual(original.DeviceIsolation.KeepDevices[0].DeviceInstancePath,
            clone.DeviceIsolation.KeepDevices[0].DeviceInstancePath);

        // Mutating the clone must not affect the original
        clone.DeviceIsolation.KeepDevices[0].DeviceInstancePath = "changed";
        Assert.AreNotEqual("changed", original.DeviceIsolation.KeepDevices[0].DeviceInstancePath);
    }

    [TestMethod]
    public void DeviceIsolationSettings_Clone_IsIndependent()
    {
        var settings = new DeviceIsolationSettings
        {
            Enabled = true,
            KeepDevices = new List<IsolationDevice>
            {
                new() { DeviceInstancePath = "path1", FriendlyName = "Device 1" }
            }
        };

        var clone = settings.Clone();
        clone.KeepDevices.Add(new IsolationDevice { DeviceInstancePath = "path2" });

        Assert.AreEqual(1, settings.KeepDevices.Count);
        Assert.AreEqual(2, clone.KeepDevices.Count);
    }

    [TestMethod]
    public void IsolationJournal_JsonRoundTrip_PreservesState()
    {
        var journal = new IsolationJournal
        {
            HiddenDevicePaths = new List<string>
            {
                @"HID\VID_1234&PID_BEAD\1&0",
                @"HID\VID_2F12&PID_0DS8\2&0"
            },
            PriorCloakOn = true,
            TimestampUtc = new DateTime(2026, 7, 22, 12, 0, 0, DateTimeKind.Utc)
        };

        string json = JsonSerializer.Serialize(journal);
        var restored = JsonSerializer.Deserialize<IsolationJournal>(json);

        Assert.IsNotNull(restored);
        CollectionAssert.AreEqual(journal.HiddenDevicePaths, restored.HiddenDevicePaths);
        Assert.IsTrue(restored.PriorCloakOn);
        Assert.AreEqual(journal.TimestampUtc, restored.TimestampUtc);
    }

    [TestMethod]
    public void MappingProfile_SchemaVersion_IsAtLeastV5()
    {
        // v5 introduced ProfileType + DeviceIsolation
        Assert.IsTrue(MappingProfileData.CurrentSchemaVersion >= 5);
    }
}
