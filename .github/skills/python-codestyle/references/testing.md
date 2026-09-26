# Python Testing Conventions

This covers the **build** profile. A **lint-only** Scripts profile has no `uv.lock` and does not
use pytest, its testing conventions (`unittest`, `uvx coverage@latest run -m unittest discover`)
are in `references/profiles.md`.

Use `pytest` with configuration in `[tool.pytest.ini_options]`. Default invocation:
`uv run pytest`.

**Coverage.** A build-profile repository with tests declares **`pytest-cov`** among its test dependencies, a dev dependency group where the repository is a uv project and a `requirements*.txt` entry where it is on pip, and selects the coverage source in its own `pyproject.toml`, an `addopts` entry of `--cov=<package>` in practice. CI adds `--cov-report=xml` to the invocation, so the repository owes the dependency and the selector rather than that flag. Both halves are load-bearing and they fail differently: without the dependency the CI run exits non-zero on an unrecognized argument, and with the dependency but no selector it measures nothing, writes no file, and exits zero. Leave the report at the repository root as `coverage.xml`, the one path CI names. `WORKFLOW.md` D1.6 owns the pipeline half, the upload and the check that fails when no report was written.

- One test file per module under test, named `test_<module>.py`.
- Test functions named `test_<scenario>_<expected_behavior>`, descriptive and not numbered.
- Use fixtures (defined in `conftest.py` for shared ones, or per-test for narrowly-scoped) instead
  of setup/teardown methods.
- **Avoid mocking when fakes work.** Hand-rolled fakes that implement the protocol you depend on
  are usually clearer and break less than `unittest.mock` magic.
- **Test edge cases that the docstring promises**, not implementation details. If the test breaks
  when you refactor without changing behavior, the test is asserting on an implementation detail.
