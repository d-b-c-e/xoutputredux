# Code Signing Policy

## Signed Artifacts

The following artifacts are signed for each release:

- `XOutputRedux.exe` — Main application executable
- `XOutputRedux-*-Setup.exe` — Windows installer

Signed releases are available on the [Releases page](https://github.com/d-b-c-e/xoutputredux/releases).

## Verification

For each release, [SignPath.io](https://about.signpath.io) verifies the origin of signed files. A valid signature confirms that the binary was built from the source code in this repository via the automated CI/CD pipeline (GitHub Actions).

## Team Roles

| Role | Members |
|------|---------|
| Author & Maintainer | [@d-b-c-e](https://github.com/d-b-c-e) |
| Committer | [@d-b-c-e](https://github.com/d-b-c-e) |
| Reviewer & Approver | [@d-b-c-e](https://github.com/d-b-c-e) |

All team members use multi-factor authentication for GitHub and SignPath access.

## Privacy

This program will not transfer any information to other networked systems unless specifically requested by the user or the person installing or operating it.

Specifically:
- All settings, profiles, and game associations are stored locally
- The only outbound network request is an optional update check against the GitHub Releases API, which can be disabled

### Third-Party Components

| Component | Privacy Impact |
|-----------|---------------|
| [ViGEmBus](https://github.com/nefarius/ViGEmBus) | Kernel driver — no network activity |
| [HidHide](https://github.com/nefarius/HidHide) | Kernel driver — no network activity |
| [Vortice.DirectInput](https://github.com/amerkoleci/Vortice.Windows) | Local input library — no network activity |
| [HidSharper](https://github.com/d-b-c-e/xoutputredux) (forked HidSharp) | Local HID library — no network activity |

## Attribution

Free code signing provided by [SignPath.io](https://about.signpath.io), certificate by [SignPath Foundation](https://signpath.org).
