# Screenshot Capture for Claude Code

This folder contains tools for capturing screenshots of the XOutputRedux application during development sessions.

## Usage

### PowerShell Script

```powershell
# Capture XOutputRedux main window
.\.claude\.screenshots\capture-window.ps1

# Capture a specific window (e.g., profile editor)
.\.claude\.screenshots\capture-window.ps1 -WindowTitle "Edit Profile"

# Save to specific path
.\.claude\.screenshots\capture-window.ps1 -OutputPath "C:\temp\screenshot.png"
```

### From Claude Code

Claude can take screenshots using:
```bash
powershell -ExecutionPolicy Bypass -File .claude/.screenshots/capture-window.ps1
```

Then read the resulting PNG file to see the application state.

## Screenshots

Screenshots are saved with timestamps: `screenshot-YYYYMMDD-HHMMSS.png`

These files are gitignored and should not be committed.
