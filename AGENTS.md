# Instructions for AI Coding Agents

**PlexCleaner** is a C# .NET utility that optimizes media files for Direct Play in Plex, Emby, Jellyfin, etc.

For comprehensive coding standards and detailed conventions, refer to [`.github/copilot-instructions.md`](./.github/copilot-instructions.md) and [`CODESTYLE.md`](./CODESTYLE.md).

## Git and Commit Rules

**These rules are absolute — no exceptions:**

- **Never make git commits.** AI coding agents cannot produce cryptographically signed commits. All commits must be signed (SSH/GPG) and must be made by the developer. Stage changes with `git add` and leave the commit to the developer.
- **Never force push.** Do not run `git push --force` or `git push --force-with-lease` under any circumstances. Force pushing rewrites shared history and can cause data loss.
- **Never run destructive git commands** (`git reset --hard`, `git checkout .`, `git restore .`, `git clean -f`) without explicit developer instruction.
- **Staging is the limit.** Prepare and stage file changes; the developer runs `git commit` in their own environment where signing keys are available.

## Key Requirements for All Projects Derived from This Template

### Build & Quality Standards

- **Zero Warnings Policy**: All builds must complete without errors or warnings
  - Use `CSharpier Format`, `.Net Format`, and `Husky.Net Run` tasks

- **Code Analysis**: Enable all .NET analyzers
  - `<EnableNETAnalyzers>true</EnableNETAnalyzers>`
  - `<AnalysisLevel>latest-all</AnalysisLevel>`

### Project Configuration

- Common MSBuild properties (`TargetFramework`, `Nullable`, `ImplicitUsings`, `AnalysisLevel`, etc.)
  live in `Directory.Build.props` at the solution root. Do not duplicate these in individual `.csproj`
  files — only add a property to a `.csproj` when it is project-specific or overrides the shared default.
- All NuGet package versions are centralised in `Directory.Packages.props`. `PackageReference` elements
  in `.csproj` files must not include a `Version` attribute. Asset metadata (`PrivateAssets`,
  `IncludeAssets`) stays in the `.csproj` `PackageReference` element.

### Development Environment

- Target latest .NET SDK (currently .NET 10 with C# 14)
- Support Visual Studio Code (`.code-workspace`) and Visual Studio Community (`.slnx`)
- Support Linux, Windows, and macOS with correct line endings and permissions
- Use `.editorconfig` for style enforcement

### Project Structure

- **PlexCleaner**: CLI application
- **PlexCleanerTests**: Unit tests using xUnit and AwesomeAssertions
- **Sandbox**: Sandbox/testing utility project
- **Docker**: Multi-platform Linux containers

### Testing

- Use xUnit v3 and AwesomeAssertions
- Organize tests logically in separate files
- Follow Arrange-Act-Assert pattern
- Test naming: `MethodName_Scenario_ExpectedBehavior()`

## Authoritative References

For detailed specifications, see:

- [`.github/copilot-instructions.md`](./.github/copilot-instructions.md) - Complete coding conventions and style guide
- [`CODESTYLE.md`](./CODESTYLE.md) - Code style and formatting rules
- [`.editorconfig`](./.editorconfig) - Automated style enforcement
- Project task definitions - `CSharpier Format`, `.Net Build`, `.Net Format`, `.Net Outdated Upgrade`, `Husky.Net Run`
