using System.Text.Json;
using XOutputRedux.Core.Games;
using XOutputRedux.Core.Mapping;

namespace XOutputRedux.Tests;

[TestClass]
public class SchemaMigrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    #region MappingProfileData Tests

    [TestMethod]
    public void MappingProfileData_NewProfile_HasCurrentSchemaVersion()
    {
        var data = new MappingProfileData { Name = "Test" };

        Assert.AreEqual(MappingProfileData.CurrentSchemaVersion, data.SchemaVersion);
        Assert.IsFalse(data.NeedsMigration);
    }

    [TestMethod]
    public void MappingProfileData_LegacyVersion_NeedsMigration()
    {
        var data = new MappingProfileData { Name = "Test", SchemaVersion = 0 };

        Assert.IsTrue(data.NeedsMigration);
    }

    [TestMethod]
    public void MappingProfileData_Migrate_UpdatesToCurrentVersion()
    {
        var data = new MappingProfileData { Name = "Test", SchemaVersion = 0 };

        data.Migrate();

        Assert.AreEqual(MappingProfileData.CurrentSchemaVersion, data.SchemaVersion);
        Assert.IsFalse(data.NeedsMigration);
    }

    [TestMethod]
    public void MappingProfileData_Migrate_PreservesData()
    {
        var data = new MappingProfileData
        {
            Name = "Test Profile",
            Description = "Test Description",
            SchemaVersion = 0,
            Mappings = new List<OutputMappingData>
            {
                new OutputMappingData
                {
                    Output = XboxOutput.A,
                    Bindings = new List<InputBindingData>
                    {
                        new InputBindingData { DeviceId = "dev1", SourceIndex = 0 }
                    }
                }
            }
        };

        data.Migrate();

        Assert.AreEqual("Test Profile", data.Name);
        Assert.AreEqual("Test Description", data.Description);
        Assert.AreEqual(1, data.Mappings.Count);
        Assert.AreEqual(XboxOutput.A, data.Mappings[0].Output);
        Assert.AreEqual("dev1", data.Mappings[0].Bindings[0].DeviceId);
    }

    [TestMethod]
    public void MappingProfileData_SerializeDeserialize_PreservesSchemaVersion()
    {
        var original = new MappingProfileData
        {
            Name = "Test",
            SchemaVersion = MappingProfileData.CurrentSchemaVersion
        };

        string json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<MappingProfileData>(json, JsonOptions);

        Assert.IsNotNull(deserialized);
        Assert.AreEqual(original.SchemaVersion, deserialized.SchemaVersion);
        Assert.IsFalse(deserialized.NeedsMigration);
    }

    [TestMethod]
    public void MappingProfileData_LegacyJsonWithoutSchemaVersion_DetectedByStringCheck()
    {
        // Simulate legacy JSON that doesn't have schemaVersion field
        string legacyJson = """
            {
                "name": "Legacy Profile",
                "description": "From old version",
                "mappings": []
            }
            """;

        var data = JsonSerializer.Deserialize<MappingProfileData>(legacyJson, JsonOptions);

        Assert.IsNotNull(data);
        Assert.AreEqual("Legacy Profile", data.Name);

        // Detection of legacy format is done by checking if JSON contains "schemaVersion"
        // This is how ProfileManager.LoadProfile() and other loaders detect legacy format
        bool isLegacyFormat = !legacyJson.Contains("schemaVersion", StringComparison.OrdinalIgnoreCase);
        Assert.IsTrue(isLegacyFormat);

        // If legacy format detected, the loader would set SchemaVersion = 0 and call Migrate()
        // We simulate that here:
        if (isLegacyFormat)
        {
            data.SchemaVersion = 0;
        }
        Assert.IsTrue(data.NeedsMigration);
    }

    #endregion

    #region GamesData Tests

    [TestMethod]
    public void GamesData_NewData_HasCurrentSchemaVersion()
    {
        var data = new GamesData();

        Assert.AreEqual(GamesData.CurrentSchemaVersion, data.SchemaVersion);
        Assert.IsFalse(data.NeedsMigration);
    }

    [TestMethod]
    public void GamesData_LegacyVersion_NeedsMigration()
    {
        var data = new GamesData { SchemaVersion = 0 };

        Assert.IsTrue(data.NeedsMigration);
    }

    [TestMethod]
    public void GamesData_Migrate_UpdatesToCurrentVersion()
    {
        var data = new GamesData { SchemaVersion = 0 };

        data.Migrate();

        Assert.AreEqual(GamesData.CurrentSchemaVersion, data.SchemaVersion);
        Assert.IsFalse(data.NeedsMigration);
    }

    [TestMethod]
    public void GamesData_Migrate_PreservesGames()
    {
        var data = new GamesData
        {
            SchemaVersion = 0,
            Games = new List<GameAssociation>
            {
                new GameAssociation
                {
                    Id = "game1",
                    Name = "Test Game",
                    ExecutablePath = @"C:\Games\test.exe",
                    ProfileName = "TestProfile"
                }
            }
        };

        data.Migrate();

        Assert.AreEqual(1, data.Games.Count);
        Assert.AreEqual("game1", data.Games[0].Id);
        Assert.AreEqual("Test Game", data.Games[0].Name);
    }

    [TestMethod]
    public void GamesData_LegacyListFormat_CanBeParsedAsNewFormat()
    {
        // Legacy format was a raw list, new format is wrapped in GamesData
        // The GameAssociationManager.Load() handles this, but we test the detection logic
        string legacyListJson = """
            [
                {
                    "id": "game1",
                    "name": "Test Game",
                    "executablePath": "C:\\Games\\test.exe",
                    "profileName": "Profile1"
                }
            ]
            """;

        // Trying to parse as GamesData should fail or return invalid data
        // (no schemaVersion property in JSON means it's legacy)
        Assert.IsFalse(legacyListJson.Contains("schemaVersion", StringComparison.OrdinalIgnoreCase));

        // The legacy list can be parsed directly
        var legacyGames = JsonSerializer.Deserialize<List<GameAssociation>>(legacyListJson, JsonOptions);
        Assert.IsNotNull(legacyGames);
        Assert.AreEqual(1, legacyGames.Count);
        Assert.AreEqual("game1", legacyGames[0].Id);
    }

    [TestMethod]
    public void GamesData_NewFormat_ContainsSchemaVersion()
    {
        var data = new GamesData
        {
            Games = new List<GameAssociation>
            {
                new GameAssociation { Id = "game1", Name = "Test" }
            }
        };

        string json = JsonSerializer.Serialize(data, JsonOptions);

        Assert.IsTrue(json.Contains("schemaVersion", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void MappingProfileData_V1_DeserializesWithDefaultSensitivity()
    {
        // Simulate a v1 profile JSON (no sensitivity field)
        // XboxOutput.LeftStickX = 15 (enums serialize as integers)
        string v1Json = """
            {
                "schemaVersion": 1,
                "name": "V1 Profile",
                "mappings": [
                    {
                        "output": 15,
                        "bindings": [
                            {
                                "deviceId": "dev1",
                                "sourceIndex": 0,
                                "displayName": "Steering",
                                "invert": false,
                                "minValue": 0.0,
                                "maxValue": 1.0,
                                "buttonThreshold": 0.5
                            }
                        ]
                    }
                ]
            }
            """;

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var data = JsonSerializer.Deserialize<MappingProfileData>(v1Json, options);

        Assert.IsNotNull(data);
        Assert.IsTrue(data.NeedsMigration);

        data.Migrate();

        Assert.AreEqual(MappingProfileData.CurrentSchemaVersion, data.SchemaVersion);

        // Sensitivity should default to 1.0 (linear, same as pre-tuning behavior)
        var binding = data.Mappings[0].Bindings[0];
        Assert.AreEqual(1.0, binding.Sensitivity, 0.001);
    }

    #endregion

    #region MappingProfile Round-Trip Tests

    [TestMethod]
    public void MappingProfile_RoundTrip_PreservesAllData()
    {
        // Create a profile with various settings
        var original = new MappingProfile
        {
            Name = "Test Profile",
            Description = "Test Description",
            IsDefault = true
        };
        original.AddBinding(XboxOutput.A, new InputBinding
        {
            DeviceId = "dev1",
            SourceIndex = 0,
            DisplayName = "Button 1",
            Invert = true,
            MinValue = 0.1,
            MaxValue = 0.9,
            ButtonThreshold = 0.6
        });
        original.AddBinding(XboxOutput.LeftStickX, new InputBinding
        {
            DeviceId = "dev2",
            SourceIndex = 1,
            DisplayName = "Steering"
        });

        // Convert to data and back
        var data = MappingProfileData.FromProfile(original);
        var restored = data.ToProfile();

        // Verify all data preserved
        Assert.AreEqual(original.Name, restored.Name);
        Assert.AreEqual(original.Description, restored.Description);
        Assert.AreEqual(original.IsDefault, restored.IsDefault);
        Assert.AreEqual(original.TotalBindings, restored.TotalBindings);

        // Check specific binding
        var aBindings = restored.GetMapping(XboxOutput.A).Bindings;
        Assert.AreEqual(1, aBindings.Count);
        Assert.AreEqual("dev1", aBindings[0].DeviceId);
        Assert.AreEqual(0, aBindings[0].SourceIndex);
        Assert.IsTrue(aBindings[0].Invert);
        Assert.AreEqual(0.1, aBindings[0].MinValue, 0.001);
        Assert.AreEqual(0.9, aBindings[0].MaxValue, 0.001);
        Assert.AreEqual(0.6, aBindings[0].ButtonThreshold, 0.001);
    }

    [TestMethod]
    public void MappingProfileData_FromProfile_SetsCurrentSchemaVersion()
    {
        var profile = new MappingProfile { Name = "Test" };

        var data = MappingProfileData.FromProfile(profile);

        Assert.AreEqual(MappingProfileData.CurrentSchemaVersion, data.SchemaVersion);
    }

    #endregion

    #region Schema Version Constant Tests

    [TestMethod]
    public void AllSchemaVersions_ArePositive()
    {
        Assert.IsTrue(MappingProfileData.CurrentSchemaVersion >= 1);
        Assert.IsTrue(GamesData.CurrentSchemaVersion >= 1);
    }

    [TestMethod]
    public void SchemaVersion_ZeroAlwaysNeedsMigration()
    {
        // Version 0 represents "no version" (legacy files)
        var profileData = new MappingProfileData { SchemaVersion = 0 };
        var gamesData = new GamesData { SchemaVersion = 0 };

        Assert.IsTrue(profileData.NeedsMigration);
        Assert.IsTrue(gamesData.NeedsMigration);
    }

    #endregion
}
