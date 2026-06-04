# PlexCleaner AI Coding Instructions

## Project Overview

PlexCleaner is a .NET 10.0 CLI utility that optimizes media files for Direct Play in Plex/Emby/Jellyfin by:

- Converting containers to MKV format
- Re-encoding incompatible video/audio codecs
- Managing tracks (language tags, duplicates, subtitles)
- Verifying and repairing media integrity
- Removing closed captions and unwanted content
- Monitoring folders for changes and automatically processing new/modified files

The tool orchestrates external media processing tools (FFmpeg, HandBrake, MkvToolNix, MediaInfo, 7-Zip) via CLI wrappers.

## Branching, Releases, and Bot Behavior

For full rationale see [`AGENTS.md`](../AGENTS.md). Quick rules:

- `feature → develop → main`. PRs only.
- Develop accepts **squash merges only**; main accepts **merge commits only**. Don't suggest rebase-merge — it's disabled at the repo level.
- **Two-phase publishing.** PRs only **smoke-build** changed targets (Docker `linux/amd64`, a 2-runtime executable subset, no push). `publish-release.yml` is the sole publisher: its **weekly schedule + manual dispatch** build/publish **both** branches (develop ⇒ NBGV prereleases `X.Y.Z-g{sha}` tagged `develop`; main ⇒ stable `X.Y.Z` tagged `latest`). Routine merges do **not** publish unless the `PUBLISH_ON_MERGE` repo variable is `true`.
- Dependabot targets **both** `main` and `develop` with the same ecosystems; major NuGet bumps gate on human review, everything else auto-merges via App-token-driven merge-bot.
- Every third-party GitHub Action is pinned to a full commit SHA with a `# vX.Y.Z` comment. Don't introduce `@v6` / `@main` / `@master` floating refs.
- Never merge a PR without a fresh "no issues found" review from `copilot-pull-request-reviewer[bot]` (shown as "Copilot" in the UI) on the latest commit. `mergeStateStatus: CLEAN` is necessary but not sufficient — Copilot's re-review of the latest push is required. Re-request the review **programmatically** after every push via the `requestReviews` GraphQL mutation (don't wait on flaky auto-review-on-push) — see the [GitHub Copilot Review Runbook](#github-copilot-review-runbook) below and [`AGENTS.md`](../AGENTS.md#merging-a-pr).
- After a develop → main merge lands and main's publish workflows complete, bump the minor in `version.json` on develop (e.g. `3.16` → `3.17`) via an isolated `bump-version-X.Y` PR. Without it, develop's next prerelease version numbers fall below main's just-shipped stable.
- Don't recommend `git push --force` or `--force-with-lease`; both rulesets enforce `non_fast_forward`.
- `version.json`'s `publicReleaseRefSpec` is `^refs/heads/main$` — bumping the base `version` field is the only manual versioning action.

## Documentation

User-facing documentation is organized as follows:

- **[README.md](../README.md)**: Main project documentation, quick start, installation, usage, and FAQ.
- **[Docs/LanguageMatching.md](../Docs/LanguageMatching.md)**: Technical details on IETF/RFC 5646 language tag matching and configuration.
- **[Docs/CustomOptions.md](../Docs/CustomOptions.md)**: FFmpeg and HandBrake custom encoding parameters, hardware acceleration setup, and encoder options.
- **[Docs/ClosedCaptions.md](../Docs/ClosedCaptions.md)**: Detailed technical analysis of EIA-608/CTA-708 closed caption detection methods and tools.
- **[HISTORY.md](../HISTORY.md)**: Release notes and version history.

## Architecture

### Command Structure

PlexCleaner provides multiple commands:

- **process**: Batch process media files in specified folders
- **monitor**: Watch folders for changes and automatically process modified files
- **verify**: Verify media files using FFmpeg
- **remux**: Re-multiplex media files to MKV
- **reencode**: Re-encode media tracks using HandBrake or FFmpeg
- **deinterlace**: De-interlace media files
- **createsidecar**: Create sidecar files for existing media
- **gettoolinfo**: Display tool version information
- **gettagmap**: Analyze language tags across media files
- **getmediainfo**: Extract and display media properties
- **checkfornewtools**: Check for and download tool updates (Windows only)
- **defaultsettings**: Create default configuration file
- **createschema**: Generate JSON schema for configuration validation
- **removesubtitles**: Remove all subtitle tracks
- **removeclosedcaptions**: Remove embedded EIA-608/CTA-708 closed captions from video streams
- **updatesidecar**: Create or update sidecar files to current schema/tool info
- **getsidecarinfo**: Display sidecar file information
- **testmediainfo**: Test parsing media tool information for non-Matroska containers
- **getversioninfo**: Print application and media tool version information

### Fluent Builder Pattern for Media Tools

All media tool command-line construction uses fluent builders (`*Builder.cs`). Never concatenate strings:

```csharp
// Correct - fluent builder pattern
var command = new FfMpeg.GlobalOptions(args)
    .Default()
    .Add(customOption);

// Wrong - string concatenation
string args = "-hide_banner " + option;
```

### Process Execution with CliWrap

All external process execution uses [CliWrap](https://github.com/Tyrrrz/CliWrap) (v3.x):

- Builders create `ArgumentsBuilder` instances
- Execute via `Cli.Wrap(toolPath).WithArguments(builder)`
- Use `BufferedCommandResult` for output capture
- See `MediaTool.cs` for base execution patterns
- All tool execution supports cancellation via `Program.CancelToken()`

### Sidecar File System

Critical performance feature - DO NOT break compatibility:

- Each `.mkv` gets a `.PlexCleaner` sidecar JSON file
- Contains: processing state, tool versions, media properties, file hash
- Hash: First 64KB + last 64KB of file (not timestamp-based)
- Schema versioned (`SchemaVersion: 5` in `SidecarFileJsonSchema5`, global alias in `GlobalUsing.cs`)
- Processing skips verified files unless sidecar invalidated
- State flags are bitwise: `StatesType` enum with `[Flags]` attribute
- Sidecar operations: `Create()`, `Read()`, `Update()`, `Delete()`

### Media Tool Abstraction

- `MediaTool` base class defines tool lifecycle
- Each tool family has: Tool class, Builder class, Info schema
- Tool version info retrieved from CLI output, cached in `Tools.json`
- Windows supports auto-download via `GitHubRelease.cs`; Linux uses system tools
- Tool paths: `ToolsOptions.UseSystem` or `RootPath + ToolFamily/SubFolder/ToolName`
- Tool execution: Base `Execute()` method with cancellation, logging, and error handling
- Version checking: `GetInstalledVersion()`, `GetLatestVersion()` (Windows only)

### Media Properties and Track Management

**MediaProps hierarchy:**

- `MediaProps`: Container for all media information (video, audio, subtitle tracks)
- `TrackProps`: Base class for all track types
  - `VideoProps`: Video track properties (format, resolution, codec, HDR, interlacing)
  - `AudioProps`: Audio track properties (format, channels, sample rate, codec)
  - `SubtitleProps`: Subtitle track properties (format, codec, closed captions)

**Track properties:**

- Language tags: ISO 639-2B (`Language`) and RFC 5646/BCP 47 (`LanguageIetf`)
- Flags: Default, Forced, HearingImpaired, VisualImpaired, Descriptions, Original, Commentary
- State: Keep, Remove, ReMux, ReEncode, DeInterlace, SetFlags, SetLanguage, Unsupported
- Title, Format, Codec, Id, Number, Uid

**Track selection (`SelectMediaProps.cs`):**

- Separates tracks into Selected/NotSelected categories
- Used for language filtering, duplicate removal, codec selection
- Move operations: `Move(track, toSelected)`, `Move(trackList, toSelected)`
- State assignment: `SetState(selectedState, notSelectedState)`

### Language Tag Management

**IETF/RFC 5646 Support:**

- Uses external package `ptr727.LanguageTags` for language tag parsing and matching
- Tag format: `language-extlang-script-region-variant-extension-privateuse`
- Matching: Left-to-right prefix matching via `LanguageLookup.IsMatch()`
- Conversion: ISO 639-2B ↔ RFC 5646 via `GetIsoFromIetf()`, `GetIetfFromIso()`
- Special tags: `und` (undefined), `zxx` (no linguistic content), `en` (English)

**Language processing:**

- MediaInfo reports both ISO 639-2B and IETF tags (if set)
- MkvMerge normalizes to IETF tags when `SetIetfLanguageTags` enabled
- FFprobe uses tag metadata which may differ from track metadata
- Track validation: Checks ISO/IETF consistency, sets error states for mismatches

### Monitor Mode

**File system watching:**

- Uses `FileSystemWatcher` to monitor specified folders
- Monitors: Size, CreationTime, LastWrite, FileName, DirectoryName
- Handles: Changed, Created, Deleted, Renamed events
- Queue-based: Changes added to watch queue with timestamps

**Processing logic:**

- Files must "settle" (no changes for `MonitorWaitTime` seconds) before processing
- Files must be readable (not being written) before processing
- Retry logic: `FileRetryCount` attempts with `FileRetryWaitTime` delays
- Cleanup: Deletes empty folders after file removal
- Pre-process: Optional initial scan of all monitored folders on startup

**Concurrency:**

- Lock-based queue management (`_watchLock`)
- Periodic processing (1-second poll interval)
- Supports parallel processing when `--parallel` enabled

### XML and JSON Parsing

AOT-safe parsers in `MediaInfoXmlParser.cs`:

- **MediaInfoFromXml()**: Parses specific MediaInfo XML elements into `MediaInfoToolXmlSchema.MediaInfo`
  - Manually parses only known elements needed by PlexCleaner (id, format, language, etc.)
  - Used by sidecar file system to parse XML output when JSON unavailable
  - Avoids XmlSerializer (not AOT-compatible)
- **GenericXmlToJson()**: Converts any XML file to JSON format
  - Preserves all elements and attributes (unlike MediaInfoFromXml's selective parsing)
  - Handles attributes: prefix with `@` for elements with children, no prefix for leaf elements
  - Detects arrays: elements appearing multiple times become JSON arrays
  - Two-pass algorithm: collect children to detect arrays, then write JSON
  - Uses `XmlReader` and `Utf8JsonWriter` for streaming efficiency
  - Special handling for MediaInfo's mixed attribute/text content format (creatingLibrary)
- **MediaInfoXmlToJson()**: Converts parsed MediaInfo XML to MediaInfo JSON schema
  - Bridges between XML and JSON schema types
  - Maps only known MediaInfo track properties

Parser design patterns:

- Forward-only `XmlReader` with depth tracking for streaming
- Recursive `ElementData` tree for generic XML-to-JSON conversion
- Namespace filtering (skip `xmlns`, `xsi` attributes)
- Special handling for MediaInfo's mixed attribute/text content format

### Extensions Pattern

**Modern C# 13 extension syntax:**

- Uses implicit class extensions: `extension(ILogger logger)`
- Provides context-aware helper methods
- Examples:
  - `LogAndPropagate()`: Log exception and return false (propagates error)
  - `LogAndHandle()`: Log exception and return true (handles error for catch clauses)
  - `LogOverrideContext()`: Create scoped logger with LogOverride context

## Code Conventions

### Formatting Standards

- **Code formatter**: CSharpier (`.csharpier.json`) - primary formatter
- **EditorConfig**: `.editorconfig` follows .NET Runtime style guide
- **Pre-commit hooks**: Husky.Net validates style (`dotnet husky run`)
- Line endings: CRLF for Windows files (`.cs`, `.json`, `.yml`), LF for shell scripts
- Charset: UTF-8 without BOM

### Code Style

- Target: .NET 10.0 (`<TargetFramework>net10.0</TargetFramework>`)
- AOT compilation enabled: `<PublishAot>true</PublishAot>` in executable projects
- Use C# modern features (records, pattern matching, collection expressions, implicit class extensions)
- Prefer `Debug.Assert()` for internal invariants
- Logging: Serilog with thread IDs (`Log.Information/Warning/Error`)
- Exception handling: Currently uses broad `catch(Exception)` - TODO to specialize
- Global usings: `GlobalUsing.cs` defines project-wide type aliases (`ConfigFileJsonSchema`, `SidecarFileJsonSchema`)
- `Directory.Build.props`: Common MSBuild properties (`TargetFramework`, `Nullable`, `ImplicitUsings`,
  `AnalysisLevel`, etc.) shared across all projects live here at the solution root. Do not duplicate
  these in individual `.csproj` files -- only add a property to a `.csproj` when it is project-specific
  or overrides the shared default.
- `Directory.Packages.props`: All NuGet package versions are centralised here via `PackageVersion` items.
  `PackageReference` elements in `.csproj` files must not include a `Version` attribute. Asset metadata
  (`PrivateAssets`, `IncludeAssets`) stays in the `.csproj` `PackageReference` element.

### Naming and Structure

- JSON schemas: Generated via `JsonSchema.Net`, suffixed with version (e.g., `SidecarFileJsonSchema5`)
- Builder methods: Return `this` for chaining
- Media props: `*Props.cs` classes (VideoProps, AudioProps, SubtitleProps, TrackProps)
- Options classes: `*Options.cs` for command categories (ProcessOptions, VerifyOptions, ConvertOptions, ToolsOptions)
- Partial classes: Tool families use partial class structure (`*Tool.cs`, `*Builder.cs`)

### Async and Concurrency

- Main loop: Uses `WaitForCancel()` polling pattern instead of async/await
- Tool execution: Synchronous wrappers around CliWrap async operations
- Parallel processing: PLINQ with `AsParallel()`, `WithDegreeOfParallelism()`
- Lock-based synchronization: `Lock` instances for collection access
- Cancellation: Global `CancellationTokenSource` accessed via `Program.CancelToken()`

## Testing

### Test Framework

- xUnit v3.x with `AwesomeAssertions`
- Test project: `PlexCleanerTests/`
- Fixture: `PlexCleanerFixture` (assembly-level, sets up defaults and logging)
- Sample media: `Samples/PlexCleaner/` (relative path `../../../../Samples/PlexCleaner`)

### Test Coverage

- Command-line parsing: `CommandLineTests.cs`
- Configuration validation: `ConfigFileTests.cs`
- FFmpeg parsing: `FfMpegIdetParsingTests.cs`
- Sidecar functionality: `SidecarFileTests.cs`
- Version parsing: `VersionParsingTests.cs`
- Wildcards: `WildcardTests.cs`
- Filename escaping for filters: `FileNameEscapingTests.cs`

### Test Execution

- Task: `"dotnet: .Net Build"` for builds
- Unit tests: `dotnet test` or VS Code test explorer
- Docker tests: Download Matroska test files from GitHub
- CI: Separate workflows for build tests and Docker tests

## Build and Release

### Local Development

```bash
# Build
dotnet build

# Format code
dotnet csharpier .

# Verify formatting
dotnet format style --verify-no-changes --severity=info --verbosity=detailed

# Run tests
dotnet test

# Pre-commit validation (automatic via Husky)
dotnet husky run
```

### GitHub Actions

Two-phase model — reusable `*-task.yml` workflows orchestrated by two entry points:

- **test-pull-request.yml**: PR validation. `changes` (dorny/paths-filter) → always-on `unit-test` (Husky) + path-gated `smoke-build` (reduced, no-push) → `Check pull request workflow status` aggregator (ruleset-bound name; requires `changes` succeeded).
- **publish-release.yml**: the **sole publisher** (`push` + weekly `schedule` + `workflow_dispatch`). A `setup` job computes the branch list + publish gate; the `publish` matrix builds both branches via `build-release-task.yml` (executable 7-RID matrix + multi-arch Docker `linux/amd64,linux/arm64` + GitHub release), then `tool-versions`, `docker-readme` (main only), `date-badge` (main only).
- Reusable tasks: `build-release-task.yml`, `build-executable-task.yml`, `build-docker-task.yml`, `build-toolversions-task.yml`, `build-dockerreadme-task.yml`, `build-datebadge-task.yml`, `get-version-task.yml`. All thread a required `branch` input (config keys off it, never `github.ref_name`) plus `ref`/`smoke`.
- Version info: `version.json` with Nerdbank.GitVersioning format. `get-version-task.yml` surfaces `SemVer2`, the assembly versions, and `GitCommitId` (used to pin the release `target_commitish`).
- Branches: `main` (stable releases, `latest`), `develop` (pre-releases, `develop`).

### Docker

- Multi-stage builds in `Docker/Dockerfile`
- Base image: `ubuntu:rolling` only (no longer publishing Alpine or Debian variants)
- Supported architectures: `linux/amd64`, `linux/arm64` (no longer supporting `linux/arm/v7`)
- Tool installation: Ubuntu package manager (apt)
- Media tool versions match Windows versions for consistent behavior
- Test script: `Docker/Test.sh` validates all commands
- Version extraction: `Docker/Version.sh` captures tool versions for README
- User: Runs as `nonroot` user in containers
- Volumes: `/media` for media files and configuration

## Common Patterns

### Command-Line Parsing

Uses `System.CommandLine` (v2.x):

- Options defined in `CommandLineOptions.cs`
- Binding via `CommandLineParser.Bind()`
- No `System.CommandLine.NamingConventionBinder` (deprecated)
- Recursive options: Available to all subcommands (`--logfile`, `--logwarning`, `--debug`)
- Command routing: Each command maps to static method in `Program.cs`

### Parallel Processing

- `--parallel` flag enables concurrent file processing
- Uses `ProcessDriver.cs` with `AsParallel()` and `WithDegreeOfParallelism()`
- Default thread count: min(CPU/2, 4), configurable via `--threadcount`
- Lock-based collection updates in parallel contexts
- File grouping: Groups by path (excluding extension) to prevent concurrent access to same file

### File Processing States

```csharp
[Flags]
enum StatesType {
    None, SetLanguage, ReMuxed, ReEncoded, DeInterlaced,
    Repaired, RepairFailed, Verified, VerifyFailed,
    BitrateExceeded, ClearedTags, FileReNamed, FileDeleted,
    FileModified, ClearedCaptions, RemovedAttachments,
    SetFlags, RemovedCoverArt
}
```

Check states with `HasFlag()`, combine with `|=`

### Configuration Schema

- Settings: `PlexCleaner.defaults.json` with inline JSONC comments
- Schema: `PlexCleaner.schema.json` (auto-generated via JsonSchema.Net)
- Validation: JSON Schema.Net with source-generated context
- URL schema reference: `https://raw.githubusercontent.com/ptr727/PlexCleaner/main/PlexCleaner.schema.json`
- Versioned: ConfigFile schemas numbered (ConfigFileJsonSchema4, etc.)
- Defaults: `SetDefaults()` method in each options class
- Verification: `VerifyValues()` method validates configuration

### Keep-Awake Pattern

- Prevents system sleep during long operations
- Uses `KeepAwake.cs` with Windows API calls
- Timer-based: Refreshes every 30 seconds
- Cross-platform: No-op on non-Windows systems

### Cancellation Handling

- Global token source: `Program.s_cancelSource`
- Console handlers: Ctrl+C, Ctrl+Z, Ctrl+Q
- Keyboard monitoring: Separate task for key press handling
- Tool execution: All CliWrap calls use `Program.CancelToken()`
- Graceful cleanup: Logs cancellation messages, disables file watchers

## Critical Details

### DO NOT

- Break sidecar file compatibility (versioned schema migrations only)
- Use string concatenation for command-line arguments (use builders)
- Modify file timestamps unless `RestoreFileTimestamp` enabled
- Execute media tools without CliWrap abstractions
- Add synchronous operations in parallel processing paths
- Use `XmlSerializer` for AOT compilation (not compatible)
- Break language tag matching logic (IETF/ISO conversion)

### DO

- Add tests for media tool parsing changes (see `FfMpegIdetParsingTests.cs`)
- Update `HISTORY.md` for notable changes
- Use `Program.CancelToken()` for cancellation support
- Log with context: filenames, state transitions, tool versions
- Handle cross-platform paths (`Path.Combine`, forward slashes in Docker)
- Use modern C# features (collection expressions, pattern matching, extensions)
- Version schemas when making breaking changes
- Update global using aliases in `GlobalUsing.cs` when changing schema versions

### Performance Considerations

- Sidecar files enable fast re-processing (skip verified files)
- `--parallel` most effective with I/O-bound operations (re-mux)
- `--quickscan` limits scan to 3 minutes (trades accuracy for speed)
- `--testsnippets` creates 30s clips for testing
- Docker logging can grow large - configure rotation externally
- Monitor mode: Settle time prevents excessive re-processing

### Special Cases

**Closed Captions:**

- EIA-608/EIA-708 tracks handled specially in `SubtitleProps.HandleClosedCaptions()`
- Parsed as subtitle tracks but removed during processing
- Track IDs formatted as `{VideoId}-CC{Number}` (e.g., `256-CC1`)

**VOBSUB Subtitles:**

- Require `MuxingMode` to be set for Plex compatibility
- Missing `MuxingMode` triggers error and removal recommendation

**Duplicate Tracks:**

- Language-based grouping with flag preservation
- Preferred audio codec selection via `FindPreferredAudio()`
- Keeps one flagged track per flag type, one non-flagged track

**Language Mismatches:**

- ISO 639-2B vs IETF tag validation in `TrackProps.SetLanguage()`
- Tag metadata vs track metadata differences (FFprobe specific)
- Automatic fallback: At least one track kept even if language doesn't match

## Key Files Reference

- **Program.cs**: Entry point, command routing, global state, cancellation handling
- **ProcessDriver.cs**: File enumeration, parallel processing orchestration
- **ProcessFile.cs**: Single-file processing logic, track selection algorithms
- **Process.cs**: High-level processing workflow, empty folder deletion
- **SidecarFile.cs**: Sidecar creation, validation, state management, hashing
- **MediaTool.cs**: Base class for tool abstractions, execution patterns
- **MediaProps.cs**: Media container, track aggregation
- **TrackProps.cs**: Base track properties, language handling, flag management
- **VideoProps.cs / AudioProps.cs / SubtitleProps.cs**: Track-specific properties
- **MediaInfoXmlParser.cs**: AOT-safe XML/JSON parsing (MediaInfo output)
- **Monitor.cs**: File system watching, change queue management
- **Convert.cs**: Re-encoding and re-muxing orchestration
- **MkvProcess.cs**: MKV-specific operations (attachment removal, flag setting)
- **Tools.cs**: Tool instances, version verification, update checking
- **Language.cs**: IETF tag matching, language list extraction
- **SelectMediaProps.cs**: Track filtering and selection logic
- **CommandLineOptions.cs**: CLI parsing, option definitions
- **Extensions.cs**: Logger extensions, implicit class extensions
- **GlobalUsing.cs**: Global type aliases for schema versions
- **KeepAwake.cs**: System sleep prevention
- **PlexCleaner.defaults.json**: Canonical configuration reference
- **.editorconfig** / **.csharpier.json**: Code style definitions

## Git and Commit Rules

- **Default to staging, not committing.** Stage changes with `git add` and leave `git commit` to the developer unless explicitly authorized to commit for the current ask ("commit this", "open a PR"). Authorization is scope-bound to that task.
- **All commits must be cryptographically signed (SSH/GPG)** — branch protection rejects unsigned commits. Signing depends on environment config (`commit.gpgsign`, a `user.signingkey`, a loaded agent). If signing isn't configured, **do not commit** — stop at `git add` and surface it. Verify first: `git config --get commit.gpgsign && ssh-add -L`.
- **Never force push.** Do not run `git push --force` or `git push --force-with-lease`. Force pushing rewrites shared branch history and is blocked by branch protection rules.
- **Never run destructive git commands** (`git reset --hard`, `git checkout .`, `git restore .`, `git clean -f`) without explicit developer instruction.
- **The `develop → main` release merge is maintainer-only.** Drive `feature → develop` PRs end-to-end when authorized (commit, push, Copilot review loop, squash-merge), but never self-merge a release to `main`.

## GitHub Copilot Review Runbook

Provider-specific mechanics for driving GitHub Copilot reviews entirely via `gh`/GraphQL — no manual UI clicks. The review-loop *contract* (re-request on every push, verify head-SHA coverage, triage, reply + resolve, escalate when stuck) is in [AGENTS.md → Merging a PR](../AGENTS.md#merging-a-pr); this section is how to make Copilot reliably execute it.

### Triggering and Polling

Auto-review on push is configured (the branch ruleset's `copilot_code_review` rule with `review_on_push: true`) but fires inconsistently — treat it as best-effort. After every push, **re-request a review programmatically** via the GraphQL `requestReviews` mutation, passing the Copilot reviewer's bot node id in `botIds`. This drives the loop end-to-end without a maintainer clicking "re-request review" in the UI.

```sh
# 1. PR node id + the Copilot reviewer's bot node id (read from any existing
#    Copilot review; the reviewer login is `copilot-pull-request-reviewer`).
PR_NODE=$(gh pr view <N> --json id --jq '.id')
BOT_ID=$(gh api graphql -f query='
{
  repository(owner: "ptr727", name: "PlexCleaner") {
    pullRequest(number: <N>) {
      reviews(first: 50) { nodes { author { __typename login ... on Bot { id } } } }
    }
  }
}' --jq '[.data.repository.pullRequest.reviews.nodes[]
          | select(.author.login == "copilot-pull-request-reviewer")
          | .author.id] | first')

# 2. Re-request a Copilot review on the current head.
gh api graphql -f query='
mutation($pr: ID!, $bot: ID!) {
  requestReviews(input: { pullRequestId: $pr, botIds: [$bot], union: true }) {
    pullRequest { id }
  }
}' -F pr="$PR_NODE" -F bot="$BOT_ID"
```

The bot node id is read from an existing Copilot review, so step 1 needs at least one prior review on the PR — auto-review-on-open normally supplies the first. If none exists yet and auto-review didn't fire, request `Copilot` once through the GitHub PR UI to seed it, then use the mutation for every subsequent re-request. The Copilot reviewer bot's global node id is `BOT_kgDOCnlnWA` (login `copilot-pull-request-reviewer`) if you need to skip discovery.

**Do NOT post `@Copilot review` as a PR comment.** That triggers the Copilot *coding agent* (`copilot-swe-agent[bot]`), which makes code changes rather than posting a review.

Known non-working request paths (use the `requestReviews` mutation instead):

- `POST /requested_reviewers` with `reviewers=[Copilot]` can return 200 but no-op.
- `copilot-pull-request-reviewer` as a requested reviewer slug returns 422.

### Verify Review Covered Current Head

Before merging, confirm Copilot reviewed the current PR head SHA. Copilot may respond as a formal review (carries an exact commit SHA) or an issue comment (no SHA). Check both.

```sh
PR_HEAD=$(gh pr view <N> --json headRefOid --jq '.headRefOid')

# 1. Formal review — exact SHA match.
gh pr view <N> --json reviews --jq \
  '.reviews[] | select(.author.login=="copilot-pull-request-reviewer") | .commit.oid' \
  | grep -q "$PR_HEAD" && echo "covered via formal review"

# 2. Issue comment — show the most recent Copilot comment for manual confirmation.
gh api repos/ptr727/PlexCleaner/issues/<N>/comments --jq \
  '[.[] | select(.user.login=="copilot-pull-request-reviewer")] | last | {created_at, body: .body[:200]}'
```

Coverage is confirmed when (1) exits 0. For issue comments (path 2), body content is the only reliable signal — `created_at` is not (commit timestamps can predate the push). Treat path (2) as confirmed only when the comment body explicitly refers to the current changes.

### Bounded Retry Workflow

If a review did not run on the current head:

1. Wait briefly and check head-SHA coverage (above).
1. Re-request via the `requestReviews` mutation; fall back to the GitHub PR UI only if the mutation no-ops.
1. Retry up to two more times (three total).
1. If still missing, mark the review blocked and escalate to the maintainer with what was attempted.

### Reply and Thread Resolution Workflow

List unresolved threads (`first: 100` + cursor pagination; if `hasNextPage`, re-run with `after: "<endCursor>"`):

```sh
gh api graphql -f query='
{
  repository(owner: "ptr727", name: "PlexCleaner") {
    pullRequest(number: <N>) {
      reviewThreads(first: 100) {
        nodes {
          id isResolved path
          comments(first: 1) { nodes { author { login } body } }
        }
        pageInfo { hasNextPage endCursor }
      }
    }
  }
}' | jq '
  .data.repository.pullRequest.reviewThreads |
  (.pageInfo | "hasNextPage=\(.hasNextPage) endCursor=\(.endCursor)"),
  (.nodes[] | select(.isResolved == false))
'
```

Reply on a thread, then resolve it:

```sh
gh api graphql -f query='
mutation($threadId: ID!, $body: String!) {
  addPullRequestReviewThreadReply(input: { pullRequestReviewThreadId: $threadId, body: $body }) {
    comment { id }
  }
}' -F threadId="PRRT_..." -F body="Fixed in <SHA>: <one-line summary>."

gh api graphql -f query='
mutation($threadId: ID!) {
  resolveReviewThread(input: { threadId: $threadId }) { thread { id isResolved } }
}' -F threadId="PRRT_..."
```

Issue-level Copilot comments (those in `issues/<N>/comments`) have no resolution action — reply if the finding warrants it; no resolution step is possible.

Reply-body conventions:

- Accepted bug/style fix: include fixing commit SHA and a one-line summary.
- Declined style comment: cite the rule (AGENTS.md or CODESTYLE) and the existing-tree precedent.
- Declined architecture proposal: one-sentence rationale.

A PR is mergeable when `mergeStateStatus == CLEAN` and there are 0 unresolved threads on the current head. After the final push, sweep-resolve stale older threads for removed code paths.
