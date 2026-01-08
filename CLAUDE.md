# XOutputRenew

Streamlined Xbox controller emulator for Windows. Maps inputs from multiple gaming devices (steering wheels, joysticks, gamepads) to a single emulated Xbox 360 controller.

## Project Goals

1. **Multi-input to single output** - Map multiple physical buttons to same Xbox button (e.g., wheel B button + handbrake → Xbox B)
2. **CLI integration** - Full command-line control for gaming frontends and Stream Deck
3. **Profile management** - Create, duplicate, and switch profiles easily
4. **Device hiding** - HidHide integration to prevent game confusion from seeing both real and emulated controllers
5. **Simple GUI** - Interactive "press to map" configuration interface

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
| SharpDX.DirectInput | 4.2.0 | DirectInput device access |
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

### Phase 1: Foundation
- [ ] Solution structure with projects
- [ ] Port DirectInput handling from XOutput
- [ ] Port RawInput handling from XOutput
- [ ] Port ViGEm Xbox emulation from XOutput
- [ ] New mapping system with OR logic
- [ ] Basic console test harness

### Phase 2: Profile System
- [ ] Profile model (JSON schema)
- [ ] Profile storage (`%AppData%\XOutputRenew\profiles\`)
- [ ] Load/save/duplicate functionality
- [ ] CLI: `list-profiles`, `list-devices`

### Phase 3: GUI - Core
- [ ] WPF application with system tray
- [ ] Device list view (detected devices)
- [ ] Profile list view
- [ ] Start/stop profile controls

### Phase 4: GUI - Interactive Mapping
- [ ] "Press button to map" functionality
- [ ] Visual input display (see current values)
- [ ] Profile editor with mapping configuration
- [ ] Multi-input to single output UI

### Phase 5: HidHide Integration
- [ ] HidHide client library
- [ ] Auto-hide devices on profile start
- [ ] Auto-unhide on profile stop
- [ ] Whitelist XOutputRenew.exe

### Phase 6: CLI & IPC
- [ ] Full CLI with System.CommandLine
- [ ] Named pipe server for runtime control
- [ ] Commands: `--start=Profile`, `--stop=Profile`, `--status`
- [ ] Exit codes for scripting

---

## CLI Interface

```
XOutputRenew - Xbox Controller Emulator

Usage: XOutputRenew [command] [options]

Commands:
  run                     Start the application (default, opens GUI)
  list-devices            List detected input devices
  list-profiles           List available profiles
  duplicate-profile       Duplicate an existing profile

Options:
  --start-profile=<name>  Start emulation with profile on launch
  --minimized             Start minimized to system tray
  --headless              Run without GUI (CLI mode only)

Runtime Commands (sent to running instance):
  --start=<name>          Start a profile
  --stop=<name>           Stop a profile
  --stop-all              Stop all profiles
  --status                Get current status (JSON output)
  --quit                  Quit the application

Examples:
  XOutputRenew                                    # Open GUI
  XOutputRenew --start-profile=MozaWheel1         # Start with profile
  XOutputRenew --start-profile=MozaWheel1 --minimized
  XOutputRenew --start=MozaWheel1                 # Control running instance
  XOutputRenew --status                           # Check what's running
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
`A`, `B`, `X`, `Y`, `LeftBumper`, `RightBumper`, `Back`, `Start`, `Guide`, `LeftStick`, `RightStick`, `DPadUp`, `DPadDown`, `DPadLeft`, `DPadRight`

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

## Workspace Reference

Additional source repositories in workspace for reference:
- `E:\Source\XOutputRenew\XOutput` - Original XOutput (archived)
- `E:\Source\XOutputRenew\ViGEmBus` - ViGEmBus driver source
- `E:\Source\XOutputRenew\HidHide` - HidHide driver source
