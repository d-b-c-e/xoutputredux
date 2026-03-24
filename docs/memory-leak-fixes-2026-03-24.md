# Memory Leak Fixes — 2026-03-24

## Problem

XOutputRedux was consuming **720 MB private bytes** and **2.1 TB virtual memory** after 1.8 days of uptime, with **18,723 seconds of CPU time**. This is excessive for a controller redirect tool that should be near-idle when no active profile is mapping inputs.

The investigation was prompted by a routine `/pc-wellness` system checkup.

## Root Cause Analysis

Three primary leak sources were identified, plus two secondary allocation pressure issues:

### 1. RawInputDevice Event Handler Accumulation (CRITICAL)

**File:** `src/XOutputRedux.Input/RawInput/RawInputDevice.cs`

**Bug:** `Stop()` only unsubscribed the `_inputReceiver.Received` event handler if `_running` was true, but `_running` was set to false *before* the unsubscribe. Additionally, `Start()` added a new subscription without first removing any existing one, so repeated Start/Stop cycles caused handlers to pile up.

**Fix:**
- `Start()` now always unsubscribes before subscribing (`-=` then `+=`), preventing duplicate handlers
- `Stop()` now unsubscribes unconditionally regardless of `_running` state
- `Dispose()` was fixed to unsubscribe the actual handler reference instead of `null`

**Impact:** Each leaked handler kept the input receiver's event delegate chain alive, retaining references to the device and all its buffers. With multiple devices across profile changes, this was likely the largest contributor to the 720 MB footprint.

### 2. Dispatcher.BeginInvoke Closure Flooding (HIGH)

**File:** `src/XOutputRedux.App/ProfileEditorWindow.xaml.cs`

**Bug:** Two event handlers (`Device_InputChanged_Monitor` and `Device_InputChanged_Listen`) called `Dispatcher.BeginInvoke()` on every single input change event. With input polling at 1ms intervals per device, this created ~1,000 closures/second/device, each capturing references to the device, event args, and formatted strings.

**Fix:**
- Added debounce flags (`_monitorUpdatePending`, `_listenUpdatePending`) so only one Dispatcher callback is queued at a time
- Latest input values are stored in volatile fields and read by the single queued callback
- The listen handler now reads from `_latestInputValues` (already populated by the monitor handler) rather than capturing per-event state

**Impact:** Eliminated ~86 million unnecessary closure allocations per device per day. Each closure captured string allocations and object references that promoted to Gen2 GC, contributing to both memory footprint and CPU time spent in garbage collection.

### 3. AppLogger ConcurrentQueue Unbounded Growth (MEDIUM)

**File:** `src/XOutputRedux.App/AppLogger.cs`

**Bug:** The `ConcurrentQueue<string>` log queue had no maximum size. With verbose input logging enabled (or even normal logging at high input rates), strings accumulated faster than the 100ms/500-line flush could drain them. Over 1.8 days, this could accumulate hundreds of MB of formatted log strings.

**Fix:**
- Added `MaxQueueSize = 10,000` constant — messages are dropped when the queue exceeds this size
- Added `_droppedCount` tracking via `Interlocked` operations
- When draining resumes, a summary line is injected: "Dropped N log messages (queue overflow)"

**Impact:** Caps log queue memory usage at ~2-5 MB worst case (10K strings averaging 200-500 bytes each), preventing unbounded growth.

### 4. ProcessReports Hot-Path Allocation (MEDIUM)

**File:** `src/XOutputRedux.Input/RawInput/RawInputDevice.cs`

**Bug:** `ProcessReports()` created a new `Dictionary<Usage, DataValue>` on every call (~1,000 calls/second/device). While each dictionary was short-lived, the sheer volume of allocations (86M+/day) created GC pressure, promoting objects to Gen1/Gen2 and increasing CPU time spent in collection.

**Fix:** Moved the dictionary to a reusable instance field (`_changedUsages`) that is cleared at the start of each call instead of reallocated.

**Impact:** Eliminates ~86 million dictionary allocations per device per day. Reduces GC pressure and CPU time.

## Files Changed

| File | Change |
|---|---|
| `src/XOutputRedux.Input/RawInput/RawInputDevice.cs` | Event handler leak fix, hot-path allocation fix |
| `src/XOutputRedux.App/AppLogger.cs` | Queue size cap with drop tracking |
| `src/XOutputRedux.App/ProfileEditorWindow.xaml.cs` | Dispatcher.BeginInvoke debouncing |

## Verification

After building and restarting XOutputRedux, monitor:
- Private bytes should stabilize under 100 MB (vs 720 MB before)
- Virtual memory should stay under 1 GB (vs 2.1 TB before)
- CPU time should grow much slower (~1-2% average vs ~12% before)
- The log file should show "Dropped N log messages" entries if input logging is heavy, confirming the queue cap is working

Use this PowerShell command to check:
```powershell
Get-Process XOutputRedux | Select-Object @{N='PrivateMB';E={[math]::Round($_.PrivateMemorySize64/1MB,1)}}, @{N='WorkingSetMB';E={[math]::Round($_.WorkingSet64/1MB,1)}}, @{N='CPUs';E={[math]::Round($_.CPU,1)}}, HandleCount, Threads
```

## Rollback

If any of these changes cause functional issues:

1. **Event handler fix** (RawInputDevice.cs): If devices stop receiving input after Stop/Start, revert the `Stop()` method to check `_running` before unsubscribing. The core fix in `Start()` (unsubscribe-before-subscribe) is safe and should be kept.

2. **Dispatcher debounce** (ProfileEditorWindow.xaml.cs): If the input monitor display in the profile editor feels laggy or misses captures, revert the debounce and restore the direct `Dispatcher.BeginInvoke` calls. The capture functionality uses `_latestInputValues` so it should still work, but visual feedback may lag slightly.

3. **Log queue cap** (AppLogger.cs): If you need complete logs for debugging, increase `MaxQueueSize` to 100,000 or remove the check. The dropped count summary will tell you if messages are being lost.

4. **Hot-path dict reuse** (RawInputDevice.cs): This is fully safe — `_changedUsages.Clear()` at the start of each call is equivalent to `new Dictionary()`. No rollback should be needed.
