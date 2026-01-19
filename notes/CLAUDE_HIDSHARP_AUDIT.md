# HidSharp Usage Audit

Audit conducted 2026-01-18 to understand what parts of HidSharp are used before deciding on a fork strategy.

## Summary

HidSharp is used **only** in the `XOutputRedux.Input/RawInput/` folder (3 files). It provides HID device enumeration, report descriptor parsing, and asynchronous input reading.

---

## HidSharp Types Used

### Core Device Types (`HidSharp` namespace)

| Type | Usage | Notes |
|------|-------|-------|
| `DeviceList` | `DeviceList.Local.GetHidDevices()` | Static singleton for device enumeration |
| `HidDevice` | Device representation | Properties: `DevicePath`, methods: `GetReportDescriptor()`, `TryOpen()`, `GetProductName()`, `GetMaxInputReportLength()` |
| `HidStream` | Read/write HID reports | Only `Dispose()` called directly; passed to `HidDeviceInputReceiver.Start()` |

### Report Descriptor Types (`HidSharp.Reports` namespace)

| Type | Usage | Notes |
|------|-------|-------|
| `ReportDescriptor` | Parse HID report structure | Properties: `DeviceItems`, `InputReports`. Methods: `CreateHidDeviceInputReceiver()` |
| `DeviceItem` | Single device in descriptor | Properties: `Usages`. Methods: `CreateDeviceItemInputParser()` |
| `Report` | Individual HID report | Properties: `ReportID` |
| `DataItem` | Data field in report | Properties: `LogicalMinimum` |
| `DataValue` | Parsed value from report | Properties: `Usages`, `DataItem`. Methods: `GetScaledValue()`, `GetLogicalValue()` |
| `Usage` | HID usage enum | Constants like `GenericDesktopX`, `Button1`, `GenericDesktopHatSwitch`, etc. |

### Input Receiver Types (`HidSharp.Reports.Input` namespace)

| Type | Usage | Notes |
|------|-------|-------|
| `HidDeviceInputReceiver` | Async input reader | Properties: `IsRunning`. Events: `Received`. Methods: `Start()`, `TryRead()` |
| `DeviceItemInputParser` | Parse reports to values | Properties: `HasChanged`. Methods: `TryParseReport()`, `GetNextChangedIndex()`, `GetValue()` |

---

## Detailed API Usage by File

### RawInputDeviceProvider.cs

```csharp
// Device enumeration
DeviceList.Local.GetHidDevices()      // Get all HID devices

// Device info
HidDevice.DevicePath                   // Get device path string
HidDevice.GetReportDescriptor()        // Get report descriptor
HidDevice.TryOpen(out HidStream)       // Open device for I/O

// Report descriptor
ReportDescriptor.DeviceItems           // Get device items collection
DeviceItem.Usages.GetAllValues()       // Get usages for filtering gaming devices
```

### RawInputDevice.cs

```csharp
// Device info
HidDevice.GetProductName()             // Get friendly name
HidDevice.GetMaxInputReportLength()    // Get input buffer size

// Report descriptor
ReportDescriptor.CreateHidDeviceInputReceiver()   // Create async reader
ReportDescriptor.InputReports                      // Get input report definitions
DeviceItem.CreateDeviceItemInputParser()          // Create parser

// Input receiver (event-based reading)
HidDeviceInputReceiver.IsRunning                  // Check if running
HidDeviceInputReceiver.Received                   // Event for new data
HidDeviceInputReceiver.Start(HidStream)           // Start receiving
HidDeviceInputReceiver.TryRead(buffer, offset, out Report)  // Read report

// Input parser
DeviceItemInputParser.TryParseReport(buffer, offset, report)  // Parse bytes
DeviceItemInputParser.HasChanged                  // Check for changes
DeviceItemInputParser.GetNextChangedIndex()       // Get changed item index
DeviceItemInputParser.GetValue(index)             // Get DataValue

// Report/DataValue
Report.ReportID                                   // Report identifier
DataValue.Usages                                  // Get usages for this value
```

### RawInputSource.cs

```csharp
// Usage enum constants
Usage.GenericDesktopX, Y, Z, Rx, Ry, Rz          // Axis usages
Usage.Button1 through Button31                    // Button usages
Usage.GenericDesktopHatSwitch                     // DPad usage
Usage.GenericDesktopSlider, Dial, Wheel           // Slider usages

// DataValue methods
DataValue.GetScaledValue(min, max)                // Scale to 0.0-1.0
DataValue.GetLogicalValue()                       // Get raw integer value
DataValue.DataItem.LogicalMinimum                 // Get range minimum
```

---

## Complexity Assessment

### Simple to Reimplement (~200 lines)
- `DeviceList` / `HidDevice` enumeration - Windows SetupAPI + HID API
- `HidStream` - CreateFile + ReadFile/WriteFile
- `Usage` enum - Just constants, easy to define

### Moderate Complexity (~300 lines)
- `ReportDescriptor` parsing - Parse HID report descriptor binary format
- `DeviceItem` / `DataItem` extraction from descriptor

### Most Complex (~200 lines)
- `HidDeviceInputReceiver` - Async read with event notification
- `DeviceItemInputParser` - Track changes, extract values from raw bytes

---

## Fork Strategy Options

### Option A: Minimal Extraction (~500-700 lines)
Extract only the classes we use. Pros: Small, focused. Cons: Need to understand HidSharp internals deeply.

### Option B: Slim Fork (~1000-1500 lines)
Copy relevant HidSharp source files, remove unused code. Pros: Faster. Cons: More code to maintain.

### Option C: Alternative Library
Use `Windows.Devices.HumanInterfaceDevice` (UWP/WinRT). Pros: Microsoft-maintained. Cons: API differences, may need significant rewrite.

### Option D: Direct Win32 HID API
Use P/Invoke to `hid.dll` and `SetupAPI.dll` directly. Pros: No dependencies. Cons: Most work, low-level.

---

## Recommendation

**Option B (Slim Fork)** is likely the best balance:

1. HidSharp is MIT licensed - forking is allowed
2. The code is well-structured and readable
3. We can remove ~80% of the library (serial ports, output reports, etc.)
4. Keep the report descriptor parser intact - it handles edge cases

### Files to Extract from HidSharp

Based on the audit, we'd need approximately these HidSharp source files:
- `DeviceList.cs` (partial - HID only)
- `HidDevice.cs`
- `HidStream.cs`
- `Platform/Windows/` HID implementation files
- `Reports/ReportDescriptor.cs`
- `Reports/DeviceItem.cs`
- `Reports/DataItem.cs`
- `Reports/DataValue.cs`
- `Reports/Usage.cs`
- `Reports/Input/HidDeviceInputReceiver.cs`
- `Reports/Input/DeviceItemInputParser.cs`

### What to Remove
- Serial port support
- Bluetooth support
- macOS/Linux platform code
- Output report functionality
- Feature report functionality
- Most of the "Experimental" namespace

---

## Next Steps

1. Clone HidSharp source from https://github.com/IntergatedCircuits/HidSharp
2. Identify exact files needed
3. Create `XOutputRedux.Hid` project with extracted code
4. Update namespace from `HidSharp` to `XOutputRedux.Hid`
5. Remove unused code
6. Test with existing devices (MOZA, VelocityOne, X-Arcade)
