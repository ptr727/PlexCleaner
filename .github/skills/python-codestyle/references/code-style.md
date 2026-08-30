# Python Code Style: Full Reference

## Formatting and linting

- **`ruff format` is authoritative.** Don't argue with the formatter, and if it reformats your
  code, that's the final form. Configure (line length, target version) in `pyproject.toml`
  `[tool.ruff]`, not via inline `# fmt:` directives.
- **Run `ruff check --fix` before committing.** Most ruff lint rules have safe autofixes, let the
  tool handle them. The configured rule families are listed under `[tool.ruff.lint]` `select`. Add
  new rule families project-wide rather than scattering inline `# noqa` markers.
- **`# noqa` is a last resort.** When you must use one, scope it narrowly (`# noqa: E501`, not
  bare `# noqa`) and add a short comment on the same line explaining why. False-positive patterns
  that recur across the codebase belong in `[tool.ruff.lint]` `ignore` or per-file
  `[tool.ruff.lint.per-file-ignores]`, with a comment. Porting an existing codebase is not a
  license to add `ignore` / `per-file-ignores` blocks to mute newly surfaced lint. Fix it.

## Comments

- **Inline `#` comments**: keep tight and local. One line is preferred, but multi-line is fine
  when you need to document a non-obvious implementation constraint, a local trade-off, or
  coupling that future edits could easily break. Keep that rationale next to the affected block so
  the reviewer/maintainer sees it at edit-time.
- **Don't explain what the code does.** Well-named identifiers handle that. Don't reference the
  current task ("added for X", "used by Y"), which belongs in the PR description.

## Docstrings

- Follow [PEP 257][pep-0257-link]. Focus docstrings primarily on the behavior contract (what
  callers and tests can rely on), public semantics, and edge-case expectations.
  Implementation-local rationale belongs in inline `#` comments, not docstrings.
- A short one-liner is fine for trivial functions and tests with self-documenting names.
- For non-trivial behavior (non-obvious test scenarios, contracts a test pins, edge cases callers
  must know about, design trade-offs that are load-bearing for future maintainers), write a
  one-line summary, blank line, then a details paragraph. Multi-paragraph docstrings are fine when
  the contract earns it.
- Design notes belong in the code (docstrings or inline comments). They do NOT belong in
  `HISTORY.md`, which is end-user release notes, not a design log.

## Type hints

- **All public APIs are typed.** The repo's configured type checker runs on `src/` (pyright strict
  via `[tool.pyright]` `strict = ["src"]`, or mypy where that is the CI checker), and tests run in
  the checker's looser/standard mode.
- **Use modern syntax**: `list[int]` not `List[int]`, `dict[str, X]` not `Dict[str, X]`,
  `X | None` not `Optional[X]`, `from __future__ import annotations` only when needed for forward
  references.
- **Don't hedge that syntax for an older interpreter.** `pyproject.toml` pins `target-version` /
  `python_version` to 3.13 for every Python profile in this repo, and `spec/host-tools.json`
  carries that as the host floor `scripts/host_gate.py` enforces, so 3.10+-only syntax (`X | None`,
  `match`, etc.) needs no quoting, no `typing.Union` fallback, and no `from __future__ import
  annotations` guard on that account alone. Add that import only when a real forward reference
  needs it, per the bullet above. Three named exceptions carry a lower floor on purpose and say so
  themselves: `scripts/skills_install.sh` and the `install-skills.*` bootstrap scripts, which must
  run on whatever interpreter a host already has before this floor's toolchain exists to install
  one, and `spec/resolve_description.py`, which `repo-config/configure.sh`'s own bootstrap probe
  accepts down to 3.7 for the same reason, and which carries `from __future__ import annotations`
  for exactly that purpose rather than out of habit. No other `spec/` code has a reason to hedge,
  so that import or a quoted annotation appearing anywhere else in `spec/` is a sign this one
  exception got copied rather than a pattern to follow.
- **Don't add `# type: ignore` to silence pyright errors without a comment** explaining the
  constraint. If a recurring false positive needs suppression, configure it project-wide in
  `[tool.pyright]`. A new port doesn't change this, fix freshly surfaced type errors rather than
  muting them.

## Naming

- `snake_case` for functions, methods, variables, modules, package directories.
- `PascalCase` for classes, type aliases, type vars, enum members.
- `UPPER_SNAKE_CASE` for module-level constants.
- Single leading underscore for module-private, double leading underscore for name-mangled (rare,
  and usually means rethink the design).

## Imports

- **Let ruff sort imports.** `[tool.ruff.lint]` `select` includes the `I` rule family
  (isort-equivalent). Don't hand-sort.
- Standard library first, then third-party, then first-party (the project itself), each block
  separated by a blank line, which ruff enforces automatically.
- Avoid wildcard imports (`from x import *`) outside `__init__.py` re-exports.

## Patterns to avoid

- **Don't add backward-compat shims, `# removed` markers, or rename-to-`_` for unused vars**, just
  delete. Git history is the audit trail.
- **Don't add error handling for impossible cases.** Trust internal code, and validate only at
  boundaries (user input, parsed config, external APIs).
- **Don't use exceptions for expected control flow.** Exceptions are for unexpected states.
- **Don't suppress errors silently** (`except Exception: pass`). Either handle the specific
  exception and document why it's safe, or let it propagate.

<!-- External -->

[pep-0257-link]: https://peps.python.org/pep-0257/
