# XOutputRedux

Streamlined Xbox controller emulator for Windows. Maps inputs from multiple gaming devices (steering wheels, joysticks, gamepads) to a single emulated Xbox 360 controller.

## Features

- **Multi-input to single output** - Map multiple physical buttons to the same Xbox button using OR logic (e.g., wheel B button + handbrake = Xbox B)
- **DirectInput & RawInput support** - Works with virtually any gaming controller
- **Force Feedback** - Route rumble/vibration from games to physical devices (steering wheels, gamepads with FFB)
- **Device Hiding** - HidHide integration to hide physical controllers from games, preventing double-input issues
- **Game Auto-Profile** - Automatically start profiles when specific games launch, stop when they exit
- **Profile management** - Create, duplicate, and switch profiles easily
- **Interactive mapping** - "Press button to map" configuration interface
- **System tray integration** - Minimize to tray, run in background
- **CLI support** - Command-line interface for scripting and automation
- **Toast notifications** - Windows notifications when profiles start/stop or games are detected
- **Backup/restore** - Export and import all settings via `.xorbackup` files
- **Crash reporting** - One-click GitHub issue creation with diagnostic info
- **Portable mode** - Create `portable.txt` next to exe to store settings locally

## Requirements

- Windows 10/11
- .NET 8.0 Runtime
- [ViGEmBus driver](https://github.com/nefarius/ViGEmBus/releases) (required for Xbox controller emulation)
- [HidHide driver](https://github.com/nefarius/HidHide/releases) (optional, for device hiding)

## Installation

### Download
Download the latest release from the [Releases page](https://github.com/d-b-c-e/xoutputredux/releases):
- **Setup installer** (`XOutputRedux-x.x.x-Setup.exe`) - Recommended, includes options for PATH and startup
- **Portable ZIP** (`XOutputRedux-x.x.x-Portable.zip`) - Extract and run, no installation needed

### Prerequisites
1. Install the [ViGEmBus driver](https://github.com/nefarius/ViGEmBus/releases) (required)
2. Optionally install [HidHide](https://github.com/nefarius/HidHide/releases) for device hiding (XOutputRedux will prompt to install if missing)

## Usage

### GUI Mode

Simply run `XOutputRedux.exe` to open the graphical interface.

1. **Devices tab** - View all detected input devices with "Listen for Input" highlighting
2. **Profiles tab** - Create and manage mapping profiles
3. **Games tab** - Configure game-to-profile associations for auto-start
4. **Status tab** - Check driver status and active emulation
5. **Options tab** - Configure startup behavior and settings
6. **Test tab** - Visual Xbox controller showing real-time output state
7. **About tab** - Version info and links to GitHub

### Creating a Profile

1. Click "New Profile" and enter a name
2. Double-click the profile to edit (or right-click → Edit)
3. In the profile editor:
   - Double-click an Xbox output to start capturing, or select and click "Capture Input"
   - Press the button or move the axis you want to map
   - Add multiple inputs to the same output for OR logic
   - Configure Force Feedback routing in the "Force Feedback" tab
   - Set up device hiding in the "Device Hiding" tab
4. Click "Save" to save your mappings

**Tip**: If a profile is running, you can still view its mappings (read-only mode).

### Running a Profile

1. Select a profile from the list
2. Click "Start" to begin emulation
3. The app will minimize to system tray and continue running
4. Click "Stop" or right-click the tray icon to stop

### CLI Commands

```bash
# Start the default profile (launches GUI if not running)
XOutputRedux start

# Start a specific profile
XOutputRedux start "My Profile"

# Stop the running profile
XOutputRedux stop

# Check status of running instance
XOutputRedux status [--json]

# Enable/disable game monitoring
XOutputRedux monitor on
XOutputRedux monitor off

# List detected input devices
XOutputRedux list-devices [--json]

# List available profiles
XOutputRedux list-profiles [--json]

# Show help
XOutputRedux help
```

### Stream Deck Integration

XOutputRedux includes a native Stream Deck plugin for controlling profiles directly from your Stream Deck.

**Installation:**
1. Download `com.xoutputredux.streamDeckPlugin` from the [Releases page](https://github.com/d-b-c-e/xoutputredux/releases)
2. Double-click to install

**Available Actions:**
- **Start/Stop Profile** - Toggle a specific profile on/off (select from dropdown)
- **Game Monitor** - Toggle game auto-profile monitoring on/off
- **Launch App** - Open the XOutputRedux GUI

The plugin communicates directly with the running GUI via IPC. If the GUI isn't running when you press an action, it will start automatically (minimized to tray).

### Startup Options

```bash
# Start with a specific profile
XOutputRedux --start-profile "MyProfile"

# Start minimized to system tray
XOutputRedux --minimized

# Combine options
XOutputRedux --start-profile "MyProfile" --minimized
```

## Building from Source

```powershell
# Clone the repository
git clone https://github.com/d-b-c-e/xoutputredux.git
cd xoutputredux

# Build
dotnet build -c Release

# Run
dotnet run --project src/XOutputRedux.App

# Run tests
dotnet test

# Create release (requires Inno Setup 6)
.\release.ps1
```

## Project Structure

```
XOutputRedux/
├── src/
│   ├── XOutputRedux.Core/           # Core abstractions, config, interfaces
│   ├── XOutputRedux.Input/          # DirectInput, RawInput device handling
│   ├── XOutputRedux.Emulation/      # ViGEm Xbox controller emulation
│   ├── XOutputRedux.HidHide/        # HidHide integration
│   ├── XOutputRedux.App/            # WPF GUI + CLI application
│   │   └── Assets/                  # Icons, banners, branding assets
│   └── XOutputRedux.StreamDeck/     # Stream Deck plugin
└── tests/
    └── XOutputRedux.Tests/
```

## Current Status

**v0.8.4-alpha** - Improved update checker error handling.

### What's Working
- Device detection (DirectInput and RawInput/HID devices)
- **Stable device IDs** - Devices maintain same ID across USB port changes (VID/PID-based)
- Profile creation and editing with interactive "press to map" capture
- **Double-click to capture** - Double-click any output to start input capture
- Real-time input monitoring with output highlighting in profile editor
- Multi-input OR logic (multiple buttons can trigger same Xbox output)
- ViGEm integration for Xbox 360 controller emulation
- Force feedback routing from games to physical devices
- HidHide integration (auto-hide devices, auto-install prompt)
- **Application whitelist management** - Add apps like Moza Pit House to see hidden devices
- System tray with minimize/restore
- Device renaming and info display
- Verbose logging for debugging device issues
- Dark mode UI theme with dark tooltips and dialogs
- **Synthwave branding** - New logo and gradient color scheme
- Visual Xbox controller test display
- Options for startup profile and "Start with Windows"
- Profile editor with tabbed interface (Mapping, Force Feedback, Device Hiding)
- **Read-only profile view** when profile is running
- **Toast notifications** - Windows notifications when profiles start/stop
- **CLI/IPC** - Control running instance from command line (`start`, `stop`, `status`, `monitor on/off`)
- **Default profile** - Set a profile as default for quick CLI start
- **Add to System PATH** - Option to add XOutputRedux to PATH for CLI access from anywhere
- **Game auto-profile** - Automatically start/stop profiles when games launch/exit
- **Steam game browser** - Browse installed Steam games with smart executable detection
- **Stream Deck plugin** - Native plugin with profile toggle, monitoring toggle, and launch actions
- **ViGEmBus auto-install** - Prompt to download and install if missing
- **Auto-update** - Check for updates on startup with in-app download and install
- **Crash reporting** - Detailed crash dialog with one-click GitHub issue creation
- **Backup/restore settings** - Export all settings and profiles to `.xorbackup` file for easy migration
- **Portable mode** - Create `portable.txt` next to exe to store all data in `data\` subfolder
- **About tab** - Version info, GitHub links, and acknowledgments

### Devices Tested
- MOZA R12 steering wheel base (DirectInput)
- Turtle Beach VelocityOne Multi-Shift gear shifter (RawInput)
- X-Arcade dual joystick (RawInput)

### Coming Soon
- **Chocolatey package** - Easy install/update via `choco install xoutputredux`

## Background

XOutputRedux is a modernized reimplementation inspired by the archived [XOutput](https://github.com/csutorasa/XOutput) project. It focuses on:

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
- [Vortice.Windows](https://github.com/amerkoleci/Vortice.Windows) - DirectInput access
- [HidSharp](https://github.com/IntergatedCircuits/HidSharp) - HID device access
