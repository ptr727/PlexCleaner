---
name: add-host-tool
description: >-
  Adds or changes a managed host tool across the ptr727/ProjectTemplate fleet contract, Linux and
  Windows installers, platform documentation, and tests. Use this whenever adding, removing,
  renaming, or changing the source, probe, version floor, install, report, upgrade, or dry-run
  behavior of a tool in host-setup or spec/host-tools.json. Triggers even when the request names
  only one platform, because a required fleet tool needs an executable remedy everywhere it
  applies and native verification must stay on the platform being tested.
---

# Add Host Tool

## Establish the Contract

1. Read the issue and all follow-up comments before choosing a source or package identifier.
2. Add the tool to `spec/host-tools.json` in name order.
3. Use the executable's real version banner for the probe and pattern.
4. Set a floor only when it is measured or anchored to every supported distribution.
5. Provide `source` and executable `remedy` entries for every applicable platform.

## Implement Each Platform

- Keep the existing named-tool interface and default selection behavior.
- Prefer the distribution package when it meets the floor.
- Use the platform's established package manager and official package identifier.
- Keep install and upgrade idempotent.
- Before an apt-managed install, detect an unowned downloaded copy that shadows it. Report it with the command that removes it by hand, per `GOVERNANCE.md` "Design Before Code". Skip that tool's install until the copy is gone, since the shadowing copy would keep answering on PATH.
- Before a downloaded install, detect a conflicting package-managed copy. Report it and skip the install the same way.
- Preserve report, list, explicit selection, install, upgrade, reinstall, and dry-run behavior.
- Hand each native mutation to the operator on its own platform, never a Windows mutation on Linux or a Linux mutation on Windows.

Verify each platform's registry and tests without claiming a native install, whether or not the platform is available. Hand off the exact native commands and expected observations to the operator.

## Update the Complete Surface

Update the platform installers, `spec/host-tools.json`, `docs/host-setup.md`, and the applicable platform READMEs. Update installer and host-gate tests for selection, reporting, installation, upgrade, and dry-run behavior. Sweep prose that describes tool sources or the managed set.

## Verify

1. Run the spec validator and the focused installer and host-gate tests.
2. Run the repository's formatting, lint, type, and test gates required by the changed files.
3. On the current native platform, exercise list and report first.
4. Exercise install and upgrade dry runs.
5. Hand the operator the exact native commands for the install, a repeat install proving
   idempotence, and the host gate, with the expected observations. Verify what the operator reports.
6. Record untested platforms explicitly and leave cross-platform verification open.
