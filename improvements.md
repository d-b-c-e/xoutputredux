# XOutputRedux IPC Improvements

## Context

XOutputRedux has a named pipe IPC server (`XOutputRedux_IPC`) that accepts length-prefixed JSON commands. A LaunchBox plugin sends commands to start/stop profiles when games launch. Currently the IPC protocol has gaps that force the plugin to work around limitations.

## Current IPC Commands (in `ProcessCommand()`)

**File**: `src/XOutputRedux.App/IpcService.cs`

| Command | Behavior |
|---------|----------|
| `start` | Requires `ProfileName` — returns error if null/empty |
| `stop` | Stops active profile |
| `status` | Returns running state, profile name, monitoring status |
| `monitor-on` | Enables game monitoring |
| `monitor-off` | Disables game monitoring |

## Changes Needed

### 1. Add `list-profiles` command

Add a new case in `ProcessCommand()` that returns a list of available profile names.

The `ProfileManager` is available via dependency injection or can be accessed through the app's service container. Check how `MainWindow.xaml.cs` or other classes access it. The profile manager has methods like `LoadProfiles()` or similar that return the available profiles.

The response should include the profile list. You'll need to add a `Profiles` property to `IpcResult`:

```csharp
public class IpcResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public IpcStatus? Status { get; set; }
    public List<string>? Profiles { get; set; }  // ADD THIS
}
```

The `list-profiles` handler should return profile names (just the names, not full paths or JSON content):

```csharp
case "list-profiles":
    var profiles = /* get profile names from ProfileManager */;
    return new IpcResult { Success = true, Profiles = profiles };
```

### 2. Make `start` use default profile when `ProfileName` is null

Currently line ~241 returns an error when `ProfileName` is empty. Instead, resolve the default profile:

```csharp
case "start":
    var profileToStart = command.ProfileName;
    if (string.IsNullOrEmpty(profileToStart))
    {
        // Resolve default profile
        profileToStart = /* get default profile name from ProfileManager */;
        if (string.IsNullOrEmpty(profileToStart))
            return new IpcResult { Success = false, Message = "No default profile configured" };
    }
    StartProfileRequested?.Invoke(profileToStart);
    return new IpcResult { Success = true, Message = $"Starting profile: {profileToStart}" };
```

The `ProfileManager` should have a `GetDefaultProfile()` method or you can iterate profiles and find the one with `IsDefault == true`.

### 3. Add `get-default` command (optional but nice)

```csharp
case "get-default":
    var defaultProfile = /* get default profile name */;
    return new IpcResult
    {
        Success = defaultProfile != null,
        Message = defaultProfile ?? "No default profile configured",
        ProfileName = defaultProfile  // may need to add this property to IpcResult
    };
```

## Key Architecture Notes

- **JSON serialization**: XOutputRedux uses default `System.Text.Json` with **PascalCase** property names (no `[JsonPropertyName]` attributes). Keep it that way.
- **Wire protocol**: `[4-byte little-endian length][UTF-8 JSON]` in both directions.
- **`IpcService` needs access to `ProfileManager`**: Check how it's wired up. It may need a constructor parameter, a static reference, or access through the app's DI container. Look at how `MainWindow` or `App.xaml.cs` creates the `IpcService` instance.
- **Thread safety**: `ProcessCommand` runs on the pipe's async thread. If `ProfileManager` isn't thread-safe, you may need to dispatch to the UI thread or use a lock.
- **Don't break existing commands** — the `start`, `stop`, `status`, `monitor-on`, `monitor-off` commands must continue working exactly as they do now.

## Testing

After making changes, verify with PowerShell:

```powershell
# Test list-profiles
$pipe = [System.IO.Pipes.NamedPipeClientStream]::new(".", "XOutputRedux_IPC", [System.IO.Pipes.PipeDirection]::InOut)
$pipe.Connect(2000)
$req = [System.Text.Encoding]::UTF8.GetBytes('{"Command":"list-profiles"}')
$len = [BitConverter]::GetBytes($req.Length)
$pipe.Write($len, 0, 4); $pipe.Write($req, 0, $req.Length); $pipe.Flush()
$rl = [byte[]]::new(4); $null = $pipe.Read($rl, 0, 4); $n = [BitConverter]::ToInt32($rl, 0)
$rb = [byte[]]::new($n); $t = 0; while ($t -lt $n) { $r = $pipe.Read($rb, $t, $n - $t); if ($r -eq 0) { break }; $t += $r }
[System.Text.Encoding]::UTF8.GetString($rb, 0, $t)
$pipe.Dispose()

# Expected: {"Success":true,"Profiles":["Moza CS Pro (A EBrake)","Moza CS Pro (B EBrake)","Moza ESX (A EBrake)","Moza ESX (B EBrake)"]}

# Test start with no profile (should use default)
# Same pattern but with: '{"Command":"start"}'
# Expected: {"Success":true,"Message":"Starting profile: Moza CS Pro (A EBrake)"}
```

## After These Changes

Once XOutputRedux is rebuilt and reinstalled, the LaunchBox plugin (`E:\Source\launchbox\launchbox-plugin-xoutputredux`) can be simplified to use IPC for profile listing and default resolution instead of the current disk-scan workarounds.

---

# HidHide Visibility & Control panel (feature request, 2026-07-18)

## Motivation — a real incident

With **no XOutput profile running**, HidHide was still hiding the user's **X-Arcade arcade controller** and **both Stream Decks**: the X-Arcade never appeared in Windows "Game Controllers", and the Stream Deck app couldn't push profile updates to a hidden deck. XOutputRedux is what manipulates HidHide, yet there's no way to see or fix HidHide's state from inside XOutputRedux — diagnosis required external tooling (`HidHideCLI --cloak-state / --dev-gaming / --app-list`, plus registry blacklist inspection at `HKLM\SYSTEM\CurrentControlSet\Services\HidHide`).

Three underlying problems this exposed:
1. **No visibility/control** of the cloak state + hide-list from XOutputRedux, independent of a running profile.
2. **Over-broad hiding.** XOutputRedux hides real Xbox pads by VID/PID `045E:028E` so its virtual pad wins — but the **X-Arcade UFB presents as that same Xbox 360 VID/PID**, so it got hidden too. Both Stream Decks (`0FD9:0080`, `0FD9:00C6`) were also on the list. No way to exclude specific devices.
3. **Inconsistent driver state.** HidHide reported `--cloak-off` while still filtering hidden devices (it got into a bad state after a broken install → repair; XOutputRedux itself intermittently reports "HidHide not installed"). No self-heal / reset.

## What's needed — a "HidHide" tab/panel (works with no profile active)
- **Cloak state** — on/off indicator + toggle.
- **Hide-list** — every hidden device: friendly name, VID/PID, present/absent, and **why it's hidden** (which profile added it, or "manual / global / stale"). Per-row **Unhide** / Hide.
- **Whitelist** — apps allowed to see hidden devices (XOutputRedux, HidHideCLI, …).
- **Orphan/stale detection** — flag devices hidden but not referenced by any current profile (the X-Arcade + Stream Decks here) with a one-click "unhide all orphans".
- **Reset / repair** — force the cloak state consistent (toggle + restart the HidHide service), detect a broken HidHide install, offer repair/reinstall.
- **Never-hide exclusion list** — user-defined devices that must never be swept into a hide rule (X-Arcade, Stream Decks, any non-wheel device).

## Also fix the root over-hiding
- Hide the *specific wheel device instance(s)*, not a bare Xbox VID/PID.
- Never hide non-game-controller HID devices (Stream Decks are consumer-control devices, not gamepads).

## Implementation notes
- Reuse XOutputRedux's existing HidHide integration (the `Nefarius.Drivers.HidHide` client library, or shell out to `HidHideCLI`). Surface: cloak get/set, blacklist get/add/remove, whitelist get, device enumeration (gaming + all) with present/absent status.
- This is the "what is HidHide doing right now, and let me fix it" view the incident proved was missing.
