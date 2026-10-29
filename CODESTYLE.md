# Code Style and Formatting Rules

This is the single code-style guide for the fleet. The **General** section applies to every language. Each **language section** (.NET, Python, Shell) is self-contained: a repo follows only the section(s) for the languages it ships and ignores the rest. A repo keeps the whole file rather than trimming it. An unused-language section costs nothing, the same whole-file model as [`.editorconfig`][root], whose inert `[*.cs]` block a non-.NET repo keeps.

Cross-cutting *process* rules live in [GOVERNANCE.md][governance] and are not repeated here. They cover PR titles, branching, US English, Markdown style, comments philosophy, workflow YAML, and PR review etiquette. They also cover the verification discipline that defines the pre-push lint gate.

## General

These rules apply to every language in the repo.

### Tooling Names and Casing

Use each tool's official casing in task labels, docs, and prose, per the `comment-and-doc-style` Skill at `.agents/skills/comment-and-doc-style/SKILL.md` in the hub. That is not a repo-relative link, since the path is hub-local and not carried into every fleet repo.

### Clean-Compile Verification

Each language defines a **clean-compile** verification: the combination of build, formatter, linter, and code-analysis tools that must report clean before a commit. It is exposed as one or more **named** VS Code tasks (or, where a language ships no tasks, documented commands). Those definitions are the same across the fleet. The concrete names live in each language section below.

- **Run it after every code change, and it is not the whole gate.** The relevant language's clean-compile must pass before you commit. CI runs those same language checks as a backstop **plus everything else its validation workflow runs**. All of it reports into the one required status. A green clean-compile therefore does not predict a green CI. That remainder is at least the doc-lint set (markdownlint, cspell, actionlint, `editorconfig-checker`) and whatever spec, config, and script gates the repo carries. So read the workflow for the full list rather than assuming the previous sentence enumerates it. What has to pass before a push is the repo's **whole** lint gate, per [GOVERNANCE.md "Verification Discipline"][governance-verification-discipline]. Each linter's known-working invocation is in `GOVERNANCE.md` "Running the Linters Locally (Known-Working Invocations)", a hub-only section read in a hub checkout rather than carried into every fleet repo.
- **The named task definition is the canonical spec** - its exact command sequence, arguments, and strictness. You may run it through the VS Code task **or** by invoking the equivalent native commands directly. Either is fine **only if the sequence, arguments, and strictness match exactly**. No shortcuts and no more-lenient options (for example, never drop `--verify-no-changes` or loosen a `--severity`).
- **A working local commit/pre-commit gate is strongly suggested, not the repo's free choice to skip.** The *mechanism* is bounded by the toolchain the repo already keeps rather than by which languages its checks cover, and canonical shapes carry it. Husky.Net runs from a .NET tool manifest declaring it, so a repo that keeps no such manifest uses the `pre-commit` framework instead. Any repo may also wire an equivalent hook of its own at `.husky/pre-commit`, enabled with `core.hooksPath` and sourcing nothing. Canonical shapes for Husky.Net and the `pre-commit` framework live in `catalog/snippets/` in the hub. That is not a repo-relative path, since it is hub-local and not carried into every fleet repo. What that gate must cover, its per-clone enablement steps, and what its absence means for the audit are not restated here. They are in `GOVERNANCE.md` "Running the Linters Locally (Known-Working Invocations)", the same hub-only section. Keeping a working gate is not drift.

### Analyzer Diagnostics and Suppressions

- **A new port is not a license to silence diagnostics.** Brownfield / just-ported status never justifies relaxing analyzer or linter severities or muting newly surfaced warnings. Fix them. (The only brownfield allowance is the one-time git-signing / line-ending migration described in [GOVERNANCE.md][governance] and [README.md][readme], which has nothing to do with code analysis.)
- **Suppress only genuine false-positives or deliberate, documented exceptions**, always at the **narrowest scope that fits**, in this order of preference:
  1. An **in-code annotation on the specific symbol**, with a justification, in the language's attribute/comment form, never a blanket pragma spanning a region.
  2. The **owning project's local config** when the exception is project-wide for one project (e.g. a test project's own `.editorconfig` / `pyproject.toml`).
  3. The **root / shared config** only when the suppression is genuinely applicable to **every** project in the repo.
- **Never blanket-relax a batch of rules project-wide** to get a port to build. The per-language mechanics (which attribute, which config key) are in each language section.

### Operations and Failure Messages

- **Let the real operation be the check, not a prediction of it.** Attempt the read, the write, or the call itself, and turn its failure into a clean refusal. Do not probe first with an existence, type, or permission test. Where the operation could block or succeed on the wrong kind of target, judge what it opened instead. A probe is a second model of the operation. It misjudges each case its author did not foresee, such as a dangling link, a link loop, or a platform's own permission model. The operation cannot disagree with itself.
- **Classify a subprocess failure by which command failed and the status code it reports, never by the prose of its message.** That prose varies by tool version and locale, and a status code's value does not, even one printed inside the message. A printed code is read by the notation that marks it, and a match finding none counts as an unexpected failure. Where one command's status code cannot separate the failures its caller handles differently, give each expected failure its own real operation. Where no such operation exists, treat the failure as unexpected. A test asserting a script's output, such as the refusal "Command-Line Arguments" requires, verifies that output rather than classifying a failure.
- **A failure message states the error, the path or other target, and the remedy, and claims nothing about the state the failure left.** The error the operation raised is the one fact the code holds at that point. A description of what the failure left behind is a prediction. It reads false once an unforeseen state reaches the message, such as a file written in full that then fails to close. A remedy such as "fix what it names, then re-run" needs no such claim.

### Parsing Structured Formats

- **A program reading a structured format uses a maintained parser rather than a hand-written one, where a field-tested parser exists for the runtime in use.** That covers YAML, Markdown, TOML, JSON, a programming language's comments or strings, and a shell command line. The standard library's own parsers count, such as Python's `json`, `tomllib`, `ast`, `tokenize`, and `html.parser`. A hand-written reader converges on the format's full grammar one review finding at a time, and every script that grows one pays that cost again.

### Command-Line Arguments

A command-line script is one a person or an agent runs by name. A hook its host runs, taking its input from that host rather than from a command line, is not one.

- **Every command-line script answers `--help` and `-h`** with the same text on stdout, then exits 0, changing nothing. That text opens on a usage line naming the script. An agent learns a script's interface by asking it, so a script that reads `--help` as an input acts in a way nobody predicted.
- **An argument the script does not recognize is refused**, with a non-zero exit before any read or write. The refusal names it as an unknown or unrecognized option, argument, flag, or parameter, and PowerShell's own refusal of a parameter it cannot bind counts. An unrecognized first argument is never taken as a default mode or as a target.

### Script and Utility File Names

| Type | Rule | Basis |
| --- | --- | --- |
| Python `.py` | lowercase snake_case | [PEP 8][pep8-names-link] module naming. A hyphenated module also cannot be named in an `import` statement |
| Shell `.sh` and PowerShell `.ps1` | lowercase-hyphenated | The Linux command convention. A cross-platform pair keeps one name, `sync-tools.sh` beside `sync-tools.ps1` |
| Windows batch `.cmd` and `.bat` | lowercase-hyphenated | The same convention as shell and PowerShell. A shim that puts a command on `PATH` takes the name of the command it provides, `sync-tools.cmd` launching `sync-tools.ps1` |
| PowerShell functions inside a `.ps1` | Verb-Noun with an approved verb (`Get-Verb`) | PSScriptAnalyzer `PSUseApprovedVerbs`. This is a command rule, not a file rule |
| Env and secret files, tracked or not | lowercase fields joined by `_`, with no dot but the extension | `mail_relay.env`, `db_root_password`, `app_web-01_prod.env` |
| Env variable names and CI secret names | UPPER_SNAKE_CASE | The POSIX and GitHub convention |

- **Installed commands.** An executable or launcher on `PATH` or in a system slot (systemd, cron, run-parts, motd) is named as a command, whatever its language. That name is lowercase-hyphenated. It carries no extension unless the platform needs one to run it, such as `.cmd` or `.ps1` on Windows.
- **Wrapper exemption.** A thin launcher whose only job is to run a Python tool keeps that tool's stem, so `fetch_report.py`, `fetch_report.sh`, and `fetch_report.ps1` stay grouped together. It covers only a launcher run by its path. A launcher installed onto `PATH` or into a system slot is an installed command instead, such as `fetch-report` rather than `fetch_report`.
- **Env and secret file names reach every file a repository names.** That covers a tracked file or template and a secret file mounted into a container. It also covers a host-side file the repository's docs or scripts name, such as an `ENV_FILE` path or a compose `env_file`.
- **`_` joins the fields of an env or secret file name, and each field keeps its own valid form.** A host name field stays verbatim, hyphens included, so `app_web-01_prod.env` is right and `app_web_01_prod.env` and `app.web-01.prod.env` are not. That field is a short name, the host's first DNS label (`web-01`) or a logical role name (`vps-staging`), and never a fully qualified one. Either way it takes the DNS label form, so its own words join with `-`. In every other field a hyphen never joins words, since that is the job of `_`. A host name label can carry `-` and never `_`, so every `_` is a separator and the name splits back into its fields. The name is lowercase, carries no dot but its extension, and starts and ends with a letter or digit. A tracked template's `.example` suffix is not counted as a dot.
- **A variable name converts separately.** An env variable or CI secret name allows only `A-Z`, `0-9`, and `_`. Where a field becomes one, the hyphen becomes `_` there, as in `APP_WEB_01_PROD_TOKEN`, and never in the file name.
- **Some consumers set limits of their own.** A name used as a Docker secret stays within the 64 characters Docker allows. A name that maps to an env variable or CI secret does not start with a digit, which POSIX and GitHub both forbid.
- **Externally fixed names are exempt.** That covers Git hook names, `__init__.py`, `conftest.py`, and Pester's `.Tests.ps1` suffix, whose stem mirrors the script under test. It covers a PowerShell data file (`.psd1`) whose name a tool fixes, such as a module manifest named for its module. `PSScriptAnalyzerSettings.psd1` is another, since PSScriptAnalyzer finds it by that name. So is an env file a tool reads by a fixed name, such as `.env`, `.env.local`, or `.env.production`. An env or secret file in a `.secrets/` directory is never one, since it is read by a path the repository configures. The exemption also covers any name a platform defines.
- **Directories are out of scope.** That means directory names. How a repository splits its Python across directories is a profile rule, stated in the "Python" section below.

Departures, stated on purpose so a reader does not "fix" them back:

- The [Google Shell Style Guide][google-shell-link] asks for underscores in shell file names. The fleet uses hyphens instead, matching installed Linux commands.
- Microsoft's [approved verbs][ps-approved-verbs-link] govern command names. A `.ps1` run by path is a file rather than an exported command, so the file takes the shell convention and its functions take Verb-Noun.

### Markdown and Spelling

These rules apply repo-wide, in every directory. Markdown lints clean via `markdownlint-cli2` against the shared config. Spelling is US English via CSpell against the shared `cspell.json`, and the CI spelling gate covers `README.md` and `HISTORY.md` only. `HISTORY.md` mirrors the README's opening, and "Markdown" is a proper noun in prose. A repo excluding a subtree of its own that it does not treat as authored prose puts a `.markdownlint-cli2.jsonc` beside that content. That file carries its own `ignores`, instead of the repo editing the shared root config, whose contents are fleet-fixed. A committed data archive, a vendored theme, and a hand-maintained record each count as such a subtree. Those `ignores` patterns resolve against the directory holding them rather than against the repo root. So a repo-root-relative entry there matches nothing and reports no error saying so. Excluding through the CI workflow's own negated Markdown glob input instead is a CI-only fix. It leaves the same files flagged for anyone running the linter locally. The full rules are in the `comment-and-doc-style` Skill referenced above.

## .NET

*This section applies only to the .NET side. A repo with no .NET projects still carries it (the file is carried whole) and ignores it.*

The style guide for any .NET projects in this repo covers the zero-warnings build policy and its clean-compile task chain. It covers central `Directory.Build.props`/`Directory.Packages.props` configuration, C# language and naming conventions, XML documentation, and analyzer suppression scope. It covers the library-versus-application logging split, async and error-handling patterns, and xUnit v3 + AwesomeAssertions testing conventions. It covers the runner declaration, package references and version floor that an MTP-based test project needs under `WORKFLOW.md` D1.6. With them it covers the local diagnostic for a run that reports no tests. It also covers AOT-compatible project configuration.

This is packaged as the `dotnet-codestyle` Skill at `.agents/skills/dotnet-codestyle/SKILL.md` in the hub. That is not a repo-relative link, since the path is hub-local and not carried into every fleet repo. The summary above sketches the scope. Read the skill for the full rules, code examples, and mechanics.

### PlexCleaner .NET Conventions

The conventions below are this repo's own, beyond the carried rules.

- **The clean-compile is the `.NET Format` VS Code task**, which chains `CSharpier Format` -> `.NET Build` -> `dotnet format style --verify-no-changes --severity=info`. The three task definitions in [`.vscode/tasks.json`][tasks] are the canonical command spec, and `Lint: All (CI parity)` runs the CI lint gate locally except shellcheck, shfmt, and the prose and repo gates.
- **This repo wires Husky.Net as its local commit gate.** `dotnet husky run` (configured in `.husky/task-runner.json`) runs CSharpier on the staged `.cs` files and the `dotnet format style` verify, and the hook also runs markdownlint and cspell through Docker when a Markdown file is staged, skipping quietly when Docker or the image is absent. CI enforces all of it regardless.
- **`dotnet format style --verify-no-changes --severity=info` gates the commit, and two IDE fixers bite.** CSharpier alone is not enough, since info-level IDE analyzers run in the `.NET Format` verify. `IDE0072` wants every enum member listed in a `switch` expression, and a `_ =>` discard arm does **not** satisfy it, so map values with explicit arms or a ternary chain rather than leaning on the fixer's `throw new NotImplementedException()`. `IDE0046` rejects `if (!cond) return false; return expr;`, so write the combined `return cond && expr;`. Run the `.NET Format` task before committing to surface these, and review any file it rewrote rather than staging it blind.
- **Repo-wide analyzer relaxations live in the root [`.editorconfig`][root] `[*.cs]` block, each with its reason.** They hold for every project here because PlexCleaner is a console application rather than a reusable library (public API surface rules, localization, `ConfigureAwait`, and a few false positives), and that is the only reason the root scope is justified. A new relaxation follows the scope order in [Analyzer Diagnostics and Suppressions](#analyzer-diagnostics-and-suppressions).
- **AOT is opt-in.** `<PublishAot>false</PublishAot>` is the default, matching the shipped builds, and the plugin loader compiles only in non-AOT builds. Publish with `-p:PublishAot=true` to build an AOT binary.
- **Internals are visible to the test project.** `PlexCleaner.csproj` declares `<InternalsVisibleTo Include="PlexCleanerTests" />`.

## Python

*This section applies only to the Python side. A repo with no Python projects still carries it (the file is carried whole) and ignores it.*

The style guide for any Python project(s) in this repo covers the build-versus-lint-only profile split, the uv/ruff/pyright/mypy/pytest toolchain, and `src` layout. It covers formatting and linting, comment and docstring conventions, type hints, naming, imports, and patterns to avoid. It covers test conventions, including the `pytest-cov` dependency and coverage selector a Python directory running pytest owes under `WORKFLOW.md` D1.6. It also covers versioning.

A profile belongs to a declared directory, not to the repository. Each deliverable takes one build-profile directory of its own, except that a uv workspace is declared once, by its root. A repository declaring no Python directory has its root gated only where it tracks a root `pyproject.toml`, as `WORKFLOW.md` D1.2 states. Lint-only Python may sit beside the component it serves. Only a split by purpose alone, within one place, is forbidden, since the file name carries the purpose. One place means directories serving the same component, or serving none.

This is packaged as the `python-codestyle` Skill at `.agents/skills/python-codestyle/SKILL.md` in the hub. That is not a repo-relative link, since the path is hub-local and not carried into every fleet repo. The summary above sketches the scope. Read the skill for the full rules and the profile-adaptation guidance.

### Tests and Script Dependencies

- **A test lives in a `tests/` directory, never inside the production file it tests.** A deployed script may keep a `--selftest` that runs a small smoke check in seconds, and the case matrix lives in `tests/`. A self-test shipped inside the file it tests grows the file every reviewer reads, and its cases can come to outweigh the code they test.
- **A hub-hosted standalone script that needs a third-party library declares it in PEP 723 inline script metadata, and runs as `uv run --no-project --script <path>`.** The block opens on `# /// script`, closes on `# ///`, and carries `dependencies` and `requires-python`. The fleet already installs uv on hosts and in CI. `--no-project` keeps uv from syncing the project around a script that has no block yet, where it would write a lockfile and a virtual environment. A script with no such block runs under `python3` and uv alike. A hook an agent harness runs on every tool call stays standard-library only, since it runs before any toolchain is guaranteed.

### PlexCleaner Python Conventions

The only Python in this repo is the stdlib-only tooling under `RegressionTests/`, and it follows the **lint-only profile**. It has no `uv.lock` and no project or build metadata, and `RegressionTests/pyproject.toml` carries only the ruff and mypy configuration. Its `unittest` suite lives in `RegressionTests/tests/`, and CI runs it under coverage with `uvx coverage@latest run -m unittest discover -s tests`. Run the tools with `uvx` from that directory (`uvx ruff@latest check .`, `uvx ruff@latest format --check .`, `uvx mypy@latest`), or through the `Lint: Ruff`, `Lint: Ruff Format`, and `Lint: Mypy` VS Code tasks. CI runs the same latest tools through the hub's reusable validator.

## Shell

Bash, and only where a program cannot be Python: a bootstrap that installs the interpreter cannot be written in it. Nor can a host tool that must run before a development toolchain exists depend on Python. Everything else is Python, with a test under its own scripts tree's `tests/` directory. The `shell-codestyle` Skill packages the mandatory `set -Eeuo pipefail` header, the pipefail-versus-early-reader pitfall, and self-recursive command shims. It also packages self-locating scripts, the `shellcheck`-plus-`shfmt` clean-compile, and the why-not-what comment rule. It is at `.agents/skills/shell-codestyle/SKILL.md` in the hub, not a repo-relative link since that path is hub-local and not carried into every fleet repo. Read the skill for the full rules. A script's own arguments follow "Command-Line Arguments" under General, which the Skill carries whole. Run the clean-compile check itself per `GOVERNANCE.md` "Running the Linters Locally (Known-Working Invocations)", not by probing `command -v shellcheck`. That section is hub-only, read in a hub checkout rather than carried into every fleet repo.

<!-- Repo -->

[governance]: ./GOVERNANCE.md
[governance-verification-discipline]: ./GOVERNANCE.md#verification-discipline
[readme]: ./README.md
[root]: ./.editorconfig
[tasks]: ./.vscode/tasks.json

<!-- External -->

[google-shell-link]: https://google.github.io/styleguide/shellguide.html
[pep8-names-link]: https://peps.python.org/pep-0008/#package-and-module-names
[ps-approved-verbs-link]: https://learn.microsoft.com/en-us/powershell/scripting/developer/cmdlet/approved-verbs-for-windows-powershell-commands
