# Code Style and Formatting Rules

This is the single code-style guide for the repo. The **General** section applies to every language. Each **language section** (.NET, Python) is self-contained: a repo reads only the section(s) for the languages it ships and ignores the rest. The whole file is carried, not trimmed - an unused-language section costs nothing and keeps re-sync a clean overwrite, the same carry-whole model as [`.editorconfig`](./.editorconfig), whose inert `[*.cs]` block a non-.NET repo keeps.

Cross-cutting *process* rules (PR titles, branching, US English, markdown style, comments philosophy, workflow YAML, PR review etiquette) live in [AGENTS.md](./AGENTS.md) and are not repeated here.

## General

These rules apply to every language in the repo.

### Tooling Names and Casing

Use each tool's official casing in task labels, docs, and prose - `.NET` (not `.Net`), `CSharpier`, `ruff`, `pyright`, `uv`. Don't invent personal variants.

### Clean-Compile Verification

Each language defines a **clean-compile** verification - the combination of build, formatter, linter, and code-analysis tools that must report clean before a commit. It is exposed as one or more **named** VS Code tasks (or, where a language ships no tasks, documented commands), and those definitions are **carried verbatim** across derived repos. The concrete names live in each language section below.

- **Run it after every code change.** The relevant language's clean-compile must pass before you commit; CI runs the same checks as a backstop.
- **The named task definition is the canonical spec** - its exact command sequence, arguments, and strictness. You may run it through the VS Code task **or** by invoking the equivalent native commands directly; either is fine **only if the sequence, arguments, and strictness match exactly**. No shortcuts and no more-lenient options (for example, never drop `--verify-no-changes` or loosen a `--severity`).
- **A local commit/pre-commit gate is the derived repo's choice - the template ships no hook runner only because no single runner fits every language it targets** (a `dotnet`-tool runner like Husky.Net suits .NET but not Python), **not** as a recommendation against commit gates. CI is the authoritative backstop regardless; a local gate is an additive convenience a repo may wire and keep - Husky.Net (and `dotnet husky run` as a style step) for .NET, `pre-commit` for Python. Keeping a working gate is not drift, and "no hooks ship by default" must not be read as "remove your gate to stay aligned".

### Analyzer Diagnostics and Suppressions

- **A new port is not a license to silence diagnostics.** Brownfield / just-ported status never justifies relaxing analyzer or linter severities or muting newly surfaced warnings - fix them. (The only brownfield allowance in this template is the one-time git-signing / line-ending migration described in [AGENTS.md](./AGENTS.md) and [README.md](./README.md), which has nothing to do with code analysis.)
- **Suppress only genuine false-positives or deliberate, documented exceptions**, always at the **narrowest scope that fits**, in this order of preference:
  1. An **in-code annotation on the specific symbol**, with a justification - the language's attribute/comment form, never a blanket pragma spanning a region.
  2. The **owning project's local config** when the exception is project-wide for one project (e.g. a test project's own `.editorconfig` / `pyproject.toml`).
  3. The **root / shared config** only when the suppression is genuinely applicable to **every** project in the repo.
- **Never blanket-relax a batch of rules project-wide** to get a port to build. The per-language mechanics (which attribute, which config key) are in each language section.

### Markdown and Spelling

These apply repo-wide, in every directory:

1. **Markdown linting**: All `.md` files must be lint-clean (error and warning free) via the VS Code `markdownlint` extension. [`.markdownlint-cli2.jsonc`](./.markdownlint-cli2.jsonc) at the repo root is the single source of truth - the davidanson `markdownlint` extension and a command-line `markdownlint-cli2` run both read it, so the IDE and CLI stay in lock-step. Rules it deliberately disables (e.g. `MD013` line-length, `MD033` inline HTML) are **intentional** - do not "fix" them. Fix violations at the source rather than disabling rules.
2. **Spelling**: All spelling must be clean via the CSpell VS Code integration; words must be correctly spelled in **US English** (the repo-wide convention - see [AGENTS.md](./AGENTS.md)). Project-specific terms go in the workspace CSpell config.

## .NET

*This section applies only to the .NET side. A repo with no .NET projects still carries it (the file is carried whole) and ignores it.*

This is the style guide for the **.NET projects** in this repo.

### Build Requirements

#### Zero Warnings Policy

**CRITICAL**: All builds must complete without warnings. The project enforces this through:

1. **The `.NET Format` clean-compile task** (see [Clean-Compile Verification](#clean-compile-verification))
   - The .NET clean-compile is the **`.NET Format`** VS Code task, which chains `CSharpier Format` -> `.NET Build` -> `dotnet format style --verify-no-changes`. These three task definitions are carried verbatim in [`.vscode/tasks.json`](./.vscode/tasks.json).
   - After any code change it must pass before commit. Run the `.NET Format` task. To run it natively instead, reproduce that task chain from [`.vscode/tasks.json`](./.vscode/tasks.json) exactly - `CSharpier Format`, then `.NET Build`, then the `dotnet format style --verify-no-changes --severity=info ...` verify - without dropping or loosening any argument (tasks.json is the canonical command spec). Bare `dotnet format` alone, skipping CSharpier or the build, is not sufficient.

2. **Analyzer configuration**
   - `<EnableNETAnalyzers>true</EnableNETAnalyzers>` with `<AnalysisLevel>latest-all</AnalysisLevel>` and `<AnalysisMode>All</AnalysisMode>` (full analyzer set enabled)
   - `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` - warnings fail the build, so every diagnostic must be addressed, not muted (see [Analyzer Diagnostics and Suppressions](#analyzer-diagnostics-and-suppressions))

3. **CI lint backstop**
   - CI runs the same clean-compile commands (`dotnet csharpier check` and `dotnet format style --verify-no-changes`) on every PR as the authoritative backstop
   - Git hooks are opt-in - no hook runner ships by default; wire `pre-commit`/Husky.Net yourself if you want local enforcement

#### Build Tasks

Available VS Code tasks (run them from VS Code's task runner - **Terminal -> Run Task** - or an agent's task-running tool). The first three are the clean-compile set, carried verbatim; the rest are convenience tasks a derived repo adapts or drops:

- `.NET Build`: Build with diagnostic verbosity *(clean-compile)*
- `CSharpier Format`: Auto-format code with CSharpier *(clean-compile)*
- `.NET Format`: Run CSharpier and build, then verify formatting and style with `--verify-no-changes` *(clean-compile; the task to run after edits)*
- `.NET Tool Update`: Update dotnet tools *(convenience)*
- `.NET Outdated Upgrade`: Upgrade outdated NuGet dependencies, interactive prompt *(convenience)*
- `.NET Benchmark`: Run BenchmarkDotNet *(project-specific; present only if a Benchmarks project exists)*

### Tooling and Editor

#### Code Formatting and Tooling

1. **CSharpier**: Primary code formatter
   - Invoked by the `CSharpier Format` task / `dotnet csharpier format --log-level=debug .`
2. **dotnet format**: Style verification
   - Verify no changes: `dotnet format style --verify-no-changes --severity=info --verbosity=detailed`
3. **Other tools**
   - `dotnet-outdated-tool`: Dependency update checks
   - Nerdbank.GitVersioning: Version management

Pre-commit git hooks are not installed by default - CI is the lint backstop. Hooks are opt-in; wire Husky.Net (or another runner) yourself if you want local enforcement.

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
   - Extension methods declared inside an `extension(<receiver>) { ... }` block
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

## Python

*This section applies only to the Python side. A repo with no Python projects still carries it (the file is carried whole) and ignores it.*

This is the style guide for the **Python project** in this repo.

### Toolchain

| Tool | Role | Config |
|---|---|---|
| [uv](https://docs.astral.sh/uv/) | env, deps, build, publish | `pyproject.toml` `[dependency-groups]`, `uv.lock` |
| [hatchling](https://hatch.pypa.io/latest/) | build backend | `pyproject.toml` `[build-system]` |
| [ruff](https://docs.astral.sh/ruff/) | lint + format + import sort | `pyproject.toml` `[tool.ruff]` |
| [pyright](https://microsoft.github.io/pyright/) | type checker | `pyproject.toml` `[tool.pyright]` |
| [pytest](https://docs.pytest.org/) | test runner | `pyproject.toml` `[tool.pytest.ini_options]` |

`pyright` is consumed in two places: as a dev dependency (`uv run pyright` for CI/scripted runs) and via VS Code's **Pylance** extension (which embeds pyright). The standalone `ms-pyright.pyright` extension is in `unwantedRecommendations` because Pylance covers it. `mypy` is **not used** here - don't introduce it.

### Local Development Loop

From inside the Python project directory:

```sh
uv sync                          # creates .venv, installs deps + dev group
uv run ruff format               # auto-format
uv run ruff check --fix          # auto-fix lint
uv run ruff check                # verify lint clean
uv run ruff format --check       # verify format clean
uv run pyright                   # verify types
uv run pytest                    # run tests
uv build                         # produce wheel + sdist in ./dist
```

The Python clean-compile (see [Clean-Compile Verification](#clean-compile-verification)) is `uv run ruff format` + `uv run ruff check` + `uv run pyright`; run it (plus `uv run pytest`) before committing. These are documented commands, not VS Code tasks. CI runs the same clean-compile commands as the authoritative backstop. Git hooks are opt-in; wire `pre-commit` for `ruff` and `pyright` yourself if you want local enforcement.

### Layout

`src` layout - keeps the package out of the repo root and prevents accidental imports of unbuilt code:

```text
<python-project>/
    pyproject.toml
    README.md
    uv.lock                # committed for reproducible CI
    src/
        <package_name>/
            __init__.py
            _version.py
            <modules>.py
    tests/
        __init__.py
        test_<module>.py
```

### Code Style

#### Formatting and Linting

- **`ruff format` is authoritative.** Don't argue with the formatter; if it reformats your code, that's the final form. Configure (line length, target version) in `pyproject.toml` `[tool.ruff]`, not via inline `# fmt:` directives.
- **Run `ruff check --fix` before committing.** Most ruff lint rules have safe autofixes; let the tool handle them. The configured rule families are listed under `[tool.ruff.lint]` `select`. Add new rule families project-wide rather than scattering inline `# noqa` markers.
- **`# noqa` is a last resort.** When you must use one, scope it narrowly (`# noqa: E501`, not bare `# noqa`) and add a short comment on the same line explaining why. False-positive patterns that recur across the codebase belong in `[tool.ruff.lint]` `ignore` or per-file `[tool.ruff.lint.per-file-ignores]`, with a comment. Porting an existing codebase is not a license to add `ignore` / `per-file-ignores` blocks to mute newly surfaced lint - fix it (see [Analyzer Diagnostics and Suppressions](#analyzer-diagnostics-and-suppressions)).

#### Comments

- **Inline `#` comments**: keep tight and local. One line is preferred, but multi-line is fine when you need to document a non-obvious implementation constraint, a local trade-off, or coupling that future edits could easily break. Keep that rationale next to the affected block so the reviewer/maintainer sees it at edit-time.
- **Don't explain *what* the code does** - well-named identifiers handle that. Don't reference the current task ("added for X", "used by Y"); that belongs in the PR description.

#### Docstrings

- Follow [PEP 257](https://peps.python.org/pep-0257/). Focus docstrings primarily on the **behavior contract** (what callers and tests can rely on), public semantics, and edge-case expectations. Implementation-local rationale belongs in inline `#` comments, not docstrings.
- A short one-liner is fine for trivial functions and tests with self-documenting names.
- For non-trivial behavior - non-obvious test scenarios, contracts a test pins, edge cases callers must know about, design trade-offs that are load-bearing for future maintainers - write a one-line summary, blank line, then a details paragraph. Multi-paragraph docstrings are fine when the contract earns it.
- Design notes belong **in the code** (docstrings or inline comments). They do NOT belong in [`HISTORY.md`](./HISTORY.md) - that file is end-user release notes, not a design log.

#### Type Hints

- **All public APIs are typed.** Pyright runs on `src/` in strict mode (`[tool.pyright]` `strict = ["src"]`); tests run in standard mode.
- **Use modern syntax**: `list[int]` not `List[int]`, `dict[str, X]` not `Dict[str, X]`, `X | None` not `Optional[X]`, `from __future__ import annotations` only when needed for forward references.
- **Don't add `# type: ignore` to silence pyright errors without a comment** explaining the constraint. If a recurring false positive needs suppression, configure it project-wide in `[tool.pyright]`. A new port doesn't change this - fix freshly surfaced type errors rather than muting them (see [Analyzer Diagnostics and Suppressions](#analyzer-diagnostics-and-suppressions)).

#### Naming

- `snake_case` for functions, methods, variables, modules, package directories.
- `PascalCase` for classes, type aliases, type vars, enum members.
- `UPPER_SNAKE_CASE` for module-level constants.
- Single leading underscore for module-private; double leading underscore for name-mangled (rare - usually means rethink the design).

#### Imports

- **Let ruff sort imports.** `[tool.ruff.lint]` `select` includes the `I` rule family (isort-equivalent). Don't hand-sort.
- Standard library first, then third-party, then first-party (the project itself), each block separated by a blank line - ruff enforces this automatically.
- Avoid wildcard imports (`from x import *`) outside `__init__.py` re-exports.

#### Patterns to Avoid

- **Don't add backward-compat shims, `# removed` markers, or rename-to-`_` for unused vars** - just delete. Git history is the audit trail.
- **Don't add error handling for impossible cases.** Trust internal code; only validate at boundaries (user input, parsed config, external APIs).
- **Don't use exceptions for expected control flow.** Exceptions are for *unexpected* states.
- **Don't suppress errors silently** (`except Exception: pass`). Either handle the specific exception and document why it's safe, or let it propagate.

### Tests

- `pytest` with the configuration in `[tool.pytest.ini_options]`. Default invocation: `uv run pytest`.
- One test file per module under test, named `test_<module>.py`.
- Test functions named `test_<scenario>_<expected_behavior>` - descriptive, not numbered.
- Use fixtures (defined in `conftest.py` for shared ones, or per-test for narrowly-scoped) instead of setup/teardown methods.
- **Avoid mocking when fakes work.** Hand-rolled fakes that implement the protocol you depend on are usually clearer and break less than `unittest.mock` magic.
- **Test edge cases that the docstring promises**, not implementation details. If the test breaks when you refactor *without changing behavior*, the test is asserting on an implementation detail.

### Versioning

`_version.py` ships with `__version__ = "0.0.0"` as a placeholder. Until you wire `_version.py` to something that increments (the usual options are `hatch-vcs`, a version.json bridge, or manual bumps), no new PyPI versions will land - publishing with `skip-existing: true` keeps a stuck placeholder version from failing the run.

### Linter Cleanliness

Before pushing or opening a PR:

- VS Code's **Problems** pane should be quiet for the files you touched. The relevant linters are ruff (via the `charliermarsh.ruff` extension) and pyright (via the `ms-python.python` extension's bundled Pylance).
- The CI gate is `uv run ruff check && uv run ruff format --check && uv run pyright && uv run pytest` - same as the local commands above, run from the Python project directory.
- Markdown in this directory follows the repo-wide [Markdown and Spelling](#markdown-and-spelling) rules.
