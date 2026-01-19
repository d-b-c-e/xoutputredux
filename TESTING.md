# XOutputRedux Testing Plan

## Device Detection
- [x] Refresh devices - do all your controllers appear?
- [x] Verify device names are recognizable
- [x] Unplug a device, refresh - does it disappear?
- [x] Plug it back in, refresh - does it reappear?

## Profile Management
- [x] Create a new profile
- [x] Duplicate a profile - does the copy have all the same mappings?
- [ ] Delete a profile
- [x] Verify profiles persist after closing and reopening the app

## Interactive Mapping (Profile Editor)
- [x] Double-click a profile to edit
- [x] Start input monitoring - do you see real-time values?
- [x] Select a button output (e.g., A), capture a button press
- [x] Select an axis output (e.g., LeftStickX), capture an axis movement
- [x] Select a trigger output, capture a trigger/pedal
- [x] Add a second binding to the same output (test OR logic)
- [x] Remove a binding
- [ ] Test invert checkbox - does it work correctly?
- [ ] Let capture timeout (10 seconds) - does it handle gracefully?
- [ ] Cancel capture mid-way
- [x] Save and verify mappings persist

## Emulation Testing
- [ ] Start a profile with mappings
- [ ] Open a game or [Gamepad Tester](https://hardwaretester.com/gamepad) in browser
- [ ] Verify button presses register on the virtual Xbox controller
- [ ] Verify axes move correctly
- [ ] Verify triggers work
- [ ] Test OR logic - do both mapped inputs trigger the same output?
- [ ] Stop the profile - does the virtual controller disconnect?

## System Tray
- [ ] Minimize window - does it go to tray?
- [ ] Click tray icon context menu "Show" - does window restore?
- [ ] Close window (X button) - does it minimize to tray instead of exiting?
- [ ] Tray "Exit" - does app fully close?

## CLI Commands
```powershell
# Run from the build output directory
.\XOutputRedux.App.exe list-devices
.\XOutputRedux.App.exe list-devices --json
.\XOutputRedux.App.exe list-profiles
.\XOutputRedux.App.exe list-profiles --json
.\XOutputRedux.App.exe --start-profile=YourProfileName
.\XOutputRedux.App.exe --start-profile=YourProfileName --minimized
```

## Force Feedback (FFB)
- [ ] Open Profile Editor - does FFB section appear at bottom of right panel?
- [ ] FFB Device dropdown shows only FFB-capable devices (DirectInput devices with FFB support)
- [ ] Enable FFB checkbox enables/disables the other controls
- [ ] Select a target device from dropdown
- [ ] Select motor mode (Large/Small/Combined/Swap)
- [ ] Adjust gain slider (0-200%)
- [ ] Save profile and reopen - do FFB settings persist?
- [ ] Start profile with FFB enabled - run a game with rumble
- [ ] Verify physical device receives force feedback
- [ ] Test different gain values - does intensity change?
- [ ] Stop profile - does FFB stop immediately?

## Edge Cases
- [ ] Start a profile with no mappings - does it handle gracefully?
- [ ] Edit a running profile - should be blocked with message
- [ ] Delete a running profile - should be blocked with message
- [ ] Start app with ViGEm not installed - does it show clear error?
- [ ] Start profile with FFB device disconnected - does it handle gracefully?

## Notes
Keep notes on anything that feels wrong, confusing, or broken below:

---

