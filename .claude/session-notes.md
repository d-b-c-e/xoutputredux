# Session Notes
<!-- Written by /wrapup. Read by /catchup at the start of the next session. -->
<!-- Overwritten each session — history preserved in git log of this file. -->

- **Date:** 2026-03-22
- **Branch:** main

## What Was Done
- Upgraded all 10 projects from .NET 8.0 to .NET 10.0, updated CI workflows, removed in-box `Microsoft.Win32.Registry` NuGet ref
- Fixed "Start with Windows" — replaced Run registry key with Scheduled Task (ONLOGON trigger) for Fast Startup reliability. Updated `AppSettings.cs` and `installer/XOutputRedux.iss`
- Added per-binding inner/outer deadzone (0.0–0.49) in `InputBinding.cs`, with UI sliders in profile editor and visual deadzone regions in curve preview
- Added visual-only Preview mode in profile editor Test tab — runs MappingEngine without ViGEm controller, editor stays editable
- Added digital-to-axis mapping (`DigitalAxisDirection` enum) — HAT/button inputs can push axes to Positive/Negative direction. Auto-defaults when capturing buttons on axis outputs
- Fixed device Refresh button to call `RecreateDirectInputDevices()` for hot-swapped wheels
- Fixed thread-safety crash in `ProfileEditorWindow._latestInputValues` — switched to `ConcurrentDictionary`
- Fixed `StopPreview` killing device polling (broke input capture after stopping preview)
- Fixed digital axis evaluation: digital bindings always override analog on the same axis
- Added 19 new unit tests (deadzones, digital axis, clone, roundtrip) — 61 total
- Added `OutputMapping.DiagnosticLogging` for axis evaluation debugging during preview
- Created `windows-fast-startup-run-key` skill for the Run key / Fast Startup diagnostic pattern

## Decisions Made
- Scheduled Task over Run key: Run key is unreliable with Windows Fast Startup (hybrid shutdown), Scheduled Task with ONLOGON trigger fires on every logon regardless of shutdown type
- Digital bindings always override analog on same axis: prevents analog noise from a steering axis overriding digital HAT center position
- Auto-default DigitalDirection to Positive when capturing button on axis output: saves the user from having to manually set it every time
- Preview mode doesn't stop devices on stop: the editor may still need them for capture/monitoring

## Open Items
- [ ] Tab background flicker in dark mode (cosmetic, low priority)
- [ ] HidSharp parse errors for certain HID report IDs (no impact on functionality)
- [ ] CI release workflow doesn't produce a portable ZIP
- [ ] Diagnostic logging left enabled in preview mode (consider making it user-toggleable in Options)

## Next Steps
1. Version bump and release (0.9.8-alpha) with all session changes
2. Real-world testing of digital-to-axis mapping with games
3. Real-world testing of Moza FFB enhancements (ETSine rumble, ambient effects)

## Context for Next Session
Major feature session: .NET 10 upgrade, deadzones, preview mode, and digital-to-axis mapping
all implemented and tested. The Scheduled Task fix for Start with Windows is already applied
to the user's machine. Profile schema is now at v4 (backward-compatible). All 61 tests pass.
The user has a Moza R12 base with CS Pro wheel (previously ESX) and uses HAT buttons mapped
to RightStickX/Y via the new digital direction feature.
