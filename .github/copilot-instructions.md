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

### Naming and Structure
- JSON schemas: Generated via `JsonSchema.Net`, suffixed with version (e.g., `SidecarFileJsonSchema5`)
- Builder methods: Return `this` for chaining
- Media props: `*Props.cs` classes (VideoProps, AudioProps, SubtitleProps, TrackProps)
- Options classes: `*Options.cs` for command categories (ProcessOptions, VerifyOptions, ConvertOptions, MonitorOptions, ToolsOptions)
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
- **BuildGitHubRelease.yml**: Multi-runtime matrix build (win, linux, osx × x64/arm/arm64)
- **BuildDockerPush.yml**: Multi-arch Docker builds (linux/amd64, linux/arm64)
- **TestBuildPr.yml** / **TestDockerPr.yml**: PR validation
- Version info: `version.json` with Nerdbank.GitVersioning format
- Branches: `main` (stable releases), `develop` (pre-releases)

### Docker
- Multi-stage builds in `Docker/Ubuntu.Rolling.Dockerfile`
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
