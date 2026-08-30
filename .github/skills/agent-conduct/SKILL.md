---
name: agent-conduct
description: >-
  Surfaces the ptr727/ProjectTemplate fleet's conduct rules at the three decision moments they are violated: about to claim work is done, verified, green, or fixed, about to proceed on an assumption the user could cheaply confirm, and a failure or review finding just surfaced a durable lesson. Use this whenever about to report success or completion of any task, whenever about to pick a default, guess an intent, or resolve an ambiguity without asking, whenever work is blocked on a decision or authorization only the user can give, and whenever an incident, a wrong answer, or a repeated correction just taught something a future session must honor. Deliberately narrow: the carried AGENTS.md sections are the always-on layer, and this skill fires at the moments rather than duplicating them, so do not load it as general background. Where a sibling skill owns the moment, it wins: git-commit-conventions for committing, pr-review-conduct for review and merge claims, comment-and-doc-style for prose. The GOVERNANCE.md sections this skill summarizes keep the full rules.
---

# Agent Conduct

## Why This Exists

The fleet's conduct rules (verification before claiming done, asking instead of assuming, recording lessons) lived only in doc sections nothing surfaced at the moment of violation, so they were honored by whoever happened to have read them recently. This skill is the decision-moment surface. The full rules stay in `GOVERNANCE.md` ("Verification Discipline", "Communicating with the User", "Durable Knowledge and Self-Improvement"), which keeps authority, and in the carried `AGENTS.md` "Context and Delegation Discipline" section, which is the always-on layer.

## Before Claiming Done

Read `GOVERNANCE.md` "Verification Discipline" before reporting success on anything non-trivial. Its unifying property: every failure it lists is green. The checks that bind here:

- **A green check is not evidence the work happened.** A skipped job and a passing job are indistinguishable in an aggregated required check, so confirm from the log that the job ran and produced what it promises.
- **Locate every check the change owes before running any**, from what the repository declares (`OPERATIONS.md` "Local Verification" beside the workflows), not from what the pipeline happens to run, since part of a contract is routinely unreachable from a runner and green is then the precise signal it was skipped.
- **Run the repo's whole lint gate before every push**, not the parts that look relevant, because the tool most likely to catch a change is often the one it seems least about.
- **A launched process is not a result.** Report the output the wait produced, and where it produced none, that absence is the report. Never name an external cause the record does not carry.
- **A local clone is not the branch it names.** Fetch immediately before reading, or read the live ref, and name the ref and commit in any finding a local read produced.
- **A checkout this session did not create is not ground truth.** One found already sitting on disk may belong to another concurrent session, sit on a stale fetch or an unexpected branch, or hold unreviewed uncommitted edits. Clone fresh or read the live API instead of trusting `git status`/`git remote -v` run against a pre-existing checkout.
- **A "does not exist" claim names the branch it was checked against.** A worktree's default branch is not necessarily the one the content lives on: in-flight content on a `release`-model repo lands on `develop` before `main`, per `GOVERNANCE.md` "Branching Model," so check that branch before reporting anything absent repo-wide.
- **A `raw.githubusercontent.com` 404 does not distinguish a private repository from a missing file.** Where visibility is not confirmed public, read content via `gh api "repos/<owner>/<repo>/contents/<path>?ref=<ref>"`, capturing the result before decoding it (`content=$(gh api ... --jq '.content') && printf '%s' "$content" | base64 -d`) rather than piping straight into `base64 -d`, whose own exit status is all a direct pipe reports, letting a failed fetch decode as an empty success. Never `2>&1` either form, which corrupts the decode with the error text instead of the payload. Verify the ref resolves before reading either failure as proof the content itself does not exist.
- **A test asserts the mechanism it names, and a gate has to be watched failing.** A case that passes for an incidental reason is worse than no case, because it is later cited as evidence.
- **Platform-specific code is verified only on the platform it runs on.** Reasoning about PowerShell, macOS, or WSL-specific behavior from a different host is not verification, however closely it matches an already-tested equivalent elsewhere. State an untested structural match as exactly that, never in the words used for a tested fact, and when no agent in the loop has access to the target platform, say so and defer or ship it labeled unverified.
- **PR-bound work runs `local-strict-review` before the claim.** Claiming a unit of work done, verified, green, or fixed for work that will become, or already is, a pull request means running `local-strict-review` against the branch's diff first, before a PR-hosted reviewer finds the same gap.

Claims about a pull request being reviewed, clean, or mergeable are owned by the `pr-review-conduct` skill, and claims that a commit landed by `git-commit-conventions`.

## Before Assuming

- **Ask when the user can cheaply confirm.** An assumption that saves one question and is wrong costs the rework plus the trust, so a genuine ambiguity in intent, scope, or authorization is raised, not resolved by picking the likelier reading. Rules that already answer the question (the committed instruction set) are not ambiguity, so read them first rather than asking what they state.
- **Raise blocked work as a direct interactive prompt** at the point the work stops, per `GOVERNANCE.md` "Communicating with the User": the blocked item is the message, the options offered are the actions themselves, and a handoff buried in a summary paragraph is a handoff that did not happen. Numbered lists are the fallback where no prompt mechanism exists.
- **References are clickable where they are read**: a pull request, issue, or commit on a Markdown surface is a Markdown link, and on a surface that renders neither, a bare `#123` with the link in the message before the prompt.
- **Capability is not permission.** A token's reach, a tool that happens to work, or a similar grant in a past session authorizes nothing, and the irreversible step (merge, publish, release, delete) stays the maintainer's.

## When a Failure Surfaces a Lesson

- **Durable knowledge lands in the committed docs, not in agent memory**, as part of the change that surfaced it, per `GOVERNANCE.md` "Durable Knowledge and Self-Improvement". Memory does not survive a new session or machine, so it holds only environment nuance and in-flight state.
- **Where the governing doc is carried from the hub, file the finding against `ptr727/ProjectTemplate`** rather than only patching it locally. A local fix leaves every sibling repo with the same trap. Search open and closed issues first, then update the matching issue or file a new one.
- **A review flags an instance, so fix the class**: sweep for the siblings before replying, because reviewers sample rather than enumerate.
- **A rule that keeps needing restating** is usually a stale or missing skills install, so run `python3 scripts/skills_install.py --report` from a hub checkout (the `fleet-conformance-check` skill) before concluding the rule does not exist.

## Delegation, in One Paragraph

The always-on rules live in `AGENTS.md` "Context and Delegation Discipline" and are not restated here. The two that intersect conduct: brief a subagent so it never needs a governance file, since anything it must honor has to be in its prompt, and never tier down the seat holding the judgment, because governance wording and the decision to decline a review finding are fleet-wide and durable when wrong.
