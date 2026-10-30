---
name: python-codestyle
description: >-
  Governs Python code style for ptr727/ProjectTemplate fleet repos: the build-versus-lint-only
  profile split, the uv/ruff/pyright/mypy/pytest toolchain, src layout, formatting and linting,
  comment and docstring conventions, type hints, naming, imports, patterns to avoid, test
  conventions, versioning, and the --help and unknown-argument rule for a script. Use this
  whenever writing, reviewing, or editing a .py file, a pyproject.toml, or a uv.lock, whenever
  running or choosing a Python formatting, lint, type-check, or test command, whenever deciding
  whether a Python subtree is a shippable project or a lint-only scripts tree, whenever choosing
  pyright versus mypy for a repo's CI gate, or whenever writing or reviewing a Python test.
  Triggers even when the task looks like a small local fix ("just add a helper function", "silence
  this lint warning", "add a dependency") or verification step ("run the tests"), because choosing
  pytest before reading the profile turns an intentional unittest suite into a false
  missing-dependency diagnosis. Applies only to a repo's Python side, a repo with no Python has no
  use for this Skill.
---

# Python Codestyle

## Why this exists

This is the Python-specific half of the fleet's code style guide, kept in one place instead of
re-derived per repo or per session. CODESTYLE.md's General section still owns the rules every
language shares (clean-compile verification as a concept, the suppression-scope order, tooling
casing in prose). This Skill is everything specific to a Python project on top of that: the two
profiles, the toolchain, layout, and the language-level conventions.

## Two profiles

Read the repo's `OPERATIONS.md` local-verification commands before substituting a generic command.
Then read each Python directory's `pyproject.toml` shape and pick its profile before running Python
tooling or tests:

- **build** (Project): third-party runtime dependencies, or a deliverable. Either a uv
  project (`[project]` + `[build-system]` + committed `uv.lock`, run with `uv run`) or a
  `pyproject.toml` beside a `requirements*.txt` and no `uv.lock`, installed with pip. Uses pytest,
  and pyright strict or mypy with its strict flags as the CI type checker. A repo enforcing
  pyright beside mypy runs pyright itself, per Toolchain below.
- **lint-only** (Scripts): no `[project]`, no `[build-system]`, no lockfile, no `requirements*.txt`
  (the hub validator runs pytest wherever one sits). Uses `uvx` for third-party tools, unittest
  for tests, and mypy as the CI gate. Do not run pytest or diagnose its absence as an environment
  defect. Use the repository's exact coverage command and unittest scope from `OPERATIONS.md`.

A profile belongs to a declared directory, not to the repository. Each deliverable takes a
build-profile directory of its own. The profile decides only how the tools are installed, never
what is owed. Every Python directory owes lint, format, a type check, a `tests/` suite, and
coverage. A repo declares each in the hub validator's `python-directories` input, except that a uv
workspace is declared once, by its root. A repo declaring none has its root gated only where it
tracks a root `pyproject.toml`, as `WORKFLOW.md` D1.2 states.

`references/profiles.md` holds the full profile specification and the rule for splitting Python
across directories. It also holds the toolchain axes a directory adapts: type checker, dependency
declaration, and versioning.

## Toolchain

| Tool | Role | Config |
|---|---|---|
| [uv][uv-link] | env, deps, build, publish (build/publish only where the repo ships a package) | `pyproject.toml` `[dependency-groups]` or `[project.optional-dependencies]`, `uv.lock` |
| [hatchling][latest-link] | build backend (published packages) | `pyproject.toml` `[build-system]` |
| [ruff][ruff-link] | lint + format + import sort | `pyproject.toml` `[tool.ruff]` |
| [pyright][pyright-link] | type checker (the default, a strict baseline) | `pyproject.toml` `[tool.pyright]` |
| [mypy][mypy-link] | additional/alternate type checker (optional, the CI checker in a mypy-in-CI repo, required for Home Assistant) | `pyproject.toml` `[tool.mypy]` (or per home-assistant/core) |
| [pytest][docs-link] | test runner (build profile only, lint-only uses `unittest`) | `pyproject.toml` `[tool.pytest.ini_options]` |

**Type checking targets strongly typed, deterministic code.** pyright in strict mode is the
default baseline on first-party code. That baseline is `[tool.pyright]` `strict = ["src"]`, or the
integration package for a Home Assistant repo, with tests run in standard mode. A repo may instead
run mypy with its strict flags in CI and keep pyright editor-only via Pylance, per the next
paragraph. pyright is the anchor because Pylance embeds it, so where CI runs pyright, the
editor and the CLI/CI run the same engine. A pyright the requirements pin can lag the one Pylance
bundles, though. The CLI/CI form is `uv run pyright` in the uv form. In the pip form a declared
directory runs the environment's own pyright, or `uvx pyright@latest` pointed at the environment
where it installs none.
The standalone `ms-pyright.pyright` extension stays in
`unwantedRecommendations` because Pylance covers it. Relax strictness on third-party code only
when a dependency has no usable types and no alternative (e.g. `pandas`). The relaxation is a
targeted, commented `# pyright: ignore[...]` or a scoped `[tool.pyright]` override, never a blanket
one.

**mypy is allowed, and required where the ecosystem demands it, it is not banned.** Running more
than one checker is normal when each serves a purpose (the .NET side pairs CSharpier and
`dotnet format` the same way). pyright's inference and mypy's plugin ecosystem (e.g.
`pydantic.mypy`) catch different classes of error. A Home Assistant integration runs
`mypy --strict` because the platinum `strict-typing` quality-scale tier requires it, and a
pydantic-heavy library may opt in for the plugin. When a repo uses mypy it runs in CI and the
editor (the `ms-python.mypy-type-checker` extension) so the two stay consistent. That repo's mypy
command joins the clean-compile. The hub validator runs at most one checker in each Python
directory, mypy where both are configured. A repo enforcing pyright beside mypy runs pyright from
its own `.github/actions/validate/action.yml` hook. That hook starts from a bare checkout, so it
sets up its own Python environment. mypy may also be a build directory's only CI checker, run with
its strict flags. Pylance's pyright diagnostics are then advisory, since CI never runs them. A
pyright-only repo is the lightest and is inherently consistent, since the editor and CI run one
engine.

## Local development loop

From inside a **build**-profile Python project directory in its uv form. A pip-form directory runs
neither `uv sync` nor `uv run`, since either writes a `uv.lock` that, once committed, makes the
directory the uv form. A pip-form directory builds its environment as the pip-form block below
shows. A
**lint-only** Scripts profile has no `uv.lock` to sync and no pytest to run. For it, substitute
`uvx` per tool and `unittest` per the Two Profiles section above:

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

The **build**-profile Python clean-compile, in its uv form, is `uv run ruff format` +
`uv run ruff check` + the repo's type checker. That checker is `uv run pyright`, or `uv run mypy`
where mypy is the CI checker, or both where the repo runs both (see Type checking above). Where the
config sits in the project directory, CI passes the checker no path, so the config's own target
settings decide what is checked. mypy left with no target that way exits with an error. A declared
subdirectory with no config of its own uses the repository root's instead. CI then runs the checker
from the root with the directory as its path, `uv run --project <dir> <checker> <dir>`, so run it
the same way. Run the clean-compile, plus `uv run pytest`, before committing.

A **build**-profile directory in its pip form builds its environment the way CI does, through uv's
pip interface, which writes no `uv.lock`. It installs every `requirements*.txt` in one command, since
separate installs let one file's pins downgrade what another resolved. Where its `pyproject.toml`
carries a `[project]` table it then installs the directory itself as editable. Without that, a
`src`-layout package is not importable and pytest fails locally where CI passes:

```sh
uv venv --clear                           # creates .venv, replacing an existing one
uv pip install -r requirements.txt -r requirements-dev.txt   # one -r per requirements*.txt
uv pip install -e .                       # only where pyproject.toml has a [project] table
# in a declared directory whose .venv installs ruff, .venv/bin/python -m ruff replaces uvx ruff@latest
uvx ruff@latest format                    # auto-format
uvx ruff@latest check --fix               # auto-fix lint
uvx ruff@latest check                     # verify lint clean
uvx ruff@latest format --check            # verify format clean
.venv/bin/python -m pytest                # run tests
```

Its type checker runs against that environment. mypy runs as `.venv/bin/python -m mypy` where the
environment installs it, and otherwise as `uvx mypy@latest --python-executable .venv/bin/python`,
adding `--python-version` with the environment's version unless the mypy config pins one. pyright
runs as `.venv/bin/python -m pyright --pythonpath .venv/bin/python` where the environment installs
it, and otherwise as `uvx pyright@latest --pythonpath .venv/bin/python`. ruff follows the same
precedence, as `.venv/bin/python -m ruff` where the environment installs it, in place of the
`uvx ruff@latest` above. In a directory the validator's `python-directories` input declares, a tool
the requirements pin is then the version CI runs. Where the config
sits in the project directory, CI runs the checker there with no path, as in the uv form. A
declared subdirectory with no config of its own uses the repository root's instead. CI then runs
the checker from the root with the directory as its path and the interpreter as `<dir>/.venv/bin/python`. Run it the same
way, as in `<dir>/.venv/bin/python -m mypy <dir>`. On Windows each interpreter path ends in
`.venv\Scripts\python.exe` instead. Those ruff commands and that type checker are the pip form's
clean-compile, run with pytest before committing.

A **lint-only** profile's clean-compile substitutes its `uvx` and `unittest` equivalents, per Two
Profiles above, and has no such command to run before committing beyond those. These are documented
commands. The hub's `vscode-tasks-python.json` snippet carries the VS Code tasks mirror of the uv
form that the fleet baseline expects. Its header names the pip-form and lint-only adaptations. Every command-executing task in it is `type: process`, and every aggregator is
`dependsOn`-only. Neither chains with `&&`, so the mirror runs the same on any task shell.

CI runs these same commands as the authoritative backstop in every directory the hub validator's
`python-directories` input declares. A repo declaring none has its root gated only where it tracks
a root `pyproject.toml`, per `WORKFLOW.md` D1.2. That undeclared root is gated more loosely. CI
reads its type-checker configuration only from the `[tool.mypy]` or `[tool.pyright]` section of its
`pyproject.toml`. It skips the type check where neither is there,
whatever the form. In its pip form CI installs nothing for that check and runs a bare
`uvx <checker>@latest`. So the local check against the environment is the stricter one. And CI runs
its tests only where a pytest suite in `tests/` sits beside a `uv.lock` or `requirements*.txt`.

A repo that keeps no .NET tool manifest declaring Husky.Net wires its local hook from the canonical
`catalog/snippets/pre-commit/` directory. So does one that prefers the `pre-commit` framework. That
directory is hub-local and not carried into every fleet repo. Any repo may instead wire an equivalent
hook of its own at `.husky/pre-commit`, enabled with `core.hooksPath` and sourcing nothing. That
path and `.pre-commit-config.yaml` are the ones the audit reads. The runner is bounded by the
toolchain the repo already keeps rather than by the languages the hook checks. So a repo keeping a
Husky.Net manifest may run these same Python checks from the `catalog/snippets/husky/` shape
instead. Each shape carries whichever language checks its own repo keeps. The `pre-commit`
directory's own README names the second file to copy alongside the config. GOVERNANCE.md's hub-only
"Running the Linters Locally (Known-Working Invocations)" section carries the obligation itself,
what the hook must cover, its audit treatment, and the per-clone enablement steps.

A restricted executor gives each task a cache directory under a writable temporary root. Point
`UV_CACHE_DIR`, `RUFF_CACHE_DIR`, `MYPY_CACHE_DIR`, and `COVERAGE_FILE` into that directory before
running the applicable tools. This keeps their generated state outside both the home directory
and the checkout. Do not change `HOME` or an agent configuration directory. A denied network
request means the tool did not run, so preserve the denial and rerun through the executor's scoped
approval mechanism.

## Layout

`src` layout, which keeps the package out of the repo root and prevents accidental imports of
unbuilt code:

```text
<python-project>/
    pyproject.toml
    README.md
    uv.lock                # uv form, committed for reproducible CI
    requirements*.txt      # pip form, in place of uv.lock
    src/
        <package_name>/
            __init__.py
            _version.py        # published packages; a source-only repo uses a static version instead
            <modules>.py
    tests/
        __init__.py
        test_<module>.py
```

## Code style

Key rules for every Python task:

- **`ruff format` is authoritative.** Don't argue with the formatter. Configure in `pyproject.toml`
  `[tool.ruff]`, not via inline `# fmt:` directives.
- **Run `ruff check --fix` before committing.** The configured rule families are in
  `[tool.ruff.lint]` `select`. Add new rule families project-wide, not scattered inline `# noqa`.
- **`# noqa` is a last resort.** Scope it narrowly (`# noqa: E501`) with a comment. Recurring
  false positives belong in `[tool.ruff.lint]` `ignore` or `per-file-ignores`.
- **All public APIs are typed.** Use modern syntax (`list[int]`, `X | None`). Don't add
  `# type: ignore` without an explaining comment.
- **Don't add backward-compat shims.** Just delete unused code. Git history is the audit trail.
- **Don't add error handling for impossible cases.** Trust internal code. Validate only at boundaries.

For comments, docstrings, full type-hint rules, naming, imports, and all patterns to avoid, see
`references/code-style.md`.

## Tests

`uv run pytest` for a build profile (`.venv/bin/python -m pytest` in its pip form, from the
environment the Local development loop builds). `unittest` for a lint-only Scripts profile (see Two
Profiles above). One test file per module (`test_<module>.py`). A build profile prefers fixtures
over `unittest`'s `setUp`/`tearDown` lifecycle hooks. A lint-only profile uses those hooks directly,
since `unittest` has no fixture-injection mechanism of its own. Fakes over mocks either way. Test
the docstring's contract, not implementation details. See `references/testing.md` for the full
build-profile conventions, and `references/profiles.md` for the lint-only `unittest` conventions.

## Tests and Script Dependencies

<!-- include: CODESTYLE.md > Tests and Script Dependencies -->

- **A test lives in a `tests/` directory, never inside the production file it tests.** A deployed script may keep a `--selftest` that runs a small smoke check in seconds, and the case matrix lives in `tests/`. A self-test shipped inside the file it tests grows the file every reviewer reads, and its cases can come to outweigh the code they test.
- **A hub-hosted standalone script that needs a third-party library declares it in PEP 723 inline script metadata, and runs as `uv run --no-project --script <path>`.** The block opens on `# /// script`, closes on `# ///`, and carries `dependencies` and `requires-python`. The fleet already installs uv on hosts and in CI. `--no-project` keeps uv from syncing the project around a script that has no block yet, where it would write a lockfile and a virtual environment. A script with no such block runs under `python3` and uv alike. A hook an agent harness runs on every tool call stays standard-library only, since it runs before any toolchain is guaranteed.

<!-- /include -->

## Versioning

Published packages use `_version.py` with `__version__ = "0.0.0"` as a placeholder. Wire
`hatch-vcs` or equivalent to increment, publish with `skip-existing: true`. Source-only repos use
a static `version` in `[project]` with no `_version.py`. See `references/profiles.md` for details.

## Linter cleanliness

Before pushing or opening a PR:

- VS Code's Problems pane should be quiet for the files you touched. The relevant linters are ruff
  (via the `charliermarsh.ruff` extension) and pyright (via the `ms-python.python` extension's
  bundled Pylance).
- The **build**-profile CI gate, in its uv form, runs the local loop's commands above, each from
  where that loop runs it. Those are `uv run ruff check`, `uv run ruff format --check`, the repo's
  type checker, and `uv run pytest`. CI invokes them as separate steps, not `&&`-chained, so the
  runner shell is irrelevant. Per `references/profiles.md`, the pip form's CI gate is its commands
  in the local loop above. Per the same file, a **lint-only** profile's is its `uvx` equivalents
  plus its `unittest` suite. The local loop names
  what CI relaxes for an undeclared root.
- Markdown in this directory follows CODESTYLE.md's repo-wide Markdown and Spelling rules,
  packaged as the `comment-and-doc-style` Skill.

## Parsing Structured Formats

<!-- include: CODESTYLE.md > Parsing Structured Formats -->

- **A program reading a structured format uses a maintained parser rather than a hand-written one, where a field-tested parser exists for the runtime in use.** That covers YAML, Markdown, TOML, JSON, a programming language's comments or strings, and a shell command line. The standard library's own parsers count, such as Python's `json`, `tomllib`, `ast`, `tokenize`, and `html.parser`. A hand-written reader converges on the format's full grammar one review finding at a time, and every script that grows one pays that cost again.

<!-- /include -->

## Command-Line Arguments

<!-- include: CODESTYLE.md > Command-Line Arguments -->

A command-line script is one a person or an agent runs by name. A hook its host runs, taking its input from that host rather than from a command line, is not one.

- **Every command-line script answers `--help` and `-h`** with the same text on stdout, then exits 0, changing nothing. That text opens on a usage line naming the script. An agent learns a script's interface by asking it, so a script that reads `--help` as an input acts in a way nobody predicted.
- **An argument the script does not recognize is refused**, with a non-zero exit before any read or write. The refusal names it as an unknown or unrecognized option, argument, flag, or parameter, and PowerShell's own refusal of a parameter it cannot bind counts. An unrecognized first argument is never taken as a default mode or as a target.

<!-- /include -->

<!-- External -->

[docs-link]: https://docs.pytest.org/
[latest-link]: https://hatch.pypa.io/latest/
[mypy-link]: https://mypy-lang.org/
[pyright-link]: https://microsoft.github.io/pyright/
[ruff-link]: https://docs.astral.sh/ruff/
[uv-link]: https://docs.astral.sh/uv/
