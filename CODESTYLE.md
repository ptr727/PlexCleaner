# Code Style and Formatting Rules

This is the single code-style guide for the fleet. The **General** section applies to every language. Each **language section** (.NET, Python) is self-contained: a repo follows only the section(s) for the languages it ships and ignores the rest. A repo keeps the whole file rather than trimming it. An unused-language section costs nothing, the same whole-file model as [`.editorconfig`][root], whose inert `[*.cs]` block a non-.NET repo keeps.

Cross-cutting *process* rules (PR titles, branching, US English, Markdown style, comments philosophy, workflow YAML, PR review etiquette, and the verification discipline that defines the pre-push lint gate) live in [GOVERNANCE.md][governance] and are not repeated here.

## General

These rules apply to every language in the repo.

### Tooling Names and Casing

Use each tool's official casing in task labels, docs, and prose: `.NET` (not `.Net`), `CSharpier`, `ruff`, `pyright`, `uv`. Don't invent personal variants.

### Clean-Compile Verification

Each language defines a **clean-compile** verification: the combination of build, formatter, linter, and code-analysis tools that must report clean before a commit. It is exposed as one or more **named** VS Code tasks (or, where a language ships no tasks, documented commands), and those definitions are the same across the fleet. The concrete names live in each language section below.

- **Run it after every code change, and it is not the whole gate.** The relevant language's clean-compile must pass before you commit. CI runs those same language checks as a backstop **plus everything else its validation workflow runs**, and all of it reports into the one required status, so a green clean-compile does not predict a green CI. That remainder is at least the doc-lint set (markdownlint, cspell, actionlint, `editorconfig-checker`) and whatever spec, config, and script gates the repo carries, so read the workflow for the full list rather than assuming this sentence enumerates it. What has to pass before a push is the repo's **whole** lint gate, per [GOVERNANCE.md "Verification Discipline"][governance-verification-discipline]. Each linter's known-working invocation is in [GOVERNANCE.md "Running the Linters Locally"][governance-running-the-linters-locally].
- **The named task definition is the canonical spec** - its exact command sequence, arguments, and strictness. You may run it through the VS Code task **or** by invoking the equivalent native commands directly, and either is fine **only if the sequence, arguments, and strictness match exactly**. No shortcuts and no more-lenient options (for example, never drop `--verify-no-changes` or loosen a `--severity`).
- **A local commit/pre-commit gate is the repo's choice.** No single hook runner fits every language (a `dotnet`-tool runner like Husky.Net suits .NET but not Python), so none is mandated, but that is **not** a recommendation against commit gates. CI is the authoritative backstop regardless, and a local gate is an additive convenience a repo may wire and keep: Husky.Net (and `dotnet husky run` as a style step) for .NET, `pre-commit` for Python. Keeping a working gate is not drift.

### Analyzer Diagnostics and Suppressions

- **A new port is not a license to silence diagnostics.** Brownfield / just-ported status never justifies relaxing analyzer or linter severities or muting newly surfaced warnings. Fix them. (The only brownfield allowance is the one-time git-signing / line-ending migration described in [GOVERNANCE.md][governance] and [README.md][readme], which has nothing to do with code analysis.)
- **Suppress only genuine false-positives or deliberate, documented exceptions**, always at the **narrowest scope that fits**, in this order of preference:
  1. An **in-code annotation on the specific symbol**, with a justification, in the language's attribute/comment form, never a blanket pragma spanning a region.
  2. The **owning project's local config** when the exception is project-wide for one project (e.g. a test project's own `.editorconfig` / `pyproject.toml`).
  3. The **root / shared config** only when the suppression is genuinely applicable to **every** project in the repo.
- **Never blanket-relax a batch of rules project-wide** to get a port to build. The per-language mechanics (which attribute, which config key) are in each language section.

PlexCleaner keeps its console-app analyzer exceptions in the root `.editorconfig` because they apply to the complete shipped application surface. The exceptions cover collection-return guidance, public-API validation, localization, string-comparison suggestions, asynchronous context capture, and a small number of known false positives. They do not disable analyzer enforcement generally. `IDE0072` still requires explicit enum arms in switch expressions, and `IDE0046` still requires the combined boolean return form; fix those diagnostics rather than adding suppressions.

### Markdown and Spelling

These apply repo-wide, in every directory:

1. **Markdown linting**: All `.md` files must be lint-clean (error and warning free) via the VS Code `markdownlint` extension. [`.markdownlint-cli2.jsonc`][markdownlint-cli2] at the repo root is the single source of truth, and the davidanson `markdownlint` extension and a command-line `markdownlint-cli2` run both read it, so the IDE and CLI stay in lock-step. Rules it deliberately disables (e.g. `MD013` line-length) are **intentional**, so do not "fix" them. `MD033` inline HTML stays **enabled**: HTML comments are permitted (markdownlint does not flag them), `details` and `summary` are allowed because a GitHub collapsible has no Markdown equivalent, every other element is flagged, and anything with a native Markdown equivalent uses the Markdown. Fix violations at the source rather than disabling rules.
2. **Spelling**: All spelling must be clean via the CSpell VS Code integration, and words must be correctly spelled in **US English** (the repo-wide convention, per [GOVERNANCE.md][governance]). The shared `cspell.json` sets `"language": "en-US"` so British spellings are flagged, where a bare `"en"` accepts both US and British and silently passes the wrong spelling. Project-specific terms go in the shared `cspell.json` `words` list, the single source of truth the extension, CLI, and CI all read. The `.code-workspace` must **not** carry its own `cspell.words`/`cSpell.words` block, and when externalizing words into `cspell.json`, delete any word list left in the workspace (a leftover one duplicates the list and silently drifts).
3. **Spelling CI scope**: The enforced CI spell-check gate covers **`README.md` and `HISTORY.md` only**, because these are the files every repo visitor sees, so they must be clean. It is deliberately **not** all `**/*.md`: repos carry many Markdown files full of technical terms, and gating every one of them would mean endlessly padding `cspell.json` just to keep CI green. Broad, live spell-checking across any file (source, Markdown, text) is the **cspell editor extension's** job, so typos still surface to whoever is editing. A repo owner **may** widen their own CI file list, but README + HISTORY are the default. Keep the CI workflow, the `Lint: Spelling` VS Code task, and the GOVERNANCE.md cspell one-liner on the same file list. The list is explicit (not a glob), so a repo that ships no `HISTORY.md` (e.g. one with no changelog) must drop it from all three surfaces and gate on `README.md` alone, since cspell errors on a listed file that does not exist. Markdown *linting* (item 1) stays repo-wide `**/*.md`, which does not choke on technical terms.
4. **`HISTORY.md` mirrors the README opening**: `HISTORY.md` is the maintainer-curated changelog and opens as the README's twin, carrying the same `# <Title>` (without the README's ToC-omit comment) and the same **tagline** copied verbatim, then a `## Release History` section. The tagline is the first line after the README's H1, and it is the whole of the mirror: a README may carry further paragraphs below it, explaining the project to a reader before the fold, and the changelog does not repeat them, because it opens on the identity and then goes straight to the releases. The mirrored opening keeps the project identity consistent for a reader who lands on the changelog directly. The audit checks that the title and the tagline match the README, with HTML comments stripped.
5. **"Markdown" is the format's name**: The format is a proper noun, so prose capitalizes it, meaning a Markdown file, a Markdown link, and the Markdown a surface renders. Lowercase is for the strings a machine reads and for nothing else: a tool or package name (`markdownlint`, `markdownlint-cli2`, `yzhang.markdown-all-in-one`), a settings key (`markdown.extension.toc.levels`), a heading anchor (`#markdown-and-spelling`), an identifier in code, and a file extension. A hyphenated compound in prose is prose, so it capitalizes too (Markdown-only), which is the boundary a mechanical sweep gets wrong, since it reads the hyphen as the mark of an identifier. What this settles is the mix rather than either spelling, because a file carrying both gives the next author no default to follow and a reviewer a finding to raise on whichever one it wrote last. The rule lives here because every repo carries this file, so the convention arrives with it rather than being re-decided per repo.

## .NET

*This section applies only to the .NET side. A repo with no .NET projects still carries it (the file is carried whole) and ignores it.*

This is the style guide for any **.NET projects** in this repo.

### Build Requirements

#### Zero Warnings Policy

**CRITICAL**: All builds must complete without warnings. The project enforces this through:

1. **The `.NET Format` clean-compile task** (see [Clean-Compile Verification][clean-compile-verification])
   - The .NET clean-compile is the **`.NET Format`** VS Code task, which chains `CSharpier Format` -> `.NET Build` -> `dotnet format style --verify-no-changes`. A repo carries those three definitions in its own `.vscode/tasks.json`, matching the canonical in [`vscode-tasks.json`][vscode-tasks-link].
   - After any code change it must pass before commit. Run the `.NET Format` task. To run it natively instead, reproduce that task chain exactly (`CSharpier Format`, then `.NET Build`, then the `dotnet format style --verify-no-changes --severity=info ...` verify) without dropping or loosening any argument, reading it from [`vscode-tasks.json`][vscode-tasks-link], which is the canonical command spec a repo's own `tasks.json` is written against. Bare `dotnet format` alone, skipping CSharpier or the build, is not sufficient.

2. **Analyzer configuration**
   - `<EnableNETAnalyzers>true</EnableNETAnalyzers>` with `<AnalysisLevel>latest-all</AnalysisLevel>` and `<AnalysisMode>All</AnalysisMode>` (full analyzer set enabled)
   - `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, so any diagnostic surfaced as a warning fails the build and must be fixed or deliberately suppressed, not left to accumulate (see [Analyzer Diagnostics and Suppressions][analyzer-diagnostics-and-suppressions])

3. **CI lint backstop**
   - CI runs the clean-compile checks on every PR as the authoritative backstop
   - Git hooks are optional, and a repo may wire a local runner (Husky.Net) for pre-commit enforcement, but CI is the gate that matters

#### Central Build and Package Configuration

Shared MSBuild configuration is centralized at the repository root, never duplicated per project:

- **`Directory.Build.props`** carries the properties every project shares: the analyzer set and `TreatWarningsAsErrors` from the Zero Warnings Policy above, plus `LangVersion`, `TargetFramework` where uniform, and any repo-wide build metadata. A csproj carries only what is genuinely project-specific (`OutputType`, `IsPackable`, project references).
- **`Directory.Packages.props`** owns central package management: it sets `ManagePackageVersionsCentrally` to `true` (in this file, not `Directory.Build.props`) and declares every dependency version once as a `PackageVersion` item, so a csproj's `PackageReference` items are versionless. One file to review on a bump, one Dependabot surface, and no version skew between projects.

A repo whose projects still carry per-project analyzer settings or versioned `PackageReference` items is drifted, so move the shared property or version up to the root file rather than editing it in place.

#### Build Tasks

Available VS Code tasks (run them from VS Code's task runner, **Terminal -> Run Task**, or an agent's task-running tool). The three clean-compile tasks below are carried verbatim, and a repo adds its own convenience tasks (tool updates, dependency upgrades, benchmarks) on top:

- `.NET Build`: Build with diagnostic verbosity *(clean-compile)*
- `CSharpier Format`: Auto-format code with CSharpier *(clean-compile)*
- `.NET Format`: Run CSharpier and build, then verify formatting and style with `--verify-no-changes` *(clean-compile; the task to run after edits)*

### Tooling and Editor

#### Code Formatting and Tooling

1. **CSharpier**: Primary code formatter
   - Invoked by the `CSharpier Format` task / `dotnet csharpier format --log-level=debug .`
2. **dotnet format**: Style verification
   - Verify no changes: `dotnet format style --verify-no-changes --severity=info --verbosity=detailed`
3. **Other tools**
   - `dotnet-outdated-tool`: Dependency update checks
   - Nerdbank.GitVersioning: Version management

CI is the authoritative lint backstop. Local pre-commit hooks are optional, so wire Husky.Net (or another runner) if you want local enforcement.

#### Editor Baseline

1. **Required VS Code extensions**: CSharpier, markdownlint, CSpell
2. **VS Code settings**: Use the workspace settings without overrides

### Coding Standards and Conventions

Note: Code snippets are illustrative examples only. Replace namespaces/types to match your project.

#### C# Language Features

1. **File-scoped namespaces**

   ```csharp
   namespace Example.Project.Library;
   ```

2. **Nullable reference types**: Enabled (`<Nullable>enable</Nullable>`)
   - Use nullable annotations appropriately
   - Use `required` for mandatory properties

3. **Modern C# features**: Prefer modern language constructs
   - Primary constructors when appropriate
   - Top-level statements for console apps
   - Pattern matching over traditional checks
   - Collection expressions when types loosely match
   - Extension methods, in the classic `this`-parameter form or an `extension(<receiver>) { ... }` block on C# 14+
   - Implicit object creation when type is apparent
   - Range and index operators

4. **Expression-bodied members**: Use for applicable members
   - Methods, properties, accessors, operators, lambdas, local functions

5. **`var` keyword**: Do NOT use `var` (always use explicit types)

   ```csharp
   // Correct
   int count = 42;
   string name = "test";

   // Incorrect
   var count = 42;
   var name = "test";
   ```

#### Naming Conventions

1. **Private fields**: underscore prefix with camelCase

   ```csharp
   private readonly HttpClient _httpClient;
   private int _counter;
   ```

2. **Static fields**: `s_` prefix with camelCase

   ```csharp
   private static int s_instanceCount;
   ```

3. **Constants**: PascalCase

   ```csharp
   private const int MaxRetries = 3;
   ```

#### Code Structure

1. **Global usings**: Use `GlobalUsings.cs` for common namespaces

   ```csharp
   global using System;
   global using System.Net.Http;
   global using System.Threading.Tasks;
   global using Microsoft.Extensions.Logging;
   ```

2. **Usings placement**: Outside namespace, sorted with `System` directives first

   ```csharp
   using System.CommandLine;
   using System.Runtime.CompilerServices;
   using Example.Project.Library;

   namespace Example.Project.Console;
   ```

3. **Braces**: Allman style

   ```csharp
   public void Method()
   {
       if (condition)
       {
           // code
       }
   }
   ```

4. **Indentation**
   - C# files: 4 spaces
   - XML/csproj files: 2 spaces
   - YAML files: 2 spaces
   - JSON files: 4 spaces

5. **Line endings**: not specified here, but governed per repo by `.editorconfig` / `.gitattributes` per the [GOVERNANCE.md][governance] "Line Endings" section.

6. **`#region`**: Do not use regions. Prefer logical file/folder/namespace organization.
7. **Member ordering (StyleCop SA1201)**: const -> static readonly -> static fields -> instance readonly fields -> instance fields -> constructors -> public (events -> properties -> indexers -> methods -> operators) -> non-public in same order -> nested types

#### Comments and Documentation

1. **XML documentation**
   - `<GenerateDocumentationFile>true</GenerateDocumentationFile>`
   - Missing XML comments for public APIs are suppressed (`.editorconfig`)
   - Must document all public surfaces.
   - Single-line summaries, additional details in remarks, document input parameters, return values, exceptions, and add crefs

   ```csharp
   /// <summary>
   /// Example of a single line summary.
   /// </summary>
   /// <remarks>
   /// Additional important details about usage.
   /// Multiple lines if needed.
   /// </remarks>
   /// <param name="category">
   /// The quote category to request
   /// </param>
   /// <param name="cancellationToken">
   /// A <see cref="System.Threading.CancellationToken"/> that can be used to cancel the request.
   /// </param>
   /// <returns>
   /// A <see cref="string"/> containing the quote text.
   /// </returns>
   /// <exception cref="System.ArgumentException">
   /// Thrown when <paramref name="category"/> is not a supported value.
   /// </exception>
   public async Task<string> GetQuoteOfTheDayAsync(string category, CancellationToken cancellationToken) {}
   ```

#### Analyzer Suppressions (.NET)

Follow the scope hierarchy in [Analyzer Diagnostics and Suppressions][analyzer-diagnostics-and-suppressions]. .NET mechanics, narrowest first:

- **Never use `#pragma warning disable`** to silence an analyzer.
- **Symbol-scoped**: a `[System.Diagnostics.CodeAnalysis.SuppressMessage(...)]` attribute with a `Justification`, on the specific member or type:

  ```csharp
  [System.Diagnostics.CodeAnalysis.SuppressMessage(
      "Design",
      "CA1034:Nested types should not be visible",
      Justification = "https://github.com/dotnet/sdk/issues/51681"
  )]
  ```

- **Project-scoped** (e.g. a test project): a `dotnet_diagnostic.<RULE>.severity` entry in *that project's own* `.editorconfig`, with a comment explaining why.
- **Repo-wide**: a `dotnet_diagnostic.<RULE>.severity` entry in the root `.editorconfig`, only when the rule is genuinely not applicable to any project. Relaxing a batch of `CA*` rules (or `dotnet_analyzer_diagnostic.severity`) to push a brownfield port through the build is exactly what this forbids.

#### Error Handling and Logging

1. **Structured logging**: Use structured message templates. Serilog is the **application's** concrete backend, and a library never references it (see item 2)

   ```csharp
   logger.LogError(exception, "{Function}", function);
   ```

2. **Libraries log through abstractions, never a concrete backend.** A NuGet **library** depends only on `Microsoft.Extensions.Logging.Abstractions` and exposes an `ILoggerFactory` seam: a settable global factory defaulting to `NullLoggerFactory.Instance` (fallback `NullLogger.Instance`) with `SetFactory`/`TrySetFactory`, and/or an `ILoggerFactory`/`ILogger` parameter in its API. It must **not** reference Serilog or any sink, which would force a logging framework on every consumer and drag in AOT-incompatible dependencies. The consuming **application** owns the concrete logger (Serilog is fine there), bridges it to `ILoggerFactory` (e.g. `SerilogLoggerFactory` from `Serilog.Extensions.Logging`), and injects it. Reference pattern: a `LogOptions` seam in the library, against which the consuming CLI builds the Serilog-backed factory and injects it via `LogOptions.SetFactory`.

3. **CallerMemberName**: Use for automatic function name tracking

   ```csharp
   public bool LogAndPropagate(
       Exception exception,
       [CallerMemberName] string function = "unknown"
   )
   ```

4. **Logger extensions**: Use `Extensions.cs` for logger and other extension methods

   ```csharp
   extension(ILogger logger)
   {
       public bool LogAndPropagate(Exception exception, ...) { }
   }
   ```

5. **Exceptions**: Do not swallow exceptions, and either log and rethrow or translate to a domain-specific exception

#### Code Patterns

1. **Guard clauses**: Prefer early returns for validation and error handling
2. **Async all the way**: Avoid blocking calls (`.Result`, `.Wait()`) and use `async`/`await`
3. **Cancellation tokens**: Accept `CancellationToken` as the last parameter and pass it through
4. **ConfigureAwait**: In library code, use `ConfigureAwait(false)` unless context is required
   - Do not call `ConfigureAwait(false)` in xUnit tests (see xUnit1030)
5. **Disposables**: Use `await using` for async disposables, and prefer `using` declarations
6. **LINQ vs loops**: Use LINQ for clarity, loops for hot paths or allocations
7. **HTTP**: Reuse `HttpClient` via factory, never per-request instantiation
8. **Collections**: Prefer `IReadOnlyList<T>`/`IReadOnlyCollection<T>` for public APIs
9. **Immutability**: Prefer immutable records, use init-only setters when records are not suitable, and prefer immutable or frozen collections for read-only data
10. **Exceptions as control flow**: Avoid using exceptions for expected flow
11. **Sealing classes**: Seal classes that are not designed for inheritance
12. **Read-only data**: Use immutable or frozen collections for read-only data sets
13. **Lazy initialization**: Use `Lazy<T>` for static, thread-safe instantiation (e.g., logger factory, HTTP factory)

#### Testing Conventions

1. **Framework**: **xUnit v3 or later** (the `xunit.v3` package, never the legacy v2 `xunit` package) with **AwesomeAssertions** for every assertion. Native xUnit asserts (`Assert.Equal`, `Assert.True`, ...) are not allowed, so use the fluent `.Should()` API. Dynamic test skipping (`Assert.Skip`, `Assert.SkipWhen`) is control flow, not an assertion, and stays native.

   ```csharp
   [Fact]
   public void MethodName_Scenario_ExpectedBehavior()
   {
       // Arrange
       int expected = 42;

       // Act
       int actual = GetValue();

       // Assert
       actual.Should().Be(expected);
   }
   ```

2. **Organization**: Arrange-Act-Assert pattern
3. **Naming**: Descriptive names with underscores
4. **Theory tests**: Use `[Theory]` with `[InlineData]`

### Project Configuration

1. **Target framework**: .NET 10.0 (`<TargetFramework>net10.0</TargetFramework>`)

2. **AOT compatibility**
   - `<IsAotCompatible>true</IsAotCompatible>`
   - `<VerifyReferenceAotCompatibility>true</VerifyReferenceAotCompatibility>`

3. **Assembly information**
   - Use semantic versioning
   - Include SourceLink: `<PublishRepositoryUrl>true</PublishRepositoryUrl>`
   - Embed untracked sources: `<EmbedUntrackedSources>true</EmbedUntrackedSources>`

4. **Internal visibility**: Use `InternalsVisibleTo` for test and benchmark access (adapt the project names to your repo's test/benchmark projects)

   ```xml
   <ItemGroup>
     <InternalsVisibleTo Include="YourBenchmarkProject" />
     <InternalsVisibleTo Include="YourTestProject" />
   </ItemGroup>
   ```

### Best Practices

1. **Code reviews**: All changes go through pull requests

## Python

*This section applies only to the Python side. A repo with no Python projects still carries it (the file is carried whole) and ignores it.*

This is the style guide for any **Python project(s)** in this repo.

**Adapt before propagating.** The rules below describe the default Python profile: a package that publishes to PyPI, type-checked by `pyright` in strict mode, dependencies in `[dependency-groups]`. A derived repo often differs, and when it does, **adapt these fields to match the repo's actual toolchain rather than copying verbatim** (a verbatim copy that misdescribes the repo is inaccurate and gets rejected in review). The axes that commonly vary per repo:

- **Type checker in CI** - `pyright` strict, **`mypy` in CI with `pyright` editor-only** (Pylance), or both. Whichever runs in CI is the one the clean-compile and the CI gate invoke.
- **Dependency declaration** - `[dependency-groups]`, or PEP 621 `[project.optional-dependencies]` (dev tools installed with `uv sync --extra <group>`).
- **Versioning / publishing** - a published package (`_version.py` + a version source + `uv build` + a PyPI publish step), or a **source-only** repo with a static `version` and no publish step (see [Versioning][versioning-section]).
- **Disabled markdownlint rules** - repo-specific. `.markdownlint-cli2.jsonc` at the repo root is the source of truth, not any example rule named here.
- **VS Code config home** - editor **settings/extensions** may live in `.vscode/*.json` **or** the `<Repo>.code-workspace`, while **tasks / launch / debug** configs can only be external `.vscode/*.json` (they cannot live in the workspace file). The repo's own `tasks.json` sits wherever it keeps it, and the canonical task definitions it is written against are the hub snippet the [`vscode-tasks.json`][vscode-tasks-link] reference names, which resolves the same way from every repo.

**Two profiles.** A repo's Python is one of two shapes, declared as the `build` or `lint-only` profile and validated against the `pyproject.toml` shape. The rest of this section (uv project, `uv.lock`, `uv run`, `src` layout, pytest coverage) describes the **Project** shape (the `build` profile). The two differ by whether the Python has **third-party runtime dependencies**, which shows up structurally in `pyproject.toml`, so the audit reads the shape there (`python.profile.detect`):

- **Project** (the `build` profile): the Python has third-party runtime dependencies, or is the repo's deliverable. It is a PEP 621 uv project: `[project]` with `dependencies` (dev tools in `[project.optional-dependencies]` or `[dependency-groups]`), a `[build-system]`, and a committed `uv.lock` (pinned LF, per [Line Endings][line-endings]). CI runs `uv sync --frozen` + `uv run <tool>`, so the lockfile pins tool versions.
- **Scripts** (the `lint-only` profile): stdlib-only utility scripts embedded in a **non-Python** repo (e.g. a Python tooling subtree of a `csharp` app). Run the tools with **`uvx`** (no project install, no lockfile): the `pyproject.toml` carries **only** tool config (`[tool.ruff]`, `[tool.mypy]`, and an optional `[tool.pyright]` editor block), with no `[project]`, no `[build-system]`, and no `uv.lock` (that metadata would misrepresent it as a shippable package). **mypy** is the type-check gate (there is no first-party package for pyright strict to anchor on), and a `[tool.pyright]` block in **standard** mode keeps Pylance quiet in the editor, the same mypy-gate/pyright-editor split the build profile uses. There is no lockfile, and a `uvx <tool>@<ver>` pin in a `run:` step is not something Dependabot tracks, so **CI runs `uvx ruff@latest` / `uvx mypy@latest`** rather than a manual pin that would silently go stale. The fleet rule is to pin only what Dependabot auto-updates (SHA-pinned actions, package deps) and otherwise run latest, so the VS Code tasks, README, and CI all run the unpinned latest here. `.py` files follow the repo's line-ending default (CRLF in a CRLF-default repo, and a shebang-executed script is LF-pinned by path, per [Line Endings][line-endings]). There is no pytest suite and no coverage gate. A script that carries a gate still earns tests, written with the standard library's `unittest` so they run under bare `python3` with nothing installed, as `test_<script>.py` beside the script it exercises. Measure them with `uvx coverage@latest run -m unittest discover -s <dir>` when a number is wanted, without adopting a threshold. A co-present `csharp` type still carries `codecov.yml` for its own tests.

**PlexCleaner uses the Scripts profile.** `RegressionTests/` contains stdlib-only utilities embedded in a C# application. Its `pyproject.toml` carries only Ruff and mypy configuration, there is no `uv.lock` or Python package metadata, and CI runs `uvx ruff@latest` and `uvx mypy@latest` from that directory. The regression tools have no pytest or coverage gate; their corpus is external and is verified through the harness described in [OPERATIONS][operations].

### Toolchain

| Tool | Role | Config |
|---|---|---|
| [uv][uv-link] | env, deps, build, publish (build/publish only where the repo ships a package) | `pyproject.toml` `[dependency-groups]` or `[project.optional-dependencies]`, `uv.lock` |
| [hatchling][latest-link] | build backend (published packages) | `pyproject.toml` `[build-system]` |
| [ruff][ruff-link] | lint + format + import sort | `pyproject.toml` `[tool.ruff]` |
| [pyright][pyright-link] | type checker (the default, a strict baseline) | `pyproject.toml` `[tool.pyright]` |
| [mypy][mypy-link] | additional/alternate type checker (optional, the CI checker in a mypy-in-CI repo, required for Home Assistant) | `pyproject.toml` `[tool.mypy]` (or per home-assistant/core) |
| [pytest][docs-link] | test runner | `pyproject.toml` `[tool.pytest.ini_options]` |

**Type checking targets strongly typed, deterministic code.** `pyright` in **strict** mode is the default baseline on first-party code (a repo may instead run `mypy` in CI and keep `pyright` editor-only via Pylance, per the next paragraph) (`[tool.pyright]` `strict = ["src"]`, or the integration package for a Home Assistant repo, with tests run in standard mode). pyright is the anchor because **Pylance embeds it**, so the editor and the CLI/CI (`uv run pyright`) run the *same* engine and never disagree. The standalone `ms-pyright.pyright` extension stays in `unwantedRecommendations` because Pylance covers it. Relax strictness on **third-party** code only when a dependency has no usable types and no alternative (e.g. `pandas`): a targeted, commented `# pyright: ignore[...]` or a scoped `[tool.pyright]` override, never a blanket relaxation.

**`mypy` is allowed, and required where the ecosystem demands it. It is not banned.** Running more than one checker is normal when each serves a purpose (the .NET side pairs `CSharpier` and `dotnet format` the same way), and pyright's inference and mypy's plugin ecosystem (e.g. `pydantic.mypy`) catch different classes of error. A **Home Assistant** integration runs `mypy --strict` because the platinum `strict-typing` quality-scale tier requires it, and a pydantic-heavy library may opt in for the plugin. When a repo uses mypy it runs in **CI and the editor** (the `ms-python.mypy-type-checker` extension) so the two stay consistent, and its mypy command joins the clean-compile. A repo with no such need stays pyright-only, which is lighter and inherently consistent.

### Local Development Loop

From inside a Python project directory, use the Project profile commands below. PlexCleaner's Scripts profile uses the equivalent `uvx` commands from [OPERATIONS][operations]:

```sh
uv sync                          # creates .venv, installs deps + dev group
uv run ruff format               # auto-format
uv run ruff check --fix          # auto-fix lint
uv run ruff check                # verify lint clean
uv run ruff format --check       # verify format clean
uv run pyright                   # verify types
uv run pytest                    # run tests
uv build                         # produce wheel + sdist in ./dist (published packages only)
```

The Python clean-compile (see [Clean-Compile Verification][clean-compile-verification]) is `uv run ruff format` + `uv run ruff check` + the repo's type checker: `uv run pyright`, or `uv run mypy src` where mypy is the CI checker, or both where the repo runs both (see Type checking above). Run it, plus `uv run pytest`, before committing. These are documented commands, and an optional VS Code tasks mirror (all `type: process`, no `&&` shell chaining, so it runs the same on any task shell) is in [`vscode-tasks-python.json`][vscode-tasks-python-link]. CI runs the same clean-compile commands as the authoritative backstop. Git hooks are opt-in, so wire `pre-commit` for `ruff` and the type checker yourself if you want local enforcement.

### Layout

`src` layout, which keeps the package out of the repo root and prevents accidental imports of unbuilt code:

```text
<python-project>/
    pyproject.toml
    README.md
    uv.lock                # committed for reproducible CI
    src/
        <package_name>/
            __init__.py
            _version.py        # published packages; a source-only repo uses a static version instead
            <modules>.py
    tests/
        __init__.py
        test_<module>.py
```

### Code Style

#### Formatting and Linting

- **`ruff format` is authoritative.** Don't argue with the formatter, and if it reformats your code, that's the final form. Configure (line length, target version) in `pyproject.toml` `[tool.ruff]`, not via inline `# fmt:` directives.
- **Run `ruff check --fix` before committing.** Most ruff lint rules have safe autofixes, so let the tool handle them. The configured rule families are listed under `[tool.ruff.lint]` `select`. Add new rule families project-wide rather than scattering inline `# noqa` markers.
- **`# noqa` is a last resort.** When you must use one, scope it narrowly (`# noqa: E501`, not bare `# noqa`) and add a short comment on the same line explaining why. False-positive patterns that recur across the codebase belong in `[tool.ruff.lint]` `ignore` or per-file `[tool.ruff.lint.per-file-ignores]`, with a comment. Porting an existing codebase is not a license to add `ignore` / `per-file-ignores` blocks to mute newly surfaced lint. Fix it (see [Analyzer Diagnostics and Suppressions][analyzer-diagnostics-and-suppressions]).

#### Comments

- **Inline `#` comments**: keep tight and local. One line is preferred, but multi-line is fine when you need to document a non-obvious implementation constraint, a local trade-off, or coupling that future edits could easily break. Keep that rationale next to the affected block so the reviewer/maintainer sees it at edit-time.
- **Don't explain *what* the code does.** Well-named identifiers handle that. Don't reference the current task ("added for X", "used by Y"), which belongs in the PR description.

#### Docstrings

- Follow [PEP 257][pep-0257-link]. Focus docstrings primarily on the **behavior contract** (what callers and tests can rely on), public semantics, and edge-case expectations. Implementation-local rationale belongs in inline `#` comments, not docstrings.
- A short one-liner is fine for trivial functions and tests with self-documenting names.
- For non-trivial behavior (non-obvious test scenarios, contracts a test pins, edge cases callers must know about, design trade-offs that are load-bearing for future maintainers), write a one-line summary, blank line, then a details paragraph. Multi-paragraph docstrings are fine when the contract earns it.
- Design notes belong **in the code** (docstrings or inline comments). They do NOT belong in [`HISTORY.md`][history], which is end-user release notes, not a design log.

#### Type Hints

- **All public APIs are typed.** The repo's configured type checker runs on `src/` (pyright strict via `[tool.pyright]` `strict = ["src"]`, or `mypy` where that is the CI checker), and tests run in the checker's looser/standard mode.
- **Use modern syntax**: `list[int]` not `List[int]`, `dict[str, X]` not `Dict[str, X]`, `X | None` not `Optional[X]`, `from __future__ import annotations` only when needed for forward references.
- **Don't add `# type: ignore` to silence pyright errors without a comment** explaining the constraint. If a recurring false positive needs suppression, configure it project-wide in `[tool.pyright]`. A new port doesn't change this, so fix freshly surfaced type errors rather than muting them (see [Analyzer Diagnostics and Suppressions][analyzer-diagnostics-and-suppressions]).

#### Naming

- `snake_case` for functions, methods, variables, modules, package directories.
- `PascalCase` for classes, type aliases, type vars, enum members.
- `UPPER_SNAKE_CASE` for module-level constants.
- Single leading underscore for module-private, double leading underscore for name-mangled (rare, and usually means rethink the design).

#### Imports

- **Let ruff sort imports.** `[tool.ruff.lint]` `select` includes the `I` rule family (isort-equivalent). Don't hand-sort.
- Standard library first, then third-party, then first-party (the project itself), each block separated by a blank line, which ruff enforces automatically.
- Avoid wildcard imports (`from x import *`) outside `__init__.py` re-exports.

#### Patterns to Avoid

- **Don't add backward-compat shims, `# removed` markers, or rename-to-`_` for unused vars** - just delete. Git history is the audit trail.
- **Don't add error handling for impossible cases.** Trust internal code, and validate only at boundaries (user input, parsed config, external APIs).
- **Don't use exceptions for expected control flow.** Exceptions are for *unexpected* states.
- **Don't suppress errors silently** (`except Exception: pass`). Either handle the specific exception and document why it's safe, or let it propagate.

### Tests

- `pytest` with the configuration in `[tool.pytest.ini_options]`. Default invocation: `uv run pytest`.
- One test file per module under test, named `test_<module>.py`.
- Test functions named `test_<scenario>_<expected_behavior>`, descriptive and not numbered.
- Use fixtures (defined in `conftest.py` for shared ones, or per-test for narrowly-scoped) instead of setup/teardown methods.
- **Avoid mocking when fakes work.** Hand-rolled fakes that implement the protocol you depend on are usually clearer and break less than `unittest.mock` magic.
- **Test edge cases that the docstring promises**, not implementation details. If the test breaks when you refactor *without changing behavior*, the test is asserting on an implementation detail.

### Versioning

**Published packages.** `_version.py` ships with `__version__ = "0.0.0"` as a placeholder. Until you wire `_version.py` to something that increments (the usual options are `hatch-vcs`, a version.json bridge, or manual bumps), no new PyPI versions will land, and publishing with `skip-existing: true` keeps a stuck placeholder version from failing the run.

**Source-only repos** (no PyPI publish, with a source-release on dispatch or no release at all) do not need `_version.py`: keep a static `version` in `pyproject.toml` `[project]`, or let the release pipeline's version source (e.g. NBGV + `version.json`) own the tag. There is no publish step to guard, so `skip-existing` does not apply.

### Linter Cleanliness

Before pushing or opening a PR:

- VS Code's **Problems** pane should be quiet for the files you touched. The relevant linters are ruff (via the `charliermarsh.ruff` extension) and pyright (via the `ms-python.python` extension's bundled Pylance).
- The CI gate is `uv run ruff check`, `uv run ruff format --check`, the repo's type checker (`uv run pyright` or `uv run mypy src`), and `uv run pytest`, the same commands as the local loop above, run from the Python project directory. (Invoke them as separate steps, not `&&`-chained, so the runner shell is irrelevant.)
- Markdown in this directory follows the repo-wide [Markdown and Spelling][markdown-and-spelling] rules.

## Shell

Bash, and only where a program cannot be Python: a bootstrap that installs the interpreter cannot be written in it, and a host tool that must run before a development toolchain exists cannot depend on one. Everything else is Python, with a test beside it.

- **A reader that stops early needs its producer read first.** Under `pipefail`, a producer writing to a closed pipe exits non-zero, so `curl ... | grep -q` reports a successful fetch as a failure whenever the match is found early enough. Capture the output, then search it.
- **Self-locating, never dependent on the caller's directory.** A script resolves its own directory from `BASH_SOURCE` and references its payloads through it, since the working directory at invocation is not a property of the script.
- **`shellcheck` clean, and a deliberate exception carries its reason inline.** A `# shellcheck disable=SCxxxx` names why the rule does not apply here, so the next reader can tell a considered exception from an unread warning. The hub-hosted repository configuration script is the worked example, carrying narrowly scoped shellcheck exceptions where a single-quoted `jq` program must stay unexpanded.
- **Comments say why, never what.** The code states what it does. A comment restating it goes stale silently, where a comment carrying a reason fails visibly when the reason stops being true.

<!-- Repo -->

[analyzer-diagnostics-and-suppressions]: #analyzer-diagnostics-and-suppressions
[clean-compile-verification]: #clean-compile-verification
[governance]: ./GOVERNANCE.md
[governance-running-the-linters-locally]: ./GOVERNANCE.md#running-the-linters-locally-known-working-invocations
[governance-verification-discipline]: ./GOVERNANCE.md#verification-discipline
[history]: ./HISTORY.md
[line-endings]: ./GOVERNANCE.md#line-endings
[markdown-and-spelling]: #markdown-and-spelling
[markdownlint-cli2]: ./.markdownlint-cli2.jsonc
[operations]: ./OPERATIONS.md
[readme]: ./README.md
[root]: ./.editorconfig
[versioning-section]: #versioning

<!-- External -->

[docs-link]: https://docs.pytest.org/
[latest-link]: https://hatch.pypa.io/latest/
[mypy-link]: https://mypy-lang.org/
[pep-0257-link]: https://peps.python.org/pep-0257/
[pyright-link]: https://microsoft.github.io/pyright/
[ruff-link]: https://docs.astral.sh/ruff/
[uv-link]: https://docs.astral.sh/uv/
[vscode-tasks-link]: https://github.com/ptr727/ProjectTemplate/blob/main/catalog/snippets/configs/vscode-tasks.json
[vscode-tasks-python-link]: https://github.com/ptr727/ProjectTemplate/blob/main/catalog/snippets/configs/vscode-tasks-python.json
