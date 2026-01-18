# XOutputRenew

Streamlined Xbox controller emulator for Windows. Maps inputs from multiple gaming devices (steering wheels, joysticks, gamepads) to a single emulated Xbox 360 controller.

## Project Goals

1. **Multi-input to single output** - Map multiple physical buttons to same Xbox button (e.g., wheel B button + handbrake → Xbox B)
2. **CLI integration** - Full command-line control for gaming frontends and Stream Deck
3. **Profile management** - Create, duplicate, and switch profiles easily
4. **Device hiding** - HidHide integration to prevent game confusion from seeing both real and emulated controllers
5. **Simple GUI** - Interactive "press to map" configuration interface

## Working Preferences

- **Ask before implementing**: When soundboarding or discussing possible features, ask for explicit verification before proceeding with implementation. Explore ideas freely in discussion without jumping to code changes.

## Architecture

### Project Structure
```
XOutputRenew/
├── XOutputRenew.sln
├── src/
│   ├── XOutputRenew.Core/           # Core abstractions, config, interfaces
│   ├── XOutputRenew.Input/          # DirectInput, RawInput device handling
│   ├── XOutputRenew.Emulation/      # ViGEm Xbox controller emulation
│   ├── XOutputRenew.HidHide/        # HidHide integration
│   └── XOutputRenew.App/            # WPF GUI + CLI application
└── tests/
    └── XOutputRenew.Tests/
```

### Key Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| Nefarius.ViGEm.Client | 1.21.256 | Virtual Xbox controller |
| Vortice.DirectInput | 3.8.2 | DirectInput device access |
| HidSharp | 2.1.0 | RawInput/HID device access |
| System.CommandLine | 2.0.0-beta4 | CLI parsing |
| Hardcodet.NotifyIcon.Wpf | 1.1.0 | System tray |

### External Projects (may fork if needed)
- **ViGEmBus**: https://github.com/nefarius/ViGEmBus - Virtual gamepad driver
- **HidHide**: https://github.com/nefarius/HidHide - Device hiding driver

---

## Reference: XOutput Analysis

XOutputRenew is based on principles from the archived XOutput project. Key code to adapt:

### Extract and Adapt
- DirectInput device polling (SharpDX) - `XOutput.App/Devices/Input/DirectInput/`
- RawInput device handling (HidSharp) - `XOutput.App/Devices/Input/RawInput/`
- ViGEm integration - `XOutput.Emulation/ViGEm/`
- Device identification (UniqueId) - `XOutput.App/Devices/Input/IdHelper.cs`
- Value normalization/deadzone - `XOutput.App/Devices/Input/InputSource.cs`

### Do Not Use
- Web server, REST API, WebSockets (XOutput.Server, XOutput.Client)
- DS4 controller support
- SCPToolkit emulation
- HidGuardian (deprecated, use HidHide instead)
- Existing mapping system (redesign for OR logic)

---

## Design Decisions

| Aspect | Decision | Rationale |
|--------|----------|-----------|
| Framework | .NET 8.0 Windows | LTS, WPF support |
| Output | Xbox 360 only | Simplicity, universal game support |
| Emulation | ViGEm only | Current maintained solution |
| Device hiding | HidHide | Replaces deprecated HidGuardian |
| Mapping | OR logic | Any mapped input triggers output |
| Config | JSON | Human-readable, easy to edit |
| IPC | Named pipes | Stream Deck/frontend integration |
| GUI | WPF | Required for interactive mapping |

---

## Implementation Phases

| Phase | Status | Summary |
|-------|--------|---------|
| 1: Foundation | ✓ Complete | DirectInput/RawInput/ViGEm ported, OR-logic mapping, unit tests |
| 2: Profile System | ✓ Complete | JSON profiles, ProfileManager, CLI commands |
| 3: GUI - Core | ✓ Complete | WPF app, system tray, device/profile views, status tab |
| 4: Interactive Mapping | ✓ Complete | Press-to-map capture, multi-binding UI, binding settings |
| 4.5: Device Enhancements | ✓ Complete | Device deduplication, input highlighting, friendly names |
| 4.6: Profile & Options | ✓ Complete | Context menus, options tab, app settings persistence |
| 4.7: Force Feedback | ✓ Complete | FFB routing from ViGEm to physical devices |
| 5: HidHide Integration | ✓ Complete | Auto-hide/unhide, whitelist management, auto-install |
| 5.5: UX Improvements | ✓ Complete | VID/PID identification, read-only view, UI polish |
| 6: CLI & IPC | ✓ Complete | Named pipes, start/stop/status commands, toast notifications |
| 7: Game Auto-Profile | ✓ Complete | Process monitoring, Steam integration, auto-start profiles |
| 8: Headless Mode | ✗ Removed | Replaced by IPC - Stream Deck uses direct pipe communication |
| 9: Update Checker | ✓ Complete | GitHub releases, semantic versioning, in-app download |
| 10: Crash Reporting | ✓ Complete | GitHub issue URL with exception details, crash dialog |
| 11: Chocolatey Package | Post-1.0 | Package distribution after stable release |
| 12: ViGEmBus Check | ✓ Complete | Driver detection, auto-install prompt |
| 13: Stream Deck Plugin | ✓ Complete | Native C# plugin with IPC, profile/monitor toggles |
| 14: Backup/Restore Settings | ✓ Complete | Export/import to `.xorbackup` file (ZIP containing all settings, profiles, games.json) |
| 15: Portable Mode | ✓ Complete | True portable support - detect `portable.txt` and store settings in `data\` subfolder |
| 16: Code Signing | Planned | Sign installer/exe to avoid Windows Defender/SmartScreen warnings |
| 17: Rebrand to XOutput Redux | Planned | New name, icons, and color themes |

### Completed Dependency Upgrades

| Item | Status | Notes |
|------|--------|-------|
| **Migrate SharpDX → Vortice.DirectInput** | ✓ Complete | Migrated 2026-01-18. Vortice 3.8.2 actively maintained. |

### Future Roadmap (Late 2026)

| Item | Rationale | Effort |
|------|-----------|--------|
| **Upgrade to .NET 10** | .NET 8 LTS ends Nov 2026; .NET 10 is next LTS | ~1 day |
| **Fork/slim HidSharp** | HidSharp abandoned (2020); slim to ~500 lines for our needs | ~2-3 days |

*Note: These are maintenance/future-proofing upgrades, not performance improvements. See [CLAUDE_DEPENDENCY_ANALYSIS.md](CLAUDE_DEPENDENCY_ANALYSIS.md) for detailed analysis.*

---

## CLI Interface

```
XOutputRenew - Xbox Controller Emulator

Usage: XOutputRenew [command] [options]

Commands:
  (no command)            Launch the GUI application
  run                     Launch the GUI (same as no command)
  headless [profile] [--monitor]  Run without GUI
  list-devices [--json]   List detected input devices
  list-profiles [--json]  List available profiles
  start [profile]         Start a profile (uses default if not specified)
  stop                    Stop the running profile
  monitor on              Enable game monitoring
  monitor off             Disable game monitoring
  status [--json]         Get status from running instance
  help                    Show detailed help

Startup Options:
  --start-profile <name>  Start with a profile already running
  --minimized             Start minimized to system tray

Exit Codes:
  0  Success
  1  Error
  2  Profile not found
  3  No running instance (for remote commands)

Examples:
  XOutputRenew                                    # Open GUI
  XOutputRenew headless                           # Run headless with default profile
  XOutputRenew headless "My Wheel"                # Run headless with specific profile
  XOutputRenew headless --monitor                 # Run headless with game monitoring only
  XOutputRenew headless "My Wheel" --monitor      # Run headless with profile + monitoring
  XOutputRenew start                              # Start default profile
  XOutputRenew start "My Wheel"                   # Start specific profile
  XOutputRenew --start-profile "My Wheel" --minimized
  XOutputRenew stop                               # Stop running profile
  XOutputRenew status --json                      # Get status as JSON
```

---

## Profile Format

```json
{
  "name": "MozaWheel1",
  "description": "Moza R9 wheel with handbrake",
  "inputDevices": [
    {
      "uniqueId": "a1b2c3d4e5f6...",
      "friendlyName": "MOZA R9",
      "inputMethod": "DirectInput"
    },
    {
      "uniqueId": "f6e5d4c3b2a1...",
      "friendlyName": "USB Handbrake",
      "inputMethod": "DirectInput"
    }
  ],
  "buttonMappings": {
    "A": [
      { "deviceId": "a1b2c3d4e5f6...", "sourceIndex": 0, "sourceName": "Button 1" }
    ],
    "B": [
      { "deviceId": "a1b2c3d4e5f6...", "sourceIndex": 1, "sourceName": "Button 2" },
      { "deviceId": "f6e5d4c3b2a1...", "sourceIndex": 0, "sourceName": "Handbrake" }
    ],
    "X": [
      { "deviceId": "a1b2c3d4e5f6...", "sourceIndex": 2, "sourceName": "Button 3" }
    ]
  },
  "axisMappings": {
    "LeftStickX": {
      "deviceId": "a1b2c3d4e5f6...",
      "sourceIndex": 0,
      "sourceName": "Steering",
      "invert": false,
      "deadzone": 0.02
    },
    "LeftTrigger": {
      "deviceId": "a1b2c3d4e5f6...",
      "sourceIndex": 2,
      "sourceName": "Brake",
      "invert": false,
      "deadzone": 0.0
    },
    "RightTrigger": {
      "deviceId": "a1b2c3d4e5f6...",
      "sourceIndex": 3,
      "sourceName": "Throttle",
      "invert": false,
      "deadzone": 0.0
    }
  },
  "hidHide": {
    "enabled": true,
    "deviceIds": [
      "HID\\VID_346E&PID_0006",
      "HID\\VID_1234&PID_5678"
    ]
  }
}
```

---

## Xbox Controller Outputs

### Buttons
`A`, `B`, `X`, `Y`, `LeftBumper`, `RightBumper`, `Back`, `Start`, `Guide`, `LeftStickPress`, `RightStickPress`, `DPadUp`, `DPadDown`, `DPadLeft`, `DPadRight`

### Axes (0.0 - 1.0, center at 0.5 for sticks)
`LeftStickX`, `LeftStickY`, `RightStickX`, `RightStickY`

### Triggers (0.0 - 1.0)
`LeftTrigger`, `RightTrigger`

---

## Build & Run

```powershell
# Build
dotnet build -c Release

# Run
dotnet run --project src/XOutputRenew.App

# Run tests
dotnet test
```

## Requirements

- Windows 10/11
- .NET 8.0 Runtime
- ViGEmBus driver installed (https://github.com/nefarius/ViGEmBus/releases)
- HidHide driver installed (optional, for device hiding) (https://github.com/nefarius/HidHide/releases)

---

## External Dependencies - Technical Analysis

### ViGEmBus (Virtual Gamepad Emulation Bus)

**Status**: RETIRED (September 2023) - but still functional

**What Happened**: Trademark conflict with ViGEM GmbH led to project retirement. No active successor yet (VirtualPad in development but not ready).

**Key Points**:
- Last stable driver: v1.22.0 (November 2023)
- Last .NET client: Nefarius.ViGEm.Client v1.21.256
- Still works on Windows 10/11, just no new development
- XOutput's implementation pattern is correct and proven

**Usage Pattern** (from XOutput, validated):
```csharp
// Initialize once (expensive operation)
var client = new ViGEmClient();  // Throws if driver not installed

// Create controller
var controller = client.CreateXbox360Controller();
controller.AutoSubmitReport = false;  // Manual batching
controller.Connect();

// Send input (batch multiple changes, then submit)
controller.SetButtonState(Xbox360Button.A, true);
controller.SetAxisValue(Xbox360Axis.LeftThumbX, axisValue);  // -32768 to 32767
controller.SetSliderValue(Xbox360Slider.LeftTrigger, triggerValue);  // 0-255
controller.SubmitReport();

// Force feedback
controller.FeedbackReceived += (s, e) => {
    double large = e.LargeMotor / 255.0;
    double small = e.SmallMotor / 255.0;
};
```

**Value Conversions**:
- Axes: `(normalized - 0.5) * 2 * 32767` → -32768 to 32767
- Triggers: `normalized * 255` → 0 to 255
- Buttons: boolean

**Gotchas**:
- NOT thread-safe - synchronize access
- `ViGEmClient()` constructor is expensive - create once
- Always set `AutoSubmitReport = false` for batch updates

---

### HidHide (Device Hiding)

**Status**: ACTIVELY MAINTAINED - recent commits December 2024

**Purpose**: Kernel-mode filter driver that hides HID devices from applications unless whitelisted.

**Key Points**:
- No .NET NuGet package - use CLI or P/Invoke
- CLI approach recommended for simplicity
- Does NOT require admin after installation
- Device Instance Paths used for identification

**CLI Commands** (via `HidHideCLI.exe`):
```bash
# Cloaking control
--cloak-on              # Enable hiding
--cloak-off             # Disable hiding
--cloak-state           # Query state

# Device management
--dev-hide <path>       # Hide device by instance path
--dev-unhide <path>     # Unhide device
--dev-list              # List hidden devices
--dev-gaming            # List gaming devices (JSON)

# Application whitelist
--app-reg <exe-path>    # Whitelist application
--app-unreg <exe-path>  # Remove from whitelist
--app-list              # List whitelisted apps
```

**Device Instance Path Format**:
```
USB\VID_046D&PID_C294\123456
HID\VID_28DE&PID_1205&COL01
```

**Checking if Installed** (Registry):
```csharp
using Microsoft.Win32;
var key = Registry.ClassesRoot.OpenSubKey(
    @"Installer\Dependencies\NSS.Drivers.HidHide.x64");
bool installed = key?.GetValue("Version") != null;
```

**Integration Approach** (CLI wrapper):
```csharp
public bool HideDevice(string deviceInstancePath)
{
    var process = Process.Start(new ProcessStartInfo {
        FileName = "HidHideCLI.exe",
        Arguments = $"--dev-hide \"{deviceInstancePath}\"",
        CreateNoWindow = true,
        UseShellExecute = false
    });
    process.WaitForExit();
    return process.ExitCode == 0;
}
```

---

## Key Source Files

### Input System (`XOutputRenew.Input`)
- `IInputDevice.cs`, `IInputSource.cs` - Core interfaces
- `InputSource.cs` - Base class with deadzone handling
- `InputDeviceManager.cs` - Device discovery and lifecycle
- `IdHelper.cs` - Device identification (SHA256, hardware ID extraction)
- `DirectInput/` - DirectInput device/source/provider
- `RawInput/` - RawInput device/source/provider (HidSharp)

### Mapping System (`XOutputRenew.Core/Mapping`)
- `XboxOutput.cs` - Xbox controller output enum
- `InputBinding.cs` - Single input binding with transform settings
- `OutputMapping.cs` - Multiple inputs → one output with OR logic
- `MappingProfile.cs` - Complete profile with serialization
- `MappingEngine.cs` - Real-time input evaluation
- `ProfileManager.cs` - Profile load/save/manage

### Emulation (`XOutputRenew.Emulation`)
- `ViGEmService.cs` - ViGEm client wrapper
- `XboxController.cs` - Emulated Xbox 360 controller

### Device Hiding (`XOutputRenew.HidHide`)
- `HidHideService.cs` - CLI wrapper for HidHide operations

### Application (`XOutputRenew.App`)
- `Program.cs` - CLI entry point with System.CommandLine
- `MainWindow.xaml/.cs` - Main GUI with Devices/Profiles/Status/Options/Test tabs
- `ProfileEditorWindow.xaml/.cs` - Interactive mapping editor with output highlighting
- `DeviceSettings.cs` - Persists device friendly names
- `AppSettings.cs` - Persists app options (minimize to tray, start with Windows, startup profile)
- `AppLogger.cs` - File-based async logging for debugging
- `DarkModeHelper.cs` - Windows DWM API for dark title bars
- `ViewModels/` - DeviceViewModel, ProfileViewModel

---

## Logging & Debugging

### Log Files
- **Location**: `%AppData%\XOutputRenew\logs\xoutputrenew-YYYY-MM-DD.log`
- **Format**: `timestamp [LEVEL] [CallerMethod] message`
- **Levels**: INFO, WARN, ERROR

### Reading Logs
```powershell
# View today's log
Get-Content "$env:APPDATA\XOutputRenew\logs\xoutputrenew-$(Get-Date -Format 'yyyy-MM-dd').log" -Tail 50

# Or from bash
cat "$APPDATA/XOutputRenew/logs/xoutputrenew-$(date +%Y-%m-%d).log"
```

### Using the Logger
```csharp
AppLogger.Initialize();  // Call once at startup
AppLogger.Info("Something happened");
AppLogger.Warning("Something concerning");
AppLogger.Error("Something failed", exception);
```

---

## Data Storage Locations

| Data | Location |
|------|----------|
| Profiles | `%AppData%\XOutputRenew\Profiles\*.json` |
| Device Settings | `%AppData%\XOutputRenew\device-settings.json` |
| App Settings | `%AppData%\XOutputRenew\app-settings.json` |
| Logs | `%AppData%\XOutputRenew\logs\` |

---

## Known Issues

### Clipboard Copy Fails (CLIPBRD_E_CANT_OPEN)
- **Symptom**: "Copy Device Info" fails with clipboard error
- **Cause**: Another application holding clipboard lock (clipboard managers, Remote Desktop)
- **Workaround**: Info is shown in a dialog instead when copy fails
- **Solution**: Close clipboard managers or check Remote Desktop clipboard settings

### Tab Background Flicker (Dark Mode)
- **Symptom**: Tab headers flicker between shades of gray when hovering
- **Cause**: WPF's built-in TabItem ControlTemplate has light-theme hover/focus states that show through dark backgrounds
- **Fix**: Requires custom ControlTemplate for TabItem with dark-themed VisualStates (~50-80 lines XAML)
- **Priority**: Low (cosmetic only)

### Options Tab Content Overflow
- **Symptom**: Options tab content extends beyond window height, bottom options not visible
- **Cause**: Tab content is taller than minimum window size (600px)
- **Fix**: Add ScrollViewer to Options tab, or refactor layout to be more compact
- **Priority**: Medium (usability issue)

---

## Workspace Reference

Additional source repositories in workspace for reference:
- `E:\Source\XOutputRenew\XOutput` - Original XOutput (archived)
- `E:\Source\XOutputRenew\ViGEmBus` - ViGEmBus driver source
- `E:\Source\XOutputRenew\HidHide` - HidHide driver source

---

## Development History

See [CLAUDENOTES.md](CLAUDENOTES.md) for detailed session notes and development history.