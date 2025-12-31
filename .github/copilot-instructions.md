# PlexCleaner AI Coding Instructions

## Project Overview

PlexCleaner is a .NET 10.0 CLI utility that optimizes media files for Direct Play in Plex/Emby/Jellyfin by:
- Converting containers to MKV format
- Re-encoding incompatible video/audio codecs
- Managing tracks (language tags, duplicates, subtitles)
- Verifying and repairing media integrity
- Removing closed captions and unwanted content

The tool orchestrates external media processing tools (FFmpeg, HandBrake, MkvToolNix, MediaInfo, 7-Zip) via CLI wrappers.

## Architecture

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

### Sidecar File System
Critical performance feature - DO NOT break compatibility:
- Each `.mkv` gets a `.PlexCleaner` sidecar JSON file
- Contains: processing state, tool versions, media properties, file hash
- Hash: First 64KB + last 64KB of file (not timestamp-based)
- Schema versioned (`SchemaVersion: 4` in `SidecarFileJsonSchema4`)
- Processing skips verified files unless sidecar invalidated
- State flags are bitwise: `StatesType` enum with `[Flags]` attribute

### Media Tool Abstraction
- `MediaTool` base class defines tool lifecycle
- Each tool family has: Tool class, Builder class, Info schema
- Tool version info retrieved from CLI output, cached in `Tools.json`
- Windows supports auto-download; Linux uses system tools
- Tool paths: `ToolsOptions.UseSystem` or `RootPath + ToolFamily/SubFolder/ToolName`

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
- Use C# modern features (records, pattern matching, collection expressions)
- Prefer `Debug.Assert()` for internal invariants
- Logging: Serilog with thread IDs (`Log.Information/Warning/Error`)
- Exception handling: Currently uses broad `catch(Exception)` - TODO to specialize
- Global usings: `GlobalUsing.cs` defines project-wide imports

### Naming and Structure
- JSON schemas: Generated via `JsonSchema.Net`, suffixed with version (e.g., `SidecarFileJsonSchema4`)
- Builder methods: Return `this` for chaining
- Media props: `*Props.cs` classes (VideoProps, AudioProps, SubtitleProps, TrackProps)
- Options classes: `*Options.cs` for command categories

## Testing

### Test Framework
- xUnit v3.x with `AwesomeAssertions`
- Test project: `PlexCleanerTests/`
- Fixture: `PlexCleanerFixture` (assembly-level, sets up defaults and logging)
- Sample media: `Samples/PlexCleaner/` (relative path `../../../../Samples/PlexCleaner`)

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
- **BuildDockerPush.yml**: Multi-arch Docker builds (ubuntu, alpine, debian)
- **TestBuildPr.yml** / **TestDockerPr.yml**: PR validation
- Version info: `version.json` with Nerdbank.GitVersioning format
- Branches: `main` (stable releases), `develop` (pre-releases)

### Docker
- Multi-stage builds in `Docker/*Dockerfile`
- Base images: `ubuntu:rolling`, `alpine:latest`, `debian:stable-slim`
- Tool installation: Platform-specific package managers
- Test script: `Docker/Test.sh` validates all commands
- Version extraction: `Docker/Version.sh` captures tool versions for README

## Common Patterns

### Command-Line Parsing
Uses `System.CommandLine` (v2.x):
- Options defined in `CommandLineOptions.cs`
- Binding via `CommandLineParser.Bind()`
- No `System.CommandLine.NamingConventionBinder` (deprecated)

### Parallel Processing
- `--parallel` flag enables concurrent file processing
- Uses `ProcessDriver.cs` with `AsParallel()` and `WithDegreeOfParallelism()`
- Default thread count: min(CPU/2, 4)
- Lock-based collection updates in parallel contexts

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
- Schema: `PlexCleaner.schema.json` (auto-generated)
- Validation: JSON Schema.Net
- URL schema reference: `https://raw.githubusercontent.com/ptr727/PlexCleaner/main/PlexCleaner.schema.json`

## Critical Details

### DO NOT
- Break sidecar file compatibility (versioned schema migrations only)
- Use string concatenation for command-line arguments (use builders)
- Modify file timestamps unless `RestoreFileTimestamp` enabled
- Execute media tools without CliWrap abstractions
- Add synchronous operations in parallel processing paths

### DO
- Add tests for media tool parsing changes (see `FfMpegIdetParsingTests.cs`)
- Update `HISTORY.md` for notable changes
- Use `Program.CancelToken()` for cancellation support
- Log with context: filenames, state transitions, tool versions
- Handle cross-platform paths (`Path.Combine`, forward slashes in Docker)

### Performance Considerations
- Sidecar files enable fast re-processing (skip verified files)
- `--parallel` most effective with I/O-bound operations (re-mux)
- `--quickscan` limits scan to 3 minutes (trades accuracy for speed)
- `--testsnippets` creates 30s clips for testing
- Docker logging can grow large - configure rotation externally

## Key Files Reference
- **Program.cs**: Entry point, command routing, global state
- **ProcessDriver.cs**: File enumeration, parallel processing orchestration
- **ProcessFile.cs**: Single-file processing logic
- **SidecarFile.cs**: Sidecar creation, validation, state management
- **MediaTool.cs**: Base class for tool abstractions
- **FfMpegBuilder.cs**: Example fluent builder implementation
- **PlexCleaner.defaults.json**: Canonical configuration reference
- **.editorconfig** / **.csharpier.json**: Code style definitions
