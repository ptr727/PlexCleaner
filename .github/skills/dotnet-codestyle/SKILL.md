---
name: dotnet-codestyle
description: >-
  Governs C#/.NET code style for ptr727/ProjectTemplate fleet repos: the zero-warnings build policy
  and its clean-compile task chain, central Directory.Build.props and Directory.Packages.props, C#
  language and naming conventions, XML documentation, analyzer suppression scope, the
  library-versus-application logging split, async and error-handling patterns, xUnit v3 and
  AwesomeAssertions testing, and AOT-compatible project configuration. Use this whenever writing,
  reviewing, or editing a .cs file, a .csproj, Directory.Build.props, Directory.Packages.props, or global.json,
  whenever choosing where to suppress an analyzer diagnostic, whenever a NuGet library needs to log
  without depending on Serilog directly, or whenever writing or reviewing an xUnit test. Triggers
  even when the task looks like a small local fix ("just silence this warning", "add a quick log
  line"), because each of these rules erodes one file at a time and never as one obvious diff.
  Applies only to a repo's .NET side.
---

# .NET Codestyle

## Why this exists

This is the .NET-specific half of the fleet's code style guide, kept in one place instead of
re-derived per repo or per session. CODESTYLE.md's General section still owns the rules every
language shares (clean-compile verification as a concept, the suppression-scope order, tooling
casing in prose). On top of that, this Skill is everything specific to a C#/.NET project. That is
the concrete `.NET Format` task chain, the analyzer configuration that makes the zero-warnings
policy real, and the language, naming, logging, and testing conventions.

## Build requirements

### Zero warnings policy

All builds must complete without warnings, enforced as follows:

- **The `.NET Format` clean-compile task.** It chains `CSharpier Format` -> `.NET Build` ->
  `dotnet format style --verify-no-changes`. A repo carries those task definitions in its
  own `.vscode/tasks.json`, matching the canonical `vscode-tasks.json` snippet at
  `github.com/ptr727/ProjectTemplate/blob/main/catalog/snippets/configs/vscode-tasks.json`. Run
  the `.NET Format` task after any code change, before commit. To run it natively instead,
  reproduce that exact task chain (`CSharpier Format`, then `.NET Build`, then
  `dotnet format style --verify-no-changes --severity=info --verbosity=detailed`), reading it from
  that same canonical snippet. Reproduce it without dropping or loosening any argument. Bare `dotnet format`
  alone, skipping CSharpier or the build, is not sufficient.
- **Analyzer configuration.** `<EnableNETAnalyzers>true</EnableNETAnalyzers>` with
  `<AnalysisLevel>latest-all</AnalysisLevel>` and `<AnalysisMode>All</AnalysisMode>` (the full
  analyzer set), plus `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`. With that, any diagnostic
  surfaced as a warning fails the build. Such a diagnostic must be fixed or deliberately suppressed at the
  narrowest scope that fits (see Analyzer suppressions below), never left to accumulate.
- **CI lint backstop.** CI runs the clean-compile checks on every PR as the authoritative gate.
  Husky.Net is wired from the canonical `catalog/snippets/husky/` config in the hub, hub-local
  and not carried into every fleet repo. Its hook needs a .NET tool manifest declaring
  Husky.Net, which the snippet does not ship. A repo keeping no such manifest takes the other
  canonical shape, `catalog/snippets/pre-commit/`, and so does a repo that simply prefers the
  `pre-commit` framework. Each shape carries whichever language checks its own repo keeps.
  A repo may also wire an equivalent hook of its own at `.husky/pre-commit`, enabled with
  `core.hooksPath`. That hook sources nothing, since the husky snippet's own hook sources
  a file only `dotnet husky install` generates. That path and `.pre-commit-config.yaml` are the
  ones the audit reads.
  GOVERNANCE.md's hub-only "Running the Linters Locally (Known-Working Invocations)" section
  carries the obligation itself, what the hook must cover, its audit treatment, and the
  per-clone enablement steps.

**A new port is not a license to silence diagnostics.** Brownfield or just-ported status never
justifies relaxing analyzer severities or muting newly surfaced warnings. Fix them. (The only
brownfield allowance in the fleet is the one-time git-signing / line-ending migration described in
GOVERNANCE.md and README.md. That migration has nothing to do with code analysis.)

### Central build and package configuration

Shared MSBuild configuration is centralized at the repository root, never duplicated per project:

- **`Directory.Build.props`** carries the properties every project shares. Those are the analyzer set and
  `TreatWarningsAsErrors` from the zero-warnings policy above, plus `TargetFramework` where
  uniform, and any repo-wide build metadata. A `.csproj` carries only what
  is genuinely project-specific (`OutputType`, `IsPackable`, project references).
- **`Directory.Packages.props`** owns central package management. It sets
  `ManagePackageVersionsCentrally` to `true` (in this file, not `Directory.Build.props`) and
  declares every dependency version once as a `PackageVersion` item. As a result, a `.csproj`'s
  `PackageReference` items are versionless. One file to review on a bump, one Dependabot surface,
  no version skew between projects.
- **`global.json`** is required in every .NET repo, carried from the hub's
  `catalog/snippets/configs/global.json`. It selects the SDK line with an `sdk` block.
  The example also carries the `test` runner key, which only a repo with an MTP-based test project
  adds (see Testing conventions below). A repo still on VSTest leaves the `test` key out, since it
  switches `dotnet test` to the MTP driver:

  ```json
  {
      "sdk": {
          "version": "10.0.100",
          "rollForward": "latestFeature",
          "allowPrerelease": false
      },
      "test": {
          "runner": "Microsoft.Testing.Platform"
      }
  }
  ```

  With `latestFeature`, `10.0.100` means the newest installed 10.0.x SDK. It is a floor on the
  line rather than a pin to one release, so it never goes stale and is not Dependabot-managed.
  `allowPrerelease` is `false` because it defaults to `true` outside Visual Studio, which would let
  a 10.0 preview band win. Without the block, the `dotnet` host picks the newest installed SDK. A
  host carrying a newer major than CI then runs `dotnet format`, `dotnet build`, and `dotnet test`
  on a different SDK than CI does.

  The major tracks the repo's .NET TFM (`net10.0`), never a `netstandard` target an analyzer or
  source-generator project carries beside it. It moves only with that TFM (`11.0.100` alongside
  `net11.0`), and such a project's fixed `LangVersion` moves with them. The hub's workflows and composite actions install the SDK line through
  `setup-dotnet`'s `dotnet-version`, and the hub's devcontainer snippet through its image tag. A
  repo calling them therefore cannot move to a new major alone, since CI would then find no
  compatible SDK. Such a move starts in the hub, as a fleet-wide change. The hub's steps and image
  provide both lines until every repo has moved. A repo still on
  `10.0.100` would otherwise find no compatible SDK once the hub installs only the new line.

  A developer host now needs a release SDK on the line, 10.0.x today. A host holding only a newer
  major, or only a 10.0 preview, fails with a clear "A compatible .NET SDK was not found" error
  rather than silently drifting. Install the line's SDK beside the newer one.

**`LangVersion` is left unset** in every `.props` or `.targets` file the repo holds and every
project targeting a .NET TFM. The SDK's own targets derive it, which is the point. The C# version then follows the TFM (`net10.0` gives C# 14 under any SDK). Never set it
anywhere to `latest`, `latestMajor`, or `preview`, and never set `EnablePreviewFeatures`, which
reaches `preview` through the SDK. Those float with the installed SDK's compiler. A newer
SDK then has IDE0028 suggest a syntax the older SDK cannot compile (CS8652), so no single source
text passes `dotnet format style --verify-no-changes --severity=info` on both. A `netstandard`
analyzer or source-generator project is the one exception to leaving it unset. Unset, it compiles
at C# 7.3, which `Nullable` `enable` refuses (CS8630). So it sets a fixed number in its own
`.csproj`, the TFM's C# version (`14` beside `net10.0`), which does not float. `AnalysisLevel` stays
`latest-all` with `AnalysisMode` `All`, since it is not what floats the syntax. The `sdk` block
and an unset `LangVersion` together guarantee the same SDK major and C# version locally and in CI.
Analyzer output can still differ between feature bands on one line, such as a distro-packaged
1xx SDK against CI's latest band.

A repo whose projects still carry per-project analyzer settings or versioned `PackageReference`
items is drifted. Move the shared property or version up to the root file rather than editing it
in place. A repo with no `global.json`, a `global.json` with no `sdk` block, or a `LangVersion`
the rule above does not allow is drifted too. So is an `sdk` block whose `version` is not exactly
the TFM major's `.0.100` (`10.0.100`). So is one whose `rollForward` is not `latestFeature` or whose
`allowPrerelease` is not `false`. Any later version is a higher floor, such as the `10.0.404` that
`dotnet new globaljson` writes on a newer host. It refuses a host whose newest SDK on the line is
older, such as a lagging distro-packaged one.

### Build tasks

Run these from VS Code's task runner (Terminal -> Run Task) or an agent's task-running tool. The
clean-compile tasks are carried unchanged, and a repo adds its own convenience tasks (tool
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
- CI is the authoritative lint backstop. A repo already keeping a .NET tool manifest declaring
  Husky.Net wires its local pre-commit hook from `catalog/snippets/husky/` in the hub. That snippet is hub-local
  and not carried into every fleet repo. A repo keeping none, or one preferring the `pre-commit`
  framework, takes `catalog/snippets/pre-commit/` instead. Either shape covers the shared doc
  gates alongside the language checks. Each snippet's own README names the per-clone steps and
  the second file to copy alongside it.
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
symbol-scoped before project-scoped before repo-wide. That section allows suppression only for a genuine false-positive or a
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
   on `Microsoft.Extensions.Logging.Abstractions` and exposes an `ILoggerFactory` seam. That seam is a settable
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
parameterized tests. A test project on `xunit.v3` 4.0.0 or later is MTP-based, and also carries a `global.json` runner declaration, a `Microsoft.Testing.Extensions.CodeCoverage` floor, and no `xunit.runner.visualstudio`. See `references/testing.md` for the framework setup template and that configuration.

## Project configuration

.NET 10.0 target, AOT-compatible (`IsAotCompatible=true`, with `VerifyReferenceAotCompatibility=true` only under the `PublishAot` condition),
SourceLink, embedded untracked sources, `InternalsVisibleTo` for test/benchmark access. See
`references/project-config.md` for the full property list.

## Best practices

All changes go through pull requests.
