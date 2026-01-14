# XOutputRenew Stream Deck Plugin

Control XOutputRenew Xbox controller emulator profiles from your Stream Deck.

## Features

- **Start Profile** - Start a specific XOutputRenew profile
- **Stop Profile** - Stop the currently running profile
- **Toggle Profile** - Toggle a profile on/off with visual state indicator

## Requirements

- Stream Deck software 6.9 or higher
- XOutputRenew installed and added to system PATH
- Node.js 20+ (for development only)

## Installation

### From Release

1. Download the `.streamDeckPlugin` file from the releases page
2. Double-click to install in Stream Deck

### From Source

1. Install dependencies:
   ```bash
   cd streamdeck-plugin
   npm install
   ```

2. Build the plugin:
   ```bash
   npm run build
   ```

3. For development with auto-reload:
   ```bash
   npm run watch
   ```

4. To create distributable package:
   ```bash
   npm run pack
   ```

## Actions

### Start Profile

Starts a specific XOutputRenew profile. Configure which profile to start in the action settings.

- Select "Default Profile" to start whichever profile is marked as default in XOutputRenew
- Or select a specific profile from the dropdown

### Stop Profile

Stops the currently running XOutputRenew profile. No configuration needed.

### Toggle Profile

Toggles a specific profile on/off. The button state updates to show whether the profile is currently running.

- Select a profile from the dropdown
- Press to toggle on/off
- Button shows different icon when profile is running

## Troubleshooting

### "XOutputRenew not found" error

Ensure XOutputRenew is installed and added to your system PATH:
1. Open XOutputRenew
2. Go to Options tab
3. Check "Add to System PATH"
4. Restart Stream Deck software

### Profiles not showing in dropdown

Click the "Refresh Profiles" button in the action settings to reload the profile list from XOutputRenew.

## Icon Assets

The plugin requires these icon files in `com.xoutputrenew.sdPlugin/imgs/`:

| File | Size | Description |
|------|------|-------------|
| `plugin-icon.png` | 144x144 | Main plugin icon |
| `plugin-icon@2x.png` | 288x288 | Retina plugin icon |
| `category-icon.png` | 28x28 | Category icon |
| `category-icon@2x.png` | 56x56 | Retina category icon |
| `action-start.png` | 72x72 | Start action icon |
| `action-start@2x.png` | 144x144 | Retina start action icon |
| `action-stop.png` | 72x72 | Stop action icon |
| `action-stop@2x.png` | 144x144 | Retina stop action icon |
| `action-toggle-off.png` | 72x72 | Toggle off state icon |
| `action-toggle-off@2x.png` | 144x144 | Retina toggle off icon |
| `action-toggle-on.png` | 72x72 | Toggle on state icon |
| `action-toggle-on@2x.png` | 144x144 | Retina toggle on icon |

## Development

### Project Structure

```
streamdeck-plugin/
├── com.xoutputrenew.sdPlugin/    # Plugin bundle
│   ├── bin/                      # Compiled JavaScript
│   ├── imgs/                     # Icon assets
│   ├── ui/                       # Property Inspector HTML
│   └── manifest.json             # Plugin manifest
├── src/                          # TypeScript source
│   ├── actions/                  # Action implementations
│   ├── plugin.ts                 # Entry point
│   └── xoutput-cli.ts           # CLI wrapper
├── package.json
├── rollup.config.mjs
└── tsconfig.json
```

### Building

```bash
npm install      # Install dependencies
npm run build    # Build once
npm run watch    # Build with auto-reload
npm run pack     # Create .streamDeckPlugin file
```

## License

MIT - Same as XOutputRenew
