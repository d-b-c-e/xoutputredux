# XOutputRenew Testing Plan

## Device Detection
- [x] Refresh devices - do all your controllers appear?
- [ ] Verify device names are recognizable
  Feedback:  I see two devices listed that I don't know what they are.  "Controller (TS-UFB01B-X)" - it's listed twice actually with the same Hardware ID, so I suspect maybe it listing twice is a bug, but otherwise it would be nice to know what it is.  It might be my two Stream Decks.  Can we add a right-click context menu on the device to Copy info about it for debugging purposes, and what about a "Rename" function so we can give it a friendly name once we figure out what it is?  Secondly, can we put a checkbox on this tab that says "Listen for input" and if these devices are registering any input, it highlights that row?  That was a nice feature from Xoutput I liked, so you could press a button on a device and it would highlight, telling you what the device actually was.  
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
.\XOutputRenew.App.exe list-devices
.\XOutputRenew.App.exe list-devices --json
.\XOutputRenew.App.exe list-profiles
.\XOutputRenew.App.exe list-profiles --json
.\XOutputRenew.App.exe --start-profile=YourProfileName
.\XOutputRenew.App.exe --start-profile=YourProfileName --minimized
```

## Edge Cases
- [ ] Start a profile with no mappings - does it handle gracefully?
- [ ] Edit a running profile - should be blocked with message
- [ ] Delete a running profile - should be blocked with message
- [ ] Start app with ViGEm not installed - does it show clear error?

## Notes
Keep notes on anything that feels wrong, confusing, or broken below:

---

