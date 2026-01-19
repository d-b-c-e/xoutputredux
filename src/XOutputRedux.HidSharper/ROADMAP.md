# XOutputRedux.HidSharper Improvement Roadmap

Analysis performed: 2026-01-18
Library: Forked from HidSharp 2.1.0, Windows-only, HID-only (73 files, ~11k lines)

## Summary

The library is functional but shows signs of its origins as a multi-platform library. Key areas for improvement:
1. **Dead code removal** - BLE/Serial/Linux/macOS remnants
2. **Modernization** - .NET 8 features, async patterns
3. **Performance** - Buffer allocation, lock contention
4. **Code quality** - Nullable types, obsolete API removal

---

## Priority 1: Quick Wins (< 1 hour each)

### 1.1 ~~Replace Obsolete SHA256Managed~~ DONE
- **Files**: `Platform/SystemEvents.cs` (lines 1063, 1071, 1425)
- **Issue**: `SHA256Managed` is obsolete in .NET Core
- **Fix**: Replaced with `SHA256.HashData()` static method
- **Completed**: 2026-01-18

### 1.2 ~~Fix CA2014 Stackalloc Warning~~ DONE
- **File**: `Platform/Windows/WinHidStream.cs` (line 180)
- **Issue**: `stackalloc` inside loop risks stack overflow
- **Fix**: Moved allocation outside loop, reset struct each iteration
- **Completed**: 2026-01-18

---

## Priority 2: ~~Dead Code Removal~~ DONE

### 2.1 ~~Remove Unused Platform GUIDs~~ DONE
- **Removed from**: `Platform/Windows/NativeMethods.cs`
- **Completed**: 2026-01-18

### 2.2 ~~Remove Bluetooth P/Invoke Declarations~~ DONE
- **Removed from**: `Platform/Windows/NativeMethods.cs`
- **Impact**: ~350 lines of dead code removed (structs, enums, P/Invoke)
- **Completed**: 2026-01-18

### 2.3 ~~Remove BLE/Serial Implementation Details~~ DONE
- **Removed from**: `ImplementationDetail.cs`
- Removed: `MacOS`, `Linux`, `BleDevice`, `SerialDevice`, `HidrawApi` GUIDs
- **Completed**: 2026-01-18

### 2.4 ~~Clean Up Disabled Code~~ DONE
- **Removed from**: `WinHidDevice.cs`
- Removed commented `GetDevicePathHierarchy()`, `GetDevicePaths()`, `TryGetDeviceUsbRoot()`
- Removed `GetSerialPorts()` from both `WinHidDevice.cs` and `HidDevice.cs`
- **Completed**: 2026-01-18

---

## Priority 3: Code Quality (3-4 hours)

### 3.1 Fix Nullable Reference Type Warnings - IN PROGRESS
- Started with ~163 unique warnings, reduced to ~80 (51% fixed)
- **Fixed files**:
  - `Throw.cs` - Made return types nullable for fluent API
  - `AsyncResult.cs` - Made `_waitHandle`, callbacks, state nullable
  - `DeviceStream.cs` - Made events and BeginRead/Write params nullable
  - `Device.cs` - Made TryOpen out params and OpenConfiguration nullable
  - `HidDevice.cs` - Fixed GetTopLevelUsage null dereference
  - `OpenOption.cs` - Made static properties use `= null!`, fixed delegates
  - `OpenConfiguration.cs` - Made GetOption return nullable
  - `DeviceOpenUtility.cs` - Made fields and local variables nullable
  - `HidManager.cs` - Made EventManager and delegate fields nullable
  - `WinHidDevice.cs` - Made fields nullable, fixed TryCreate return type
- **Remaining** (~80 warnings in 15 files):
  - `SystemEvents.cs` (23) - Complex Windows event system
  - `HidDeviceInputReceiver.cs` (11) - Report input handling
  - `NativeMethods.cs` (5) - P/Invoke declarations
  - Various Reports/* files
- **Completed**: 2026-01-18 (partial)

### 3.2 ~~Remove Obsolete Properties~~ DONE
- **Removed from**: `HidDevice.cs`
- Removed 7 obsolete properties (~125 lines):
  - `ProductVersion` → use `ReleaseNumberBcd`
  - `Manufacturer` → use `GetManufacturer()`
  - `ProductName` → use `GetProductName()`
  - `SerialNumber` → use `GetSerialNumber()`
  - `MaxInputReportLength` → use `GetMaxInputReportLength()`
  - `MaxOutputReportLength` → use `GetMaxOutputReportLength()`
  - `MaxFeatureReportLength` → use `GetMaxFeatureReportLength()`
- **Deleted**: `HidDeviceLoader.cs` (entire obsolete class)
- **Removed from**: `DeviceList.cs` - obsolete static `DeviceListChanged` event
- **Completed**: 2026-01-18

### 3.3 ~~Fix Volatile + Lock Anti-Pattern~~ DONE
- **File**: `WinHidManager.cs` (lines 57-58)
- **Issue**: `volatile` fields also protected by locks (redundant)
- **Fix**: Removed `volatile` keyword since locks provide memory barriers
- **Completed**: 2026-01-18

---

## Priority 4: Performance Improvements (4-6 hours)

### 4.1 Use ArrayPool for Buffers
- **Files**: `HidStream.cs`, `WinHidStream.cs`, `SysHidStream.cs`
- **Current**: Allocates `new byte[]` for every read/write
- **Fix**: Use `ArrayPool<byte>.Shared.Rent()` / `Return()`
- **Impact**: Reduces GC pressure in hot I/O paths

```csharp
// Before
byte[] buffer = new byte[Device.GetMaxInputReportLength()];

// After
byte[] buffer = ArrayPool<byte>.Shared.Rent(Device.GetMaxInputReportLength());
try { /* use buffer */ }
finally { ArrayPool<byte>.Shared.Return(buffer); }
```

### 4.2 Reduce Lock Contention in Device Enumeration
- **File**: `WinHidManager.cs`
- **Issue**: Heavy locking in `GetHidDeviceKeys()` and notification paths
- **Options**:
  - Use `ReaderWriterLockSlim` (many readers, few writers)
  - Cache device list with longer TTL
  - Use `ConcurrentDictionary` for device cache

### 4.3 Optimize SysHidStream Synchronization
- **File**: `Platform/SysHidStream.cs`
- **Issue**: Queue + Monitor.Wait for every I/O operation
- **Options**:
  - Replace Queue with `Channel<T>` for better async
  - Reduce locking granularity

---

## Priority 5: Modernization (6-8 hours)

### 5.1 Span<T> and Memory<T> Usage
- **Candidates**:
  - `DataItem.ReadRaw()` / `ReadLogical()` - parameter could be `ReadOnlySpan<byte>`
  - `NativeMethods.NTString()` - could use stack-allocated Span
  - Report parsing hot paths

### 5.2 Modernize Async Pattern
- **File**: `AsyncResult.cs`
- **Current**: Legacy `IAsyncResult` with ThreadPool callbacks
- **Target**: Replace with `Task<T>` and async/await
- **Note**: Breaking change to public API

### 5.3 Flatten Abstraction Hierarchy
- **Current**: 3-4 levels of inheritance for single platform
  - `Device` → `HidDevice` → `WinHidDevice`
  - `DeviceStream` → `HidStream` → `SysHidStream` → `WinHidStream`
- **Target**: Collapse to 2 levels since Windows-only
- **Impact**: ~200 lines removed, simpler code

---

## Future Considerations (Post-1.0)

### F1. Report Descriptor Reconstruction
- **File**: `WinHidDevice.ReportDescriptorReconstructor.cs` (line 29)
- **Note**: "This likely will only work for very simple descriptors"
- **Issue**: 12+ `NotImplementedException` locations
- **Consider**: Either complete implementation or document limitations

### F2. Better Error Handling
- Many methods catch and swallow exceptions
- Consider logging or propagating specific errors

### F3. Unit Tests
- No tests exist for HidSharper itself
- Consider adding tests for:
  - Report descriptor parsing
  - Data value scaling
  - Device enumeration (mocked)

---

## Effort Estimates

| Priority | Description | Effort | Status |
|----------|-------------|--------|--------|
| P1 | Quick wins (SHA256, CA2014) | 30 min | **DONE** |
| P2 | Dead code removal | 2-3 hrs | **DONE** |
| P3.1 | Nullable reference type fixes | 2-3 hrs | **51% DONE** (~80 warnings remain) |
| P3.2 | Remove obsolete properties | 30 min | **DONE** |
| P3.3 | Fix volatile + lock | 10 min | **DONE** |
| P4 | Performance improvements | 4-6 hrs | Pending |
| P5 | Modernization | 6-8 hrs | Pending |

**Total**: ~11-15 hours remaining (P1, P2, P3.2, P3.3, ~50% of P3.1 complete)

---

## How to Use This Roadmap

1. Start with **Priority 1** - immediate quality improvements
2. **Priority 2** before any release - remove dead code
3. **Priority 3-4** as time permits
4. **Priority 5** only if async/performance becomes a bottleneck

Most improvements can be done incrementally without breaking changes.
