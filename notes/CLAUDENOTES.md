# XOutputRedux Development Changelog

Development session notes moved from CLAUDE.md for historical reference.

---

## Session 2026-01-18

**Migrate SharpDX → Vortice.DirectInput - COMPLETE**

Replaced abandoned SharpDX.DirectInput (last updated 2019) with actively maintained Vortice.DirectInput 3.8.2.

**Changes:**
- Updated `XOutputRedux.Input.csproj` package reference
- Migrated `DirectInputDeviceProvider.cs`:
  - `DirectInput` class → `IDirectInput8` interface
  - `new DirectInput()` → `DInput.DirectInput8Create()`
  - `new Joystick()` → `CreateDevice()` returning `IDirectInputDevice8`
  - Added `SetDataFormat<RawJoystickState>()` call (required in Vortice)
- Migrated `DirectInputDevice.cs`:
  - `Joystick` type → `IDirectInputDevice8`
  - `GetCurrentState()` → `GetCurrentJoystickState(ref state)`
  - `SharpDXException` → `SharpGen.Runtime.SharpGenException`
- Migrated `DirectInputSource.cs`: namespace change only
- Migrated `DirectDeviceForceFeedback.cs`:
  - `Joystick` → `IDirectInputDevice8`
  - `Effect` → `IDirectInputEffect`
  - `new Effect()` → `device.CreateEffect()`

**API Mapping Reference:**
| SharpDX | Vortice |
|---------|---------|
| `DirectInput` class | `IDirectInput8` interface |
| `Joystick` class | `IDirectInputDevice8` interface |
| `Effect` class | `IDirectInputEffect` interface |
| `joystick.GetCurrentState()` | `device.GetCurrentJoystickState(ref state)` |
| `new Effect(joystick, guid, params)` | `device.CreateEffect(guid, params)` |
| `SharpDXException` | `SharpGen.Runtime.SharpGenException` |

All 30 tests pass after migration.

---

**Update Checker Error Handling - COMPLETE**

Fixed issue where failed update checks (e.g., private repo returning 404) would incorrectly show "You're running the latest version" instead of an error message.

**Changes:**
- Added `UpdateCheckResult` class to distinguish between three states:
  - Success with update available
  - Success with no update (on latest version)
  - Error with message
- Specific error handling for HTTP 404 (private repo or URL changed)
- User-friendly error messages shown in "Check Now" dialog
- Startup update check silently ignores errors (doesn't bother user)

**Files Modified:**
- `App/UpdateService.cs` - Added UpdateCheckResult class, updated CheckForUpdateAsync()
- `App/MainWindow.xaml.cs` - Updated CheckForUpdatesNow_Click and CheckForUpdatesOnStartupAsync

---

## Session 2026-01-13

**Phase 12: ViGEmBus Driver Check - COMPLETE**

Implemented automatic ViGEmBus driver detection and installation, following the same pattern as HidHide.

**Features Added:**
- Startup check for ViGEmBus driver with warning dialog if not installed
- Clear messaging: "This driver is REQUIRED for XOutputRedux"
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
XOutputRedux headless "My Profile"    # Run without GUI
XOutputRedux stop                      # Stop from another terminal
# Or press Ctrl+C to stop
```

**Features Added:**
- `headless <profile>` CLI command runs the emulation without any window
- Initializes ViGEm, HidHide, input devices, and IPC server
- Graceful shutdown via Ctrl+C or `XOutputRedux stop`
- Toast notifications still work (visible after exiting fullscreen games)
- Full force feedback support
- Device hiding via HidHide (if configured in profile)
- Console output for status feedback

**New Files:**
- `App/HeadlessRunner.cs` - Standalone runner for headless operation

**Files Modified:**
- `App/Program.cs` - Added `headless` command and `RunHeadless()` method

---

## Session 2026-01-12 (Part 2)

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
- `installer/XOutputRedux.iss` - Inno Setup script with PATH and startup options
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

## Session 2026-01-12

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
- `XOutputRedux start` (no args) uses the default profile
- Appropriate error message if no default is set

**Dark Mode Improvements**
- Dark-themed ToolTip style in App.xaml
- Custom HelpDialog window (replaces MessageBox for help popups)
- Silent dialogs (removed MessageBoxImage.Information sound)
- Lighter blue (#64B5F6) for "?" help icons

**Executable Rename**
- Changed AssemblyName from `XOutputRedux.App` to `XOutputRedux`
- Executable is now `XOutputRedux.exe` instead of `XOutputRedux.App.exe`
- Updated all help text and documentation

**Add to System PATH**
- New checkbox in Options tab to add XOutputRedux to system PATH
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
- `XOutputRedux.App.csproj` - AssemblyName, TFM update, toast package

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

---

## Session 2026-01-11 (Part 2)

**HidHide Integration Complete**
- Profile editor now has three tabs: Mapping, Force Feedback, Device Hiding
- HidHide auto-install: prompts user on startup if not installed, downloads from GitHub
- Device hiding settings saved per-profile
- Auto-hide devices when profile starts, auto-unhide when stopped
- XOutputRedux automatically whitelisted in HidHide
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

## Session 2026-01-11 (Part 1)

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

## Session 2026-01-10 (Part 2)

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

## Session 2026-01-10 (Part 1)

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

## Session 2026-01-08

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

---

## Testing Status

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

---

## Session 2026-02-04

### Moza SDK Same-Process DirectInput Calibration Issue

**Problem**: When the Moza SDK's `setMotorLimitAngle(270)` is called from the same process as DirectInput, the physical wheel stop moves correctly but DirectInput axis calibration does not update. The axis only reaches ~25% deflection at full lock because DirectInput is still calibrated for 1080°.

**What was tried (all failed from same process):**
1. SDK call before DirectInput acquisition → readback=270 but axis still ~25%
2. Adding 2s delay after SDK call before DirectInput → same result
3. SDK call on background thread after DirectInput opens → SDK call silently fails (readback=1080)
4. Keeping SDK session alive (not calling removeMozaSDK) → no effect

**What works**: A different process (MozaHotkey) calling the SDK while XOutputRedux has DirectInput open → axis recalibrates correctly.

**Root cause**: DirectInput axis calibration only updates when the SDK call originates from a different process than the one that acquired the DirectInput device.

### Architecture Options for Moza SDK Integration

#### Option 1: Helper Exe (Selected for implementation)
XOutputRedux's Moza plugin spawns a small console app to apply settings:
```
MozaHelper.exe --rotation 270 --ffb 80 --damping 50
```
- Plugin builds command-line args from profile data, calls `Process.Start()`
- Helper applies settings via SDK and exits
- XOutputRedux never loads the Moza SDK in-process
- Stream Deck continues using MozaHotkey as before
- No IPC needed, no service lifecycle to manage
- If both XOutputRedux and Stream Deck set values, last one wins (fire-and-forget)

#### Option 2: Moza Service (Replaces MozaHotkey)
A background process that owns the SDK, both XOutputRedux and Stream Deck talk to it:
```
XOutputRedux plugin  ──┐
                       ├──► MozaService (named pipe) ──► Moza SDK
Stream Deck plugin   ──┘
```
- Replaces MozaHotkey entirely
- One unified Moza control layer
- Named pipe IPC (pattern already exists in XOutputRedux)
- Could run as tray app or launched on-demand
- More work upfront, but cleaner long-term

#### Option 3: Hybrid (Recommended long-term path)
Start with Option 1 (helper exe). The helper exe code becomes the foundation for Option 2 later — just add a pipe listener and keep it running instead of exiting.

### Input Range UI (Added, may be revisited)
Added MinValue/MaxValue controls to the profile editor for manual axis range configuration. The `InputBinding.TransformValue()` already supported this — just needed UI exposure. This remains useful for non-Moza devices or fine-tuning, even if the helper exe approach solves the Moza calibration issue.

**Files added/changed:**
- `ProfileEditorWindow.xaml` — Input Range panel with Capture Min/Max buttons
- `ProfileEditorWindow.xaml.cs` — Event handlers, live value tracking, capture logic

---

## Session 2026-02-04/05: Moza Axis Auto-Scaling

### Problem

When the Moza helper exe sets `setMotorLimitAngle(270)`, the physical motor stop changes correctly but the steering axis only reads ~25% of its range (reads like 1080° when physically limited to 270°).

### Investigation: Per-Process DirectInput Calibration Cache (DISPROVEN)

**Theory**: DirectInput caches HID descriptor calibration data at first device enumeration. If the SDK changes the descriptor after enumeration, DirectInput keeps using the old range.

**Three attempts to defer DirectInput operations — all failed:**

1. **Deferred `Acquire()` only**: Moved `Acquire()` and axis property setting from constructor to `Start()`. No effect.
2. **Deferred all DI device creation**: Added `_directInputEnabled` flag preventing `RefreshDevices()` until profile start. Even with zero DI operations before rotation change, still wrong.
3. **Deferred `DInput.DirectInput8Create()` COM initialization**: Made `IDirectInput8` creation lazy. Even with NO DirectInput COM objects created before rotation change, still wrong.

**Conclusion**: The per-process calibration cache theory was **wrong**. All three attempts failed because the issue is not about when DirectInput is initialized.

### Root Cause: HID Descriptor Never Changes

Added diagnostic logging to track raw axis values during polling:
- **All axes report native range 0-65535** regardless of rotation setting
- At 270° with ~1080° reference rotation:
  - X axis min at full left: **24324** (normalized: 0.371)
  - X axis max at full right: **41230** (normalized: 0.629)
  - Expected from 270/1080 ratio: 0.375-0.625 (close match within mechanical tolerance)

**The Moza SDK's `setMotorLimitAngle()` only changes the physical motor stop.** The HID descriptor always reports 0-65535 mapped to the **reference rotation** (the rotation at boot / Pit House default). When the target rotation is smaller, the axis only uses a proportional fraction of that range.

### Solution: Software Auto-Scaling

Use the existing `InputBinding.MinValue/MaxValue` transform to remap the partial axis range to full 0.0-1.0 at profile start time.

**Scaling formula:**
```
ratio = targetRotation / referenceRotation
axisMin = 0.5 - (0.5 * ratio)    // e.g., 0.375 for 270/1080
axisMax = 0.5 + (0.5 * ratio)    // e.g., 0.625 for 270/1080
```

The existing `InputBinding.TransformValue()` then maps axisMin→0.0, axisMax→1.0 with clamping.

**Key design decisions:**
- Reference rotation queried from device via `getMotorLimitAngle()` **before** changing it
- Plugin stores the **first-seen** reference rotation across stop/start cycles (same app session)
- App restart clears stored reference — next query gets boot/Pit House default (correct)
- Axis overrides applied in-memory only — saved profile keeps user's manual values

### Changes

**New plugin interface extension:**
- `IXOutputPlugin.cs` — Added `GetAxisRangeOverrides()` default interface method and `AxisRangeOverride` record

**MozaHelper output:**
- `Program.cs` — Queries `getMotorLimitAngle()` before applying settings, outputs `ref-rotation=XXXX` to stdout

**MozaPlugin scaling:**
- `MozaPlugin.cs` — Parses `ref-rotation=` from helper stdout, calculates axisMin/axisMax, implements `GetAxisRangeOverrides()` returning override for X axis (sourceIndex=0) on Moza device (`VID_346E&PID_0006`)

**Runtime application:**
- `MainWindow.xaml.cs` — New `ApplyPluginAxisOverrides()` method called after plugin start and device recreation, before mapping engine starts. Matches devices by hardware ID, sets MinValue/MaxValue on matching bindings.

**Infrastructure:**
- `DirectInputDeviceProvider.cs` — Added `RecreateDevices()` method (dispose all + refresh)
- `InputDeviceManager.cs` — Added `RecreateDirectInputDevices()` wrapper

**Reverted:**
- All deferred-DI changes (lazy IDirectInput8, `_directInputEnabled` flag, `AcquireForPolling()`, diagnostic logging) — these didn't help and added unnecessary complexity
