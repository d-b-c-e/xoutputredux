# HidHide upstream status — why we carry our own workarounds

**Checked 2026-08-20. Re-check when the reminder below fires.**

## The situation

HidHide's `master` is at roughly **v1.7.346**, but the newest **signed** release is
**v1.5.230.0 (2024-05-11)** — 93+ commits behind. HidHide is a **kernel driver**, so an
unsigned self-build will not load on a normal Windows install (test-signing or Secure
Boot off is not an acceptable ask for our users, or for the rig).

Per upstream [issue #217](https://github.com/nefarius/HidHide/issues/217), releases
require the maintainer to code-sign locally by hand after CI produces unsigned
artifacts. That is the whole bottleneck. **There is no route to those fixes for us
until a signed release ships.**

## What we are missing (and why it stings)

Merged upstream, unreachable to us:

| Upstream | Why we care |
|---|---|
| **Process-lifetime session blacklist (#201)** | Cloaks tied to a process lifetime. This is *exactly* the mechanism whose absence lets a stale blacklist entry survive an interrupted stop — see below. |
| **Xbox360/XUSB hiding fixes (#210)** | We fight XInput-class devices constantly (X-Arcade impersonates `VID_045E&PID_028E`; see `xarcade-defeats-identity-matching`). |
| Crash on broken device enumeration (#209) | General robustness. |
| `HidHideCollectionToMultiString` buffer fix (#211) | Allocation correctness in the API we call. |

Known-bad territory in the version we *do* run (open upstream issues):
**#208** "loses all cloaks after attempting to add new cloak", **#215** config UI/CLI
crash with `ERROR_INVALID_PARAMETER` in `GetWhitelist`, **#182** breakage after Windows
11 24H2 update KB5064401.

## What this means for our code

We keep the workarounds. Specifically the **`priorHidden` flaw** in
`src/XOutputRedux.HidHide/DeviceIsolationController.cs` (~line 114/226): devices already
hidden when a profile starts are treated as belonging to someone else — left alone, and
**never claimed for revert**. One interrupted stop therefore wedges an entry that every
later run politely refuses to clear.

This wedged a stale hide on the X-Arcade and killed VPX pinball input on the Arcade
build for an entire session (diagnosed 2026-08-18; the entry was the panel's own
`IG_04` path — the very device the profile was meant to *keep*).

**Fix to implement on our side** (do not wait for upstream): reconcile against our
journal at startup, or assert the blacklist is empty when no profile is running.
Upstream's session blacklist would make this unnecessary — but it is unreachable, so
the fix has to be ours.

Diagnosis commands:

```
"C:\Program Files\Nefarius Software Solutions\HidHide\x64\HidHideCLI.exe" --dev-list
"C:\Program Files\Nefarius Software Solutions\HidHide\x64\HidHideCLI.exe" --cloak-state
... --dev-unhide "HID\VID_xxxx&PID_xxxx&IG_0n\..."
```

Note HidHide hides by **HID device path**, not USB container path — blacklisting
`USB\VID_...` is silently accepted and hides nothing.

## Re-check procedure

```bash
gh api repos/nefarius/HidHide/releases -q '.[0] | "\(.tag_name)  \(.published_at[:10])"'
gh issue view 217 --repo nefarius/HidHide --json state -q .state
```

If a release **newer than v1.5.230.0** appears: read its notes for #201/#210, update the
installed driver, then re-test (a) the Moza "Hide Everything Except Wheel" profile,
(b) the X-Arcade profile — remembering that identity matching cannot isolate that panel
regardless — and (c) whether the session blacklist lets us delete our `priorHidden`
workaround entirely.
