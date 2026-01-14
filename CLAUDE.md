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

### Phase 4.7: Force Feedback ✓ COMPLETE
- [x] ForceFeedbackSettings model (Enabled, TargetDeviceId, Mode, Gain)
- [x] IForceFeedbackDevice interface and DirectInput implementation
- [x] ForceFeedbackService to route ViGEm rumble to physical devices
- [x] Profile editor UI for FFB settings (device, mode, gain)
- [x] Motor modes: Large, Small, Combined, Swap

### Phase 5: HidHide Integration ✓ COMPLETE
- [x] HidHide CLI wrapper service
- [x] Auto-hide devices on profile start
- [x] Auto-unhide on profile stop
- [x] Whitelist XOutputRenew.exe automatically
- [x] Profile editor tab for device hiding settings
- [x] Auto-install prompt with download from GitHub
- [x] "Open Windows Game Controllers" button for testing
- [x] Application whitelist UI (Browse for exe, select from running processes)
- [x] Process picker dialog with system process filtering

### Phase 5.5: UX Improvements ✓ COMPLETE
- [x] VID/PID-based device identification (stable across USB ports)
- [x] Read-only profile view when profile is running
- [x] Auto-select first profile on load (Start button immediately usable)
- [x] Profiles tab as default tab
- [x] Game Controllers button on Profiles tab
- [x] Renamed LeftStick/RightStick to LeftStickPress/RightStickPress for clarity
- [x] Double-click output to trigger Capture Input

### Phase 6: CLI & IPC ✓ COMPLETE
- [x] Named pipe server for runtime control (IpcService)
- [x] Commands: `start [profile]`, `stop`, `status`
- [x] Smart start: launches GUI if not running, sends IPC if running
- [x] Exit codes for scripting (0=success, 1=error, 2=not found, 3=no instance)
- [x] Toast notifications when profiles start/stop
- [x] Default profile feature for quick CLI start
- [x] Add to System PATH option
- [x] Console app with hidden window for GUI mode (proper CLI support)

### Phase 7: Game Auto-Profile ✓ COMPLETE
- [x] "Games" tab in main window with Add/Edit/Remove buttons
- [x] GameAssociation model and GameAssociationManager for persistence
- [x] GameMonitorService for polling running processes and detecting games
- [x] GameEditorDialog for adding/editing games (browse exe, pick from processes, or Steam games)
- [x] SteamGamePickerDialog for browsing installed Steam games
- [x] Background monitoring: detects when configured games start → starts profile
- [x] Auto-stop: when game exits → stops profile
- [x] Toggle "Enable Monitoring" / "Disable Monitoring" button with status indicator
- [x] Toast notifications for game detected/exited
- [x] Games stored in %AppData%\XOutputRenew\games.json

### Phase 8: Headless Mode ✓ COMPLETE
- [x] Run without GUI for scripting/service scenarios
- [x] `headless <profile>` command to start without window
- [x] All control via CLI commands (start, stop, status) or Ctrl+C
- [x] Useful for Stream Deck, gaming frontend integration, or running as a service

### Phase 9: Update Checker (PLANNED)
- [ ] Query GitHub Releases API on startup (no hosted backend needed)
- [ ] Compare `tag_name` to current assembly version
- [ ] Show toast notification if update available with link to download
- [ ] "Check for updates on startup" checkbox in Options
- [ ] Cache check (once per day max) to respect rate limits

### Phase 10: Crash/Bug Reporting (PLANNED)
- [ ] Global unhandled exception handler in App.xaml.cs
- [ ] Collect: exception, stack trace, app version, Windows version, device list
- [ ] Show error dialog with "Report Issue" and "Copy to Clipboard" buttons
- [ ] "Report Issue" opens browser with pre-filled GitHub issue URL
- [ ] User reviews/redacts sensitive info before submitting (privacy-friendly)
- [ ] No server required - uses GitHub issue URL parameters

### Phase 11: Portable Mode (PLANNED)
- [ ] Detect portable mode (e.g., presence of `portable.txt` or `XOutputRenew.portable` marker file)
- [ ] Store all settings in app directory instead of %AppData% when portable
- [ ] Profiles stored in `.\Profiles\` relative to exe
- [ ] Logs stored in `.\logs\` relative to exe
- [ ] No registry modifications in portable mode
- [ ] Document portable usage in README

### Phase 12: ViGEmBus Driver Check ✓ COMPLETE
- [x] Check for ViGEmBus driver on startup (similar to HidHide check)
- [x] Show warning dialog if not installed with explanation of why it's required
- [x] "Install" button in Status tab to download and launch installer
- [x] Auto-download and launch installer (like HidHide)
- [x] "Don't show again" behavior via prompt decline
- [x] Add `ViGEmBusPromptDeclined` to AppSettings
- [x] Status tab shows clear error state (red text) when ViGEmBus missing

### Phase 13: Stream Deck Plugin (PLANNED)
- [ ] Create Stream Deck plugin using Stream Deck SDK
- [ ] Actions: Start Profile, Stop Profile, Toggle Profile
- [ ] Profile picker dropdown in action configuration
- [ ] Status indicator (icon changes when profile running)
- [ ] Uses CLI commands under the hood (`XOutputRenew start/stop`)
- [ ] Plugin installer (.streamDeckPlugin file)
- [ ] Documentation for installation and usage
- [ ] Property Inspector UI for configuration

---

## CLI Interface

```
XOutputRenew - Xbox Controller Emulator

Usage: XOutputRenew [command] [options]

Commands:
  (no command)            Launch the GUI application
  run                     Launch the GUI (same as no command)
  headless [profile]      Run without GUI (uses default profile if not specified)
  list-devices [--json]   List detected input devices
  list-profiles [--json]  List available profiles
  start [profile]         Start a profile (uses default if not specified)
  stop                    Stop the running profile
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

---

## Recent Session Notes

### Session 2026-01-13

**Phase 12: ViGEmBus Driver Check - COMPLETE**

Implemented automatic ViGEmBus driver detection and installation, following the same pattern as HidHide.

**Features Added:**
- Startup check for ViGEmBus driver with warning dialog if not installed
- Clear messaging: "This driver is REQUIRED for XOutputRenew"
- "Install" button in Status tab for manual installation
- Auto-download from GitHub releases (latest release API, with fallback)
- Installer runs with admin elevation (UAC prompt)
- "Don't show again" behavior via `ViGEmBusPromptDeclined` setting
- Status tab shows red "Not Installed" text when missing

**Files Modified:**
- `App/AppSettings.cs` - Added `ViGEmBusPromptDeclined` property
- `Emulation/ViGEmService.cs` - Added `DownloadAndInstallAsync()` method
- `App/MainWindow.xaml` - Added Install button for ViGEmBus
- `App/MainWindow.xaml.cs` - Added prompt and install logic

**Phase 8: Headless Mode - COMPLETE**

Implemented headless mode for running without GUI window - useful for scripting, services, and gaming frontend integration.

**Usage:**
```bash
XOutputRenew headless "My Profile"    # Run without GUI
XOutputRenew stop                      # Stop from another terminal
# Or press Ctrl+C to stop
```

**Features Added:**
- `headless <profile>` CLI command runs the emulation without any window
- Initializes ViGEm, HidHide, input devices, and IPC server
- Graceful shutdown via Ctrl+C or `XOutputRenew stop`
- Toast notifications still work (visible after exiting fullscreen games)
- Full force feedback support
- Device hiding via HidHide (if configured in profile)
- Console output for status feedback

**New Files:**
- `App/HeadlessRunner.cs` - Standalone runner for headless operation

**Files Modified:**
- `App/Program.cs` - Added `headless` command and `RunHeadless()` method

---

### Session 2026-01-12 (Part 2)

**Phase 7: Game Auto-Profile - COMPLETE**

Tested with "SP Grand Prix" game - working correctly after bug fix.

**Bug Fixed: Profile didn't stop when game exited**
- **Root cause**: The `finally` block was disposing ALL processes, including the one stored for tracking. On the next poll, `HasExited` was called on a disposed object.
- **Fix**: Store `processId` (int) instead of `Process` object. Uses `Process.GetProcessById()` which properly throws when process no longer exists.

**Known Limitation: Toast notifications in fullscreen**
- Toast notifications don't appear over fullscreen exclusive games (Windows limitation)
- Toasts still work and appear after exiting fullscreen or in windowed mode

**Persist Game Monitoring State**
- Added `GameMonitoringEnabled` to AppSettings
- Monitoring state remembered across app restarts

**Release Infrastructure Added**
- `build.ps1` - PowerShell build script
- `release.ps1` - Creates portable ZIP + Inno Setup installer
- `installer/XOutputRenew.iss` - Inno Setup script with PATH and startup options
- `.github/workflows/ci.yml` - CI on push/PR
- `.github/workflows/release.yml` - Auto-release on `v*` tags
- `LICENSE` - MIT license
- Version set to 0.7.0-alpha

**New Files (Phase 7):**
- `Core/Games/GameAssociation.cs` - Model for game-profile associations
- `Core/Games/GameAssociationManager.cs` - Persistence to games.json
- `App/GameMonitorService.cs` - Background process monitoring
- `App/GameEditorDialog.xaml/.cs` - Add/edit game dialog
- `App/SteamGamePickerDialog.xaml/.cs` - Steam game browser
- `App/ExecutablePickerDialog.xaml/.cs` - Executable picker for multi-exe games

---

### Session 2026-01-12

**CLI/IPC Implementation Complete**
- Full command-line interface with named pipe IPC for controlling running instance
- Commands: `start [profile]`, `stop`, `status`, `list-devices`, `list-profiles`, `help`
- Smart `start` command: launches GUI if not running, sends IPC if running
- Exit codes for scripting: 0=success, 1=error, 2=profile not found, 3=no running instance
- Console attachment for WinExe apps (AttachConsole/FreeConsole for CLI output)

**Toast Notifications**
- Windows notification center toasts when profiles start/stop
- Uses Microsoft.Toolkit.Uwp.Notifications package
- Required TFM update to `net8.0-windows10.0.17763.0`

**Default Profile Feature**
- Checkbox in profile editor to mark a profile as default
- Only one profile can be default at a time
- `XOutputRenew start` (no args) uses the default profile
- Appropriate error message if no default is set

**Dark Mode Improvements**
- Dark-themed ToolTip style in App.xaml
- Custom HelpDialog window (replaces MessageBox for help popups)
- Silent dialogs (removed MessageBoxImage.Information sound)
- Lighter blue (#64B5F6) for "?" help icons

**Executable Rename**
- Changed AssemblyName from `XOutputRenew.App` to `XOutputRenew`
- Executable is now `XOutputRenew.exe` instead of `XOutputRenew.App.exe`
- Updated all help text and documentation

**Add to System PATH**
- New checkbox in Options tab to add XOutputRenew to system PATH
- Requires admin privileges
- Enables running CLI commands from any directory

**New Files:**
- `App/ToastNotificationService.cs` - Windows toast notifications
- `App/IpcService.cs` - Named pipe server/client for IPC
- `App/HelpDialog.xaml/.cs` - Dark-themed help dialog

**Modified Files:**
- `App/Program.cs` - CLI commands, console attachment, smart start, default profile
- `App/MainWindow.xaml.cs` - IPC handlers, PATH checkbox, toast integration
- `App/MainWindow.xaml` - PATH checkbox UI
- `App/ProfileEditorWindow.xaml/.cs` - Default profile checkbox
- `App/App.xaml` - Dark ToolTip style
- `Core/Mapping/MappingProfile.cs` - IsDefault property
- `Core/Mapping/ProfileManager.cs` - GetDefaultProfile(), SetDefaultProfile()
- `XOutputRenew.App.csproj` - AssemblyName, TFM update, toast package

---

### Session 2026-01-11 (Part 2)

**HidHide Integration Complete**
- Profile editor now has three tabs: Mapping, Force Feedback, Device Hiding
- HidHide auto-install: prompts user on startup if not installed, downloads from GitHub
- Device hiding settings saved per-profile
- Auto-hide devices when profile starts, auto-unhide when stopped
- XOutputRenew automatically whitelisted in HidHide
- "Open Windows Game Controllers" button to verify hiding is working
- Friendly names from DeviceSettings shown in HidHide device list

**New Files:**
- `Core/HidHide/HidHideSettings.cs` - Profile HidHide config (Enabled, DevicesToHide list)

**Modified Files:**
- `HidHide/HidHideService.cs` - Added DownloadAndInstallAsync(), improved detection logic, fixed JSON parsing for nested device structure
- `App/MainWindow.xaml.cs` - HidHide install prompt, hide/unhide on profile start/stop
- `App/ProfileEditorWindow.xaml` - Restructured into tabs, added Device Hiding tab
- `App/ProfileEditorWindow.xaml.cs` - HidHide settings UI, friendly name lookup
- `App/AppSettings.cs` - Added HidHidePromptDeclined property

**Bug Fixes:**
- Fixed device deduplication in InputDeviceManager (was incorrectly deduping RawInput devices with same HardwareId)
- Fixed HidHide detection to verify CLI works instead of relying on registry
- Fixed HidHide JSON parsing for nested device container structure

---

### Session 2026-01-11 (Part 1)

**Force Feedback (FFB) Implementation**
- Added complete FFB support to route rumble from games to physical devices (steering wheels)
- Data flow: Game → ViGEm Xbox Controller → ForceFeedbackReceived → ForceFeedbackService → DirectInputDevice → Physical device

**New Files Created:**
- `Core/ForceFeedback/ForceFeedbackSettings.cs` - Profile FFB config (Enabled, TargetDeviceId, Mode, Gain)
- `Input/ForceFeedback/IForceFeedbackDevice.cs` - Interface for FFB-capable devices
- `Input/ForceFeedback/ForceFeedbackTarget.cs` - FFB actuator representation
- `Input/DirectInput/DirectDeviceForceFeedback.cs` - SharpDX ConstantForce effect wrapper
- `App/ForceFeedbackService.cs` - Routes FFB from ViGEm to physical devices

**Modified Files:**
- `Core/Mapping/MappingProfile.cs` - Added ForceFeedbackSettings property and serialization
- `Input/DirectInput/DirectInputDevice.cs` - Implements IForceFeedbackDevice, FFB thread (10Hz)
- `Input/DirectInput/DirectInputDeviceProvider.cs` - FFB capability detection, window handle for exclusive mode
- `Input/InputDeviceManager.cs` - Added SetWindowHandle() method
- `App/MainWindow.xaml.cs` - Wire up ForceFeedbackService on profile start/stop
- `App/ProfileEditorWindow.xaml/.cs` - FFB settings UI (device, mode, gain controls)

**FFB Motor Modes:**
- Large: Use only large motor rumble
- Small: Use only small motor rumble
- Combined: Use max of both motors (default)
- Swap: Use small motor as primary

**Technical Details:**
- Exclusive cooperative level required for FFB output (needs window handle)
- FFB runs on dedicated thread per device at 10Hz (100ms interval)
- Separate from input polling thread (1ms)
- ConstantForce effect preferred; falls back to any available effect
- Gain multiplier: 0-200% intensity scaling

**Profile Editor UI:**
- New "Force Feedback" GroupBox in right panel
- Enable checkbox toggles other controls
- Target Device dropdown shows only FFB-capable DirectInput devices
- Motor Mode dropdown with 4 options
- Gain slider (0-200%) with percentage display

---

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


---

### Session 2026-01-12

**HidHide Whitelist Management UI - Complete**
- Added Application Whitelist section to Device Hiding tab in Profile Editor
- "Browse..." button to select executable files
- "Add Process..." button with ProcessPickerDialog to select from running processes
- ProcessPickerDialog filters out system processes (C:\Windows, common system process names)
- Whitelist shows filename only, full path on hover tooltip
- Fixed GetWhitelistedApplications() to strip `--app-reg` prefix and quotes from HidHide CLI output
- Deduplication of whitelist entries (Debug vs Release paths were showing as duplicates)

**New Files:**
- `ProcessPickerDialog.xaml/.cs` - Dialog for selecting running processes to whitelist

**VID/PID Device Identification - Breaking Change**
- Changed device ID generation to use VID/PID (hardware ID) instead of full interface path
- Devices now maintain the same ID regardless of which USB port they're connected to
- DirectInput: HID devices use `HID\VID_XXXX&PID_XXXX`, non-HID use ProductGuid only
- RawInput: Uses hardware ID extracted from device path
- **Breaking**: Existing profiles need devices re-mapped once (IDs changed)

**Read-Only Profile View**
- When a profile is running, double-clicking it opens in read-only mode
- Orange banner at top: "View Only - Profile is currently running. Stop the profile to make changes."
- Save button disabled and shows "View Only"
- Capture Input button disabled

**UI Improvements**
- Profiles tab is now the default tab (SelectedIndex="1")
- First profile auto-selected on load (Start button immediately usable)
- "Game Controllers" button added next to Start on Profiles tab
- Renamed `LeftStick`/`RightStick` to `LeftStickPress`/`RightStickPress` for clarity
- Double-click an output in profile editor to trigger Capture Input
- Profile editor height increased to 850px
- Column width adjustments (480px fixed outputs, flexible right panel)
- HidHide device list reduced to 100px height

**Bug Fixes**
- Fixed DirectInput device ID generation that was breaking profile mappings (reverted InstanceGuid inclusion)
- Fixed RawInputDevice "receiver already running" error when restarting profiles
- Fixed HidHide whitelist parsing for `--app-reg "path"` format

**Commits This Session:**
1. `Add HidHide whitelist UI, read-only profile view, and UI improvements`
2. `Fix DirectInput device ID generation breaking profile mappings`
3. `Use VID/PID for device identification and UI improvements`

