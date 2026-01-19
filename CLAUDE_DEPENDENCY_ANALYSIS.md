# XOutputRedux Dependency Analysis

Detailed analysis of project dependencies, maintenance status, and upgrade/migration plans.

*Last updated: 2026-01-14*

---

## Dependency Overview

| Package | Version | Last Update | Status | Action |
|---------|---------|-------------|--------|--------|
| SharpDX.DirectInput | 4.2.0 | 2019 | Abandoned | Migrate to Vortice (Late 2026) |
| HidSharp | 2.1.0 | 2020 | Abandoned | Fork & slim down (Late 2026) |
| Nefarius.ViGEm.Client | 1.21.256 | 2023 | Retired (works) | Keep as-is |
| Hardcodet.NotifyIcon.Wpf | 1.1.0 | 2021 | Low activity | Monitor, possibly inline |
| StreamDeck-Tools | 6.3.2 | 2024 | Active | None needed |
| System.CommandLine | 2.0.0-beta4 | 2024 | Active (Microsoft) | None needed |
| Microsoft.Toolkit.Uwp.Notifications | 7.1.3 | 2023 | Active (Microsoft) | None needed |

---

## SharpDX.DirectInput → Vortice Migration

### Background

- **SharpDX**: Abandoned since 2019, no maintainer
- **Vortice.Windows**: Spiritual successor by same original author (Alexandre Mutel), actively maintained

### Current Usage (4 files, ~500 lines)

| File | Purpose |
|------|---------|
| `DirectInputDeviceProvider.cs` | Device enumeration |
| `DirectInputDevice.cs` | Device wrapper, polling, FFB |
| `DirectInputSource.cs` | Input source reading |
| `DirectDeviceForceFeedback.cs` | Force feedback effects |

### API Mapping

| SharpDX | Vortice | Notes |
|---------|---------|-------|
| `SharpDX.DirectInput.DirectInput` | `Vortice.DirectInput.IDirectInput8` | Factory method instead of constructor |
| `Joystick` | `IDirectInputDevice8` | Interface-based |
| `JoystickState` | `JoystickState` | Nearly identical |
| `DeviceInstance` | `DeviceInstance` | Same |
| `DeviceType` | `DeviceType` | Same |
| `CooperativeLevel` | `CooperativeLevel` | Same flags |
| `Effect` | `IDirectInputEffect` | Interface-based |
| `EffectParameters` | `EffectParameters` | Same |
| `ConstantForce` | `ConstantForce` | Same |

### Migration Example

```csharp
// Before (SharpDX)
private readonly SharpDX.DirectInput.DirectInput _directInput;
_directInput = new SharpDX.DirectInput.DirectInput();
var joystick = new Joystick(_directInput, instance.InstanceGuid);

// After (Vortice)
private readonly IDirectInput8 _directInput;
_directInput = DInput.DirectInput8Create();
var joystick = _directInput.CreateDevice(instance.InstanceGuid);
```

### Effort Estimate

| Task | Time |
|------|------|
| Package swap & compile errors | 1-2 hours |
| API adjustments | 2-3 hours |
| Force feedback changes | 1-2 hours |
| Testing with devices | 2-4 hours |
| **Total** | **6-11 hours** |

### Performance Impact

**None meaningful.** XOutputRedux is I/O-bound (waiting on DirectInput API and drivers). The .NET wrapper overhead is microseconds per poll cycle - imperceptible regardless of which library is used.

---

## HidSharp Fork Analysis

### Background

- Last NuGet release: March 2020 (5+ years abandoned)
- Used for: RawInput/HID device access (fallback input method for devices that don't work well with DirectInput)
- Risk: Windows HID stack changes could break it with no maintainer to fix

### Current Usage

HidSharp is used in `XOutputRedux.Input/RawInput/` for:
- HID device enumeration
- Reading HID reports via event-driven model
- Parsing HID report descriptors

### Fork Strategy

**Option A: Slim Fork (Recommended)**
- Extract only the ~500 lines needed for XOutputRedux
- Remove unused features (serial ports, other platforms, etc.)
- Inline into project or separate minimal package

**Option B: Full Fork**
- Maintain entire library
- More work, but could benefit community
- Only if we want to be HidSharp maintainers

### Effort Estimate

| Approach | Effort |
|----------|--------|
| Slim fork (recommended) | 2-3 days |
| Full fork | 1-2 weeks ongoing |

---

## Nefarius.ViGEm.Client Analysis

### Background

- Tied to ViGEmBus driver (retired September 2023 due to trademark conflict)
- Driver still works on Windows 10/11
- .NET client is a thin wrapper over driver IOCTLs

### Assessment

**No action needed.** The client library is stable and does exactly what it needs to. If ViGEmBus itself stops working on future Windows versions, the .NET client is the least of our problems - we'd need an entirely new emulation strategy.

### Alternatives (if ViGEmBus dies)

- VirtualPad (in development, not ready)
- SDL virtual controller (cross-platform but different approach)
- Custom kernel driver (significant undertaking)

---

## Hardcodet.NotifyIcon.Wpf Analysis

### Background

- Last meaningful update: 2021
- Used for: System tray icon functionality
- Very small library (~2k lines)

### Assessment

**Low priority.** The library is simple and stable. If issues arise:

**Option A: Inline minimal code**
- NotifyIcon is just P/Invoke to Shell_NotifyIcon
- We use maybe 200 lines of actual functionality
- Could inline directly into app

**Option B: Alternative library**
- H.NotifyIcon (actively maintained fork/alternative)
- WPF-UI has tray icon support

### Effort Estimate

| Approach | Effort |
|----------|--------|
| Inline minimal code | ~4 hours |
| Migrate to H.NotifyIcon | ~2 hours |

---

## .NET 10 Upgrade Analysis

### Background

- Current: .NET 8.0 (LTS, supported until November 2026)
- Target: .NET 10 (LTS, supported until November 2028)

### Performance Impact

**None meaningful for XOutputRedux.** The app is I/O-bound:

```
1ms polling loop breakdown:
├── DirectInput API call (~500μs waiting on driver)
├── Copy JoystickState struct (~1μs)
├── Evaluate mappings (~5μs computation)
├── ViGEm API call (~100μs waiting on driver)
└── Thread.Sleep(1) (~1000μs idle)
```

~99% of each cycle is waiting on drivers or sleeping. Runtime improvements don't help.

### Where .NET 10 Improvements Apply (Not Here)

| Improvement Area | Typical Gain | XOutputRedux Relevance |
|------------------|--------------|------------------------|
| Tight computational loops | 5-15% | None in hot paths |
| JSON serialization | 10-20% | Profile load/save - already instant |
| LINQ operations | 5-10% | Minimal use |
| Startup time | 100-300ms | Nice but not critical |
| GC pause times | Shorter | App isn't allocation-heavy |

### Rationale for Upgrade

The upgrade is purely for **maintenance/support**, not performance:
- Extended support timeline (+2 years)
- Ensures compatibility with future Windows versions
- Dependencies may eventually require newer runtime

---

## Decision Summary

| Dependency | Decision | Timeline | Rationale |
|------------|----------|----------|-----------|
| SharpDX | Migrate to Vortice | Late 2026 | Abandoned, maintained alternative exists |
| HidSharp | Fork & slim | Late 2026 | Abandoned, no alternative, manageable scope |
| ViGEm.Client | Keep as-is | N/A | Stable, thin wrapper, no better option |
| Hardcodet.NotifyIcon | Monitor | If issues | Low priority, easy to inline if needed |
| .NET 8 → 10 | Upgrade | Late 2026 | LTS end-of-life approaching |

---

## References

- [Vortice.Windows GitHub](https://github.com/amerkoleci/Vortice.Windows)
- [HidSharp GitHub](https://github.com/IntergatedCircuits/HidSharp)
- [ViGEmBus GitHub](https://github.com/nefarius/ViGEmBus)
- [.NET Support Policy](https://dotnet.microsoft.com/platform/support/policy)
