---
name: dotnet-codestyle
description: >-
  Governs C#/.NET code style for ptr727/ProjectTemplate fleet repos: the zero-warnings build
  policy and its three-task clean-compile chain, central Directory.Build.props/
  Directory.Packages.props configuration, C# language and naming conventions, XML documentation,
  analyzer suppression scope, the library-versus-application logging split, async and
  error-handling patterns, xUnit v3 + AwesomeAssertions testing conventions, and AOT-compatible
  project configuration. Use this whenever writing, reviewing, or editing a .cs file, a .csproj,
  Directory.Build.props, or Directory.Packages.props, whenever choosing where to suppress an
  analyzer diagnostic, whenever a NuGet library needs to log without depending on Serilog
  directly, or whenever writing or reviewing an xUnit test. Triggers even when the task looks
  like a small local fix ("just silence this warning", "add a quick log line", "bump a package
  version"), because the zero-warnings policy, the suppression-scope order, the central-package-
  management rule, and the library/application logging split are each easy to violate one file at
  a time without the pattern ever showing up as a single obvious diff. Applies only to a repo's
  .NET side, a repo with no .NET projects has no use for this Skill.
---

# .NET Codestyle

## Why this exists

This is the .NET-specific half of the fleet's code style guide, kept in one place instead of
re-derived per repo or per session. CODESTYLE.md's General section still owns the rules every
language shares (clean-compile verification as a concept, the suppression-scope order, tooling
casing in prose), this Skill is everything specific to a C#/.NET project on top of that: the
concrete `.NET Format` task chain, the analyzer configuration that makes the zero-warnings policy
real, and the language, naming, logging, and testing conventions.

## Build requirements

### Zero warnings policy

All builds must complete without warnings, enforced three ways:

- **The `.NET Format` clean-compile task.** It chains `CSharpier Format` -> `.NET Build` ->
  `dotnet format style --verify-no-changes`. A repo carries those three task definitions in its
  own `.vscode/tasks.json`, matching the canonical `vscode-tasks.json` snippet at
  `github.com/ptr727/ProjectTemplate/blob/main/catalog/snippets/configs/vscode-tasks.json`. Run
  the `.NET Format` task after any code change, before commit. To run it natively instead,
  reproduce that exact task chain (`CSharpier Format`, then `.NET Build`, then
  `dotnet format style --verify-no-changes --severity=info --verbosity=detailed`) without dropping
  or loosening any argument, reading it from that same canonical snippet. Bare `dotnet format`
  alone, skipping CSharpier or the build, is not sufficient.
- **Analyzer configuration.** `<EnableNETAnalyzers>true</EnableNETAnalyzers>` with
  `<AnalysisLevel>latest-all</AnalysisLevel>` and `<AnalysisMode>All</AnalysisMode>` (the full
  analyzer set), plus `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, so any diagnostic
  surfaced as a warning fails the build and must be fixed or deliberately suppressed at the
  narrowest scope that fits (see Analyzer suppressions below), never left to accumulate.
- **CI lint backstop.** CI runs the clean-compile checks on every PR as the authoritative gate.
  A working local hook is strongly suggested, not optional: wire Husky.Net from the canonical
  `catalog/snippets/husky/` config. See GOVERNANCE.md "Running the Linters Locally" for what the
  hook must cover and what its absence means.

**A new port is not a license to silence diagnostics.** Brownfield or just-ported status never
justifies relaxing analyzer severities or muting newly surfaced warnings. Fix them. (The only
brownfield allowance in the fleet is the one-time git-signing / line-ending migration described in
GOVERNANCE.md and README.md, which has nothing to do with code analysis.)

### Central build and package configuration

Shared MSBuild configuration is centralized at the repository root, never duplicated per project:

- **`Directory.Build.props`** carries the properties every project shares: the analyzer set and
  `TreatWarningsAsErrors` from the zero-warnings policy above, plus `LangVersion`,
  `TargetFramework` where uniform, and any repo-wide build metadata. A `.csproj` carries only what
  is genuinely project-specific (`OutputType`, `IsPackable`, project references).
- **`Directory.Packages.props`** owns central package management: it sets
  `ManagePackageVersionsCentrally` to `true` (in this file, not `Directory.Build.props`) and
  declares every dependency version once as a `PackageVersion` item, so a `.csproj`'s
  `PackageReference` items are versionless. One file to review on a bump, one Dependabot surface,
  no version skew between projects.

A repo whose projects still carry per-project analyzer settings or versioned `PackageReference`
items is drifted, move the shared property or version up to the root file rather than editing it
in place.

### Build tasks

Run these from VS Code's task runner (Terminal -> Run Task) or an agent's task-running tool. The
three clean-compile tasks are carried verbatim, and a repo adds its own convenience tasks (tool
updates, dependency upgrades, benchmarks) on top:

- `.NET Build`: build with diagnostic verbosity *(clean-compile)*
- `CSharpier Format`: auto-format code with CSharpier *(clean-compile)*
- `.NET Format`: run CSharpier and build, then verify formatting and style with
  `--verify-no-changes` *(clean-compile, the task to run after edits)*

## Tooling and editor

- **CSharpier** is the primary code formatter, invoked by the `CSharpier Format` task or
  `dotnet csharpier format --log-level=debug .`.
- **`dotnet format`** verifies style:
  `dotnet format style --verify-no-changes --severity=info --verbosity=detailed`.
- **`dotnet-outdated-tool`** checks for dependency updates, and Nerdbank.GitVersioning owns
  version management.
- CI is the authoritative lint backstop. A local pre-commit hook is strongly suggested: wire
  Husky.Net from `catalog/snippets/husky/` for local enforcement, including the shared doc gates.
- **Required VS Code extensions**: CSharpier, markdownlint, CSpell. Use the workspace settings
  without overrides.

## Coding standards and conventions

Key rules: no `var` (always explicit types), file-scoped namespaces, Allman braces, Nullable
enabled, modern C# features (primary constructors, pattern matching, collection expressions). Every
public surface has XML documentation. Private fields use `_camelCase`, static fields `s_camelCase`,
constants PascalCase. Member ordering follows StyleCop SA1201.

For language features, naming, code structure, and XML documentation examples, see
`references/conventions.md`.

## Analyzer suppressions (.NET)

CODESTYLE.md's General section sets the suppression-scope order fleet-wide: narrowest scope first,
symbol-scoped before project-scoped before repo-wide, and only for a genuine false-positive or a
deliberate, documented exception, never a blanket relaxation to get a brownfield port to build.
The .NET mechanics, narrowest first:

- **Never use `#pragma warning disable`** to silence an analyzer.
- **Symbol-scoped**: a `[System.Diagnostics.CodeAnalysis.SuppressMessage(...)]` attribute with a
  `Justification`, on the specific member or type:

  ```csharp
  [System.Diagnostics.CodeAnalysis.SuppressMessage(
      "Design",
      "CA1034:Nested types should not be visible",
      Justification = "https://github.com/dotnet/sdk/issues/51681"
  )]
  ```

- **Project-scoped** (e.g. a test project): a `dotnet_diagnostic.<RULE>.severity` entry in that
  project's own `.editorconfig`, with a comment explaining why.
- **Repo-wide**: a `dotnet_diagnostic.<RULE>.severity` entry in the root `.editorconfig`, only
  when the rule is genuinely not applicable to any project. Relaxing a batch of `CA*` rules (or
  `dotnet_analyzer_diagnostic.severity`) to push a brownfield port through the build is exactly
  what this forbids.

## Error handling and logging

1. **Structured logging**: use structured message templates. Serilog is the application's concrete
   backend, and a library never references it directly (see item 2):

   ```csharp
   logger.LogError(exception, "{Function}", function);
   ```

2. **Libraries log through abstractions, never a concrete backend.** A NuGet library depends only
   on `Microsoft.Extensions.Logging.Abstractions` and exposes an `ILoggerFactory` seam: a settable
   global factory defaulting to `NullLoggerFactory.Instance` (fallback `NullLogger.Instance`) with
   `SetFactory`/`TrySetFactory`, and/or an `ILoggerFactory`/`ILogger` parameter in its API. It
   must not reference Serilog or any sink, which would force a logging framework on every consumer
   and drag in AOT-incompatible dependencies. The consuming application owns the concrete logger
   (Serilog is fine there), bridges it to `ILoggerFactory` (e.g. `SerilogLoggerFactory` from
   `Serilog.Extensions.Logging`), and injects it. Reference pattern: a `LogOptions` seam in the
   library, against which the consuming CLI builds the Serilog-backed factory and injects it via
   `LogOptions.SetFactory`.
3. **CallerMemberName**: use for automatic function name tracking:

   ```csharp
   public bool LogAndPropagate(
       Exception exception,
       [CallerMemberName] string function = "unknown"
   )
   ```

4. **Logger extensions**: use `Extensions.cs` for logger and other extension methods:

   ```csharp
   extension(ILogger logger)
   {
       public bool LogAndPropagate(Exception exception, ...) { }
   }
   ```

5. **Exceptions**: do not swallow exceptions, either log and rethrow or translate to a
   domain-specific exception.

## Code patterns

1. **Guard clauses**: prefer early returns for validation and error handling.
2. **Async all the way**: avoid blocking calls (`.Result`, `.Wait()`), use `async`/`await`.
3. **Cancellation tokens**: accept `CancellationToken` as the last parameter and pass it through.
4. **ConfigureAwait**: in library code, use `ConfigureAwait(false)` unless context is required. Do
   not call `ConfigureAwait(false)` in xUnit tests (see xUnit1030).
5. **Disposables**: use `await using` for async disposables, prefer `using` declarations.
6. **LINQ vs loops**: use LINQ for clarity, loops for hot paths or allocations.
7. **HTTP**: reuse `HttpClient` via factory, never per-request instantiation.
8. **Collections**: prefer `IReadOnlyList<T>`/`IReadOnlyCollection<T>` for public APIs.
9. **Immutability**: prefer immutable records, use init-only setters when records are not
   suitable, and prefer immutable or frozen collections for read-only data.
10. **Exceptions as control flow**: avoid using exceptions for expected flow.
11. **Sealing classes**: seal classes that are not designed for inheritance.
12. **Lazy initialization**: use `Lazy<T>` for static, thread-safe instantiation (e.g. a logger
    factory, an HTTP factory).

## Testing conventions

xUnit v3 (`xunit.v3`, not the legacy `xunit`) + AwesomeAssertions (`.Should()` API, never native
asserts). Arrange-Act-Assert pattern, descriptive underscore names, `[Theory]`/`[InlineData]` for
parameterized tests. See `references/testing.md` for the framework setup template.

## Project configuration

.NET 10.0 target, AOT-compatible (`IsAotCompatible=true`, `VerifyReferenceAotCompatibility=true`),
SourceLink, embedded untracked sources, `InternalsVisibleTo` for test/benchmark access. See
`references/project-config.md` for the full property list.

## Best practices

All changes go through pull requests.
