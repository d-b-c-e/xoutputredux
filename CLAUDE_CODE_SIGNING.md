# Code Signing Research Notes

Research conducted 2026-01-18 for Phase 16: Code Signing.

---

## Overview

Code signing is needed to avoid Windows Defender SmartScreen warnings ("Windows protected your PC" / "Unknown publisher").

### Key Finding: Signing Alone Doesn't Eliminate Warnings Immediately

- SmartScreen builds reputation based on downloads and telemetry
- Even with a valid certificate, new apps show warnings until they build trust
- Could take 2-8 weeks, sometimes longer
- **EV certificates no longer provide instant reputation** (changed March 2024)
- When certificates renew, reputation resets (except with Azure Trusted Signing)

---

## Options for Open Source Projects

### 1. SignPath Foundation - FREE (Recommended)

**Website:** https://signpath.org/

**What they provide:**
- Free code signing for qualifying OSS projects
- Certificate links to your GitHub repo (not personal identity)
- Private key stored in HSM (no USB token needed)
- Works with CI/CD pipelines (GitHub Actions supported)
- Users can verify the binary came from your actual repository

**Apply:** https://signpath.org/apply
- Download OSSRequestForm-v4.xlsx
- Email completed form to oss-support@signpath.org
- Wait for approval (typically a few weeks)

### 2. Certum Open Source Certificate - $29/year

**Website:** https://certum.store/open-source-code-signing-code.html

- Cheap option for open source developers
- Requires hardware token (USB card reader + cryptographic card)
- Trusted by Microsoft

### 3. Azure Trusted Signing - ~$10/month

**Website:** https://azure.microsoft.com/en-us/products/trusted-signing

- Microsoft's own service
- Reputation persists across certificate renewals
- Requires US/Canada business with 3+ years history, or individual developer in US/Canada

---

## SignPath Foundation Details

### Eligibility Requirements

From https://signpath.org/terms:

| Requirement | XOutputRedux Status |
|-------------|---------------------|
| OSI-approved open source license | ✓ MIT License |
| No malware/PUPs | ✓ |
| No proprietary code (system libs OK) | ✓ |
| Active maintenance | ✓ |
| Already released/releasable | ✓ |
| Documented functionality | ✓ README |

### Additional Requirements for Their Certificate

- Multi-factor authentication for all contributors
- Clear team roles (Authors, Reviewers, Approvers)
- Each signing request needs approval from trusted team member
- Code signing policy on project homepage with SignPath attribution
- No hacking/exploit tools

### "Circumventing Security Measures" Clause Analysis

The terms state:
> "Software must not include features designed to identify or exploit security vulnerabilities or circumvent security measures of their execution environment."

**Assessment for XOutputRedux: NOT A CONCERN**

| Concern | XOutputRedux Reality |
|---------|---------------------|
| "Circumvent security measures" | Creates virtual Xbox controller via ViGEmBus - a legitimate, Microsoft-signed driver. Not bypassing anything. |
| HidHide integration | Hides physical devices to prevent double-input. This is UX, not security circumvention. HidHide itself is a signed driver. |
| Virtual controller = cheating? | No - identical to DS4Windows, x360ce, and dozens of other mappers. It's accessibility/compatibility software. |

The clause targets hacking tools, exploit kits, and security bypass software. Controller mappers are legitimate utilities.

---

## GitHub Actions Integration

Once approved, integrate with your release workflow:

**Resources:**
- SignPath GitHub Actions: https://github.com/SignPath/github-actions
- Demo project: https://github.com/SignPath/github-actions-demo
- Documentation: https://docs.signpath.io/trusted-build-systems/github

**How it works:**
1. Your workflow builds the exe/installer
2. Uploads artifact to GitHub
3. Calls SignPath action to submit for signing
4. SignPath verifies it came from your repo (origin verification)
5. Returns signed binary
6. You publish the signed release

**Required secrets/variables:**
- `SIGNPATH_API_TOKEN` - API token from SignPath (user must be submitter in signing policies)
- `SIGNPATH_ORGANIZATION_ID` - Your SignPath org ID

---

## Draft Application Form

### Section 1: Basic Information

| Field | Value |
|-------|-------|
| Project Name | XOutputRedux |
| Project Short Name | xoutputredux |
| Project Homepage | https://github.com/d-b-c-e/xoutputredux |
| Brief Description | Xbox controller emulator for Windows that maps inputs from multiple gaming devices to a single emulated Xbox 360 controller |
| Detailed Description | XOutputRedux is a streamlined Windows application that converts DirectInput/RawInput device inputs (steering wheels, joysticks, gamepads) into Xbox 360 controller format via ViGEmBus. It enables legacy and non-standard controllers to work with modern games. Features include multi-input OR-logic mapping, force feedback routing, HidHide integration for device hiding, game auto-profiles, CLI/IPC control, and Stream Deck integration. |
| License | MIT |
| License URL | https://github.com/d-b-c-e/xoutputredux/blob/main/LICENSE |
| Programming Languages | C#, .NET 8, WPF |

### Section 2: Repository Information

| Field | Value |
|-------|-------|
| Repository Type | GitHub |
| Repository URL | https://github.com/d-b-c-e/xoutputredux |
| Contributors | [fill in] |
| Commits | [fill in] |
| Age of Project | [fill in] |
| Development Status | Alpha (active development) |

### Section 3: Distribution & Downloads

| Field | Value |
|-------|-------|
| Download Page URL | https://github.com/d-b-c-e/xoutputredux/releases |
| Package Formats | Windows Installer (.exe), Portable ZIP |
| Distribution Method | GitHub Releases |
| Total Downloads | [check GitHub release stats] |
| Downloads Per Month | [estimate] |

### Section 4: Privacy Policy

| Field | Value |
|-------|-------|
| Collects user data? | No |
| What data? | None - all settings stored locally |
| Where transmitted? | Nowhere - fully offline operation |
| Privacy Policy URL | N/A (no data collection) |

### Section 5: Wikipedia Article

| Field | Value |
|-------|-------|
| Wikipedia URL | N/A |
| Why no article? | Project is new/in alpha stage. Spiritual successor to archived XOutput project (https://github.com/csutorasa/XOutput) |

### Section 6: Verification & Trust Evidence

| Field | Value |
|-------|-------|
| How to verify usage | GitHub stars, release download counts, open issues/discussions |
| Media Reports | N/A (new project) |
| Related Projects | Inspired by XOutput (archived, 1.8k stars). Similar to DS4Windows, x360ce |
| GitHub Insights | Commit history, CI/CD workflows, release automation |

### Section 7: Technical Details

| Field | Value |
|-------|-------|
| What will be signed? | Main application executable and Windows installer |
| File Types | .exe (WPF app), .exe (Inno Setup installer) |
| Signing Frequency | Per release (currently alpha releases every few days, will slow to monthly/quarterly at 1.0) |
| Build Process | `dotnet build` / `dotnet publish`, Inno Setup for installer |
| CI/CD | GitHub Actions (`.github/workflows/release.yml` triggered on version tags) |

### Section 8: Contact Information

| Field | Value |
|-------|-------|
| Primary Contact | [Your name] |
| Primary Contact Email | [Your email] |
| GitHub User/Org | d-b-c-e |

### Section 9: Checkboxes

- [x] Agreeing to Terms of Use
- [x] OSI-approved license (MIT)
- [x] Public source code
- [x] Not for commercial advantage
- [x] Freely downloadable
- [x] No intention of going proprietary
- [x] Software is legitimate, not malicious
- [x] Authorized to submit
- [x] Information is accurate

---

## Pre-Application Checklist

Before applying:

- [ ] Make repository public (currently private)
- [ ] Ensure all contributors have MFA enabled on GitHub
- [ ] Add code signing policy page to repo (required by SignPath)
- [ ] Review current release workflow for integration points

---

## Post-Approval Steps

1. Set up SignPath organization and signing policy
2. Add `SIGNPATH_API_TOKEN` secret to GitHub repo
3. Add `SIGNPATH_ORGANIZATION_ID` variable to GitHub repo
4. Update `.github/workflows/release.yml` to include signing step
5. Add SignPath attribution to download page/README
6. Test signing with a release

---

## Reference Links

- SignPath Foundation: https://signpath.org/
- SignPath Terms: https://signpath.org/terms
- SignPath Apply: https://signpath.org/apply
- SignPath GitHub Actions: https://github.com/SignPath/github-actions
- SignPath Docs: https://docs.signpath.io/
- DB Browser's SignPath Experience: https://sqlitebrowser.org/blog/signing-windows-executables-our-journey-with-signpath/
- Microsoft SmartScreen Reputation: https://www.digicert.com/blog/ms-smartscreen-application-reputation
- Submit to Microsoft for Analysis: https://www.microsoft.com/en-us/wdsi/filesubmission
