---
name: python-codestyle
description: >-
  Governs Python code style for ptr727/ProjectTemplate fleet repos: the build-versus-lint-only
  profile split, the uv/ruff/pyright/mypy/pytest toolchain, src layout, formatting and linting,
  comment and docstring conventions, type hints, naming, imports, patterns to avoid, test
  conventions, and versioning. Use this whenever writing, reviewing, or editing a .py file, a
  pyproject.toml, or a uv.lock, whenever running or choosing a Python formatting, lint, type-check,
  or test command, whenever deciding whether a Python subtree is a shippable project or a lint-only
  scripts tree, whenever choosing pyright versus mypy for a repo's CI gate, or whenever writing or
  reviewing a Python test. Triggers even when the task looks like a small local fix ("just add a
  helper function", "silence this lint warning", "add a dependency") or verification step ("run
  the tests"), because choosing pytest before reading the profile turns an intentional unittest
  suite into a false missing-dependency diagnosis. Applies only to a repo's Python side, a repo
  with no Python has no use for this Skill.
---

# Python Codestyle

## Why this exists

This is the Python-specific half of the fleet's code style guide, kept in one place instead of
re-derived per repo or per session. CODESTYLE.md's General section still owns the rules every
language shares (clean-compile verification as a concept, the suppression-scope order, tooling
casing in prose), this Skill is everything specific to a Python project on top of that: the two
profiles, the toolchain, layout, and the language-level conventions.

## Two profiles

Read the repo's `OPERATIONS.md` local-verification commands before substituting a generic command.
Then read the `pyproject.toml` shape and pick the profile before running Python tooling or tests:

- **build** (Project): `[project]` + `[build-system]` + committed `uv.lock`. Uses `uv run`, pytest,
  pyright strict (or mypy where the repo requires it).
- **lint-only** (Scripts): no `[project]`, no lockfile. Uses `uvx` for third-party tools, unittest
  for tests, and mypy as the CI gate. Do not run pytest or diagnose its absence as an environment
  defect. Use the repository's exact coverage command and unittest scope from `OPERATIONS.md`.

For the full profile specification and per-repo adaptation axes (type checker, dependency
declaration, versioning, VS Code config), see `references/profiles.md`.

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
default baseline on first-party code (a repo may instead run mypy in CI and keep pyright
editor-only via Pylance, per the next paragraph): `[tool.pyright]` `strict = ["src"]`, or the
integration package for a Home Assistant repo, with tests run in standard mode. pyright is the
anchor because Pylance embeds it, so the editor and the CLI/CI (`uv run pyright`) run the same
engine and never disagree. The standalone `ms-pyright.pyright` extension stays in
`unwantedRecommendations` because Pylance covers it. Relax strictness on third-party code only
when a dependency has no usable types and no alternative (e.g. `pandas`): a targeted, commented
`# pyright: ignore[...]` or a scoped `[tool.pyright]` override, never a blanket relaxation.

**mypy is allowed, and required where the ecosystem demands it, it is not banned.** Running more
than one checker is normal when each serves a purpose (the .NET side pairs CSharpier and
`dotnet format` the same way), and pyright's inference and mypy's plugin ecosystem (e.g.
`pydantic.mypy`) catch different classes of error. A Home Assistant integration runs
`mypy --strict` because the platinum `strict-typing` quality-scale tier requires it, and a
pydantic-heavy library may opt in for the plugin. When a repo uses mypy it runs in CI and the
editor (the `ms-python.mypy-type-checker` extension) so the two stay consistent, and its mypy
command joins the clean-compile. A repo with no such need stays pyright-only, which is lighter and
inherently consistent.

## Local development loop

From inside a **build**-profile Python project directory. A **lint-only** Scripts profile has no
`uv.lock` to sync and no pytest to run, substitute `uvx` per tool and `unittest` per the Two
Profiles section above:

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

The **build**-profile Python clean-compile is `uv run ruff format` + `uv run ruff check` + the
repo's type checker: `uv run pyright`, or `uv run mypy src` where mypy is the CI checker, or both
where the repo runs both (see Type checking above). Run it, plus `uv run pytest`, before
committing. A **lint-only** profile's clean-compile substitutes its `uvx` and `unittest`
equivalents, per Two Profiles above, and has no such command to run before committing beyond
those. These are documented commands, and an optional VS Code tasks mirror (all `type: process`,
no `&&` shell chaining, so it runs the same on any task shell) is in the hub
`vscode-tasks-python.json` snippet. CI runs the same clean-compile commands as the authoritative
backstop. A working local hook is strongly suggested, not opt-in: wire the Python `pre-commit`
framework from the canonical `catalog/snippets/pre-commit/.pre-commit-config.yaml`. See
GOVERNANCE.md "Running the Linters Locally" for what the hook must cover and what its absence
means.

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

`uv run pytest` for a build profile, `unittest` for a lint-only Scripts profile (see Two Profiles
above). One test file per module (`test_<module>.py`). A build profile prefers fixtures over
`unittest`'s `setUp`/`tearDown` lifecycle hooks. A lint-only profile uses those hooks directly,
since `unittest` has no fixture-injection mechanism of its own. Fakes over mocks either way. Test
the docstring's contract, not implementation details. See `references/testing.md` for the full
build-profile conventions, and `references/profiles.md` for the lint-only `unittest` conventions.

## Versioning

Published packages use `_version.py` with `__version__ = "0.0.0"` as a placeholder. Wire
`hatch-vcs` or equivalent to increment, publish with `skip-existing: true`. Source-only repos use
a static `version` in `[project]` with no `_version.py`. See `references/profiles.md` for details.

## Linter cleanliness

Before pushing or opening a PR:

- VS Code's Problems pane should be quiet for the files you touched. The relevant linters are ruff
  (via the `charliermarsh.ruff` extension) and pyright (via the `ms-python.python` extension's
  bundled Pylance).
- The **build**-profile CI gate is `uv run ruff check`, `uv run ruff format --check`, the repo's
  type checker (`uv run pyright` or `uv run mypy src`), and `uv run pytest`, the same commands as
  the local loop above, run from the Python project directory (invoked as separate steps, not
  `&&`-chained, so the runner shell is irrelevant). A **lint-only** profile's CI gate is its `uvx`
  equivalents plus its `unittest` suite, per `references/profiles.md`.
- Markdown in this directory follows CODESTYLE.md's repo-wide Markdown and Spelling rules,
  packaged as the `comment-and-doc-style` Skill.

<!-- External -->

[docs-link]: https://docs.pytest.org/
[latest-link]: https://hatch.pypa.io/latest/
[mypy-link]: https://mypy-lang.org/
[pyright-link]: https://microsoft.github.io/pyright/
[ruff-link]: https://docs.astral.sh/ruff/
[uv-link]: https://docs.astral.sh/uv/
