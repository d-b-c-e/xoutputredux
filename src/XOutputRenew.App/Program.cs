using System.CommandLine;
using System.Text.Json;
using XOutputRenew.Core.Mapping;
using XOutputRenew.Input;

namespace XOutputRenew.App;

/// <summary>
/// Application entry point with CLI support.
/// </summary>
public class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    [STAThread]
    public static int Main(string[] args)
    {
        // If no args, launch GUI directly
        if (args.Length == 0)
        {
            return LaunchGui(null, false);
        }

        // Build CLI
        var rootCommand = BuildRootCommand();
        return rootCommand.Invoke(args);
    }

    private static RootCommand BuildRootCommand()
    {
        var rootCommand = new RootCommand("XOutputRenew - Xbox Controller Emulator");

        // Options for run command
        var startProfileOption = new Option<string?>(
            name: "--start-profile",
            description: "Start emulation with this profile on launch");

        var minimizedOption = new Option<bool>(
            name: "--minimized",
            description: "Start minimized to system tray");

        // Add run command (default)
        var runCommand = new Command("run", "Start the application (default, opens GUI)");
        runCommand.AddOption(startProfileOption);
        runCommand.AddOption(minimizedOption);
        runCommand.SetHandler((startProfile, minimized) =>
        {
            LaunchGui(startProfile, minimized);
        }, startProfileOption, minimizedOption);
        rootCommand.AddCommand(runCommand);

        // List devices command
        var listDevicesCommand = new Command("list-devices", "List detected input devices");
        var jsonOption = new Option<bool>("--json", "Output as JSON");
        listDevicesCommand.AddOption(jsonOption);
        listDevicesCommand.SetHandler((json) =>
        {
            ListDevices(json);
        }, jsonOption);
        rootCommand.AddCommand(listDevicesCommand);

        // List profiles command
        var listProfilesCommand = new Command("list-profiles", "List available profiles");
        listProfilesCommand.AddOption(jsonOption);
        listProfilesCommand.SetHandler((json) =>
        {
            ListProfiles(json);
        }, jsonOption);
        rootCommand.AddCommand(listProfilesCommand);

        // Duplicate profile command
        var duplicateCommand = new Command("duplicate-profile", "Duplicate an existing profile");
        var sourceArg = new Argument<string>("source", "Name of the profile to duplicate");
        var newNameArg = new Argument<string>("new-name", "Name for the new profile");
        duplicateCommand.AddArgument(sourceArg);
        duplicateCommand.AddArgument(newNameArg);
        duplicateCommand.SetHandler((source, newName) =>
        {
            DuplicateProfile(source, newName);
        }, sourceArg, newNameArg);
        rootCommand.AddCommand(duplicateCommand);

        // Add global options to root for running without 'run' subcommand
        rootCommand.AddOption(startProfileOption);
        rootCommand.AddOption(minimizedOption);
        rootCommand.SetHandler((startProfile, minimized) =>
        {
            LaunchGui(startProfile, minimized);
        }, startProfileOption, minimizedOption);

        return rootCommand;
    }

    private static int LaunchGui(string? startProfile, bool minimized)
    {
        var app = new App();

        // Store startup options for the app to use
        app.Properties["StartProfile"] = startProfile;
        app.Properties["Minimized"] = minimized;

        return app.Run();
    }

    private static void ListDevices(bool asJson)
    {
        using var manager = new InputDeviceManager();

        // Wait a moment for device discovery
        Thread.Sleep(500);
        manager.RefreshDevices();

        var devices = manager.Devices;

        if (asJson)
        {
            var output = devices.Select(d => new
            {
                uniqueId = d.UniqueId,
                name = d.Name,
                method = d.Method.ToString(),
                hardwareId = d.HardwareId,
                interfacePath = d.InterfacePath,
                sources = d.Sources.Select(s => new
                {
                    index = s.Index,
                    name = s.Name,
                    type = s.Type.ToString()
                }).ToArray()
            }).ToArray();

            Console.WriteLine(JsonSerializer.Serialize(output, JsonOptions));
        }
        else
        {
            if (!devices.Any())
            {
                Console.WriteLine("No input devices detected.");
                return;
            }

            Console.WriteLine($"Detected {devices.Count} input device(s):\n");

            foreach (var device in devices)
            {
                Console.WriteLine($"  {device.Name}");
                Console.WriteLine($"    ID:       {device.UniqueId}");
                Console.WriteLine($"    Method:   {device.Method}");
                if (device.HardwareId != null)
                    Console.WriteLine($"    Hardware: {device.HardwareId}");
                Console.WriteLine($"    Sources:  {device.Sources.Count} ({CountByType(device.Sources)})");
                Console.WriteLine();
            }
        }
    }

    private static string CountByType(IReadOnlyList<IInputSource> sources)
    {
        var buttons = sources.Count(s => s.Type == InputSourceType.Button);
        var axes = sources.Count(s => s.Type == InputSourceType.Axis);
        var sliders = sources.Count(s => s.Type == InputSourceType.Slider);
        var dpads = sources.Count(s => s.Type == InputSourceType.DPad);

        var parts = new List<string>();
        if (buttons > 0) parts.Add($"{buttons} buttons");
        if (axes > 0) parts.Add($"{axes} axes");
        if (sliders > 0) parts.Add($"{sliders} sliders");
        if (dpads > 0) parts.Add($"{dpads} dpad");

        return string.Join(", ", parts);
    }

    private static void ListProfiles(bool asJson)
    {
        var profilesDir = ProfileManager.GetDefaultProfilesDirectory();
        var manager = new ProfileManager(profilesDir);
        manager.LoadProfiles();

        var profiles = manager.Profiles;

        if (asJson)
        {
            var output = profiles.Select(kvp => new
            {
                name = kvp.Value.Name,
                description = kvp.Value.Description,
                createdAt = kvp.Value.CreatedAt,
                modifiedAt = kvp.Value.ModifiedAt,
                totalBindings = kvp.Value.TotalBindings,
                devices = kvp.Value.GetReferencedDeviceIds().ToArray()
            }).ToArray();

            Console.WriteLine(JsonSerializer.Serialize(output, JsonOptions));
        }
        else
        {
            if (!profiles.Any())
            {
                Console.WriteLine("No profiles found.");
                Console.WriteLine($"Profiles directory: {profilesDir}");
                return;
            }

            Console.WriteLine($"Found {profiles.Count} profile(s):\n");

            foreach (var kvp in profiles)
            {
                var profile = kvp.Value;
                Console.WriteLine($"  {profile.Name}");
                if (!string.IsNullOrEmpty(profile.Description))
                    Console.WriteLine($"    {profile.Description}");
                Console.WriteLine($"    Bindings: {profile.TotalBindings}");
                Console.WriteLine($"    Modified: {profile.ModifiedAt:g}");
                Console.WriteLine();
            }
        }
    }

    private static void DuplicateProfile(string source, string newName)
    {
        var profilesDir = ProfileManager.GetDefaultProfilesDirectory();
        var manager = new ProfileManager(profilesDir);
        manager.LoadProfiles();

        var duplicate = manager.DuplicateProfile(source, newName);
        if (duplicate != null)
        {
            Console.WriteLine($"Created profile '{newName}' from '{source}'");
        }
        else
        {
            Console.Error.WriteLine($"Error: Profile '{source}' not found");
            Environment.ExitCode = 1;
        }
    }
}
