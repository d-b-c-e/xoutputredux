# XOutputRenew Stream Deck Plugin

Control XOutputRenew Xbox controller emulator profiles from your Stream Deck.

## Features

### Actions (Keypad)

| Action | Description |
|--------|-------------|
| **Start Profile** | Start a specific XOutputRenew profile |
| **Stop Profile** | Stop the currently running profile |
| **Toggle Profile** | Toggle a profile on/off with visual state indicator |
| **Start Monitoring** | Start game monitoring (auto-switch profiles) |
| **Stop Monitoring** | Stop game monitoring |
| **Toggle Monitoring** | Toggle game monitoring on/off |
| **Launch App** | Launch the XOutputRenew application |

### Encoder Actions (Stream Deck+)

| Action | Description |
|--------|-------------|
| **Profile Dial** | Rotate to browse profiles, press to toggle selected profile |

## Requirements

- Stream Deck software 6.9 or higher
- XOutputRenew installed and added to system PATH
- Node.js 20+ (for development only)

## Installation

### From XOutputRenew App

1. Open XOutputRenew
2. Go to **Options** tab
3. Click **Install Stream Deck Plugin**

### From Release

1. Download the `.streamDeckPlugin` file from the releases page
2. Double-click to install in Stream Deck

### From Source

```bash
cd streamdeck-plugin
npm install
npm run build
.\scripts\build-plugin.ps1
```

The packaged plugin will be in the `dist` folder.

## Actions

### Start Profile

Starts a specific XOutputRenew profile. Configure which profile to start in the action settings.

- Select "Default Profile" to start whichever profile is marked as default
- Or select a specific profile from the dropdown

### Stop Profile

Stops the currently running XOutputRenew profile. No configuration needed.

### Toggle Profile

Toggles a specific profile on/off. The button state updates to show whether the profile is currently running.

### Start/Stop/Toggle Monitoring

Controls the game monitoring feature which automatically switches profiles when games are detected.

### Launch App

Opens the XOutputRenew application window.

### Profile Dial (Stream Deck+ only)

Use the dial to browse through profiles:
- **Rotate**: Browse profiles
- **Press**: Toggle the selected profile on/off
- **Touch**: Refresh profile list

The LCD strip shows the current profile name and status.

## Troubleshooting

### "XOutputRenew not found" error

Ensure XOutputRenew is installed and added to your system PATH:
1. Open XOutputRenew
2. Go to Options tab
3. Check "Add to System PATH"
4. Restart Stream Deck software

### Profiles not showing in dropdown

Click the "Refresh Profiles" button in the action settings to reload the profile list.

### Plugin doesn't appear in Stream Deck

1. Ensure Stream Deck software is version 6.9 or higher
2. Try restarting the Stream Deck software
3. Check Windows Event Viewer for errors

## Development

### Project Structure

```
streamdeck-plugin/
├── com.xoutputrenew.sdPlugin/    # Plugin bundle
│   ├── bin/                      # Compiled JavaScript
│   ├── imgs/                     # Icon assets
│   ├── ui/                       # Property Inspector HTML
│   └── manifest.json             # Plugin manifest
├── scripts/
│   ├── generate-icons.ps1        # Generate icon assets
│   └── build-plugin.ps1          # Build .streamDeckPlugin package
├── src/                          # TypeScript source
│   ├── actions/                  # Action implementations
│   ├── plugin.ts                 # Entry point
│   └── xoutput-cli.ts            # CLI wrapper
├── package.json
├── rollup.config.mjs
└── tsconfig.json
```

### Building

```bash
npm install           # Install dependencies
npm run build         # Build once
npm run watch         # Build with auto-reload
.\scripts\build-plugin.ps1   # Create .streamDeckPlugin file
```

### Generating Icons

```powershell
.\scripts\generate-icons.ps1
```

This creates all required icons in `com.xoutputrenew.sdPlugin/imgs/`.

## License

MIT - Same as XOutputRenew
