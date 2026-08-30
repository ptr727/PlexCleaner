# Python Testing Conventions

This covers the **build** profile. A **lint-only** Scripts profile has no `uv.lock` and does not
use pytest, its testing conventions (`unittest`, `uvx coverage@latest run -m unittest discover`)
are in `references/profiles.md`.

Use `pytest` with configuration in `[tool.pytest.ini_options]`. Default invocation:
`uv run pytest`.

- One test file per module under test, named `test_<module>.py`.
- Test functions named `test_<scenario>_<expected_behavior>`, descriptive and not numbered.
- Use fixtures (defined in `conftest.py` for shared ones, or per-test for narrowly-scoped) instead
  of setup/teardown methods.
- **Avoid mocking when fakes work.** Hand-rolled fakes that implement the protocol you depend on
  are usually clearer and break less than `unittest.mock` magic.
- **Test edge cases that the docstring promises**, not implementation details. If the test breaks
  when you refactor without changing behavior, the test is asserting on an implementation detail.
