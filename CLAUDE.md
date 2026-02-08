# XOutputRedux

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
XOutputRedux/
├── XOutputRedux.sln
├── src/
│   ├── XOutputRedux.Core/           # Core abstractions, config, interfaces
│   ├── XOutputRedux.Input/          # DirectInput, RawInput device handling
│   ├── XOutputRedux.Emulation/      # ViGEm Xbox controller emulation
│   ├── XOutputRedux.HidHide/        # HidHide integration
│   ├── XOutputRedux.App/            # WPF GUI + CLI application
│   │   └── Assets/                  # Icons, banners, branding assets
│   ├── XOutputRedux.StreamDeck/     # Stream Deck plugin
│   ├── XOutputRedux.Moza.Plugin/    # Moza wheel plugin (optional, built separately)
│   ├── XOutputRedux.Moza.Helper/    # Out-of-process Moza SDK helper (keeps SDK alive)
│   └── XOutputRedux.HidSharper/     # Forked/slimmed HidSharp library (Windows-only HID)
└── tests/
    └── XOutputRedux.Tests/
```

### Key Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| Nefarius.ViGEm.Client | 1.21.256 | Virtual Xbox controller |
| Vortice.DirectInput | 3.8.2 | DirectInput device access |
| XOutputRedux.HidSharper | 1.0.0 | RawInput/HID device access (forked from HidSharp, Windows-only) |
| System.CommandLine | 2.0.0-beta4 | CLI parsing |
| Hardcodet.NotifyIcon.Wpf | 1.1.0 | System tray |

### External Projects (may fork if needed)
- **ViGEmBus**: https://github.com/nefarius/ViGEmBus - Virtual gamepad driver
- **HidHide**: https://github.com/nefarius/HidHide - Device hiding driver

---

## Reference: XOutput Analysis

XOutputRedux is based on principles from the archived XOutput project. Key code to adapt:

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
| 15: Portable Mode | ⏸ Shelved | Code exists but not advertised or distributed. Removed from release artifacts and public docs pending proper testing. |
| 16: Code Signing | ⏳ Applied | SignPath Foundation application submitted 2026-02-07. CODESIGNING.md + README added. Awaiting approval (typically a few weeks), then wire into CI. |
| 17: Rebrand to XOutput Redux | ✓ Complete | Codebase renamed to XOutput Redux |
| 18: Rename GitHub Repository | ✓ Complete | Renamed repo to `xoutputredux`, URLs already pointed to new name |
| 19: Quick Add Game Hotkey | ✓ Complete | Global hotkey (Ctrl+Shift+G) to add focused game to running profile |
| 20: Plugin System | ✓ Complete | Simple plugin loader, per-profile plugin data, profile editor tab injection |
| 21: Moza Wheel Plugin | ✓ Complete | XOutputRedux.Moza.Plugin — 8 wheel settings via out-of-process helper exe + Pit House SDK. Auto-scales steering axis when rotation differs from device reference. |
| 22: Axis Response Curves | ✓ Complete | Per-binding Sensitivity parameter (power/gamma curve, 0.1–5.0), symmetric for axes, simple power for triggers. Visual curve preview in profile editor. |

### Completed Dependency Upgrades

| Item | Status | Notes |
|------|--------|-------|
| **Migrate SharpDX → Vortice.DirectInput** | ✓ Complete | Migrated 2026-01-18. Vortice 3.8.2 actively maintained. |
| **Fork HidSharp → XOutputRedux.HidSharper** | ✓ Complete | Forked 2026-01-18. Windows-only, removed BLE/Serial support, 73 files. |

### Future Roadmap (Late 2026)

| Item | Rationale | Effort |
|------|-----------|--------|
| **Upgrade to .NET 10** | .NET 8 LTS ends Nov 2026; .NET 10 is next LTS | ~1 day |

*Note: These are maintenance/future-proofing upgrades, not performance improvements. See [CLAUDE_DEPENDENCY_ANALYSIS.md](CLAUDE_DEPENDENCY_ANALYSIS.md) for detailed analysis.*

### Future Enhancements

| Item | Description | Priority |
|------|-------------|----------|
| **Improved Wheel FFB** | Current FFB uses ConstantForce in one direction (left), designed for gamepad rumble. Enhancements: (1) Use oscillating/periodic effects instead of constant force for more rumble-like feel, (2) Allow configuring effect type/direction in profile, (3) Apply magnitude symmetrically to avoid one-sided pull. Note: Xbox rumble → wheel FFB is inherently limited; games not designed for wheels will never feel like proper wheel games. | Low |
| **Steering Wheel Axis Tuning (Extended)** | Phase 1 (response curve) complete. Remaining ideas: (1) Per-axis inner/outer deadzone at binding level, (2) S-curve or custom curve editor, (3) Additional curve presets. | Low |
| **Live Profile Preview in Editor** | Visual-only live preview mode in the Profile Editor. Runs input through the mapping pipeline (including response curves, input range, invert) and displays simulated Xbox controller output in real-time — without creating a ViGEm controller. Enables tweaking sensitivity/settings and seeing the result immediately without stop/edit/restart cycle. Key work: run mapping pipeline from editor using existing device polling, output visualization panel, live-updating bindings. | Medium |
| **Portable Mode (Revisit)** | Code exists (detects `portable.txt`, stores settings in `data\` subfolder) but removed from release artifacts and docs because it's untested. Needs: automated test coverage for portable paths, manual QA pass, then re-add to release workflow and docs. | Low |

---

## CLI Interface

```
XOutputRedux - Xbox Controller Emulator

Usage: XOutputRedux [command] [options]

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
  XOutputRedux                                    # Open GUI
  XOutputRedux headless                           # Run headless with default profile
  XOutputRedux headless "My Wheel"                # Run headless with specific profile
  XOutputRedux headless --monitor                 # Run headless with game monitoring only
  XOutputRedux headless "My Wheel" --monitor      # Run headless with profile + monitoring
  XOutputRedux start                              # Start default profile
  XOutputRedux start "My Wheel"                   # Start specific profile
  XOutputRedux --start-profile "My Wheel" --minimized
  XOutputRedux stop                               # Stop running profile
  XOutputRedux status --json                      # Get status as JSON
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

## Versioning

Format: `Major.Minor.Patch.BuildNumber[-suffix]`

| Component | Description | Example |
|-----------|-------------|---------|
| VersionPrefix | Manual major.minor.patch | 0.9.0 |
| BuildNumber | Auto-generated: YYDDDHHmm | 260260925 |
| VersionSuffix | Optional prerelease tag | alpha, beta, rc |

**BuildNumber format: YYDDDHHmm**
- YY = Year (26 = 2026)
- DDD = Day of year (026 = Jan 26)
- HHmm = Time (0925 = 09:25)

Example: `0.9.0.260260925-alpha` = Version 0.9.0, built Jan 26 2026 at 09:25, alpha prerelease.

**To change version:**
- `VersionPrefix` - bump for releases (0.9.0 → 0.9.1 or 1.0.0)
- `VersionSuffix` - set to `alpha`, `beta`, `rc`, or leave empty for stable
- BuildNumber auto-increments on every build

**Override in CI/CD:**
```bash
dotnet build -p:BuildNumber=12345
```

---

## Build & Run

```powershell
# Build
dotnet build -c Release

# Run
dotnet run --project src/XOutputRedux.App

# Run tests
dotnet test

# Build Moza plugin separately (not part of main solution)
dotnet publish src/XOutputRedux.Moza.Plugin -c Release -o publish-moza-plugin --self-contained false

# Deploy plugin for local testing (copy to app's output plugins folder)
# Copy XOutputRedux.Moza.Plugin.dll, MOZA_SDK.dll, MOZA_API_C.dll, MOZA_API_CSharp.dll
# to: src/XOutputRedux.App/bin/Release/net8.0-windows10.0.17763.0/plugins/Moza/

# Create full release (installer + Stream Deck + Moza plugin)
.\scripts\release.ps1

# Skip optional components
.\scripts\release.ps1 -SkipStreamDeck -SkipMozaPlugin
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

### Input System (`XOutputRedux.Input`)
- `IInputDevice.cs`, `IInputSource.cs` - Core interfaces
- `InputSource.cs` - Base class with deadzone handling
- `InputDeviceManager.cs` - Device discovery and lifecycle
- `IdHelper.cs` - Device identification (SHA256, hardware ID extraction)
- `DirectInput/` - DirectInput device/source/provider
- `RawInput/` - RawInput device/source/provider (HidSharp)

### Mapping System (`XOutputRedux.Core/Mapping`)
- `XboxOutput.cs` - Xbox controller output enum
- `InputBinding.cs` - Single input binding with transform settings
- `OutputMapping.cs` - Multiple inputs → one output with OR logic
- `MappingProfile.cs` - Complete profile with serialization
- `MappingEngine.cs` - Real-time input evaluation
- `ProfileManager.cs` - Profile load/save/manage

### Emulation (`XOutputRedux.Emulation`)
- `ViGEmService.cs` - ViGEm client wrapper
- `XboxController.cs` - Emulated Xbox 360 controller

### Device Hiding (`XOutputRedux.HidHide`)
- `HidHideService.cs` - CLI wrapper for HidHide operations

### Application (`XOutputRedux.App`)
- `Program.cs` - CLI entry point with System.CommandLine
- `MainWindow.xaml/.cs` - Main GUI with Devices/Profiles/Status/Options/Test tabs
- `ProfileEditorWindow.xaml/.cs` - Interactive mapping editor with output highlighting
- `DeviceSettings.cs` - Persists device friendly names
- `AppSettings.cs` - Persists app options (minimize to tray, start with Windows, startup profile)
- `AppLogger.cs` - File-based async logging for debugging
- `DarkModeHelper.cs` - Windows DWM API for dark title bars
- `PluginLoader.cs` - Discovers and loads plugins from `plugins/` subdirectory
- `ViewModels/` - DeviceViewModel, ProfileViewModel

### Plugin System (`XOutputRedux.Core/Plugins`)
- `IXOutputPlugin.cs` - Plugin interface (Initialize, CreateEditorTab, OnProfileStart/Stop, GetAxisRangeOverrides)
- `AxisRangeOverride` record - Describes per-axis input range overrides applied by plugins at profile start
- Plugins are loaded from `plugins/<Name>/` subdirectory next to exe
- Plugin data stored in profile JSON under `pluginData` dictionary keyed by plugin ID
- DLLs matching `*.Plugin.dll` are scanned; each plugin gets its own `AssemblyLoadContext`

### Moza Wheel Plugin (`XOutputRedux.Moza.Plugin`)
- `MozaPlugin.cs` - IXOutputPlugin implementation, spawns helper exe, parses ref-rotation, calculates axis auto-scaling
- `MozaDevice.cs` - Trimmed Moza SDK wrapper (reads only, used for editor defaults)
- `MozaEditorTab.cs` - WPF tab UI built in code (enable checkbox, rotation slider, FFB slider)
- `XOutputRedux.Moza.Helper/Program.cs` - Out-of-process helper that applies SDK settings and keeps SDK alive
- Requires Moza Pit House running; SDK DLLs bundled in plugin folder
- **Axis auto-scaling**: When target rotation < reference rotation, the steering axis only uses a fraction of 0-65535. The plugin queries the reference rotation before changing it, calculates axisMin/axisMax, and returns `AxisRangeOverride` to auto-scale the binding's InputRange at profile start.

---

## Logging & Debugging

### Log Files
- **Location**: `%AppData%\XOutputRedux\logs\xoutputredux-YYYY-MM-DD.log`
- **Format**: `timestamp [LEVEL] [CallerMethod] message`
- **Levels**: INFO, WARN, ERROR

### Reading Logs
```powershell
# View today's log
Get-Content "$env:APPDATA\XOutputRedux\logs\xoutputredux-$(Get-Date -Format 'yyyy-MM-dd').log" -Tail 50

# Or from bash
cat "$APPDATA/XOutputRedux/logs/xoutputredux-$(date +%Y-%m-%d).log"
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
| Profiles | `%AppData%\XOutputRedux\Profiles\*.json` |
| Device Settings | `%AppData%\XOutputRedux\device-settings.json` |
| App Settings | `%AppData%\XOutputRedux\app-settings.json` |
| Logs | `%AppData%\XOutputRedux\logs\` |

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


### HidSharp Parse Errors for Certain Report IDs
- **Symptom**: Some RawInput devices (e.g., VelocityOne Multi-Shift) generate parse errors for specific HID report IDs
- **Cause**: HidSharp's parser can't handle certain report formats (likely feature/configuration reports, not input reports)
- **Impact**: None - device input still works correctly; these reports aren't needed for input
- **Mitigation**: Error logging is now throttled (logs once per report ID, with summary on dispose)
- **Future**: Investigate during HidSharp fork (Phase: Fork/slim HidSharp) - may be able to fix parser or skip non-input reports


---

## Bug Fixes

### v0.9.2-alpha: Start with Windows not working (2026-01-23)
- **Symptom**: "Start with Windows" checkbox was checked but app didn't start at Windows login
- **Root Cause**: Windows uses TWO registry keys for startup apps:
  1. `HKCU\...\Run` - Contains the command to run
  2. `HKCU\...\StartupApproved\Run` - Binary flag controlling enabled/disabled status
- **Issue**: Code only wrote to `Run` key but not `StartupApproved`. Windows/Task Manager uses the `StartupApproved` key to determine if an app should actually run.
- **Fix**: Updated `AppSettings.cs` to write to both registry keys when toggling startup, and check both keys when reading status
- **Files Changed**: `src/XOutputRedux.App/AppSettings.cs`

### v0.9.3-alpha: HidHide interferes with SDL2 inputs (2026-01-25)
- **Symptom**: SDL2-based programs (games, emulators) couldn't detect controllers when HidHide was installed, even when XOutputRedux wasn't actively using device hiding
- **Root Cause**: HidHide is a kernel filter driver that intercepts device inputs. Once started, it stays running and can interfere with other input libraries.
- **Issue**: XOutputRedux enabled cloaking and left HidHide running even after profiles stopped
- **Fix**:
  1. Start HidHide driver only when a profile with device hiding starts
  2. Disable cloaking when profile stops
  3. Stop HidHide driver entirely when no longer needed
- **Files Changed**:
  - `src/XOutputRedux.HidHide/HidHideService.cs` - Added `StartDriver()`, `StopDriver()`, `IsDriverRunning()` methods
  - `src/XOutputRedux.App/MainWindow.xaml.cs` - Updated `HideProfileDevices()` and `UnhideProfileDevices()` to manage driver lifecycle

### v0.9.4-alpha: Stream Deck plugin not included in release (2026-01-25)
- **Symptom**: Stream Deck plugin file not found in installed application
- **Root Cause**: Release script referenced wrong path for Stream Deck build script (`streamdeck-plugin/` instead of `src/XOutputRedux.StreamDeck/`)
- **Fix**: Updated `scripts/release.ps1` to use correct paths for Stream Deck project and output file
- **Files Changed**: `scripts/release.ps1`

### v0.9.5-alpha: Stream Deck plugin filename mismatch (2026-01-25)
- **Symptom**: "Plugin Not Found" error when clicking Install Stream Deck Plugin button
- **Root Cause**: Code looked for `XOutputRedux.streamDeckPlugin` but actual file is `com.xoutputredux.streamDeckPlugin`
- **Fix**: Updated code to use correct filename
- **Files Changed**: `src/XOutputRedux.App/MainWindow.xaml.cs`

### v0.9.2-alpha: Moza steering axis not using full range after rotation change (2026-02-05)
- **Symptom**: Setting wheel rotation to 270° (from 1080° default) worked physically, but the Test tab showed the axis not reaching full deflection — only ~25% of the range was used
- **Root Cause**: `MozaHelper.exe` calls `removeMozaSDK()` then `installMozaSDK()` to clean stale state. After re-init, `getMotorLimitAngle()` returns `hardware=0, game=0` even though the device reports as "ready" — the SDK needs extra time to sync rotation values from Pit House.
- **Issue**: With ref-rotation=0, the auto-scaling ratio calculation (`target / ref`) was skipped, so the axis mapping treated the full HID range (0-65535) as valid even though only a fraction was active at 270°.
- **Secondary Issue**: `_firstSeenRefRotation` in MozaPlugin was never reset on profile stop, so a stale `0` persisted across start/stop cycles without restarting the app.
- **Fix**:
  1. Added retry loop in `MozaHelper.exe` — queries `getMotorLimitAngle()` up to 5 times (1s apart) until a non-zero value is returned
  2. Reset `_firstSeenRefRotation` to null in `OnProfileStop()` so each profile start gets a fresh reading
- **Files Changed**:
  - `src/XOutputRedux.Moza.Helper/Program.cs` - Added ref-rotation retry loop
  - `src/XOutputRedux.Moza.Plugin/MozaPlugin.cs` - Reset `_firstSeenRefRotation` on profile stop

---

## Workspace Reference

Additional source repositories in workspace for reference:
- `E:\Source\XOutputRedux\XOutput` - Original XOutput (archived)
- `E:\Source\XOutputRedux\ViGEmBus` - ViGEmBus driver source
- `E:\Source\XOutputRedux\HidHide` - HidHide driver source

---

## Development History

See [CLAUDENOTES.md](CLAUDENOTES.md) for detailed session notes and development history.