---
name: code-review
description: >-
  Reviews a pull request or change set against the repository's contracts, with explicit diff
  coverage and no suppressed findings. Use this whenever asked to review code, a pull request,
  a patch, or a proposed change, and whenever GitHub Copilot performs code review. Triggers even
  when the diff is documentation-only or workflow-only, because the review must load the
  applicable general, language, documentation, and workflow skills before judging the change.
---

# Code Review

## Establish the Contract

1. Read the root `AGENTS.md` and the sections it routes to for the changed paths.
2. Read the complete diff and enumerate every changed file before forming findings.
3. Load every applicable sibling skill from the current skill distribution:
   - `comment-and-doc-style` for Markdown, prose, comments, commit messages, and PR titles.
   - `dotnet-codestyle` for C# and .NET changes.
   - `python-codestyle` for Python changes.
   - `shell-codestyle` for shell changes.
   - `workflow-ci-contract` for GitHub Actions and CI/CD changes.
4. Treat a missing executable on `PATH` as no evidence that its check is unavailable. Read the
   repository's documented local invocation before reporting a check as skipped.

Do not substitute a familiar convention for the repository's written contract. Report a
conflict between instructions instead of silently choosing one.

## Review the Change

Review for correctness, regressions, security, compatibility, error handling, concurrency,
resource lifetime, tests, and contract drift. Follow data and control flow beyond the edited
lines when the behavior depends on unchanged callers or consumers.

For each candidate finding:

1. Verify it against the current head tree, not an unfetched checkout or the base branch.
2. Identify the concrete failing behavior and the conditions that reach it.
3. Confirm that the repository does not already prevent it elsewhere.
4. Prefer one root-cause finding over several symptoms of the same defect.
5. Omit pure preferences that no repository rule or user-visible risk supports.

Review carried fleet content by intent and fidelity. A byte-locked reference to a path that one
downstream repository does not carry is not a broken link. A substantive defect in canonical
content remains a finding, with the fix located at its canonical source.

## Publish Every Finding

Never suppress or hide a finding because confidence is low. Investigate until it is supported
or discard it. Publish every supported finding as an inline review comment when a changed line
can anchor it. Use the review body only when no valid inline anchor exists.

Each finding states:

- A concise imperative title with a severity.
- The file and smallest useful line range.
- The behavior that fails and the input or state that triggers it.
- Why the change causes the failure.
- A bounded direction for the fix when one is known.

Do not report a clean review until every changed file has been read. End the review body with
exactly one ASCII marker, replacing the numbers with measured counts:

```text
<!-- fleet-review: reviewed=N changed=N findings=N -->
```

`reviewed` is the number of changed files actually reviewed. `changed` is the total number of
changed files. `findings` is the number of published findings, including body-only findings.
Never emit `reviewed=changed` as a placeholder. If full coverage is impossible, emit the actual
counts and explain the limitation in the review body.
