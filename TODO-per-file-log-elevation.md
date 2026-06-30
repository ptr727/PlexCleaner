# Continuation: Per-File Dynamic Log-Level Elevation

Working handoff doc for finishing the `feature/per-file-log-elevation` branch on a
system that has the test media files. Delete this file before opening the PR (it is
a scratch aid, not permanent repo documentation).

## Goal

When PlexCleaner runs in monitor mode with `--logwarning`, fault **detection**
messages log at `Warning` and are visible, but the **remediation** steps that follow
log at `Information` and are suppressed. The warning log therefore shows that a
problem was found but never what was done about it. The feature surfaces each
faulted/modified file's full remediation detail in the warning-level log, live as it
happens, while files that need no work stay silent. Behavior without `--logwarning`
is unchanged.

This is the "scoped dynamic log-level elevation" pattern: a configured floor level,
raised for the remainder of a file's processing "session" by a trigger event.

## What is already implemented (committed on this branch)

Branch `feature/per-file-log-elevation`, based on `origin/develop`. Code complete,
builds clean, all unit tests pass. Only the live functional verification remains.

- `PlexCleaner/PerFileLogLevel.cs` (new) - per-file session controller:
  - An `AsyncLocal<Holder?>` holds each file's effective level, so parallel file
    processing stays isolated.
  - `BeginScope(floor)` starts a session (returns `IDisposable`); `Elevate()` raises
    the active session to `Information` (idempotent, no-op outside a session).
  - Nested `Filter : ILogEventFilter` gates every event: LogOverride-context events
    always pass; outside a session the configured floor applies; inside a session the
    first `Warning`/`Error` self-elevates the session and passes.
- `PlexCleaner/Program.cs`:
  - Added `LogFloorLevel` property (`Warning` when `--logwarning`, else `Information`).
  - `CreateLogger()` now sets `MinimumLevel.Is(Information)` unconditionally (so
    Information events are generated for the filter to consider) and adds
    `.Filter.With(new PerFileLogLevel.Filter(LogFloorLevel))`. The existing
    `MinimumLevel.Override` for the LogOverride context is unchanged.
- `PlexCleaner/Process.cs`:
  - `ProcessFile(...)` wraps the per-file jump loop in
    `using IDisposable logScope = PerFileLogLevel.BeginScope(Program.LogFloorLevel);`.
- `PlexCleaner/SidecarFile.cs`:
  - `State` is now a property with a backing field `_state`; the setter calls
    `PerFileLogLevel.Elevate()` when the file gains a real (non-`None`) state, which
    covers all `State |= ...` modification sites at once (the "OR modified" trigger).
  - The persisted-state load in `GetMediaPropsFromJson()` assigns `_state` directly
    (not via the setter) so re-reading a previously processed clean file's state does
    not falsely elevate it.

No remediation `Log.Information(...)` call-sites were edited.

## What is left to do: live functional verification

Run these on the system with test media (needs FFmpeg, HandBrake, MkvToolNix,
MediaInfo, 7-Zip available, same as any normal PlexCleaner run). Prepare a small test
folder with at least one file that needs remediation (e.g. a known bad seek-index or
interlaced sample) and one already-clean file.

1. Build and run the clean-compile chain (see Conventions below).
2. Process the folder with `--logwarning --logfile test.log` (use the `process` or
   `monitor` verb as appropriate). Confirm in `test.log`:
   - The faulted file's `Information` remediation lines (e.g. "Remux to repair
     Matroska structure", "Reencoding required tracks", "Repair succeeded") appear,
     interleaved live with its `Warning` detection line.
   - The clean file produces no log output.
   - Timestamps show remediation lines arriving during processing, not batched at the
     end (confirms instant feedback, not buffer-and-flush).
3. Run the same WITHOUT `--logwarning`: output must match current behavior (all
   `Information` shown for all files).
4. Run with `--parallel` over a folder mixing clean and faulted files: confirm no
   cross-file bleed - a clean file processed concurrently with a faulted one does not
   emit elevated lines (validates the `AsyncLocal` scope isolation).
5. Confirm startup / LogOverride verbose messages still appear as before.

If verification surfaces a problem, the likely suspects are: (a) a remediation path
that logs at Information but never logs a Warning and never changes `State` (it would
stay hidden - decide if that path should elevate); (b) log events emitted from a
worker thread spawned inside processing that does not flow the `AsyncLocal` (rare;
not a remediation line).

## After verification: PR flow

Honor the repo's documented workflow (`AGENTS.md`).

- Feature branches merge to `develop` and are squash-only. Do NOT merge to `main`
  (maintainer-only `develop -> main` promotion).
- Push, then drive the Copilot review loop: request review on the current head SHA,
  triage findings, fix or reply with rationale, resolve threads, repeat to green.
- Merge gate: required checks green AND Copilot review on current head SHA AND every
  finding closed AND explicit maintainer permission. `mergeStateStatus: CLEAN` alone
  is never sufficient.
- Put any `Closes #N` keyword on the eventual `develop -> main` promotion PR, not on
  this feature or develop PR (auto-close only fires from the PR that merges to `main`).
- `version.json` bump: this is a functional change (new behavior), so a `version.json`
  minor bump is appropriate, but only by maintainer instruction - confirm before
  bumping.
- Delete this `TODO-per-file-log-elevation.md` file as part of the PR.

## Conventions to honor (from AGENTS.md / CODESTYLE.md)

- Clean-compile after any edit: run the `.NET Format` VS Code task, or natively
  `dotnet csharpier format .` then `dotnet build` then
  `dotnet format style --verify-no-changes --severity=info --verbosity=detailed`.
  Zero warnings (the build treats warnings as errors).
- Run tests: `dotnet test PlexCleanerTests/PlexCleanerTests.csproj`.
- C# line endings are CRLF (`.editorconfig`); preserve them. New `.cs` files must be
  CRLF.
- No `var` (explicit types), Allman braces, file-scoped namespaces, `_camelCase`
  private fields, `s_` static fields.
- US English spelling in all text including commit messages.
- All commits must be cryptographically signed (SSH/GPG); verify signing is live
  before the first commit (`git config --get commit.gpgsign` and `ssh-add -L`).
- Do NOT add `Co-Authored-By:` trailers (AGENTS.md forbids unless the maintainer
  asks).
- Default to staging, not committing; commit only for the authorized task.

## Quick status check commands

```bash
git log --oneline origin/develop..HEAD
dotnet build PlexCleaner/PlexCleaner.csproj
dotnet test PlexCleanerTests/PlexCleanerTests.csproj
```
