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

### Phase 1: Foundation ✓ COMPLETE
- [x] Solution structure with projects
- [x] Port DirectInput handling from XOutput
- [x] Port RawInput handling from XOutput
- [x] Port ViGEm Xbox emulation from XOutput
- [x] New mapping system with OR logic
- [x] Unit tests for mapping system (14 passing)

### Phase 2: Profile System ✓ COMPLETE
- [x] Profile model (JSON schema) - MappingProfileData
- [x] Profile storage (`%AppData%\XOutputRenew\Profiles\`)
- [x] Load/save/duplicate functionality - ProfileManager
- [x] CLI: `list-devices`, `list-profiles`, `duplicate-profile`
- [x] CLI with System.CommandLine, JSON output support

### Phase 3: GUI - Core ✓ COMPLETE
- [x] WPF application with system tray (minimize to tray)
- [x] Device list view with refresh
- [x] Profile list view with create/duplicate/delete
- [x] Start/stop profile controls with real-time input mapping
- [x] Status tab showing ViGEm and HidHide status
- [x] InputDialog for profile naming

### Phase 4: GUI - Interactive Mapping ✓ COMPLETE
- [x] ProfileEditorWindow with full mapping UI
- [x] "Press button to map" with 10-second capture timeout
- [x] Real-time input monitor showing device activity
- [x] Multi-binding UI (OR logic) - add multiple inputs to one output
- [x] Binding settings: invert, button threshold
- [x] Double-click profile to edit

### Phase 4.5: Device Tab Enhancements ✓ COMPLETE
- [x] De-duplicate devices (same device from DirectInput/RawInput)
- [x] "Listen for Input" checkbox - highlights devices receiving input
- [x] Right-click context menu: Copy Device Info, Rename Device
- [x] Device friendly names (persisted to device-settings.json)

### Phase 4.6: Profile & Options Enhancements ✓ COMPLETE
- [x] Profile context menu (Edit, Rename, Duplicate, Delete)
- [x] Removed Edit/Duplicate/Delete buttons from toolbar (cleaner UI)
- [x] Profile rename with overwrite confirmation
- [x] Options tab with settings:
  - Minimize to tray on close (toggle)
  - Start with Windows (registry-based)
  - Startup profile selection (auto-start a profile on launch)
  - Run as Administrator status and restart button
- [x] AppSettings persisted to `%AppData%\XOutputRenew\app-settings.json`

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

---

## Recent Session Notes

### Session 2026-01-10 (Part 2)

**Test Tab - Visual Xbox Controller**
- Added new "Test" tab showing real-time Xbox controller output state
- Visual controller layout with buttons, triggers, D-pad, and analog sticks
- Buttons light up green when pressed
- Triggers show fill bars (0-100%)
- Analog sticks show moving dot indicators
- Data panel on right shows exact numeric values
- Auto-updates when a profile is running
- Shows "Start a profile to see controller output" overlay when no profile active

**Dark Mode Implementation**
- Dark theme is now the default for the "gaming crowd"
- Dark title bar using Windows DWM API (`DwmSetWindowAttribute`)
- Colors: Background #1E1E1E, Controls #2D2D30, Foreground #E0E0E0
- Applied to both MainWindow and ProfileEditorWindow
- Created `DarkModeHelper.cs` for dark title bar API calls

**Profile Editor - Listen for Input (Output Highlighting)**
- "Listen for Input" checkbox now highlights **Xbox Controller Outputs** rows on the left
- When input is detected for a mapped output, that row lights up orange
- Buttons highlight when pressed above threshold
- Triggers highlight when pressed > 10%
- Axes highlight when moved > 15% from center
- Highlights fade after 300ms of inactivity
- Much more useful than the previous device-highlighting approach

**Application Icon**
- Added `console-controller.png` gamepad icon
- Shows in window title bars and taskbar
- PNG works directly with WPF (no .ico conversion needed for windows)
- Note: .exe icon in Explorer still needs .ico format if desired later

**New Files**
- `DarkModeHelper.cs` - Windows DWM API for dark title bars
- `console-controller.png` - Gamepad icon for windows
- `console-controller.svg` - Source vector for icon

---

### Session 2026-01-10 (Part 1)

**Profile Context Menu**
- Moved Edit, Duplicate, Delete from toolbar buttons to right-click context menu on profile list
- Added Rename option with overwrite confirmation for existing profiles
- Cleaner UI with only "New Profile" and "Start/Stop" buttons in toolbar

**Options Tab Added**
- New tab in main window with three sections:
  - **Behavior**: "Minimize to tray when closing" checkbox - when unchecked, X button exits app
  - **Startup**: "Start with Windows" checkbox (uses registry HKCU\Software\Microsoft\Windows\CurrentVersion\Run), Startup Profile dropdown
  - **Administrator**: Shows current admin status, "Restart as Administrator" button

**Bug Fix: Profile Rename**
- Fixed issue where RenameProfile only moved file but didn't update name inside JSON
- Now properly saves profile with new name, then deletes old file
- Added overwrite confirmation when renaming to existing profile name
- Added detailed error output parameter for debugging

**New Files**
- `AppSettings.cs` - Settings model with registry helpers for Start with Windows

---

### Session 2026-01-08

**Event-Driven Input Handling**
- Changed RawInputDevice from polling (1ms loop) to event-driven using HidSharp's `Received` event
- Eliminates 5-second delay that was occurring with VelocityOne Multi-Shift and other devices
- Input now registers immediately when device sends data

**Async Buffered Logging**
- Replaced synchronous `File.AppendAllText` (opened/closed file per message) with async buffered approach
- Uses `ConcurrentQueue<string>` for message buffering
- Background writer thread batches writes every 100ms
- Eliminates I/O blocking on input threads
- `AppLogger.Shutdown()` called on app exit to flush remaining messages

**Profile Editor Improvements**
- Fixed ThreadStateException when restarting capture (threads created fresh in Start())
- Added 300ms grace period for capture baseline to prevent axis noise from triggering
- Added help buttons ("?") for Invert and Threshold settings with tooltips
- Clear capture hint text when switching outputs
- Added "Clear Bindings" context menu option

**VelocityOne Multi-Shift Support**
- Device sends two HID report types: Report ID 1 (standard) and Report ID 36 (vendor-specific)
- Added try-catch around parse/process to handle vendor-specific reports gracefully
- Device now works correctly with event-driven approach

### Testing Status

**Devices Tested:**
- MOZA R12 steering wheel base - Working (DirectInput)
- VelocityOne Multi-Shift gear shifter - Working (RawInput, after event-driven fix)

**Features Tested (2026-01-10):**
- [x] Profile rename with overwrite confirmation - Working
- [x] Options tab settings - Working
- [x] Close behavior (minimize to tray vs exit) - Working
- [x] Test tab visual controller - Working
- [x] Dark mode theme - Working
- [x] Profile editor output highlighting - Working
- [x] Application icon in title bar - Working

**Still Need Testing:**
- [ ] Profile save/load cycle - verify bindings persist correctly
- [ ] Multiple devices in one profile
- [ ] Actual emulation output - verify Xbox controller works in games
- [ ] Start/stop profile multiple times
- [ ] Startup profile setting (Options tab)
- [ ] Start with Windows setting
- [ ] HidHide integration (Phase 5)

### Next Steps

1. **Emulation Testing (Priority)**
   - Start a profile and verify virtual Xbox controller appears
   - Test in a game (e.g., Forza Horizon, any Xbox controller game)
   - Verify input values map correctly

2. **HidHide Integration (Phase 5)**
   - Implement auto-hide devices on profile start
   - Auto-unhide on profile stop
   - Whitelist XOutputRenew.exe

3. **CLI & IPC (Phase 6)**
   - Named pipe server for runtime control
   - `--start`, `--stop`, `--status` commands to control running instance

---

## Workspace Reference

Additional source repositories in workspace for reference:
- `E:\Source\XOutputRenew\XOutput` - Original XOutput (archived)
- `E:\Source\XOutputRenew\ViGEmBus` - ViGEmBus driver source
- `E:\Source\XOutputRenew\HidHide` - HidHide driver source
