# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/).

## [0.9.5-alpha] - 2026-02-07

### Added
- Per-axis sensitivity/response curves — power/gamma curve (0.1–5.0) with visual curve preview in profile editor
- Collapsible "Advanced Settings" section in profile editor for Input Range and Axis Tuning (less intimidating for basic users)
- Profile schema v2 with backward-compatible migration (legacy profiles load with default sensitivity)
- 12 new unit tests for response curve math and schema migration

### Fixed
- Update check dialog appearing on local development builds — now skipped automatically
- HidHide device list text barely readable in dark mode — checkboxes no longer dimmed by disabled parent
- Release workflow using auto-generated changelog links instead of actual CHANGELOG.md content

### Changed
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
