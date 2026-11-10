# Python Profile Details

## Adapt before propagating

The rules in `SKILL.md` describe the default Python toolchain: a package that publishes to PyPI,
type-checked by pyright in strict mode, dependencies in `[dependency-groups]`. A directory in a
derived repo often differs, so adapt these fields to its actual toolchain rather than copying them
verbatim. A verbatim copy that misdescribes the directory is inaccurate and gets rejected in
review. The axes that commonly vary:

- **Type checker in CI**: pyright strict, mypy with its strict flags (run in CI and the editor, with
  pyright kept editor-only through Pylance), or both. A repo running both runs pyright itself,
  per `SKILL.md` "Toolchain". The clean-compile runs every checker CI runs.
- **Dependency declaration**: the uv form declares the dev tools CI runs in the `dev` group of
  `[dependency-groups]`. The `dev` group is the one group a plain local `uv sync` or `uv run`
  installs. CI's
  `uv sync --all-groups --frozen` installs that group too, since the command takes every group and
  no extra. PEP 621
  `[project.optional-dependencies]` (installed with `uv sync --extra <group>`) suits only a tool CI
  does not run. The pip form declares its dependencies in `requirements*.txt` files, installed
  together in one resolve, per `SKILL.md` "Local development loop".
- **Versioning / publishing**: a published package or a source-only repo (see Versioning below).
  A published package has `_version.py` plus a version source, `uv build`, and a PyPI publish step.
  A source-only repo has a static `version` and no publish step.

## Two profiles: full specification

A directory's Python is one of two shapes, declared as the `build` or `lint-only` profile and
validated against that directory's structural shape. Most of the `SKILL.md` rules (src layout,
pytest coverage) describe the Project shape (the `build` profile). Its uv project, `uv.lock`, and
`uv run` rules describe that shape's uv form. The two differ by trait, whether the Python has
third-party runtime dependencies or is a deliverable. The fleet's audit reads the shape
that trait leaves rather than inspecting imports. A `[project]` or `[build-system]` table in
`pyproject.toml`, a committed `uv.lock`, or a `requirements*.txt` beside it marks the build profile.
Tool configuration alone marks the lint-only one:

- **Project** (the `build` profile): the Python has third-party runtime dependencies, or is a
  deliverable. It takes one of these forms. The uv form is a PEP 621 project: `[project]`
  with `dependencies`, a `[build-system]`, and a committed `uv.lock`. Its dev tools sit in
  `[dependency-groups]`, per the dependency-declaration axis above. The `uv.lock` is pinned LF, per
  GOVERNANCE.md's "Line Endings" section. CI runs `uv sync --all-groups --frozen` +
  `uv run <tool>`, so the lockfile pins tool versions. That sync installs every `[dependency-groups]` group and no
  `[project.optional-dependencies]` extra. The pip form is a `pyproject.toml` beside a
  `requirements*.txt`, installed with pip, whether or not it carries a `[project]` table. A
  committed `uv.lock` makes a directory the uv form even where a `requirements*.txt` sits beside it.
  In the pip form CI builds the environment as `SKILL.md` "Local development loop" shows, and runs
  pytest from it as `.venv/bin/python -m pytest`. A directory declared in the hub validator's
  `python-directories` input runs ruff and the type checker from that environment where it
  installs them. It otherwise runs them through `uvx`, with the checker pointed at that
  environment. The undeclared default root runs ruff through `uvx` at the pinned version the
  lint-only profile below names, and a bare `uvx <checker>@latest`, with nothing installed.
- **Scripts** (the `lint-only` profile): stdlib-only utility scripts (e.g. a
  Python tooling subtree of a `csharp` app). Run the tools with `uvx` (no project install,
  no lockfile). The `pyproject.toml` carries only tool config (`[tool.ruff]`, `[tool.mypy]`, and
  an optional `[tool.pyright]` editor block). There is no `[project]`, no `[build-system]`, and no
  `uv.lock` (that metadata would misrepresent it as a shippable package). mypy is the type-check
  gate (there is no first-party package for pyright strict to anchor on). A `[tool.pyright]`
  block in standard mode keeps Pylance quiet in the editor, the same mypy-gate/pyright-editor
  split the build profile uses. There is no lockfile, and a `uvx <tool>@<ver>` literal in a `run:`
  step is not something Dependabot tracks. The fleet rule is to pin only what Dependabot
  auto-updates (SHA-pinned actions, package deps) and otherwise run latest. A manual pin would
  silently go stale. So CI runs ruff at the version the hub validator's
  `python-tool-pins` action reads from the `requirements.txt` beside it, which the hub's
  Dependabot pip ecosystem bumps. A caller reaches that version at the hub commit its workflow
  pins, so a ruff release reaches its lint only through a hub release. CI runs `uvx mypy@latest`,
  and a carrier's VS Code tasks and README run the unpinned latest of every tool. The hub's own
  tasks and hook take that `requirements.txt` as a requirement instead. `.py` files follow the
  repo's LF line-ending default (per GOVERNANCE.md's "Line Endings" section). There is no pytest suite, and `unittest` is
  the runner instead. The directory still owes tests, written with the standard
  library's `unittest` so they need no test runner installed. Each is a `test_<script>.py`
  under a `tests/` directory beside the scripts it exercises and their `pyproject.toml`
  (`<scripts-dir>/tests/`), where the validator looks for it. That directory stays apart so a
  test never reads as a tool. Within the scripts directory the name carries the kind: a gate
  that checks and exits non-zero on a finding takes a `_lint` or `_gate` suffix. A utility there
  that does work takes none. Every declared directory
  owes the same gates whatever its profile: lint, format, a type check, a test suite, and a
  coverage report to Codecov. The hub validator runs them in each directory the caller declares
  in its `python-directories` input, which the registry's `pythonDirectories` mirrors. From inside
  a lint-only directory it runs `uvx ruff@<pin> check`, `uvx ruff@<pin> format --check`,
  `uvx mypy@latest`, and `uvx coverage@latest run -m unittest discover -s tests`. It adds
  `--with-requirements tests/requirements.txt` to that last where the directory carries one. It then
  writes `coverage.xml` and uploads it best-effort per `WORKFLOW.md` D1.6, with no threshold
  adopted. A
  declared directory with no `tests/` fails that job.

A profile belongs to a declared directory, not to the repository. A repository declaring none has
its root gated only where it tracks a root `pyproject.toml`, as `WORKFLOW.md` D1.2 states. Each
deliverable takes one build-profile directory of
its own, with its own `uv.lock` or `requirements*.txt`. A uv workspace is the exception: it is
declared once, by its root, and its members share the root's lock. Lint-only Python may sit beside
the component it serves. Only a split by purpose alone, within one place, is forbidden. One place
means directories serving the same component, or serving none. Within one directory the file name
carries the purpose instead, as the Scripts bullet's `_gate` and `_lint` suffixes do. A stdlib-only
`tools/` beside a `render/` that imports a third-party imaging library is a legitimate split by
profile trait. Merging them would put every stdlib gate behind that library's install. Two
deliverables, `render/` and `publish/`, each keep a build-profile directory of their own. Lint-only
Python serving an `infra/` component may sit in an `infra-checks/` directory beside it, declared
apart from `tools/`. Two stdlib-only root directories, `tools/` and `audits/`, serve no component of
their own and differ only by purpose, so they merge.

## Versioning

**Published packages.** `_version.py` ships with `__version__ = "0.0.0"` as a placeholder. Until
you wire `_version.py` to something that increments (the usual options are `hatch-vcs`, a
version.json bridge, or manual bumps), no new PyPI versions land. Publishing with
`skip-existing: true` keeps a stuck placeholder version from failing the run.

**Source-only repos** (no PyPI publish, with a source-release on dispatch or no release at all) do
not need `_version.py`. Keep a static `version` in `pyproject.toml` `[project]`, or let the
release pipeline's version source (e.g. NBGV plus `version.json`) own the tag. There is no publish
step to guard, so `skip-existing` does not apply.
