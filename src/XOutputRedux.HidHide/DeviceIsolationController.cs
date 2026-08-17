using System.Text.Json;

namespace XOutputRedux.HidHide;

/// <summary>
/// Applies and reverts "device isolation": hide every HID gaming device EXCEPT
/// a configured keep-visible set, via HidHide. No virtual controller is
/// involved — the kept device(s) stay fully native so DirectInput force
/// feedback keeps working.
///
/// Safety model:
/// - Before touching anything, the prior HidHide state is captured (which
///   devices were already hidden, whether cloaking was on) so revert restores
///   exactly that state and composes with other HidHide users (including
///   XOutputRedux's own per-profile HidHideSettings).
/// - Devices that were already hidden before Apply are never claimed by this
///   controller and never unhidden by it.
/// - A recovery journal is written to disk before any device is hidden. If the
///   app crashes mid-session, <see cref="RecoverStaleState"/> (called at next
///   startup) unhides everything listed and restores the recorded cloak state.
/// - Revert verifies against HidHide's actual hidden list: a path only counts
///   as a failure if it is still hidden afterwards, and failures are re-written
///   to the journal so the next startup retries them.
/// </summary>
public class DeviceIsolationController
{
    private const string JournalFileName = "isolation-recovery.json";

    private static readonly JsonSerializerOptions JournalJsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly HidHideService _svc;
    private readonly string _journalPath;
    private readonly object _lock = new();

    private List<string> _newlyHidden = new();
    private bool _priorCloakOn;

    /// <summary>
    /// Whether an isolation session is currently active.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Optional log sink (wired to the app logger by the host).
    /// </summary>
    public Action<string>? Log { get; set; }

    public DeviceIsolationController(HidHideService service, string stateDirectory)
    {
        _svc = service;
        _journalPath = Path.Combine(stateDirectory, JournalFileName);
    }

    /// <summary>
    /// Applies isolation: hides every present HID gaming device whose identity
    /// does not match <paramref name="keepVisiblePaths"/> (matched against both
    /// the device instance path and the base container path, case-insensitive).
    /// </summary>
    public IsolationApplyResult Apply(IReadOnlyCollection<string> keepVisiblePaths)
    {
        lock (_lock)
        {
            if (IsActive)
                return IsolationApplyResult.Fail("Device isolation is already active");

            if (!_svc.IsAvailable)
                return IsolationApplyResult.Fail("HidHide is not installed");

            var keepSet = new HashSet<string>(
                keepVisiblePaths.Where(p => !string.IsNullOrWhiteSpace(p)),
                StringComparer.OrdinalIgnoreCase);

            if (keepSet.Count == 0)
                return IsolationApplyResult.Fail("No keep-visible devices configured");

            // Ensure the HidHide driver is running
            if (!_svc.IsDriverRunning())
            {
                Log?.Invoke("Isolation: starting HidHide driver...");
                if (!_svc.StartDriver())
                {
                    Log?.Invoke("Isolation: failed to start HidHide driver - hiding may not work");
                }
            }

            // ── Capture prior state (the whole basis for an exact revert) ──
            var priorHidden = new HashSet<string>(_svc.GetHiddenDevices(), StringComparer.OrdinalIgnoreCase);
            _priorCloakOn = _svc.IsCloakingEnabled() == true;

            // Enumerate gaming devices and split into kept / to-hide
            var devices = _svc.GetGamingDevices().Where(d => d.Present).ToList();

            // Keep-list matching is deliberately GENEROUS: a saved profile may hold the
            // HID instance path, the base-container path, or the symbolic link, so any
            // of them identifying this device means "keep it visible".
            //
            // Exact paths are tried first, but they are NOT stable. A device replugged
            // into a different port, or an XInput pad whose IG_ slot moved (an X-Arcade
            // panel was seen shifting IG_00/01/02 -> IG_04/05 just from cycling its
            // controller mode), reports a path the saved profile has never seen. The
            // profile then matches nothing and Apply refuses - or worse, hides the very
            // device the user meant to keep.
            //
            // So: any keep-list entry that resolves to no present device falls back to
            // that entry's HARDWARE identity (VID/PID, plus MI_/COL). The fallback can
            // only ever keep MORE devices visible, never hide one that should have
            // stayed - the safe direction for a feature whose failure mode is "the user
            // is left with no working controller".
            var matchedExactly = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in devices)
                foreach (var p in HidHideDevice.MatchPaths(d))
                    if (keepSet.Contains(p)) matchedExactly.Add(p);

            var staleKeepEntries = keepSet.Where(k => !matchedExactly.Contains(k)).ToList();
            var fallbackIdentities = new HashSet<string>(
                staleKeepEntries.Select(HidHideDevice.HardwareIdentity)
                                .Where(i => !string.IsNullOrEmpty(i))
                                .Select(i => i!),
                StringComparer.OrdinalIgnoreCase);

            if (fallbackIdentities.Count > 0)
            {
                Log?.Invoke(
                    $"Isolation: {staleKeepEntries.Count} keep-list path(s) no longer resolve to a present " +
                    $"device; matching those by hardware identity instead: {string.Join(", ", fallbackIdentities)}");
            }

            bool IsKept(HidHideDevice d) =>
                HidHideDevice.MatchPaths(d).Any(p => keepSet.Contains(p))
                || (fallbackIdentities.Count > 0
                    && HidHideDevice.IdentityKeys(d).Any(fallbackIdentities.Contains));

            // Every path belonging to a kept device is protected — a kept
            // wheel's sibling HID interface must never be swept into the hide
            // set via a shared base container path.
            var protectedPaths = new HashSet<string>(keepSet, StringComparer.OrdinalIgnoreCase);
            var keptNames = new List<string>();
            foreach (var d in devices.Where(IsKept))
            {
                foreach (var p in HidHideDevice.MatchPaths(d)) protectedPaths.Add(p);
                keptNames.Add(d.Product ?? d.Description ?? d.DeviceInstancePath ?? "(unknown)");
            }

            // Safety: if no keep-visible device is actually connected, hiding
            // everything else would leave the user with ZERO working devices.
            // Refuse before touching anything.
            if (keptNames.Count == 0)
            {
                return IsolationApplyResult.Fail(
                    "None of the keep-visible devices are currently connected - " +
                    "refusing to hide all remaining devices. Plug in the kept device " +
                    "(or edit the profile) and try again.");
            }

            var toHide = new List<(string Path, string Name)>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in devices)
            {
                if (IsKept(d)) continue;

                // Hide by the device's own HID instance path. HidHide's filter attaches
                // to the HID device, NOT the USB composite parent, so a container path
                // is accepted but hides nothing. (See HidHideDevice.EffectivePath.)
                var path = HidHideDevice.EffectivePath(d);
                if (string.IsNullOrWhiteSpace(path)) continue;
                if (protectedPaths.Contains(path)) continue;
                if (!seen.Add(path)) continue;

                // Already hidden before we started — belongs to someone else
                // (manual hide, another profile's HidHideSettings). Leave it
                // alone and never claim it for revert.
                if (priorHidden.Contains(path)) continue;

                toHide.Add((path, d.Product ?? d.Description ?? path));
            }

            // ── Journal BEFORE mutating, so a crash mid-apply is recoverable ──
            _newlyHidden = toHide.Select(t => t.Path).ToList();
            WriteJournal(new IsolationJournal
            {
                HiddenDevicePaths = _newlyHidden,
                PriorCloakOn = _priorCloakOn,
                TimestampUtc = DateTime.UtcNow
            });

            // Make sure we can still see hidden devices ourselves
            _svc.WhitelistSelf();

            var hiddenNames = new List<string>();
            var failedNames = new List<string>();
            foreach (var (path, name) in toHide)
            {
                if (_svc.HideDevice(path))
                {
                    hiddenNames.Add(name);
                    Log?.Invoke($"Isolation: hid '{name}' ({path})");
                }
                else
                {
                    failedNames.Add(name);
                    Log?.Invoke($"Isolation: FAILED to hide '{name}' ({path})");
                }
            }

            bool cloakOk = _svc.EnableCloaking();
            if (!cloakOk)
                Log?.Invoke("Isolation: failed to enable cloaking - hidden devices may still be visible (run as administrator?)");

            IsActive = true;

            var message =
                $"Kept visible: {(keptNames.Count > 0 ? string.Join(", ", keptNames) : "(none present)")}; " +
                $"hidden: {hiddenNames.Count}" +
                (failedNames.Count > 0 ? $"; FAILED to hide: {string.Join(", ", failedNames)}" : "") +
                (cloakOk ? "" : "; cloaking could not be enabled");

            return new IsolationApplyResult
            {
                Success = true,
                Message = message,
                HiddenCount = hiddenNames.Count,
                FailedCount = failedNames.Count,
                KeptPresentCount = keptNames.Count,
                CloakEnabled = cloakOk
            };
        }
    }

    /// <summary>
    /// Reverts isolation: unhides only the devices this controller hid and
    /// restores the captured prior cloak state. Never leaves devices hidden —
    /// any path still hidden after revert is re-journaled for retry at next
    /// startup. Safe to call when nothing is active (no-op).
    /// </summary>
    public void Revert()
    {
        lock (_lock)
        {
            var paths = _newlyHidden;
            var priorCloak = _priorCloakOn;

            if (paths.Count == 0)
            {
                // Nothing in memory — self-heal from a leftover journal if one
                // exists (e.g. a previous crash), otherwise nothing to do.
                var journal = ReadJournal();
                if (journal == null)
                {
                    IsActive = false;
                    return;
                }
                paths = journal.HiddenDevicePaths;
                priorCloak = journal.PriorCloakOn;
            }

            if (!_svc.IsAvailable)
            {
                // Can't talk to HidHide — keep the journal so a later session
                // (with HidHide back) can still revert.
                Log?.Invoke("Isolation: revert skipped - HidHide unavailable (journal kept for retry)");
                _newlyHidden = new List<string>();
                IsActive = false;
                return;
            }

            var failures = RevertCore(paths, priorCloak);

            _newlyHidden = new List<string>();
            IsActive = false;

            if (failures.Count == 0)
            {
                DeleteJournal();
                Log?.Invoke($"Isolation: reverted ({paths.Count} device(s) unhidden, cloak restored to {(priorCloak ? "on" : "off")})");
            }
            else
            {
                WriteJournal(new IsolationJournal
                {
                    HiddenDevicePaths = failures,
                    PriorCloakOn = priorCloak,
                    TimestampUtc = DateTime.UtcNow
                });
                Log?.Invoke($"Isolation: revert incomplete - {failures.Count} device(s) still hidden, journaled for retry at next startup");
            }
        }
    }

    /// <summary>
    /// Recovers from a stale journal left by a crash or abnormal exit: unhides
    /// the listed devices and restores the recorded cloak state. Call once at
    /// startup after <see cref="HidHideService.Initialize"/>.
    /// Returns true if a stale journal was found and processed.
    /// </summary>
    public bool RecoverStaleState()
    {
        lock (_lock)
        {
            if (IsActive) return false;

            var journal = ReadJournal();
            if (journal == null) return false;

            if (!_svc.IsAvailable)
            {
                Log?.Invoke("Isolation: stale journal found but HidHide unavailable - will retry next startup");
                return false;
            }

            Log?.Invoke($"Isolation: recovering stale state from {journal.TimestampUtc:u} ({journal.HiddenDevicePaths.Count} device(s))");
            var failures = RevertCore(journal.HiddenDevicePaths, journal.PriorCloakOn);

            if (failures.Count == 0)
            {
                DeleteJournal();
                Log?.Invoke("Isolation: stale state fully recovered");
            }
            else
            {
                WriteJournal(new IsolationJournal
                {
                    HiddenDevicePaths = failures,
                    PriorCloakOn = journal.PriorCloakOn,
                    TimestampUtc = DateTime.UtcNow
                });
                Log?.Invoke($"Isolation: stale-state recovery incomplete - {failures.Count} device(s) still hidden");
            }

            return true;
        }
    }

    /// <summary>
    /// Unhides the given paths and restores cloak/driver state, composing with
    /// any other HidHide usage. Returns the paths that are verifiably still
    /// hidden afterwards.
    /// </summary>
    private List<string> RevertCore(List<string> paths, bool priorCloakOn)
    {
        foreach (var path in paths)
        {
            try { _svc.UnhideDevice(path); }
            catch { /* verified below */ }
        }

        // Verify against HidHide's actual state — only a path still on the
        // hidden list counts as a failure (unhiding an already-unhidden path
        // is a harmless no-op).
        List<string> stillHiddenAll;
        try { stillHiddenAll = _svc.GetHiddenDevices().ToList(); }
        catch { stillHiddenAll = new List<string>(); }

        var stillHiddenSet = new HashSet<string>(stillHiddenAll, StringComparer.OrdinalIgnoreCase);
        var failures = paths.Where(p => stillHiddenSet.Contains(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        try
        {
            if (stillHiddenAll.Count == 0 && !priorCloakOn)
            {
                // Nobody is using HidHide anymore — leave it fully off
                // (mirrors the mapping-profile stop behavior: cloak off and
                // driver stopped so it cannot interfere with SDL2 etc.).
                _svc.DisableCloaking();
                if (_svc.IsDriverRunning())
                {
                    _svc.StopDriver();
                }
            }
            else if (priorCloakOn)
            {
                // Other devices were legitimately hidden before we started —
                // restore cloaking exactly as we found it.
                _svc.EnableCloaking();
            }
            else
            {
                _svc.DisableCloaking();
            }
        }
        catch (Exception ex)
        {
            Log?.Invoke($"Isolation: failed to restore cloak/driver state: {ex.Message}");
        }

        return failures;
    }

    // ── Journal persistence ───────────────────────────────────────

    private void WriteJournal(IsolationJournal journal)
    {
        try
        {
            var dir = Path.GetDirectoryName(_journalPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(_journalPath, JsonSerializer.Serialize(journal, JournalJsonOptions));
        }
        catch (Exception ex)
        {
            Log?.Invoke($"Isolation: failed to write recovery journal: {ex.Message}");
        }
    }

    private IsolationJournal? ReadJournal()
    {
        try
        {
            if (!File.Exists(_journalPath)) return null;
            var json = File.ReadAllText(_journalPath);
            return JsonSerializer.Deserialize<IsolationJournal>(json, JournalJsonOptions);
        }
        catch (Exception ex)
        {
            Log?.Invoke($"Isolation: failed to read recovery journal: {ex.Message}");
            return null;
        }
    }

    private void DeleteJournal()
    {
        try
        {
            if (File.Exists(_journalPath)) File.Delete(_journalPath);
        }
        catch (Exception ex)
        {
            Log?.Invoke($"Isolation: failed to delete recovery journal: {ex.Message}");
        }
    }
}

/// <summary>
/// Result of <see cref="DeviceIsolationController.Apply"/>.
/// </summary>
public class IsolationApplyResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
    public int HiddenCount { get; init; }
    public int FailedCount { get; init; }
    public int KeptPresentCount { get; init; }
    public bool CloakEnabled { get; init; }

    public static IsolationApplyResult Fail(string message) =>
        new() { Success = false, Message = message };
}

/// <summary>
/// On-disk crash-recovery record: which devices this session hid and what the
/// cloak state was beforehand.
/// </summary>
public class IsolationJournal
{
    public List<string> HiddenDevicePaths { get; set; } = new();
    public bool PriorCloakOn { get; set; }
    public DateTime TimestampUtc { get; set; }
}
