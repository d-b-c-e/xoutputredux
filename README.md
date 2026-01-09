# XOutputRenew

Streamlined Xbox controller emulator for Windows. Maps inputs from multiple gaming devices (steering wheels, joysticks, gamepads) to a single emulated Xbox 360 controller.

## Features

- **Multi-input to single output** - Map multiple physical buttons to the same Xbox button using OR logic (e.g., wheel B button + handbrake = Xbox B)
- **DirectInput & RawInput support** - Works with virtually any gaming controller
- **Profile management** - Create, duplicate, and switch profiles easily
- **Interactive mapping** - "Press button to map" configuration interface
- **System tray integration** - Minimize to tray, run in background
- **CLI support** - Command-line interface for scripting and automation

## Requirements

- Windows 10/11
- .NET 8.0 Runtime
- [ViGEmBus driver](https://github.com/nefarius/ViGEmBus/releases) (required for Xbox controller emulation)
- [HidHide driver](https://github.com/nefarius/HidHide/releases) (optional, for device hiding)

## Installation

1. Install the [ViGEmBus driver](https://github.com/nefarius/ViGEmBus/releases)
2. Optionally install [HidHide](https://github.com/nefarius/HidHide/releases) if you want to hide your physical controllers from games
3. Download and run XOutputRenew

## Usage

### GUI Mode

Simply run `XOutputRenew.App.exe` to open the graphical interface.

1. **Devices tab** - View all detected input devices
2. **Profiles tab** - Create and manage mapping profiles
3. **Status tab** - Check driver status and active emulation

### Creating a Profile

1. Click "New Profile" and enter a name
2. Double-click the profile or select it and click "Edit"
3. In the profile editor:
   - Select an Xbox output (button/axis/trigger) from the list
   - Click "Start Monitoring" to see real-time input activity
   - Click "Capture Input" then press the button/move the axis you want to map
   - Add multiple inputs to the same output for OR logic
4. Click "Save" to save your mappings

### Running a Profile

1. Select a profile from the list
2. Click "Start" to begin emulation
3. The app will minimize to system tray and continue running
4. Click "Stop" or right-click the tray icon to stop

### CLI Commands

```bash
# List detected input devices
XOutputRenew.App list-devices [--json]

# List available profiles
XOutputRenew.App list-profiles [--json]

# Duplicate a profile
XOutputRenew.App duplicate-profile <source-name> <new-name>
```

### Startup Options

```bash
# Start with a specific profile
XOutputRenew.App --start-profile=MyProfile

# Start minimized to system tray
XOutputRenew.App --minimized

# Combine options
XOutputRenew.App --start-profile=MyProfile --minimized
```

## Building from Source

```powershell
# Clone the repository
git clone https://github.com/your-username/xoutputrenew.git
cd xoutputrenew

# Build
dotnet build -c Release

# Run
dotnet run --project src/XOutputRenew.App

# Run tests
dotnet test
```

## Project Structure

```
XOutputRenew/
├── src/
│   ├── XOutputRenew.Core/           # Core abstractions, config, interfaces
│   ├── XOutputRenew.Input/          # DirectInput, RawInput device handling
│   ├── XOutputRenew.Emulation/      # ViGEm Xbox controller emulation
│   ├── XOutputRenew.HidHide/        # HidHide integration
│   └── XOutputRenew.App/            # WPF GUI + CLI application
└── tests/
    └── XOutputRenew.Tests/
```

## Current Status

**In Development** - Core functionality is working, active testing in progress.

### What's Working
- Device detection (DirectInput and RawInput/HID devices)
- Profile creation and editing with interactive "press to map" capture
- Real-time input monitoring with device highlighting
- Multi-input OR logic (multiple buttons can trigger same Xbox output)
- ViGEm integration for Xbox 360 controller emulation
- System tray with minimize/restore
- Device renaming and info display
- Verbose logging for debugging device issues

### Devices Tested
- MOZA R12 steering wheel base (DirectInput)
- Turtle Beach VelocityOne Multi-Shift gear shifter (RawInput)

### Still In Progress
- HidHide integration (auto-hide devices during emulation)
- Full CLI/IPC for controlling running instance
- Extended in-game testing

## Background

XOutputRenew is a modernized reimplementation inspired by the archived [XOutput](https://github.com/csutorasa/XOutput) project. It focuses on:

- Simplified architecture (no web server, no DS4 support)
- Modern .NET 8.0 codebase
- OR logic for input mapping (multiple inputs can trigger one output)
- Clean WPF interface with system tray support

## License

MIT License

## Acknowledgments

- [XOutput](https://github.com/csutorasa/XOutput) - Original inspiration
- [ViGEmBus](https://github.com/nefarius/ViGEmBus) - Virtual gamepad driver
- [HidHide](https://github.com/nefarius/HidHide) - Device hiding driver
- [SharpDX](https://github.com/sharpdx/SharpDX) - DirectInput access
- [HidSharp](https://github.com/IntergatedCircuits/HidSharp) - HID device access
