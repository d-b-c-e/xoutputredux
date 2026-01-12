using System.Text.Json;
using System.Text.Json.Serialization;
using XOutputRenew.Core.ForceFeedback;
using XOutputRenew.Core.HidHide;

namespace XOutputRenew.Core.Mapping;

/// <summary>
/// A complete mapping profile containing all Xbox output mappings.
/// </summary>
public class MappingProfile
{
    /// <summary>
    /// Profile name for display.
    /// </summary>
    public string Name { get; set; } = "New Profile";

    /// <summary>
    /// Optional description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Profile creation date.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Last modified date.
    /// </summary>
    public DateTime ModifiedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Force feedback settings for this profile.
    /// </summary>
    public ForceFeedbackSettings? ForceFeedbackSettings { get; set; }

    /// <summary>
    /// HidHide settings for this profile.
    /// </summary>
    public HidHideSettings? HidHideSettings { get; set; }

    /// <summary>
    /// All output mappings indexed by Xbox output.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyDictionary<XboxOutput, OutputMapping> Mappings => _mappings;

    private readonly Dictionary<XboxOutput, OutputMapping> _mappings = new();

    /// <summary>
    /// Creates a new empty profile.
    /// </summary>
    public MappingProfile()
    {
        // Initialize all output mappings
        foreach (XboxOutput output in Enum.GetValues<XboxOutput>())
        {
            _mappings[output] = new OutputMapping(output);
        }
    }

    /// <summary>
    /// Gets or creates the mapping for a specific output.
    /// </summary>
    public OutputMapping GetMapping(XboxOutput output)
    {
        return _mappings[output];
    }

    /// <summary>
    /// Adds a binding to a specific output.
    /// </summary>
    public void AddBinding(XboxOutput output, InputBinding binding)
    {
        _mappings[output].AddBinding(binding);
        ModifiedAt = DateTime.Now;
    }

    /// <summary>
    /// Removes a binding from a specific output.
    /// </summary>
    public bool RemoveBinding(XboxOutput output, InputBinding binding)
    {
        bool removed = _mappings[output].RemoveBinding(binding);
        if (removed) ModifiedAt = DateTime.Now;
        return removed;
    }

    /// <summary>
    /// Clears all bindings from a specific output.
    /// </summary>
    public void ClearBindings(XboxOutput output)
    {
        _mappings[output].ClearBindings();
        ModifiedAt = DateTime.Now;
    }

    /// <summary>
    /// Clears all bindings from all outputs.
    /// </summary>
    public void ClearAllBindings()
    {
        foreach (var mapping in _mappings.Values)
        {
            mapping.ClearBindings();
        }
        ModifiedAt = DateTime.Now;
    }

    /// <summary>
    /// Creates a deep copy of this profile.
    /// </summary>
    public MappingProfile Clone()
    {
        var clone = new MappingProfile
        {
            Name = Name + " (Copy)",
            Description = Description,
            CreatedAt = DateTime.Now,
            ModifiedAt = DateTime.Now,
            ForceFeedbackSettings = ForceFeedbackSettings?.Clone(),
            HidHideSettings = HidHideSettings?.Clone()
        };

        foreach (var kvp in _mappings)
        {
            foreach (var binding in kvp.Value.Bindings)
            {
                clone.AddBinding(kvp.Key, new InputBinding
                {
                    DeviceId = binding.DeviceId,
                    SourceIndex = binding.SourceIndex,
                    DisplayName = binding.DisplayName,
                    Invert = binding.Invert,
                    MinValue = binding.MinValue,
                    MaxValue = binding.MaxValue,
                    ButtonThreshold = binding.ButtonThreshold
                });
            }
        }

        return clone;
    }

    /// <summary>
    /// Gets the total number of bindings in the profile.
    /// </summary>
    public int TotalBindings => _mappings.Values.Sum(m => m.Bindings.Count);

    /// <summary>
    /// Gets all device IDs referenced by this profile.
    /// </summary>
    public IEnumerable<string> GetReferencedDeviceIds()
    {
        return _mappings.Values
            .SelectMany(m => m.Bindings)
            .Select(b => b.DeviceId)
            .Distinct();
    }
}

/// <summary>
/// Serializable version of MappingProfile for JSON storage.
/// </summary>
public class MappingProfileData
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public List<OutputMappingData> Mappings { get; set; } = new();
    public ForceFeedbackSettingsData? ForceFeedback { get; set; }
    public HidHideSettingsData? HidHide { get; set; }

    /// <summary>
    /// Creates data from a profile.
    /// </summary>
    public static MappingProfileData FromProfile(MappingProfile profile)
    {
        var data = new MappingProfileData
        {
            Name = profile.Name,
            Description = profile.Description,
            CreatedAt = profile.CreatedAt,
            ModifiedAt = profile.ModifiedAt,
            ForceFeedback = profile.ForceFeedbackSettings != null
                ? ForceFeedbackSettingsData.FromSettings(profile.ForceFeedbackSettings)
                : null,
            HidHide = HidHideSettingsData.FromSettings(profile.HidHideSettings)
        };

        foreach (var kvp in profile.Mappings)
        {
            if (kvp.Value.Bindings.Count > 0)
            {
                data.Mappings.Add(OutputMappingData.FromMapping(kvp.Key, kvp.Value));
            }
        }

        return data;
    }

    /// <summary>
    /// Creates a profile from data.
    /// </summary>
    public MappingProfile ToProfile()
    {
        var profile = new MappingProfile
        {
            Name = Name,
            Description = Description,
            CreatedAt = CreatedAt,
            ModifiedAt = ModifiedAt,
            ForceFeedbackSettings = ForceFeedback?.ToSettings(),
            HidHideSettings = HidHide?.ToSettings()
        };

        foreach (var mappingData in Mappings)
        {
            foreach (var bindingData in mappingData.Bindings)
            {
                profile.AddBinding(mappingData.Output, bindingData.ToBinding());
            }
        }

        // Restore modified time (AddBinding updates it)
        profile.ModifiedAt = ModifiedAt;

        return profile;
    }
}

public class OutputMappingData
{
    public XboxOutput Output { get; set; }
    public List<InputBindingData> Bindings { get; set; } = new();

    public static OutputMappingData FromMapping(XboxOutput output, OutputMapping mapping)
    {
        return new OutputMappingData
        {
            Output = output,
            Bindings = mapping.Bindings.Select(InputBindingData.FromBinding).ToList()
        };
    }
}

public class InputBindingData
{
    public string DeviceId { get; set; } = "";
    public int SourceIndex { get; set; }
    public string? DisplayName { get; set; }
    public bool Invert { get; set; }
    public double MinValue { get; set; } = 0.0;
    public double MaxValue { get; set; } = 1.0;
    public double ButtonThreshold { get; set; } = 0.5;

    public static InputBindingData FromBinding(InputBinding binding)
    {
        return new InputBindingData
        {
            DeviceId = binding.DeviceId,
            SourceIndex = binding.SourceIndex,
            DisplayName = binding.DisplayName,
            Invert = binding.Invert,
            MinValue = binding.MinValue,
            MaxValue = binding.MaxValue,
            ButtonThreshold = binding.ButtonThreshold
        };
    }

    public InputBinding ToBinding()
    {
        return new InputBinding
        {
            DeviceId = DeviceId,
            SourceIndex = SourceIndex,
            DisplayName = DisplayName,
            Invert = Invert,
            MinValue = MinValue,
            MaxValue = MaxValue,
            ButtonThreshold = ButtonThreshold
        };
    }
}
