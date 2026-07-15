# Code Style and Formatting Rules

This is the code-style guide for the repo. The **General** section applies repo-wide; the **.NET** section covers the C# code.

Cross-cutting *process* rules (PR titles, branching, US English, markdown style, comments philosophy, workflow YAML, PR review etiquette) live in [AGENTS.md](./AGENTS.md) and are not repeated here.

## General

These rules apply repo-wide.

### Tooling Names and Casing

Use each tool's official casing in task labels, docs, and prose - `.NET` (not `.Net`), `CSharpier`, `ruff`, `pyright`, `uv`. Don't invent personal variants.

### Clean-Compile Verification

Each language defines a **clean-compile** verification - the combination of build, formatter, linter, and code-analysis tools that must report clean before a commit. It is exposed as one or more **named** VS Code tasks; the concrete names live in the .NET section below.

- **Run it after every code change.** The relevant language's clean-compile must pass before you commit; CI runs the same checks as a backstop.
- **The named task definition is the canonical spec** - its exact command sequence, arguments, and strictness. You may run it through the VS Code task **or** by invoking the equivalent native commands directly; either is fine **only if the sequence, arguments, and strictness match exactly**. No shortcuts and no more-lenient options (for example, never drop `--verify-no-changes` or loosen a `--severity`).
- **A local commit/pre-commit gate is the repo's choice.** CI is the authoritative backstop regardless; a local gate is an additive convenience - this repo wires Husky.Net (with `dotnet husky run` as a style step). Keeping a working gate is not drift.

### Analyzer Diagnostics and Suppressions

- **A new port is not a license to silence diagnostics.** Brownfield / just-ported status never justifies relaxing analyzer or linter severities or muting newly surfaced warnings - fix them.
- **Suppress only genuine false-positives or deliberate, documented exceptions**, always at the **narrowest scope that fits**, in this order of preference:
  1. An **in-code annotation on the specific symbol**, with a justification - the language's attribute/comment form, never a blanket pragma spanning a region.
  2. The **owning project's local config** when the exception is project-wide for one project (e.g. a test project's own `.editorconfig` / `pyproject.toml`).
  3. The **root / shared config** only when the suppression is genuinely applicable to **every** project in the repo.
- **Never blanket-relax a batch of rules project-wide** to get a port to build. The mechanics (which attribute, which config key) are in the .NET section.

### Markdown and Spelling

These apply repo-wide, in every directory:

1. **Markdown linting**: All `.md` files must be lint-clean (error and warning free) via the VS Code `markdownlint` extension. [`.markdownlint-cli2.jsonc`](./.markdownlint-cli2.jsonc) at the repo root is the single source of truth - the davidanson `markdownlint` extension and a command-line `markdownlint-cli2` run both read it, so the IDE and CLI stay in lock-step. Rules it deliberately disables (e.g. `MD013` line-length, `MD033` inline HTML) are **intentional** - do not "fix" them. Fix violations at the source rather than disabling rules.
2. **Spelling**: All spelling must be clean via the CSpell VS Code integration; words must be correctly spelled in **US English** (the repo-wide convention - see [AGENTS.md](./AGENTS.md)). Project-specific terms go in the workspace CSpell config.
3. **Spelling CI scope**: The enforced CI spell-check gate covers **`README.md` and `HISTORY.md` only** - these are the files every repo visitor sees, so they must be clean. It is deliberately **not** all `**/*.md`: repos carry many markdown files full of technical terms, and gating every one of them would mean endlessly padding `cspell.json` just to keep CI green. Broad, live spell-checking across any file (source, markdown, text) is the **cspell editor extension's** job, so typos still surface to whoever is editing. A repo owner **may** widen their own CI file list, but the template ships README + HISTORY as the default; keep the CI workflow, the `Lint: Spelling` VS Code task, and the AGENTS.md cspell one-liner on the same file list. The list is explicit (not a glob), so a repo that ships no `HISTORY.md` (e.g. one with no changelog) must drop it from all three surfaces and gate on `README.md` alone - cspell errors on a listed file that does not exist. Markdown *linting* (item 1) stays repo-wide `**/*.md` - it does not choke on technical terms.

## .NET

This is the style guide for any **.NET projects** in this repo.

### Build Requirements

#### Zero Warnings Policy

**CRITICAL**: All builds must complete without warnings. The project enforces this through:

1. **The `.NET Format` clean-compile task** (see [Clean-Compile Verification](#clean-compile-verification))
   - The .NET clean-compile is the **`.NET Format`** VS Code task, which chains `CSharpier Format` -> `.NET Build` -> `dotnet format style --verify-no-changes`. These three task definitions live in [`.vscode/tasks.json`](./.vscode/tasks.json).
   - After any code change it must pass before commit. Run the `.NET Format` task. To run it natively instead, reproduce that task chain from [`.vscode/tasks.json`](./.vscode/tasks.json) exactly - `CSharpier Format`, then `.NET Build`, then the `dotnet format style --verify-no-changes --severity=info ...` verify - without dropping or loosening any argument (tasks.json is the canonical command spec). Bare `dotnet format` alone, skipping CSharpier or the build, is not sufficient.

2. **Analyzer configuration**
   - `<EnableNETAnalyzers>true</EnableNETAnalyzers>` with `<AnalysisLevel>latest-all</AnalysisLevel>` and `<AnalysisMode>All</AnalysisMode>` (full analyzer set enabled)
   - `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` - any diagnostic surfaced as a warning fails the build, so it must be fixed or deliberately suppressed, not left to accumulate (see [Analyzer Diagnostics and Suppressions](#analyzer-diagnostics-and-suppressions))

3. **CI lint backstop**
   - CI runs the clean-compile checks on every PR as the authoritative backstop
   - Git hooks are optional; a repo may wire a local runner (Husky.Net) for pre-commit enforcement, but CI is the gate that matters

#### Build Tasks

Available VS Code tasks (run them from VS Code's task runner - **Terminal -> Run Task** - or an agent's task-running tool). The three clean-compile tasks below are the canonical set; add your own convenience tasks (tool updates, dependency upgrades, benchmarks) on top:

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

CI is the authoritative lint backstop. Local pre-commit hooks are optional - wire Husky.Net (or another runner) if you want local enforcement.

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
   - Extension methods - the classic `this`-parameter form, or an `extension(<receiver>) { ... }` block on C# 14+
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
   global using Serilog;
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

5. **Line endings**
   - C#, XML, YAML, JSON, Windows scripts: CRLF
   - Linux scripts (`.sh`): LF

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

Follow the scope hierarchy in [Analyzer Diagnostics and Suppressions](#analyzer-diagnostics-and-suppressions). .NET mechanics, narrowest first:

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

1. **Serilog logging**: Use structured logging

   ```csharp
   logger.Error(exception, "{Function}", function);
   ```

2. **Library log configuration**: Libraries must expose logging configuration
   - Provide options or settings to supply an `ILoggerFactory` and/or `ILogger`
   - Offer a global fallback logger for static usage when needed

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

5. **Exceptions**: Do not swallow exceptions; log and rethrow or translate to a domain-specific exception

#### Code Patterns

1. **Guard clauses**: Prefer early returns for validation and error handling
2. **Async all the way**: Avoid blocking calls (`.Result`, `.Wait()`); use `async`/`await`
3. **Cancellation tokens**: Accept `CancellationToken` as the last parameter and pass it through
4. **ConfigureAwait**: In library code, use `ConfigureAwait(false)` unless context is required
   - Do not call `ConfigureAwait(false)` in xUnit tests (see xUnit1030)
5. **Disposables**: Use `await using` for async disposables; prefer `using` declarations
6. **LINQ vs loops**: Use LINQ for clarity, loops for hot paths or allocations
7. **HTTP**: Reuse `HttpClient` via factory; avoid per-request instantiation
8. **Collections**: Prefer `IReadOnlyList<T>`/`IReadOnlyCollection<T>` for public APIs
9. **Immutability**: Prefer immutable records; use init-only setters when records are not suitable; prefer immutable or frozen collections for read-only data
10. **Exceptions as control flow**: Avoid using exceptions for expected flow
11. **Sealing classes**: Seal classes that are not designed for inheritance
12. **Read-only data**: Use immutable or frozen collections for read-only data sets
13. **Lazy initialization**: Use `Lazy<T>` for static, thread-safe instantiation (e.g., logger factory, HTTP factory)

#### Testing Conventions

1. **Framework**: xUnit with AwesomeAssertions

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
