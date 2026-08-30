# Python Profile Details

## Adapt before propagating

The rules in `SKILL.md` describe the default Python profile: a package that publishes to PyPI,
type-checked by pyright in strict mode, dependencies in `[dependency-groups]`. A derived repo
often differs, and when it does, adapt these fields to match the repo's actual toolchain rather
than copying verbatim (a verbatim copy that misdescribes the repo is inaccurate and gets rejected
in review). The axes that commonly vary per repo:

- **Type checker in CI**: pyright strict, mypy in CI with pyright editor-only (Pylance), or both.
  Whichever runs in CI is the one the clean-compile and the CI gate invoke.
- **Dependency declaration**: `[dependency-groups]`, or PEP 621 `[project.optional-dependencies]`
  (dev tools installed with `uv sync --extra <group>`).
- **Versioning / publishing**: a published package (`_version.py` plus a version source,
  `uv build`, and a PyPI publish step), or a source-only repo with a static `version` and no
  publish step (see Versioning below).
- **Disabled markdownlint rules**: repo-specific, `.markdownlint-cli2.jsonc` at the repo root is
  the source of truth, not any example rule named here.
- **VS Code config home**: editor settings/extensions may live in `.vscode/*.json` or the
  `<Repo>.code-workspace`, while tasks/launch/debug configs can only be external `.vscode/*.json`
  (they cannot live in the workspace file). The repo's own `tasks.json` sits wherever it keeps it,
  and the canonical task definitions it is written against are the hub `vscode-tasks-python.json`
  snippet, which resolves the same way from every repo.

## Two profiles: full specification

A repo's Python is one of two shapes, declared as the `build` or `lint-only` profile and validated
against the `pyproject.toml` shape. Most of the `SKILL.md` rules (uv project, `uv.lock`, `uv run`,
src layout, pytest coverage) describe the Project shape (the `build` profile). The two differ by
whether the Python has third-party runtime dependencies, which shows up structurally in
`pyproject.toml`, so the fleet's audit reads the shape there:

- **Project** (the `build` profile): the Python has third-party runtime dependencies, or is the
  repo's deliverable. It is a PEP 621 uv project: `[project]` with `dependencies` (dev tools in
  `[project.optional-dependencies]` or `[dependency-groups]`), a `[build-system]`, and a committed
  `uv.lock` (pinned LF, per GOVERNANCE.md's "Line Endings" section). CI runs `uv sync --frozen` +
  `uv run <tool>`, so the lockfile pins tool versions.
- **Scripts** (the `lint-only` profile): stdlib-only utility scripts embedded in a non-Python repo
  (e.g. a Python tooling subtree of a `csharp` app). Run the tools with `uvx` (no project install,
  no lockfile): the `pyproject.toml` carries only tool config (`[tool.ruff]`, `[tool.mypy]`, and
  an optional `[tool.pyright]` editor block), with no `[project]`, no `[build-system]`, and no
  `uv.lock` (that metadata would misrepresent it as a shippable package). mypy is the type-check
  gate (there is no first-party package for pyright strict to anchor on), and a `[tool.pyright]`
  block in standard mode keeps Pylance quiet in the editor, the same mypy-gate/pyright-editor
  split the build profile uses. There is no lockfile, and a `uvx <tool>@<ver>` pin in a `run:`
  step is not something Dependabot tracks, so CI runs `uvx ruff@latest` / `uvx mypy@latest` rather
  than a manual pin that would silently go stale. The fleet rule is to pin only what Dependabot
  auto-updates (SHA-pinned actions, package deps) and otherwise run latest, so the VS Code tasks,
  README, and CI all run the unpinned latest here. `.py` files follow the repo's LF line-ending
  default (per GOVERNANCE.md's "Line Endings" section). There is no pytest suite, and `unittest` is
  the runner instead. A script that carries a gate still earns tests, written with the standard
  library's `unittest` so they run under bare `python3` with nothing installed, as
  `test_<script>.py` under a `tests/` directory beside the scripts it exercises
  (`<scripts-dir>/tests/`), kept apart so a test never reads as a tool. Within the scripts
  directory the name carries the kind: a gate that checks and exits non-zero on a finding takes a
  `_lint` or `_gate` suffix, and a utility that does work takes none. Any repo carrying Python
  carries the Python tooling in CI, coverage included, this profile too: `uvx ruff@latest check`,
  `uvx ruff@latest format --check`, `uvx mypy@latest`, and the unittest suite under
  `uvx coverage@latest run -m unittest discover -s <scripts-dir>/tests` with `coverage report`,
  informational with no threshold adopted. A co-present `csharp` type still carries `codecov.yml`
  for its own tests.

## Versioning

**Published packages.** `_version.py` ships with `__version__ = "0.0.0"` as a placeholder. Until
you wire `_version.py` to something that increments (the usual options are `hatch-vcs`, a
version.json bridge, or manual bumps), no new PyPI versions will land, and publishing with
`skip-existing: true` keeps a stuck placeholder version from failing the run.

**Source-only repos** (no PyPI publish, with a source-release on dispatch or no release at all) do
not need `_version.py`: keep a static `version` in `pyproject.toml` `[project]`, or let the
release pipeline's version source (e.g. NBGV plus `version.json`) own the tag. There is no publish
step to guard, so `skip-existing` does not apply.
