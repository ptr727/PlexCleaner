---
name: pr-review-conduct
description: >-
  Governs opening, driving, and merging a pull request review loop in a ptr727/ProjectTemplate
  fleet repo: requesting a review after a push, triaging findings (including suppressed
  low-confidence ones), replying and resolving threads, and deciding whether a PR is actually
  mergeable. Use this whenever about to open a PR, immediately after creating one, about to merge
  a PR, enable auto-merge, ask the maintainer for merge permission, push a fix and move on without
  re-checking review state, or judge a PR "green" or "clean" from CI or mergeStateStatus alone.
  Triggers even when the request sounds routine, such as "open a PR," "merge this," or "it's all
  green, go ahead," because PR creation starts the review loop and mergeStateStatus: CLEAN
  can go clean once checks pass and every known thread is resolved, while still saying nothing
  about whether the review that resolved those threads covered the current head SHA, read the
  full diff, or left a suppressed low-confidence finding, which opens no thread at all,
  unanswered. Also triggers when a review loop looks stuck
  (no review landing, findings that keep reappearing) or when deciding a finding is real, false,
  deferred, or a deliberate decline. Provider-specific mechanics are implemented by
  scripts/pr_review.py and bootstrapped by .github/copilot-instructions.md. This skill is the
  contract those surfaces implement, not a replacement for them.
---

# PR Review Conduct

## Why this exists

`mergeStateStatus: CLEAN` reflects required status checks and any review thread the ruleset's
conversation-resolution requirement already tracks as resolved. It says nothing about whether the
review that resolved those threads actually covered the **current** head SHA, whether it read the
full diff rather than part of it, or whether a suppressed low-confidence finding, which never
opens a thread for the ruleset to see, was ever answered. A PR that looks done, green checks, no
visible comments, routinely still carries a finding nobody has answered. Treating "green" as
"mergeable" is the single most common way this loop gets skipped.

## Merge Gate, check this before merging or enabling auto-merge

**Do not merge, and do not enable auto-merge, unless ALL of these hold:**

1. Required status checks are green, and where they are not, the reason is **read**, never
   inferred. `BLOCKED` covers a failed check, a required check nothing is running, an unresolved
   thread, and a missing approval alike, and the response differs by cause.
2. A review is confirmed on the **current head SHA**, matched by commit SHA rather than assumed
   from a green merge-state. A push makes checks go green *before* the re-review lands, and the
   matched review is **read**, not just counted. A review can carry the head SHA and still decline
   the PR outright, or say it read only part of the changed files. `pr_review.py`'s
   `review_on_head` names Copilot's own coverage specifically, the currently required reviewer,
   not "no review of any kind covers this head": a trialed advisory reviewer (CodeRabbit,
   Qodo) carrying the exact head under `other_reviewed`, with an empty review body and no new
   threads, is its own ordinary "reviewed, nothing to flag" shape, not a missing review (#1066).
3. **Every** finding on that head SHA is closed: threads resolved, issue-level comments (which
   have no resolve action) triaged and replied to, **and** the low-confidence findings collapsed
   in the review body investigated and answered. Those appear in no thread, so polling threads
   alone reports a clean pass while they stand. The same holds for CodeRabbit's own
   "outside diff range" comments (`cr_outside_diff` in `pr_review.py`'s digest) and for Qodo's
   comment-only findings (`qodo_open`): neither opens a `reviewThreads` entry either, so
   give each one the same triage the low-confidence findings above already get (#1058). Qodo's own
   `Resolved`/`Dismissed` self-tracked badge is a fast pre-triage signal, not a substitute for
   reading the finding, spot-verify against `gh pr diff` rather than trusting it outright.
4. Nothing in the review was a shape the tooling could not read (an unrecognized heading, a moved
   section, an unfamiliar coverage wording). An unrecognized shape blocks the gate on its own.
   File an issue naming it and quoting the body, rather than guessing what the new wording
   probably meant.
5. The maintainer has given **explicit** permission to merge.

The agent never merges on its own. A green or CLEAN PR with one open finding is not mergeable,
full stop, whatever the merge-state field says.

## Expected review loop

Open every fleet-owned pull request ready for review. Draft state delays the loop and causes
reviewers to skip, so it has no place in the internal feature-to-develop or develop-to-main
workflow. The separately documented `upstream-contribution-workflow` may use a draft while a
third-party contribution is still being prepared for upstream review.

Opening a pull request starts this loop by default. Creating the PR is not a terminal handoff.
Only an explicit maintainer instruction may stop, defer, or alter the loop. Silence or a request
that says only "open a PR" is not such an instruction.

Run every `scripts/pr_review.py` command below from a hub checkout. The script is hosted there and
is never carried into a downstream repository.

Run `local-strict-review` against the branch's current diff before step 1's push, and again before any fix push under outcome 1 below.

1. Push changes to the PR branch and open the pull request when it does not exist.
2. Run `scripts/pr_review.py status` once in the foreground and read its output.
3. Re-request a review for the **current head SHA**. Auto-trigger is unreliable, so request it
   explicitly (mechanics in the Copilot runbook). The UI is a fallback only.
4. Run a bounded `scripts/pr_review.py wait` in a background process and read its terminal output.
   A completed review raising **no findings** is a valid terminal outcome, so do not re-trigger it
   or read silence as a missing review. A review whose body says it declined to review is the one
   exception, and it is terminal the other way. Nothing follows it, and re-requesting the same
   head only repeats the decline.
5. Triage findings (see below).
6. Apply fixes or write a rationale for declines.
7. Reply to each thread and resolve what was addressed.
8. Re-run the loop after every fix push until the checks are green and no finding remains open.

The review effort setting is user-controlled. The workflow never selects or changes it. `status` reports `Lite`, `Balanced`, or `Max` when the completed review exposes that metadata, and distinguishes an inherited `Default (<level>)` from an explicit choice. Missing effort metadata reports `unknown` and does not change coverage or completion. A pending effort-labeled request can complete without a `copilot_work_started` timeline event, so absence of that event never proves the request is abandoned. The bounded timeout reports `PENDING` when no review or terminal answer arrives. After a timeout with `requested=yes`, rerun `wait` for another bounded interval by default because the request may still be active. If the maintainer directs a retry, remove Copilot in the pull request UI, add it again, and rerun `wait`. This recovery replaces only the review request and never changes the effort setting.

Drive to green, a review confirmed on the latest head SHA and every actionable finding closed,
then apply the Merge Gate above. **Never exit the loop early.** A round count is not a stopping
condition, and neither is patience running out. Reporting only that the PR was opened is an early
exit unless the maintainer explicitly instructed the agent not to monitor or drive its review.

After an authorized merge, run the `repo-worktree` post-merge cleanup procedure unless the user explicitly asks to retain the checkout or branch. The pull request loop is incomplete while its finished worktree or local task branch remains. It is also incomplete until the base clone returns to fetched and fast-forwarded `develop`.

## Every finding ends in one of five outcomes

1. **Real, so fix it.** Run `local-strict-review` against the branch's current diff before pushing
   the fix, then reply with the fixing commit SHA. For a finding on platform-specific code
   (PowerShell, a macOS- or WSL-only path), "fixed" means executed on that platform, per
   `agent-conduct` "Before Claiming Done": a fix reasoned out by analogy to a tested equivalent
   elsewhere is not yet fixed, and the reply says so rather than claiming the SHA closes it.
2. **Not real, or real but structurally out of scope, so decline in the thread with evidence.**
   Disprove a wrong finding with the command and its output, the code path that makes it
   impossible, or the rule that governs it. A finding that is factually correct but not this
   repo's to fix (a verbatim-fidelity manifest entry byte-locking the section, ownership that
   sits elsewhere) declines the same way: name the boundary and cite what proves it. Either shape
   closes the thread on its own evidence. An assertion ("this is fine") does not close a finding,
   a decline needs evidence the reviewer itself could check.
3. **Real, fixable here, but deliberately left as is, a value call rather than a scope
   boundary, so it is the maintainer's, not the agent's.** Reach for this only once outcome 2 is
   ruled out, since a scope boundary declines on its own evidence and never needs this outcome at
   all. State the finding and why the fix is unwanted, and get an explicit answer in the same
   turn, before moving to other work. A plan to ask later is resolution by silence the moment
   attention moves elsewhere. If the maintainer is not reachable right now, leave the thread open
   and say so, rather than treating the intention to ask as the asking.
4. **Real and worth doing later, so file the issue first, then reply with its link.** A deferral
   noted only in a thread is lost the moment the PR merges.
5. **Keeps recurring, so fix the class, not the instance.** A finding raised repeatedly against
   correct code means the code is not communicating something: add the comment, sharpen the name,
   narrow the interface, or fix the rule if the rule is wrong. Bouncing the same point across
   rounds is the signal to escalate the rule itself, not to keep re-arguing it.

**A disposition decided on one PR does not carry to the next.** The same finding shape recurring
on a sibling repo or PR, even within one batch or one session, gets its own outcome: its own
evidence-backed decline (outcome 2) or its own explicit maintainer answer (outcome 3). A prior
instance's outcome is context for the new one, never a standing answer to reuse in its place.

## Triaging findings

**A low-confidence (suppressed) finding is not a low-value one.** Judge each against the code,
never against its confidence label. Classify before responding:

- **Bug**, wrong behavior, missing coverage, a real code or doc divergence. Fix it.
- **Style or convention**. If the cited rule matches the existing tree, fix the code. If the rule
  contradicts the tree or industry norm, **fix the rule, not the code**, and take it to the
  maintainer (outcome 5) rather than bouncing the same code across rounds.
- **Architectural opinion**, a proposed redesign. Surface it with a recommendation, never apply
  it unilaterally.

## Answering a suppressed finding

A suppressed finding has no thread and no resolved or unresolved state, so an answer needs to
carry its own context: quote the finding (with its `file:line` anchor and enough of the
reviewer's own words to identify it), give one bold verdict per finding (`Fixed in <SHA>`,
`Disproven`, or `No change needed`), state the `(N)` count the block gave so answers can be
checked against findings, and link the review round. **Read every round, not only the head.** A
suppressed finding does not retire when a later push supersedes it, it just stops showing up in a
head-scoped query while still unanswered. Post the answer with `scripts/pr_review.py comment`
from a hub checkout. Do not use a provider connector or reconstruct the GitHub mutation.

## Escalate to the maintainer when

- A genuine design trade-off surfaces (fail-open vs. fail-closed, refactor scope).
- A finding keeps recurring. Bring the pattern and a recommended fix (rule change or code
  change), don't keep silently re-declining it.
- A finding is judged real but should not be fixed. That decision is never the agent's alone.
- An architectural redesign is proposed rather than a bug fix.

## Mechanics Live Elsewhere

This skill is the provider-agnostic contract. Use `scripts/pr_review.py` from a hub checkout for
the GitHub-specific API operations. `status` reports coverage, threads, body-only findings, and
shapes in one call. `wait` requests and polls in-process. `comment` posts a PR-conversation
answer after it reads the PR node ID. `reply` resolves a thread by matching the finding's own
words instead of a line number a fix push can move. The repository's
`.github/copilot-instructions.md` bootstraps Copilot into the `code-review` skill and its stable
coverage marker. Do not reconstruct the API operations by hand.
