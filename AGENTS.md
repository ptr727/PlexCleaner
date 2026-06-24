# Instructions for AI Coding Agents

**PlexCleaner** is a C# .NET utility that optimizes media files for Direct Play in Plex, Emby, Jellyfin, etc.

This file is the agent-agnostic source of truth for cross-cutting rules and this project's architecture and behavioral contracts. Code style lives in [`CODESTYLE.md`](./CODESTYLE.md). [`.github/copilot-instructions.md`](./.github/copilot-instructions.md) is the narrow GitHub Copilot file (inline commit/PR-title summary plus the Copilot Review Runbook); it does not carry project rules.

## Git and Commit Rules

- **Default to staging, not committing.** Stage changes with `git add` and leave `git commit` to the developer unless the developer has explicitly authorized the agent to commit for the current ask ("commit this", "open a PR", etc.). Authorization is scope-bound - it covers the commits needed for that specific task, not a blanket commit license for the rest of the session.
- **All commits must be cryptographically signed (SSH or GPG).** Branch protection enforces this on both branches; unsigned commits are rejected on push. Signing depends on environment configuration - `git config commit.gpgsign true`, a configured `user.signingkey`, and a working signing agent (loaded `ssh-agent` for SSH, or `gpg-agent` for GPG). If signing is not configured in the environment, **do not commit** - surface the missing config to the developer and stop at `git add`. Verify before any agent-authored commit (`git config --get commit.gpgsign && ssh-add -L` or the GPG equivalent). **Signing must be live before the *first* commit, not retrofitted.** Turning on `Require signed commits` against a branch that already has unsigned commits forces a rewrite of that entire history to re-sign it - changing every commit SHA and making whoever does the rewrite the committer and signer of every commit (a rebase preserves the `author` field but not the original signatures; you cannot sign another contributor's commits for them). During new-repo setup, never create commits until signing is verified.
- **Never force push.** Do not run `git push --force` or `git push --force-with-lease` under any circumstances. Force pushing rewrites shared history and can cause data loss.
- **Never run destructive git commands** (`git reset --hard`, `git checkout .`, `git restore .`, `git clean -f`) without explicit developer instruction.
- **The `develop → main` release merge is maintainer-only.** An agent may drive `feature → develop` PRs end-to-end (commit, push, review loop, squash-merge) when authorized, but never self-merges a release to `main` — prepare it and hand it off.

## Pull Request Title and Commit Message Conventions

### Format

- Imperative subject summarizing the change, <=72 characters, no trailing period. ("Add 24-hour PM2.5 average sensor", not "Added X" or "Adds X".)
- Optional body, blank-line separated, explaining *why* the change is being made when that's non-obvious. The diff shows *what*.

### Rules

- Don't write `update stuff`, `wip`, or other vague titles. (Dependabot's default `Bump X from Y to Z` titles are fine - keep them.)
- Don't add `Co-Authored-By:` lines unless the developer explicitly asks.
- Don't put release-bump magnitude in the title - no "minor", "patch", "release v0.2.0", etc. Nerdbank.GitVersioning computes the next release version from `version.json` + git history. Dependency versions in dependency-bump titles are fine and expected.
- Use US English spelling and match the existing heading style of the file you're editing: title case with lowercase short bind words (a, an, the, and, but, or, of, in, on, at, to, by, for, from); hyphenated compounds capitalize both parts unless the second is a short preposition (*Built-in*, *EPA-Corrected*, *24-Hour*).

### Examples

```text
Add structured logging extensions to library
Pin softprops/action-gh-release to commit SHA
Drop net8.0 multi-targeting from console project
Bump xunit.v3 from 3.2.2 to 3.3.0
Clarify devcontainer setup steps in README
```

## Branches and merging

- Pipeline is `feature → develop → main`. Both branches are protected by branch rulesets; everything lands via PR.
- **Feature → develop PRs squash-merge** (single commit on develop, PR title becomes the commit message; never rebase-merge).
- **Develop → main PRs merge-commit** (one merge commit on main per release, develop's tip becomes a second parent and stays in main's ancestry — see [Develop → Main Promotion](#develop--main-promotion)).
- Open feature PRs against `develop`. `develop → main` is how stable releases are cut.

Repo settings reflect this: `allow_merge_commit=true`, `allow_squash_merge=true`, `allow_rebase_merge=false`, `allow_auto_merge=true`. The `develop` ruleset enforces `allowed_merge_methods=["squash"]` and `required_linear_history`. The `main` ruleset enforces `allowed_merge_methods=["merge"]` and intentionally omits linear-history (the develop → main merge commit is non-linear by design).

## Merging a PR

**Never merge a PR without `copilot-pull-request-reviewer[bot]` (shown as "Copilot" in the GitHub UI; the `[bot]` suffix is its actual login) having posted a clean re-review on the latest commit** — defined as a review whose `commit_id` (or GraphQL `commit.oid`) equals the PR's `headRefOid`, with no new unresolved inline threads (Copilot in this repo posts `COMMENTED` reviews, not `APPROVED`, so a clean COMMENTED review with zero open threads is the "no issues found" outcome). `mergeStateStatus: CLEAN` only confirms ruleset gates (thread resolution, status checks, signatures); it does not confirm Copilot has re-evaluated the latest changes.

After resolving Copilot's threads or pushing fixes:

1. **Re-request a Copilot review programmatically** on the current head via the GraphQL `requestReviews` mutation — auto-review-on-push fires inconsistently, so don't wait on it. The full mechanics (bot node-id discovery, the mutation, head-SHA coverage check, thread reply/resolve, bounded retry) are in the [GitHub Copilot Review Runbook](./.github/copilot-instructions.md#github-copilot-review-runbook). The agent can drive this loop end-to-end without a maintainer clicking "re-request review" in the UI.
2. Verify Copilot's most recent review targets the current head — compare its `commit.oid`/`commit_id` to `headRefOid`, not timestamps (multiple reviews and authors clutter the list, and timestamp drift is unreliable).
3. If the fresh review is `COMMENTED` with zero unresolved inline threads (or `APPROVED`), the PR is good to merge.
4. If the fresh review introduces new concerns (inline threads or body-level objections), address them and loop.
5. **If Copilot does not re-review within a reasonable window (~5 min) after re-requesting**, retry per the runbook's bounded-retry workflow (up to three total); if still missing, mark the review blocked and escalate to the maintainer. Silence is not approval.

This applies to every human-authored PR (feature → develop, develop → main). The merge-bot workflow's auto-merge of dependabot bumps is the only exception and is governed separately by the `update-type` filter.

## PR Review Etiquette

> **Mandatory in every derived repo.** This entire "PR Review Etiquette" section is the provider-agnostic review-loop *contract* and must be carried **verbatim** into every repo derived from this template, alongside the [`.github/copilot-instructions.md`](./.github/copilot-instructions.md) "GitHub Copilot Review Runbook" that implements it. Without both in-repo, an agent working in the derived repo has no pointer to the reliable Copilot mechanics and falls back to ad-hoc (and known-broken) behavior.

The repo runs a review loop on every PR: local agent iteration plus remote automated review (GitHub Copilot is the configured reviewer). Treat this as a contract regardless of which local agent authored the changes.

### Merge Gate (read this first)

**Do not merge - and do not enable auto-merge - unless ALL of these hold:**

1. Required status checks are green (`mergeStateStatus: CLEAN`), **and**
2. A Copilot review is confirmed on the **current head SHA** (not an earlier push), **and**
3. **Every** Copilot finding on that head SHA is closed out - all review threads resolved, **and** any issue-level Copilot comments (which have no resolve action) triaged and replied to - so zero outstanding findings remain, **and**
4. The maintainer has given **explicit** permission to merge.

`mergeStateStatus: CLEAN` reflects **only** required statuses - it never reflects open bot review comments, so `CLEAN` alone is **never** sufficient to merge. A green/`CLEAN` PR with an unresolved Copilot finding fails this gate; treat it as "not mergeable" no matter what the merge-state field says. The agent never merges on its own (consistent with "default to staging"; merging is maintainer-authorized).

**Merging is not releasing.** A merge to a release branch does **not** by itself publish; publishing is a separate step in the repo's release pipeline (a scheduled run or a manual dispatch), not an automatic consequence of merging. Never describe a merge as cutting a release, and never trigger a publish without explicit maintainer instruction.

### Expected Review Loop

1. Push changes to the PR branch.
2. Re-request a review for the **current head SHA**. Auto-trigger is unreliable, so request it explicitly via the `requestReviews` GraphQL mutation (now reliable end-to-end - see the runbook); the UI is only a fallback.
3. Wait for review activity on that head. A completed review that raises **no findings** is a valid terminal outcome for that head - proceed; do not re-trigger it or treat the absence of comments as a missing review.
4. Triage findings.
5. Apply fixes or write a rationale for declines.
6. Reply to each thread and resolve what was addressed.
7. Re-run the loop after every fix push until no actionable findings remain.

Drive the loop to green - review confirmed on the latest head SHA and every actionable finding closed - then stop and apply the **Merge Gate** above: all four preconditions must hold, and `mergeStateStatus: CLEAN` alone never satisfies it.

For provider-specific mechanics (how to request review, query review state, post replies, resolve threads), see the **GitHub Copilot Review Runbook** in [.github/copilot-instructions.md](./.github/copilot-instructions.md). This file owns the contract; that file owns the mechanics.

### Triaging Review Comments

For each comment, classify before responding:

- **Bug** - wrong behavior, missing test coverage, or a real divergence between code and docs. Fix it. Reply with the fixing commit SHA when done.
- **Style/convention** - the comment cites a rule from this file or a language-specific style guide. Two cases:
  - The cited rule matches what the existing codebase already does -> fix the offending code.
  - The cited rule contradicts what's in the tree, or industry norm -> **update the rule instead of the code**. The rule is wrong, not the code. Bouncing the same code across rounds is the symptom of a wrong rule. Heuristic: three rounds on the same style category means the rule needs adjusting and the user should authorize the rule change.
- **Architectural opinion** - the comment proposes a different design ("constrain this to disabled-by-default", "move it elsewhere", "add a runtime guardrail"). This is judgment, not a bug. Surface it to the user with a recommendation; don't apply unilaterally.

### Responding and Resolution Expectations

Reply inline with either the fixing commit SHA (for accepted issues) or a concise rationale (for declines). Resolve review threads when addressed or intentionally declined with rationale. Issue-level comments (those at `repos/.../issues/<N>/comments` rather than tied to a specific line) have no resolution action - acknowledge with a reply if needed and move on.

After the final push on a PR, sweep older threads from earlier rounds whose code paths no longer exist; otherwise stale unresolved markers remain in the review UI.

### Escalating to the User

Bring the user in when:

- **Genuine design trade-off** surfaces (fail-open vs fail-closed, narrow vs broad refactor scope, "should we add a guardrail or trust the docstring"). Triage, recommend, ask.
- **Repeated friction** across rounds without convergence - that's the rule-needs-updating signal. Stop, summarize the pattern, and let the user authorize the rule change.
- **Architectural redesign** is requested rather than a bug fix. Surface with a recommendation; never apply unilaterally.

Anti-pattern: don't keep flipping the code on the same style point. Flip the rule once and stick to the rule.

## Develop → Main Promotion

Use the **"Create a merge commit"** option on develop → main PRs. Repo rulesets are split: PRs into `develop` are squash-only (linear history); PRs into `main` are merge-commit only. Clicking "Create a merge commit" on a develop → main PR produces a merge commit on main whose second parent is develop's tip — so develop becomes a real ancestor of main, and the *next* develop → main PR has a clean merge base (no recurring conflicts, no behind-base churn).

Under any squash-only setup this would be a recurring pain point: each develop → main squash drops develop's ancestry and forces a per-cycle admin-bypass merge commit on develop to resync. With merge-commit on main, that resync is unnecessary — main's history shows one merge commit per release (a feature, not a defect: each promotion is visible as a single auditable node), and develop stays linear.

**Immediately after a develop → main merge lands and main's publish workflows complete, bump the minor version in [version.json](version.json) on develop.** Open a small isolated feature PR `bump-version-X.Y` (e.g. `"version": "3.16"` → `"version": "3.17"`), squash into develop, and continue feature work from there. Without this bump, develop's next NBGV-computed prerelease (`3.16.<height>-g{sha}`) is *numerically lower* than the stable that just shipped (`3.16.<N>`), which is visibly confusing in HISTORY.md, `--version` output, and consumer update prompts. Bumping ensures every develop prerelease is `3.17.<height>-g{sha}` — visibly newer than main's `3.16.<N>`. Don't bundle the bump with other work; keep the PR isolated so the version change is unambiguous in git blame.

## Release flow

PlexCleaner is a "pull" project: consumers (`docker pull ptr727/plexcleaner:latest`, `docker pull ptr727/plexcleaner:develop`, GitHub Releases) track both branches. It uses a **two-phase model** that decouples merging from publishing:

- **PRs smoke-test only.** [test-pull-request.yml](.github/workflows/test-pull-request.yml) always runs unit tests, then a [`dorny/paths-filter`](.github/workflows/test-pull-request.yml) `changes` job gates a **reduced** build of only the changed targets (Docker `linux/amd64` only, executable on a `linux-x64` + `win-x64` subset), never pushing. Build-workflow files are intentionally not in the path filters — a filter can't tell a logic change from an action-version bump — so a workflow-only change isn't smoke-built; the reusable workflows are exercised by the next run that uses them. There is no CI workflow-lint job; lint workflow edits with `actionlint` locally before pushing. The `changes` job is in the `Check pull request workflow status` aggregator's `needs` and **must succeed** (not just "not fail") — a paths-filter error must never let a target-changing PR merge with its smoke build silently skipped.
- **Merges don't publish by default.** [publish-release.yml](.github/workflows/publish-release.yml) is the **sole publisher**: its **weekly schedule** (Mondays 02:00 UTC) and **manual `workflow_dispatch`** always do the full multi-arch build/publish of **both** `main` and `develop` (a branch matrix in one run). Its `push` trigger publishes only when the **`PUBLISH_ON_MERGE` repository variable** is `true` (opt-in legacy continuous-release). Unset/`false` = two-phase: routine merges to `develop`/`main` only smoke-build, and `:latest`/`:develop` Docker tags + GitHub releases refresh on the weekly run instead of on every merge.

A `setup` job computes the plan: `push` ⇒ the pushed branch with `publish = (vars.PUBLISH_ON_MERGE == 'true')`; `schedule`/`dispatch` ⇒ both branches with `publish = true`. The `publish` job is a `matrix.branch` fan-out over [build-release-task.yml](.github/workflows/build-release-task.yml); `tool-versions`, `docker-readme` (main only), and `date-badge` (main only) run after it.

Branch-aware config keys off the **`branch` input** threaded through every reusable task — **never `github.ref_name`** (the publisher builds `develop` from a run whose `github.ref_name` is `main`, so a fallback would mislabel it). `main` ⇒ Release / `latest` / stable release; anything else ⇒ Debug / `develop` / prerelease. The GitHub release's `target_commitish` is pinned to NBGV's `GitCommitId` (the exact built commit) — not `github.sha` (wrong on the develop leg) and not a branch name (a moving ref); `get-version-task.yml` surfaces `GitCommitId` as an output. The release step is skipped when a release for the computed `SemVer2` tag already exists (no-op weekly republish), except on `workflow_dispatch` (which can refresh a partial release).

**Per-target subsetting (derived projects).** `build-release-task.yml` has per-target `enable_*` gates and self-contained leaf tasks, so a project that drops a target deletes: its `build-<target>-task.yml`, the matching job + `github-release` `needs` entry in `build-release-task.yml`, and its path-filter entry in `test-pull-request.yml`. Versioning, badge, tool-versions, Docker README, merge-bot, and Dependabot are target-agnostic. PlexCleaner's targets are the **Docker image** and the **console executable** only (no NuGet/PyPI).

Bot-merged PRs (Dependabot) still trigger `publish-release.yml` because the merge-bot uses an App token (see the merge-bot section) — under the default two-phase model that push run is a no-op publish unless `PUBLISH_ON_MERGE` is set.

## Dependabot

[.github/dependabot.yml](.github/dependabot.yml) targets **both `main` and `develop`** with two ecosystems each (`nuget`, `github-actions`), grouped per ecosystem, daily. The duplication is intentional: both branches ship from the weekly publisher (and on every merge when `PUBLISH_ON_MERGE` is set), so develop must not drift from main's dependency baseline. A NuGet major bump landing on develop should land on main on the next promotion cycle, not weeks later.

Major NuGet bumps are not auto-merged by [merge-bot-pull-request.yml](.github/workflows/merge-bot-pull-request.yml) — they require human review. Major GitHub Actions bumps are auto-merged because the workflow execution itself is the validation surface.

## GitHub Actions pinning

Every third-party action in `.github/workflows/*.yml` is pinned to a full commit SHA with a trailing comment matching the upstream release tag, e.g. `uses: actions/checkout@de0fac2e4500dabe0009e67214ff5f5447ce83dd # v6.0.2`. The comment is whatever tag the action's repo actually publishes — typically `# vX.Y.Z`, but use `# v3` if upstream only publishes major-only tags (e.g. `addnab/docker-run-action`) and `# master` if the action ships only a moving branch (rare). Floating refs without a SHA (`@v6`, `@main`, `@master`) are never used. **Documented exception:** [`dotnet/nbgv`](.github/workflows/get-version-task.yml) is consumed via `@master` because the upstream tag stream lags `master` substantially and Dependabot's tag-tracking would propose a downgrade. Local reusable workflows (`./.github/workflows/*.yml`) are referenced by path and don't need pinning.

**Why:** Floating tags can be silently re-pointed by the action's owner (or by a compromised account) to malicious code; a SHA pin is immutable. Matching the comment to upstream's actual release tag (rather than fabricating one) lets dependabot rewrite both the SHA and the comment together when bumping.

When adding a new `uses:` line, resolve the latest release's commit SHA (`gh api repos/<owner>/<repo>/releases/latest`) and copy its `tag_name` into the comment verbatim. Don't ship a floating tag and "pin it later".

## Merge bot

[merge-bot-pull-request.yml](.github/workflows/merge-bot-pull-request.yml) auto-merges Dependabot PRs. Key design choices:

- **`pull_request_target`, not `pull_request`**: the jobs hold the App private key, so the workflow definition and its action SHAs resolve from the trusted base branch, not the PR head. This is safe because no job checks out PR code - each only runs `gh pr merge` against the PR by URL.
- **Per-PR concurrency**: under `pull_request_target` `github.ref` is the base branch (which would serialize every bot PR against that base), so the group keys on `github.event.pull_request.number` and uses `cancel-in-progress: false` so a follow-up `synchronize` doesn't cancel an in-flight `opened` run before it enables auto-merge.
- **Enable once, then sticky**: `merge-dependabot` enables auto-merge only on `opened`/`reopened`. `disable-auto-merge-on-maintainer-push` fires on a `synchronize` whose actor is not the bot and calls `gh pr merge --disable-auto`, so maintainer repair commits don't auto-merge with the bot's content; re-enable manually when ready.
- **Branch-aware merge method**: the script picks `--squash` for PRs targeting develop and `--merge` for PRs targeting main, matching each ruleset's `allowed_merge_methods`. An unknown base branch is a hard error.
- **App token, not GITHUB_TOKEN**: the merge step uses a token minted by `actions/create-github-app-token` from `CODEGEN_APP_CLIENT_ID` / `CODEGEN_APP_PRIVATE_KEY` secrets. Pushes authored by `GITHUB_TOKEN` are blocked from triggering downstream workflows by GitHub's recursion guard; without the App token, a Dependabot merge would silently skip `publish-release.yml` on the merge commit. Under the default two-phase model that push is a no-op publish (it only republishes when `PUBLISH_ON_MERGE` is `true`), but the App token keeps that opt-in path — and any future push-triggered workflow — working.

The App secrets (`CODEGEN_APP_CLIENT_ID`, `CODEGEN_APP_PRIVATE_KEY`) must exist in **both** secret namespaces: Settings → Secrets and variables → **Actions**, and Settings → Secrets and variables → **Dependabot**. Since Sept 2021, GitHub injects only the Dependabot-namespace secrets when a Dependabot-authored event fires; the regular Actions namespace is not visible to that run. Without the Dependabot duplicate the App-token step gets empty inputs and merge-bot silently fails to auto-merge.

PlexCleaner has no codegen workflow and wraps no upstream release, so the template's `merge-codegen` and `merge-upstream-version` jobs are absent (see [Template Adaptations](#template-adaptations)).

## Project Overview

PlexCleaner is a .NET 10.0 CLI utility that optimizes media files for Direct Play in Plex/Emby/Jellyfin by:

- Converting containers to MKV format
- Re-encoding incompatible video/audio codecs
- Managing tracks (language tags, duplicates, subtitles)
- Verifying and repairing media integrity
- Removing closed captions and unwanted content
- Monitoring folders for changes and automatically processing new/modified files

The tool orchestrates external media processing tools (FFmpeg, HandBrake, MkvToolNix, MediaInfo, 7-Zip) via CLI wrappers.

User-facing documentation: [README.md](./README.md) (quick start, installation, usage, FAQ), [Docs/LanguageMatching.md](./Docs/LanguageMatching.md) (IETF/RFC 5646 tag matching), [Docs/CustomOptions.md](./Docs/CustomOptions.md) (FFmpeg/HandBrake custom parameters, hardware acceleration), [Docs/ClosedCaptions.md](./Docs/ClosedCaptions.md) (EIA-608/CTA-708 detection), [HISTORY.md](./HISTORY.md) (release notes).

## Project Structure

- **PlexCleaner**: CLI application
- **PlexCleanerTests**: Unit tests using xUnit v3 and AwesomeAssertions
- **Sandbox**: Sandbox/testing utility project
- **Docker**: Multi-platform Linux containers

Common MSBuild properties (`TargetFramework`, `Nullable`, `ImplicitUsings`, `AnalysisLevel`, etc.) live in `Directory.Build.props` at the solution root; only add a property to a `.csproj` when it is project-specific or overrides the shared default. All NuGet package versions are centralised in `Directory.Packages.props`; `PackageReference` elements must not include a `Version` attribute, but asset metadata (`PrivateAssets`, `IncludeAssets`) stays in the `.csproj` element. Target latest .NET SDK (currently .NET 10 with C# 14); support VS Code (`.code-workspace`) and Visual Studio (`.slnx`) across Linux, Windows, and macOS.

## Architecture and Behavioral Contracts

These are project-specific contracts a reviewer must honor. Code-style rules live in [`CODESTYLE.md`](./CODESTYLE.md).

### Commands

`process`, `monitor`, `verify`, `remux`, `reencode`, `deinterlace`, `createsidecar`, `updatesidecar`, `getsidecarinfo`, `gettoolinfo`, `gettagmap`, `getmediainfo`, `checkfornewtools` (Windows only), `defaultsettings`, `createschema`, `removesubtitles`, `removeclosedcaptions`, `testmediainfo`, `getversioninfo`. Each command maps to a static method in `Program.cs`; recursive options (`--logfile`, `--logwarning`, `--debug`) are available to all subcommands.

### Fluent Builder Pattern for Media Tools

All media tool command-line construction uses fluent builders (`*Builder.cs`) - **never concatenate argument strings**. Builder methods return `this` for chaining.

```csharp
// Correct - fluent builder pattern
var command = new FfMpeg.GlobalOptions(args).Default().Add(customOption);

// Wrong - string concatenation
string args = "-hide_banner " + option;
```

### Process Execution with CliWrap

All external process execution uses [CliWrap](https://github.com/Tyrrrz/CliWrap) (v3.x): builders create `ArgumentsBuilder` instances; execute via `Cli.Wrap(toolPath).WithArguments(builder)`; use `BufferedCommandResult` for output capture. See `MediaTool.cs` for base execution patterns. All tool execution supports cancellation via `Program.CancelToken()`.

### Sidecar File System

Critical performance feature - **DO NOT break compatibility** (versioned schema migrations only):

- Each `.mkv` gets a `.PlexCleaner` sidecar JSON file holding processing state, tool versions, media properties, and a file hash.
- Hash: first 64KB + last 64KB of file (not timestamp-based).
- Schema versioned (`SchemaVersion: 5` in `SidecarFileJsonSchema5`, global alias in `GlobalUsing.cs`).
- Processing skips verified files unless the sidecar is invalidated.
- State flags are bitwise: `StatesType` enum with `[Flags]`; check with `HasFlag()`, combine with `|=`.
- Operations: `Create()`, `Read()`, `Update()`, `Delete()`.

### Media Tool Abstraction

`MediaTool` base class defines tool lifecycle. Each tool family has a Tool class, Builder class, and Info schema. Tool version info is retrieved from CLI output and cached in `Tools.json`. Windows supports auto-download via `GitHubRelease.cs`; Linux uses system tools. Tool paths: `ToolsOptions.UseSystem` or `RootPath + ToolFamily/SubFolder/ToolName`.

### Media Properties and Track Management

`MediaProps` aggregates video/audio/subtitle tracks. `TrackProps` is the base for `VideoProps`, `AudioProps`, `SubtitleProps`. Track properties: language tags ISO 639-2B (`Language`) and RFC 5646/BCP 47 (`LanguageIetf`); flags (Default, Forced, HearingImpaired, VisualImpaired, Descriptions, Original, Commentary); state (Keep, Remove, ReMux, ReEncode, DeInterlace, SetFlags, SetLanguage, Unsupported). Track selection (`SelectMediaProps.cs`) separates tracks into Selected/NotSelected for language filtering, duplicate removal, and codec selection.

### Language Tag Management

Uses external package `ptr727.LanguageTags` for parsing/matching. Tag format `language-extlang-script-region-variant-extension-privateuse`; left-to-right prefix matching via `LanguageLookup.IsMatch()`; conversion ISO 639-2B <-> RFC 5646 via `GetIsoFromIetf()` / `GetIetfFromIso()`. Special tags `und`, `zxx`, `en`. **Do not break the IETF/ISO conversion logic.** At least one track is always kept even if no language matches.

### Monitor Mode

`FileSystemWatcher` monitors specified folders (size, creation/last-write time, file/directory name; Changed/Created/Deleted/Renamed events) into a timestamped queue. Files must settle (no changes for `MonitorWaitTime` seconds) and be readable before processing; `FileRetryCount` attempts with `FileRetryWaitTime` delays. Lock-based queue management (`_watchLock`), 1-second poll interval, parallel processing when `--parallel` enabled.

### XML and JSON Parsing

AOT-safe parsers in `MediaInfoXmlParser.cs` - **do not use `XmlSerializer`** (not AOT-compatible). `MediaInfoFromXml()` manually parses known MediaInfo elements; `GenericXmlToJson()` converts any XML to JSON preserving all elements/attributes (two-pass array detection, streaming `XmlReader` + `Utf8JsonWriter`); `MediaInfoXmlToJson()` bridges parsed XML to the MediaInfo JSON schema.

### Concurrency and Cancellation

`--parallel` enables PLINQ (`AsParallel()`, `WithDegreeOfParallelism()`) in `ProcessDriver.cs`; default thread count min(CPU/2, 4), configurable via `--threadcount`. Files are grouped by path (excluding extension) to prevent concurrent access to the same file. The main loop uses a `WaitForCancel()` polling pattern, not async/await; tool execution wraps CliWrap synchronously. Global `CancellationTokenSource` accessed via `Program.CancelToken()`; console handlers for Ctrl+C/Z/Q.

### Configuration Schema

Settings in `PlexCleaner.defaults.json` (JSONC with inline comments); schema `PlexCleaner.schema.json` auto-generated via `JsonSchema.Net` with a source-generated context. ConfigFile schemas are versioned (`ConfigFileJsonSchema4`, etc.); each options class has `SetDefaults()` and `VerifyValues()`. URL reference: `https://raw.githubusercontent.com/ptr727/PlexCleaner/main/PlexCleaner.schema.json`.

### Special Cases

- **Closed captions**: EIA-608/CTA-708 tracks handled in `SubtitleProps.HandleClosedCaptions()`, parsed as subtitle tracks, removed during processing; IDs formatted `{VideoId}-CC{Number}` (e.g. `256-CC1`).
- **VOBSUB subtitles**: require `MuxingMode` for Plex compatibility; missing it triggers error and removal recommendation.
- **Duplicate tracks**: language-based grouping with flag preservation; preferred audio codec via `FindPreferredAudio()`; keeps one flagged track per flag type plus one non-flagged.
- **Keep-awake**: `KeepAwake.cs` prevents system sleep during long operations via Windows API (30s timer); no-op on non-Windows.

### DO / DO NOT

- **DO**: add tests for media tool parsing changes (see `FfMpegIdetParsingTests.cs`); update `HISTORY.md` for notable changes; use `Program.CancelToken()` for cancellation; log with context (filenames, state transitions, tool versions); handle cross-platform paths (`Path.Combine`); version schemas on breaking changes and update aliases in `GlobalUsing.cs`.
- **DO NOT**: break sidecar compatibility; use string concatenation for tool arguments; modify file timestamps unless `RestoreFileTimestamp` is enabled; execute media tools without the CliWrap abstraction; add synchronous operations in parallel paths; use `XmlSerializer`; break language tag matching.

## Template Adaptations

Intentional, documented deviations from the [ProjectTemplate](https://github.com/ptr727/ProjectTemplate) verbatim-carry baseline. Everything else is carried as-is.

- **`.editorconfig` CA-suppression block.** The carried `[*.cs]` block keeps a PlexCleaner-specific set of `dotnet_diagnostic.CA*` repo-wide suppressions (public-API-surface rules inapplicable to a console app, no-localization/no-SynchronizationContext context, documented false-positives). These follow CODESTYLE.md "Analyzer Diagnostics and Suppressions" - each is a repo-wide-applicable rule with an inline justification, not a brownfield batch-relax. Everything else in `.editorconfig` matches the template verbatim.
- **Husky.Net commit gate retained.** PlexCleaner keeps a working Husky.Net pre-commit gate (`dotnet husky run`) as the local convenience gate the template's CODESTYLE.md "Clean-Compile Verification" explicitly permits a .NET repo to keep. The template ships no hook runner by default; keeping a working one is not drift.
- **merge-codegen / merge-upstream-version jobs absent.** PlexCleaner has no codegen workflow and wraps no upstream release, so the merge-bot carries only `merge-dependabot` and `disable-auto-merge-on-maintainer-push`. The template's `merge-codegen` and `merge-upstream-version` jobs do not apply.
- **Targets: Docker image + console executable only.** No NuGet/PyPI side; the per-target subsetting (template Release Model) drops those leaf tasks and their `github-release` `needs` entries.
- **Docker Hub README via m4 render.** `Docker/README.md` is generated from `Docker/README.m4`; the m4 render command is passed to the canonical `publish-docker-readme-task.yml` via its optional transform-run input rather than a bespoke build step.
