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

### 3.1 Enable Nullable Reference Types
- Already enabled in csproj, but many warnings exist
- Key files needing fixes:
  - `AsyncResult.cs` - `_waitHandle` can be null
  - `WinHidManager.cs` - Static nullable fields (lines 56-62)
  - `HidDevice.cs` - Methods returning nullable strings

### 3.2 Remove Obsolete Properties
- **File**: `HidDevice.cs`
- Properties marked `[Obsolete]`:
  - `Manufacturer` (line 204) - use `GetManufacturer()`
  - `ProductName` (line 225) - use `GetProductName()`
  - `SerialNumber` (line 246) - use `GetSerialNumber()`
  - `MaxInputReportLength` (line 267) - use `GetMaxInputReportLength()`
  - `MaxOutputReportLength` (line 295) - use `GetMaxOutputReportLength()`
  - `MaxFeatureReportLength` (line 311) - use `GetMaxFeatureReportLength()`
  - `ProductVersion` (line 189) - use `ReleaseNumberBcd`
- **Also**: `HidDeviceLoader.cs` (lines 28-60), `DeviceList.cs` (line 35)

### 3.3 Fix Volatile + Lock Anti-Pattern
- **File**: `WinHidManager.cs` (lines 57-58)
- **Issue**: `volatile` fields also protected by locks (redundant)
- **Fix**: Remove `volatile` since locks provide memory barriers

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
| P3 | Code quality fixes | 3-4 hrs | Pending |
| P4 | Performance improvements | 4-6 hrs | Pending |
| P5 | Modernization | 6-8 hrs | Pending |

**Total**: ~14-19 hours remaining (P1-P2 complete)

---

## How to Use This Roadmap

1. Start with **Priority 1** - immediate quality improvements
2. **Priority 2** before any release - remove dead code
3. **Priority 3-4** as time permits
4. **Priority 5** only if async/performance becomes a bottleneck

Most improvements can be done incrementally without breaking changes.
