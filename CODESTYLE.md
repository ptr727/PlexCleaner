# Code Style and Formatting Rules

This is the single code-style guide for the fleet. The **General** section applies to every language. Each **language section** (.NET, Python, Shell) is self-contained: a repo follows only the section(s) for the languages it ships and ignores the rest. A repo keeps the whole file rather than trimming it. An unused-language section costs nothing, the same whole-file model as [`.editorconfig`][root], whose inert `[*.cs]` block a non-.NET repo keeps.

Cross-cutting *process* rules (PR titles, branching, US English, Markdown style, comments philosophy, workflow YAML, PR review etiquette, and the verification discipline that defines the pre-push lint gate) live in [GOVERNANCE.md][governance] and are not repeated here.

## General

These rules apply to every language in the repo.

### Tooling Names and Casing

Use each tool's official casing in task labels, docs, and prose, per the `comment-and-doc-style` Skill at `.agents/skills/comment-and-doc-style/SKILL.md` in the hub (not a repo-relative link, that path is hub-local and not carried into every fleet repo).

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

### Markdown and Spelling

These apply repo-wide, in every directory: Markdown lints clean via `markdownlint-cli2` against the shared config, spelling is US English via CSpell against the shared `cspell.json`, the CI spelling gate covers `README.md` and `HISTORY.md` only, `HISTORY.md` mirrors the README's opening, and "Markdown" is a proper noun in prose. The full rules are in the `comment-and-doc-style` Skill referenced above.

## .NET

*This section applies only to the .NET side. A repo with no .NET projects still carries it (the file is carried whole) and ignores it.*

The style guide for any .NET projects in this repo: the zero-warnings build policy and its three-task clean-compile chain, central `Directory.Build.props`/`Directory.Packages.props` configuration, C# language and naming conventions, XML documentation, analyzer suppression scope, the library-versus-application logging split, async and error-handling patterns, xUnit v3 + AwesomeAssertions testing conventions, and AOT-compatible project configuration.

This is packaged as the `dotnet-codestyle` Skill at `.agents/skills/dotnet-codestyle/SKILL.md` in the hub, not a repo-relative link since that path is hub-local and not carried into every fleet repo. The summary above sketches the scope. Read the skill for the full rules, code examples, and mechanics.

### PlexCleaner .NET Conventions

The conventions below are this repo's own, beyond the carried rules.

- **The clean-compile is the `.NET Format` VS Code task**, which chains `CSharpier Format` -> `.NET Build` -> `dotnet format style --verify-no-changes --severity=info`. The three task definitions in [`.vscode/tasks.json`][tasks] are the canonical command spec, and `Lint: All (CI parity)` runs the whole CI lint gate locally.
- **This repo wires Husky.Net as its local commit gate.** `dotnet husky run` (configured in `.husky/task-runner.json`) runs CSharpier on the staged `.cs` files and the `dotnet format style` verify, and the hook also runs markdownlint and cspell through Docker when a Markdown file is staged, skipping quietly when Docker or the image is absent. CI enforces all of it regardless.
- **`dotnet format style --verify-no-changes --severity=info` gates the commit, and two IDE fixers bite.** CSharpier alone is not enough, since info-level IDE analyzers run in the `.NET Format` verify. `IDE0072` wants every enum member listed in a `switch` expression, and a `_ =>` discard arm does **not** satisfy it, so map values with explicit arms or a ternary chain rather than leaning on the fixer's `throw new NotImplementedException()`. `IDE0046` rejects `if (!cond) return false; return expr;`, so write the combined `return cond && expr;`. Run the `.NET Format` task before committing to surface these, and review any file it rewrote rather than staging it blind.
- **Repo-wide analyzer relaxations live in the root [`.editorconfig`][root] `[*.cs]` block, each with its reason.** They hold for every project here because PlexCleaner is a console application rather than a reusable library (public API surface rules, localization, `ConfigureAwait`, and a few false positives), and that is the only reason the root scope is justified. A new relaxation follows the scope order in [Analyzer Diagnostics and Suppressions](#analyzer-diagnostics-and-suppressions).
- **AOT is opt-in.** `<PublishAot>false</PublishAot>` is the default, matching the shipped builds, and the plugin loader compiles only in non-AOT builds. Publish with `-p:PublishAot=true` to build an AOT binary.
- **Internals are visible to the test project.** `PlexCleaner.csproj` declares `<InternalsVisibleTo Include="PlexCleanerTests" />`.

## Python

*This section applies only to the Python side. A repo with no Python projects still carries it (the file is carried whole) and ignores it.*

The style guide for any Python project(s) in this repo: the build-versus-lint-only profile split, the uv/ruff/pyright/mypy/pytest toolchain, `src` layout, formatting and linting, comment and docstring conventions, type hints, naming, imports, patterns to avoid, test conventions, and versioning.

This is packaged as the `python-codestyle` Skill at `.agents/skills/python-codestyle/SKILL.md` in the hub, not a repo-relative link since that path is hub-local and not carried into every fleet repo. The summary above sketches the scope. Read the skill for the full rules and the profile-adaptation guidance.

### PlexCleaner Python Conventions

The only Python in this repo is the stdlib-only tooling under `RegressionTests/`, and it follows the **lint-only profile**: no `uv.lock`, no project or build metadata, and `RegressionTests/pyproject.toml` carries only the ruff and mypy configuration. Run the tools with `uvx` from that directory (`uvx ruff check .`, `uvx ruff format --check .`, `uvx mypy .`), or through the `Lint: Ruff`, `Lint: Ruff Format`, and `Lint: Mypy` VS Code tasks. CI pins exact tool versions in `validate-task.yml`, so a local run on the latest tools may differ slightly, by design, so local tooling never silently falls behind.

## Shell

Bash, and only where a program cannot be Python: a bootstrap that installs the interpreter cannot be written in it, and a host tool that must run before a development toolchain exists cannot depend on one. Everything else is Python, with a test under the scripts tree's `tests/` directory. The mandatory `set -Eeuo pipefail` header, the pipefail-versus-early-reader pitfall, self-locating scripts, `shellcheck` cleanliness, and the why-not-what comment rule are packaged as the `shell-codestyle` Skill at `.agents/skills/shell-codestyle/SKILL.md` in the hub, not a repo-relative link since that path is hub-local and not carried into every fleet repo. Read the skill for the full rules.

<!-- Repo -->

[governance]: ./GOVERNANCE.md
[governance-running-the-linters-locally]: ./GOVERNANCE.md#running-the-linters-locally-known-working-invocations
[governance-verification-discipline]: ./GOVERNANCE.md#verification-discipline
[readme]: ./README.md
[root]: ./.editorconfig
[tasks]: ./.vscode/tasks.json
