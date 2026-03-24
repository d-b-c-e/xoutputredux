# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/).

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
