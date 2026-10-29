---
name: fleet-code-review
description: >-
  Reviews a pull request or change set against the repository's contracts, with explicit diff
  coverage and no suppressed findings. Use this whenever asked to review code, a pull request, a
  patch, or a proposed change, and whenever GitHub Copilot performs code review. Triggers even
  when the diff is documentation-only or workflow-only, because the review must load the
  applicable general, language, documentation, and workflow skills before judging the change. This
  skill judges a diff: disposing of a pull request's findings is `pr-review-conduct`, and the
  pre-push pass over this branch is `local-strict-review`, which reuses this skill's criteria.
---

# Fleet Code Review

## Establish the Contract

1. Read the root `AGENTS.md` and the sections it routes to for the changed paths.
2. Read the complete diff and enumerate every changed file before forming findings.
3. Load every applicable sibling skill from the current skill distribution:
   - `comment-and-doc-style` for Markdown, prose, comments, commit messages, and PR titles.
   - `dotnet-codestyle` for C# and .NET changes.
   - `python-codestyle` for Python changes.
   - `shell-codestyle` for shell changes.
   - `workflow-ci-contract` for GitHub Actions and CI/CD changes.
4. Treat a missing executable on `PATH` as no evidence that a check the requester names is
   unavailable. Read the repository's documented local invocation before reporting it as skipped.

Do not substitute a familiar convention for the repository's written contract. Report a
conflict between instructions instead of silently choosing one.

## Review the Change

Review for correctness, regressions, security, compatibility, error handling, concurrency,
resource lifetime, tests, design, and contract drift. Follow data and control flow beyond the edited
lines when the behavior depends on unchanged callers or consumers. Beyond reading files, git state,
and the pull request, run no command the review's requester does not name. Running code is otherwise
left to the author and CI, since even a `--help` run can build environments and start servers that
`git status` misses. A finding that only an unnamed command could prove still counts as supported
once it names that command.

For each candidate finding:

1. Verify it against the current head tree, not an unfetched checkout or the base branch.
2. Identify the concrete failing behavior and the conditions that reach it, or for a design
   finding, what item 7 names.
3. Confirm that the repository does not already prevent it elsewhere.
4. Prefer one root-cause finding over several symptoms of the same defect.
5. Omit pure preferences that no repository rule or user-visible risk supports.
6. Omit a hardening finding that `GOVERNANCE.md` "Trust Boundaries and Hardening Effort",
   carried below, declines.
7. Treat a design finding as backed by `GOVERNANCE.md` "Design Before Code", carried below,
   rather than by a failing behavior. That covers logic an existing helper provides, a per-site
   fix for a defect class seen elsewhere, and code no requirement or realistic threat calls for.
   It names the existing helper, the class's other sites, or the code that answers neither, and
   that backing keeps it out of item 5.

<!-- include: GOVERNANCE.md > Design Before Code -->

A change is cheapest to shape before it is written. Hardening nothing calls for, and a near-duplicate of an existing helper, each cost a review round to find and remove once the code exists. So the design questions are answered before the first edit.

- **Model the threat before the first edit.** Name the change's inputs, the class of each under "Trust Boundaries and Hardening Effort", and the deployment the code runs in. That model sets the hardening budget, so hardening it does not call for is never written, rather than written and then declined in review.
- **Reuse before building.** Search this repository and the hub for a primitive that already does the job. Start from the list of consolidated primitives this repository's `OPERATIONS.md` keeps under `Configuration Layout` once it has consolidated any. Extend one that exists rather than adding a sibling that does almost the same thing. A near-duplicate splits every later fix between two copies.
- **A defect class seen a second time gets one primitive, and every site moves onto it.** A check written at each site repeats its own platform and edge-case defects at every site. A read that can block on a named pipe, for one, meets a new platform difference at each site that guards it alone. One reader that opens without blocking and judges the open descriptor closes that class everywhere. Sites in files a change does not touch move in a change of their own, filed as one issue naming each.
- **Complexity is a cost that needs a reason.** Code that answers no requirement and no realistic threat is removed rather than maintained, since each later review and each later change reads it again.

`GOVERNANCE.md` "Design Before Code" keeps the full rules. The `fleet-code-review` Skill at `.agents/skills/fleet-code-review/SKILL.md` in the hub carries it whole as a generated include and surfaces it wherever a change is reviewed. That path is not a repo-relative link, since it is hub-local and not carried into every fleet repo.

<!-- /include -->

<!-- include: GOVERNANCE.md > Trust Boundaries and Hardening Effort -->

Hardening is spent where a failure can actually occur. A defense against a case that cannot happen still costs a review round, a test, and the upkeep of both. So the effort an input gets is set by where it comes from and what its failure would do.

- **Every input is trusted or untrusted, and the class sets the effort.** Trusted input is the maintainer's and what the maintainer controls. That covers files, images, and data handed to a session, settings and secrets files the maintainer writes, and command-line arguments the maintainer types. It covers this repository's content and the hub's, and the maintainer's own hosts with the output read back from them. It covers state files the repository's own scripts write. Untrusted input arrives from outside. That covers a request from an internet client and every field it controls, and upstream release metadata and downloads. It covers a third-party API response, content from an outside contributor, and a bot-authored pull request.
- **The class follows where data originates, not the channel carrying it.** Any channel holding content an outsider supplied is untrusted, whatever the list above names. That covers an argument or a checked-out tree holding a pull request's content. It covers a host's log line recording an outside request, and a state file caching a third-party response. The same channel carrying the maintainer's input stays trusted.
- **Against trusted input the only threat is a maintainer's mistake.** Guard a realistic mistake, such as a typo, CRLF line endings, a missing file, or a wrong unit. Guard it only where it would fail silently or destructively, with one check at the point of read that says what is wrong. Do not build a parser for adversarial shapes or test injection through a trusted file. Do not sanitize trusted text read back from the maintainer's own hosts against injection, or file an issue for an attack on trusted input. What agent-authored text may quote from those hosts is still "Representative Data in Agent-Authored Text"'s to decide.
- **Untrusted input is validated once, at the boundary where it enters**, in proportion to what it can reach from there.
- **Effort follows likelihood times consequence, for robustness as much as for trust.** Code that fails on its ordinary path in its deployment is a bug rather than a hardening case, and is fixed. A hardening case earns a fix or an issue only where it can realistically occur in the code's actual deployment. Its failure must also be silent or destructive, or be one an outsider can trigger. Every other case is declined with that reason, neither fixed nor filed.
- **A review finding asking for hardening the bullets above rule out is itself declined**, with no fix and no issue. The decline cites this section and the input or deployment that rules the case out. This binds the reviewer as much as the author, so a reviewer does not raise such a finding. A review brief asks after races, coercions, and platform differences only where the code's deployment can produce one.
- **A repository names its own boundaries where the classes above leave them unclear.** Its `OPERATIONS.md` lists which concrete inputs are trusted and which are not, under `Configuration Layout`. A session or a reviewer then applies this section without re-deriving it. A repository naming none uses the classes above.

`GOVERNANCE.md` "Trust Boundaries and Hardening Effort" keeps the full rules. The `fleet-code-review` Skill at `.agents/skills/fleet-code-review/SKILL.md` in the hub carries it whole as a generated include and surfaces it wherever a change is reviewed. That path is not a repo-relative link, since it is hub-local and not carried into every fleet repo.

<!-- /include -->

Review carried fleet content by intent and fidelity. A byte-locked reference to a path that one
downstream repository does not carry is not a broken link. A substantive defect in canonical
content remains a finding, with the fix located at its canonical source.

A finding that a stated count is wrong suggests removing the count where it is incidental, per
`GOVERNANCE.md` "Incidental Counts", rather than correcting the number.

## Publish Every Finding

Never suppress or hide a finding because confidence is low. Investigate until it is supported
or discard it. Publish every supported finding as an inline review comment when a changed line
can anchor it. Use the review body only when no valid inline anchor exists.

Each finding states:

- A concise imperative title with a severity.
- The file and smallest useful line range.
- The behavior that fails and the input or state that triggers it, or for a design finding,
  what "Review the Change" item 7 names.
- Why the change causes the failure, or the duplication or excess.
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
