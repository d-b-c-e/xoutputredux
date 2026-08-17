# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/).

## [1.3.0] - 2026-08-17

### Changed
- **Isolation profiles now identify kept devices by hardware ID, not by instance path.** 1.2.0 added hardware-identity matching as a *fallback* — it only came into play once an entry's exact path failed to resolve, and the identity was reconstructed on the fly from that same (possibly already stale) path. Identity is now **persisted with the profile** as a first-class field and treated as authoritative, with the exact path kept as a tiebreaker.

  In practice this means a profile keeps working after a device is unplugged, moved to another USB port, or — for XInput pads, whose `IG_` slot can change on its own — simply power-cycled, without you opening the profile to re-tick anything.

  Existing profiles are migrated on load: the identity is derived from the stored path and written back on next save. Profiles written before this release have no recorded preference, so they adopt identity-first matching rather than being pinned to the old behaviour they never chose.

### Added
- **"Match kept devices by hardware ID" option**, per profile, in the isolation profile editor. On by default.

  Turn it off when two devices share a VID/PID and must be told apart — two identical wheels, or the two interfaces an X-Arcade exposes. Hardware identity cannot distinguish those, so identity matching keeps both. That is the safe direction (more devices visible, never fewer) but not always what was intended, which is why it is a choice rather than a hardcoded rule.
- `DeviceIdentity` in `XOutputRedux.Core`, so saved profiles can carry an identity without depending on HidHide being installed. `HidHideDevice.HardwareIdentity` now delegates to it, leaving one implementation.

### Notes
- `DeviceIsolationController.Apply` gains an overload taking `IsolationKeepEntry` (path + persisted identity) and an explicit matching preference. The path-only overload is retained and defaults to identity-first.
- Matching decisions are logged: look for `matching keep-list by hardware identity` when diagnosing why more or fewer devices stayed visible than expected.

## [1.2.0] - 2026-08-16

### Fixed
- **Isolation profiles broke whenever a device was replugged or changed mode.** Profiles store full HID instance paths, which are not stable: a different USB port changes the trailing instance segment, and an XInput pad's `IG_` infix tracks its XInput slot — an X-Arcade panel was observed moving from `IG_00/01/02` to `IG_04/05` purely from cycling its controller mode. The saved profile then matched nothing, so isolation either refused to start ("none of the keep-visible devices are connected") or hid the very device it was meant to keep.

  Exact paths are still tried first. Any keep-list entry that no longer resolves to a present device now falls back to that entry's **hardware identity** — VID/PID plus the `MI_`/`COL` interface qualifiers, with the instance segment, `IG_` and `REV_` stripped. All three path forms a profile may hold are accepted (`HID\...`, `USB\...`, and the `\\?\hid#...#{guid}` symbolic link). Entries with no VID/PID at all (root-enumerated virtual devices such as vJoy) stay on exact matching rather than matching broadly.

  The fallback can only ever keep *more* devices visible, never hide one that should have stayed — the safe direction for a feature whose failure mode is leaving you with no working controller.

### Changed
- **The running profile is now visible in the list.** A profile started over IPC, by the game monitor, or from `-profile` left the selection wherever it happened to be, so the Start/Stop button described a different profile than the status column — it read "Start" while a profile was demonstrably running. The running row is now bold and green regardless of selection, and starting a profile moves the selection onto it. Row background is deliberately left alone so the selection highlight still shows which row an action applies to.

### Notes
- If a keep-list falls back to hardware identity, it is logged: look for `matching those by hardware identity instead` in the log when diagnosing why more devices stayed visible than expected.

## [1.1.0] - 2026-07-27

### Added
- **Device Isolation profiles** — a new profile type that hides every gaming device except a chosen keep-list via HidHide, with no virtual controller created. Native DirectInput and force feedback are preserved, so it suits games that auto-grab the first controller they find rather than letting you pick one.
- **HidHide Manager window** — inspect which devices are currently hidden, the cloak state, and the application whitelist.
- IPC support for starting and stopping isolation profiles, so external tools (launchers, LaunchBox) can apply isolation before a game starts.

### Fixed
- **Isolation hid almost nothing.** Devices were blacklisted by their USB *container* path, but HidHide's filter attaches to the HID device — a container path is silently accepted and hides nothing. In practice only devices with an empty container path (vJoy and other root/virtual devices) were ever actually hidden. Now hides by the HID device instance path; keep-list matching still accepts the container path or symbolic link so existing profiles keep working.
- Isolation no longer applies when the keep-visible device is not connected, which previously hid every device and left no usable controller.
- Root/virtual devices reporting an empty-string base container resolved to an unusable empty path instead of falling back to the device instance path.
- **Tray tooltip could stick to the top-left of the screen.** Assigning `ToolTipText` makes Hardcodet rebuild its internal ToolTip; if a popup was open it was orphaned, staying visible and topmost with no tray icon to anchor to — so it rendered at (0,0), on top of fullscreen games.
- Cloak-state parsing.

### Notes
- Isolation must be applied **before** the game launches. The built-in game monitor is reactive (~3s poll), and hiding devices a game has already acquired can crash it.

## [1.0.5] - 2026-03-29

### Fixed
- "Default Profile" checkbox in profile editor not reflecting actual default status — `MappingProfile.Clone()` was not copying `IsDefault`, so the editor always showed it unchecked

## [1.0.4] - 2026-03-28

### Added
- IPC `list-profiles` command — returns available profile names over the named pipe, so external tools (e.g., LaunchBox plugin) don't need to scan profile files on disk
- IPC `get-default` command — returns the name of the default profile
- IPC `start` now resolves the default profile when no profile name is specified (previously returned an error)

## [1.0.3] - 2026-03-24

### Fixed
- **Memory leak:** RawInputDevice event handler accumulation — `_inputReceiver.Received` handlers piled up across Start/Stop cycles due to conditional unsubscribe. Now always unsubscribes before subscribing and unconditionally in Stop().
- **Memory leak:** Dispatcher.BeginInvoke closure flooding in ProfileEditorWindow — Monitor and Listen handlers were queuing ~1,000 closures/sec/device. Added debounce so only one callback is queued at a time.
- **Memory leak:** AppLogger ConcurrentQueue unbounded growth — log queue had no max size, accumulating hundreds of MB of strings. Capped at 10,000 entries with overflow drop counting.
- **GC pressure:** ProcessReports hot-path dictionary allocation — Dictionary was created on every call (~86M/day/device). Now reuses a single instance field.

## [1.0.2] - 2026-03-22

### Added
- "Set as Default" option in Profiles right-click context menu (toggles default on/off)

### Fixed
- Default profile checkbox in editor not saving — IsDefault was not copied back to the original profile on save
- New binding properties (deadzones, digital direction) not persisted when saving from the profile editor

## [1.0.1] - 2026-03-22

### Changed
- Added default profile indicator (star) to the Profiles grid
- Removed unused Description column from Profiles grid, widened Name column

## [1.0.0] - 2026-03-22

### Added
- Per-binding inner/outer deadzones (0.0–0.49) — eliminate drift near center or snap to full deflection near edges, with visual deadzone regions in curve preview
- Visual-only Preview mode in profile editor — test mappings in real-time without creating a ViGEm controller, editor stays editable during preview
- Digital-to-axis mapping — map HAT switches and D-pad buttons to analog stick axes with per-binding direction (Positive/Negative), auto-defaults when capturing buttons on axis outputs
- Diagnostic logging for axis evaluation in Preview mode (OutputMapping.DiagnosticLogging)

### Changed
- Upgraded from .NET 8.0 to .NET 10.0 (all projects, CI workflows, installer)
- "Start with Windows" now uses a Scheduled Task (ONLOGON trigger) instead of the Run registry key — reliable with Windows Fast Startup
- Device Refresh button now fully recreates DirectInput device handles, supporting hot-swapped wheels without restarting the app
- Profile schema v2 → v4 (backward-compatible: v3 adds deadzones, v4 adds digital direction)
- Removed unnecessary `Microsoft.Win32.Registry` NuGet package (now in-box with .NET 10)

### Fixed
- "Start with Windows" not working after Shut Down (only worked after Restart) — caused by Windows Fast Startup skipping Run key items
- Crash (InvalidOperationException) when concurrent dictionary access in profile editor input monitoring — switched to ConcurrentDictionary
- Preview mode stopping device polling, breaking input capture in the profile editor after stopping preview

## [0.9.7-alpha] - 2026-03-14

### Added
- Moza FFB Tier 1: Natural Friction, Speed Damping Start Point, and Hands Off Protection SDK settings in the Moza plugin
- Moza FFB Tier 2: ETSine rumble translation — converts Xbox rumble motor data to Moza SDK's native periodic vibration effect via `IForceFeedbackHandler` plugin interface
- Moza FFB Tier 3: Ambient persistent effects (Spring, Friction, Damper) that layer on top of game FFB for baseline wheel feel
- Portable mode release ZIP with `portable.txt` marker and empty `data\` folder
- Portable mode UI guards: "Start with Windows" disabled, update dialog opens release page instead of downloading installer

### Removed
- Code signing plans — SignPath Foundation application declined (2026-03-09, insufficient project history)
- `CODESIGNING.md` and code signing references from README

## [0.9.6-alpha] - 2026-02-08

### Added
- Test tab in Profile Editor with Start/Stop toggle button and live Xbox controller visualization — test mappings without leaving the editor
- Reusable `XboxControllerTestView` UserControl shared by main window and profile editor (auto-scales via Viewbox, compact data panel)
- Per-axis sensitivity/response curves — power/gamma curve (0.1–5.0) with visual curve preview in profile editor
- Collapsible "Advanced Settings" section in profile editor for Input Range and Axis Tuning (less intimidating for basic users)
- Profile schema v2 with backward-compatible migration (legacy profiles load with default sensitivity)
- 12 new unit tests for response curve math and schema migration

### Fixed
- Update check dialog appearing on local development builds — now skipped automatically
- HidHide device list text barely readable in dark mode — checkboxes no longer dimmed by disabled parent
- Release workflow using auto-generated changelog links instead of actual CHANGELOG.md content

### Changed
- Test tab is now the second tab in the profile editor (after Mapping, before Force Feedback)
- Main window Test tab refactored to use shared `XboxControllerTestView` UserControl
- Binding Settings panel in profile editor now scrollable to accommodate new Axis Tuning section
- Input Range and Axis Tuning grouped under collapsible Advanced Settings expander

## [0.9.3-alpha] - 2026-02-05

### Fixed
- Startup update checker not retrying after a failed check (e.g. 404 when repo was private) — now only records check timestamp on success

### Changed
- CI release workflow now builds and uploads Moza plugin and Stream Deck plugin as standalone downloads
- Release script includes MozaHelper.exe in the Moza plugin package

## [0.9.2-alpha] - 2026-02-05

### Added
- Plugin system for device-specific features loaded from `plugins/` folder
- Moza Wheel plugin with per-profile settings (rotation, FFB strength, max torque, damping, center spring, natural inertia, speed damping, FFB reverse)
- Out-of-process Moza SDK helper (`MozaHelper.exe`) that keeps SDK alive for persistent settings
- Steering axis auto-scaling when Moza rotation differs from device reference
- Input Range UI for per-axis min/max configuration in profile editor
- Tooltips for all Moza plugin settings

### Fixed
- "Start with Windows" not launching the app — now writes to both `Run` and `StartupApproved` registry keys
- Moza ref-rotation query returning 0 after SDK cleanup — added retry loop (up to 5 attempts) for rotation sync
- Stale `_firstSeenRefRotation` persisting across profile start/stop cycles

### Changed
- App icon and branding updates

## [0.9.1-alpha] - 2026-01-26

### Added
- Auto-incrementing build numbers (YYDDDHHmm format)
- Global hotkey (Ctrl+Shift+G) to quickly add focused game to running profile
- Double-click support on system tray icon to restore window
- XOutputRedux.HidSharper — forked and slimmed HidSharp library (Windows-only)

### Fixed
- Stream Deck plugin filename mismatch (`com.xoutputredux.` prefix)
- Stream Deck plugin not included in installer/portable ZIP
- HidHide interfering with SDL2 inputs — driver now starts/stops with profile lifecycle
- Stuck processes after crashes — added robust cleanup
- Crash when double-clicking tray icon while window is closing
- Installer "Run XOutputRedux" causing UAC elevation error
- OverflowException in HID input receiver
- RawInput parse error log spam — throttled to once per report ID

### Changed
- Rebranded from "XOutput Renew" to "XOutput Redux" with new logo
- Migrated from SharpDX.DirectInput to Vortice.DirectInput 3.8.2
- GitHub repository renamed to `xoutputredux`
- Moved update checker from Options tab to About tab
- Improved update checker error handling for private repos
- Removed dead POSIX/Linux/macOS code from HidSharper
- Fixed nullable reference type warnings in HidSharper
- Removed obsolete APIs and fixed volatile+lock anti-pattern in HidSharper
- Cached event handles in WinHidStream for better I/O performance

## [0.8.4-alpha] - 2026-01-18

### Fixed
- Debug logging added to update version comparison

## [0.8.3-alpha] - 2026-01-17

### Added
- About tab with version info and links to GitHub
- Installer auto-closes running app before upgrade

## [0.8.2-alpha] - 2026-01-17

### Added
- Portable mode — create `portable.txt` next to exe to store settings in `data/` subfolder
- Admin installer option for system-wide installation

### Fixed
- Console window appearing alongside GUI
- Options tab scrollbar for content overflow

## [0.8.1-alpha] - 2026-01-17

### Added
- Backup/restore settings feature — export/import all settings via `.xorbackup` files
- Crash reporting with one-click GitHub issue creation

### Fixed
- Invisible tray icon when starting with `--minimized`

## [0.8.0-alpha] - 2026-01-13

### Added
- ViGEmBus driver detection with auto-install prompt
- Headless mode (`XOutputRedux headless <profile>`) for running without GUI
- Game monitoring support in headless mode
- Game monitoring CLI commands (`monitor on/off`)
- Toast notification toggle in Options
- Stream Deck plugin (C#) with profile toggle, monitoring toggle, and launch actions
- Auto-update checker (Phase 9)
- Schema versioning for all JSON configuration files with migration tests

### Changed
- Replaced portable mode roadmap item with Chocolatey package
- Skip automatic update check in debug builds

## [0.7.0-alpha] - 2026-01-12

### Added
- Initial release with full feature set
- DirectInput and RawInput device support
- ViGEm Xbox 360 controller emulation with OR-logic mapping
- Interactive "press to map" profile editor with double-click capture
- Force feedback routing from games to physical devices
- HidHide integration for device hiding with whitelist management
- WPF GUI with Devices, Profiles, Status, Options, and Test tabs
- System tray integration with minimize/restore
- Dark mode UI theme
- CLI commands (`start`, `stop`, `status`, `list-devices`, `list-profiles`)
- IPC via named pipes for external control
- Toast notifications for profile start/stop
- Game auto-profile — automatically start profiles when games launch
- Steam game browser with smart executable detection
- VID/PID-based device identification for stable IDs across USB port changes
- Device renaming and info display
- Verbose logging for debugging
- Release infrastructure with Inno Setup installer
