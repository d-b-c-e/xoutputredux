using System.CommandLine;
using System.Runtime.InteropServices;
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

    // Exit codes
    public const int ExitSuccess = 0;
    public const int ExitError = 1;
    public const int ExitProfileNotFound = 2;
    public const int ExitNoRunningInstance = 3;

    // Console window hiding for GUI mode (Exe apps have a console by default)
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_HIDE = 0;

    [STAThread]
    public static int Main(string[] args)
    {
        // If no args, launch GUI directly (hide console window first)
        if (args.Length == 0)
        {
            HideConsoleWindow();
            return LaunchGui(null, false);
        }

        // Build and run CLI
        var rootCommand = BuildRootCommand();
        return rootCommand.Invoke(args);
    }

    private static void HideConsoleWindow()
    {
        var handle = GetConsoleWindow();
        if (handle != IntPtr.Zero)
        {
            ShowWindow(handle, SW_HIDE);
        }
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

        // Set default profile command
        var setDefaultCommand = new Command("set-default", "Set a profile as the default for 'start' command");
        var defaultProfileArg = new Argument<string>("profile", "Name of the profile to set as default");
        setDefaultCommand.AddArgument(defaultProfileArg);
        setDefaultCommand.SetHandler((profile) =>
        {
            Environment.ExitCode = SetDefaultProfile(profile);
        }, defaultProfileArg);
        rootCommand.AddCommand(setDefaultCommand);

        // Remote control commands (sent to running instance)
        var startCommand = new Command("start", "Start a profile (uses default if no name specified)");
        var profileArg = new Argument<string?>("profile", () => null, "Name of the profile to start (optional, uses default)");
        startCommand.AddArgument(profileArg);
        startCommand.SetHandler((profile) =>
        {
            Environment.ExitCode = SendStartCommand(profile);
        }, profileArg);
        rootCommand.AddCommand(startCommand);

        var stopCommand = new Command("stop", "Stop the running profile");
        stopCommand.SetHandler(() =>
        {
            Environment.ExitCode = SendStopCommand();
        });
        rootCommand.AddCommand(stopCommand);

        var statusCommand = new Command("status", "Get status from the running instance");
        statusCommand.AddOption(jsonOption);
        statusCommand.SetHandler((json) =>
        {
            Environment.ExitCode = GetStatus(json);
        }, jsonOption);
        rootCommand.AddCommand(statusCommand);

        // Help command with examples
        var helpCommand = new Command("help", "Show detailed help and examples");
        helpCommand.SetHandler(() =>
        {
            ShowDetailedHelp();
        });
        rootCommand.AddCommand(helpCommand);

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

    private static int SetDefaultProfile(string profileName)
    {
        var profilesDir = ProfileManager.GetDefaultProfilesDirectory();
        var manager = new ProfileManager(profilesDir);
        manager.LoadProfiles();

        var profile = manager.GetProfile(profileName);
        if (profile == null)
        {
            // Try case-insensitive match
            var match = manager.Profiles.FirstOrDefault(p =>
                p.Key.Equals(profileName, StringComparison.OrdinalIgnoreCase) ||
                p.Value.Name.Equals(profileName, StringComparison.OrdinalIgnoreCase));

            if (match.Value == null)
            {
                Console.Error.WriteLine($"Error: Profile '{profileName}' not found.");
                Console.Error.WriteLine("Available profiles:");
                foreach (var p in manager.Profiles.Values)
                {
                    Console.Error.WriteLine($"  {p.Name}");
                }
                return ExitProfileNotFound;
            }
            profileName = match.Key;
        }

        manager.SetDefaultProfile(profileName);
        Console.WriteLine($"Set '{profileName}' as the default profile.");
        return ExitSuccess;
    }

    private static int SendStartCommand(string? profileName)
    {
        // If no profile specified, look for default
        if (string.IsNullOrEmpty(profileName))
        {
            var profilesDir = ProfileManager.GetDefaultProfilesDirectory();
            var manager = new ProfileManager(profilesDir);
            manager.LoadProfiles();

            var defaultProfile = manager.GetDefaultProfile();
            if (defaultProfile == null)
            {
                Console.Error.WriteLine("Error: No profile specified and no default profile is set.");
                Console.Error.WriteLine("Either specify a profile name: XOutputRenew start \"ProfileName\"");
                Console.Error.WriteLine("Or set a default profile in the Profile Editor.");
                return ExitError;
            }

            profileName = defaultProfile.Name;
            Console.WriteLine($"Using default profile: {profileName}");
        }

        if (!IpcService.IsAnotherInstanceRunning())
        {
            // No running instance - launch GUI with this profile
            Console.WriteLine($"Launching XOutputRenew with profile: {profileName}");
            HideConsoleWindow();
            return LaunchGui(profileName, false);
        }

        var result = IpcService.SendStartCommand(profileName);
        if (result.Success)
        {
            Console.WriteLine(result.Message);
            return ExitSuccess;
        }
        else
        {
            Console.Error.WriteLine($"Error: {result.Message}");
            return ExitError;
        }
    }

    private static int SendStopCommand()
    {
        if (!IpcService.IsAnotherInstanceRunning())
        {
            Console.Error.WriteLine("Error: No running instance of XOutputRenew found.");
            return ExitNoRunningInstance;
        }

        var result = IpcService.SendStopCommand();
        if (result.Success)
        {
            Console.WriteLine(result.Message);
            return ExitSuccess;
        }
        else
        {
            Console.Error.WriteLine($"Error: {result.Message}");
            return ExitError;
        }
    }

    private static int GetStatus(bool asJson)
    {
        if (!IpcService.IsAnotherInstanceRunning())
        {
            if (asJson)
            {
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    running = false,
                    message = "No running instance"
                }, JsonOptions));
            }
            else
            {
                Console.WriteLine("XOutputRenew is not running.");
            }
            return ExitNoRunningInstance;
        }

        var result = IpcService.SendStatusCommand();
        if (!result.Success)
        {
            Console.Error.WriteLine($"Error: {result.Message}");
            return ExitError;
        }

        if (asJson)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                running = true,
                profileActive = result.Status?.IsRunning ?? false,
                profileName = result.Status?.ProfileName,
                vigem = result.Status?.ViGEmStatus,
                hidhide = result.Status?.HidHideStatus
            }, JsonOptions));
        }
        else
        {
            Console.WriteLine("XOutputRenew Status:");
            Console.WriteLine($"  Running: Yes");
            if (result.Status?.IsRunning == true)
            {
                Console.WriteLine($"  Active Profile: {result.Status.ProfileName}");
            }
            else
            {
                Console.WriteLine($"  Active Profile: None");
            }
            Console.WriteLine($"  ViGEm: {result.Status?.ViGEmStatus}");
            Console.WriteLine($"  HidHide: {result.Status?.HidHideStatus}");
        }

        return ExitSuccess;
    }

    private static void ShowDetailedHelp()
    {
        Console.WriteLine(@"XOutputRenew - Xbox Controller Emulator
========================================

Maps inputs from gaming devices (wheels, joysticks, gamepads) to an emulated
Xbox 360 controller using ViGEm.

USAGE:
  XOutputRenew [command] [options]

COMMANDS:
  (no command)              Launch the GUI application
  run                       Launch the GUI (same as no command)
  list-devices [--json]     List detected input devices
  list-profiles [--json]    List available profiles
  set-default <profile>     Set a profile as the default
  start [profile]           Start a profile (uses default if not specified)
  stop                      Stop the running profile
  status [--json]           Get status from the running instance
  help                      Show this help

STARTUP OPTIONS:
  --start-profile <name>    Start with a profile already running
  --minimized               Start minimized to system tray

EXAMPLES:
  # Set a default profile
  XOutputRenew set-default ""My Wheel""

  # Start the default profile
  XOutputRenew start

  # Start a specific profile
  XOutputRenew start ""My Wheel""

  # Launch minimized with a profile
  XOutputRenew --start-profile ""My Wheel"" --minimized

  # Control a running instance
  XOutputRenew stop
  XOutputRenew status

  # List profiles as JSON (for scripting)
  XOutputRenew list-profiles --json

EXIT CODES:
  0  Success
  1  Error
  2  Profile not found
  3  No running instance (for remote commands)

For more information, visit: https://github.com/your-repo/xoutputrenew
");
    }
}
