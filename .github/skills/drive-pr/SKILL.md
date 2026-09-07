---
name: drive-pr
description: >-
  Drives a ptr727/ProjectTemplate fleet pull request through its review loop, feature branch into
  develop and, when asked, on to a mergeable develop -> main promotion PR, disposing of every
  reviewer finding along the way under pr-review-conduct's outcomes, carried here whole as a
  generated include, and escalating to whoever dispatched the drive where the drive's own seat
  cannot reach the maintainer. Use this whenever asked to drive, land, take, chase, or push
  a PR toward develop or main, or to run the review loop hands off instead of narrating each
  round. When the request does not say how far ("drive this PR", "land it"), ask once whether the
  target is develop or a mergeable main promotion PR, rather than guessing. Triggers even when
  only one PR is named, because a finding raised against the develop -> main promotion PR
  routinely needs its own feature -> develop fix cycle before the promotion PR can go green, and
  stopping at the first promotion-PR finding is the early exit this skill exists to prevent. Ends
  at develop merged, or at a promotion PR meeting every pr-review-conduct Merge Gate item except
  the maintainer's explicit permission to merge, never merges main itself, that is the separate
  merge-and-release skill, its own go-ahead.
---

# Drive PR

## Why This Exists

The same request repeats every time a change is ready: drive it through review, resolve whatever
a reviewer raises, and keep going until develop, or main, actually has it. Re-explaining the
finding-disposition policy and the promotion-PR wrinkle each time is the cost this skill removes.
The wrinkle: a finding raised against the develop -> main promotion PR usually cannot be fixed on
that PR directly, its diff is develop's diff against main, so the fix lands as its own
feature -> develop PR first. Stopping at the first such finding, or forgetting to loop back to the
promotion PR once the fix lands, is the early exit this skill exists to prevent.

## How Far to Drive

- Read the invocation for an explicit target first. "To develop" or "to dev" means stop once
  merged into develop. "To main", "through to main", or "all the way" means continue to a
  mergeable promotion PR. Act on either without asking.
- When the request names no target ("drive this PR", "land it", "take this PR"), ask once,
  before the first push: develop only, or all the way to a mergeable main promotion PR. Recommend
  "all the way to main" as the default, a promotion PR left to go stale once develop is ready is
  the more common regret than driving one step too far.
- A drive dispatched as part of a larger run takes its target from the brief and asks no one,
  since a subagent stopping to ask stalls a run designed to keep moving without one, and the seat
  that dispatched it is the seat that holds the maintainer's answer. `backlog-burndown` is such a
  run, and it briefs develop only, driving the develop -> main promotion pull request in its own
  seat under "The Drive Loop"'s promotion steps. A brief naming no target at all is one to stop
  and ask its dispatcher about, and asking the dispatcher is the whole of what a dispatched drive
  does about an authorization question. A dispatched drive is not the seat that can verify a
  grant, so it does not try: the responsibility for having the maintainer's go-ahead sits with the
  dispatcher, and a worker inventing a check it cannot perform would only launder that
  responsibility rather than discharge it.
- **A brief is never itself the authorization**, which binds the dispatching seat. What authorizes
  a merge is what the maintainer said, recorded where the skill that carries the grant states its
  scope, the way `backlog-burndown`'s own "What Invoking This Skill Authorizes" does. Writing an
  approval into a brief creates none, since an agent cannot widen its own permission by writing
  itself one, and a merge is the outward-facing act this skill's own "What Invoking This Skill
  Authorizes" keeps tied to something the maintainer actually said.
- A repo on the operational workflow model (registry `workflowModel: operational`) has no
  standing promotion PR expectation, confirm whether a promotion PR is even wanted before opening
  one, per operational-vs-release-workflow's "Operational repositories (the complete delta)"
  section.

## What Invoking This Skill Authorizes

- Naming this skill, and answering its how-far question, is the maintainer's explicit, current
  go-ahead for every feature -> develop squash merge the drive performs to reach that target.
- A dispatched drive answers no such question, so what stands in its place is the go-ahead the
  dispatching seat holds, per "How Far to Drive" above, and the drive performs the same merges on
  it. Reading this bullet list is not how such a drive establishes that, since a worker cannot
  verify a grant made in a seat it has no access to.
- It is never authorization to merge the develop -> main promotion PR, or to dispatch a release.
  Those stay in merge-and-release, invoked on its own so the maintainer keeps a checkpoint before
  the harder-to-reverse step.
- The pr-review-conduct Merge Gate still gates every merge this skill performs on its own. The
  go-ahead removes the "may I merge to develop" question, not the gate itself, a feature PR with
  an open finding does not merge regardless of target.

## The Drive Loop

1. Isolate into a worktree per repo-worktree, based on the branch that skill's base rule names, develop unless the task is explicitly about main-only content, before the first edit.
2. Commit the work, then run `local-strict-review` and record its pass in the order that skill
   gives, its diff receipt following the commit, and its carried-content record instead preceding
   the commit where the change moves a carried canonical unit in the repository that authors one,
   because that ledger is tracked. Then push the branch and open the feature -> develop PR if it
   does not exist yet. A push refused by a `.husky/pre-push` hook, which the hub carries and a
   repository has only if it adds one, is that gate working rather than an
   obstacle to route around, and that
   skill's refusal table says what each refusal means and what clears it.
3. Drive pr-review-conduct's review loop on it to the Merge Gate, disposing of every finding per
   "Disposing of Every Finding" below.
4. Capture the branch's own tip before merging, `gh pr view <number> --repo <owner>/<repo> --json headRefOid --jq
   .headRefOid`, needed for the verify-then-delete step below since `gh pr merge` itself reports
   the resulting squash commit on `develop`, not the PR's `headRefOid`. Merge the feature PR into
   develop, `gh pr merge <number> --squash --repo <owner>/<repo>`. Never `--delete-branch` on this
   call, it is run from inside the task's own worktree per step 1, where the feature branch is
   checked out, and `gh pr merge --delete-branch` needs to switch that worktree to the base branch
   to delete it, which fails when `develop` is already checked out somewhere else, the ordinary
   case in this layout. Instead run repo-worktree's post-merge cleanup from the base clone, remove
   the worktree and delete the now-merged local task branch, then verify before deleting the remote
   one, which is this skill's own step rather than that one's. Both remote commands resolve `origin`,
   so they hold only where the pull request's head branch lives in this repository, which step 1
   guarantees by branching here. A pull request opened from a fork follows
   `upstream-contribution-workflow` instead and neither command applies to it, since `origin` would
   name the base repository and exit `2` would mean the branch was never there rather than already
   deleted. The object id in `git ls-remote --heads --exit-code -- origin "refs/heads/<branch>"`,
   which prints `<oid>\t<ref>` so the id is its first field, matches the `headRefOid` captured above, `--` before `origin` and the fully-qualified ref. `--heads origin
   "<branch>"` alone still tail-matches a differently-prefixed branch sharing the same suffix, and
   `--` placed after `origin` instead of before it is not equivalent either, verified empirically
   against a `refs/heads/other/--` ref: after-origin also matched it, before-origin matched only
   the one intended. `--exit-code` distinguishes exit `2`, branch genuinely gone, from any other
   non-zero exit, a failed query, an unreachable remote and a gone branch both print nothing to
   stdout otherwise. Exit `2` means the remote branch is already gone, so the delete is done and
   the step is complete. Stop and report either a mismatch or a failed query rather than deleting,
   someone could have pushed to the branch after the merge, or the name could have been reused.
   `<branch>` is the real value, substituted as its own quoted argument (a shell variable
   expansion such as `"$branch"`, or an argv element), never handed to `eval` or `sh -c` for a
   second round of shell parsing, the only way an embedded `$()` or backtick would actually run.
   A valid ref can start with `-` or carry a shell metacharacter, which is why it stays quoted
   regardless. Only once it matches, `git push origin --delete -- "<branch>"`. Never
   `--force-with-lease` here, git-commit-conventions forbids it
   unconditionally, this plain verify-then-delete is the safety gate, not a compare-and-swap at
   delete time. The
   repo's auto-delete-head-branches setting is kept off fleet-wide (to protect `develop` and
   `main` from it, GitHub has no per-branch exception), so nothing deletes an ordinary feature
   branch automatically. Stop here and report the merged PR when the target is develop only.
5. Open the develop -> main promotion PR if it does not exist yet, or find the existing one.
6. Drive its review loop the same way. A finding that needs a code change never gets pushed to
   the promotion PR directly, its head is develop, so land the fix as a fresh pass through steps
   1 to 4 in its own worktree and branch, then return here.
7. The fix landing on develop updates the promotion PR's diff and head SHA on its own, re-request
   a review on the new head and continue the loop.
8. Repeat 6 and 7 until the promotion PR meets every pr-review-conduct Merge Gate item except the
   maintainer's explicit permission to merge.
9. Report the promotion PR number and its ready state. Do not merge it.

## Disposing of Every Finding

The rule below is a generated include, so a defect in it is fixed in `pr-review-conduct` and
regenerated rather than edited here. A drive that cannot reach the maintainer directly, a
dispatched one being the ordinary case, escalates per `pr-review-conduct` "Escalate to the
maintainer when".

<!-- include: .agents/skills/pr-review-conduct/SKILL.md > Every finding ends in one of five outcomes -->

1. **Real, so fix it.** Take the fix through `local-strict-review` the same way the push that
   opened the pull request went, per `pr-review-conduct` "Expected review loop", then reply with
   the fixing commit SHA. A branch already reviewed once has not been reviewed for the fix, which
   is the round the `local-strict-review` pass gets dropped on and the churn `local-strict-review`
   exists to stop. For a finding on platform-specific code (PowerShell, a macOS- or WSL-only
   path), "fixed" means executed on that platform, per
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

`pr-review-conduct` "Every finding ends in one of five outcomes" keeps the full rule, and the
`drive-pr` Skill carries it whole as a generated include, applying it while driving.

<!-- /include -->

## Mechanics Live Elsewhere

- Review loop mechanics, the Merge Gate, and `scripts/pr_review.py`: pr-review-conduct.
- Branch rules, never delete develop, the EOL-only conflict, issue-closing keywords belonging on
  the promotion PR: operational-vs-release-workflow.
- Worktree isolation and post-merge cleanup: repo-worktree.

## Stop and Ask, Beyond the How-Far Question

- A genuine design trade-off, a recurring finding pattern, or an architectural redesign proposal
  each escalate per pr-review-conduct's own list, restated there, not duplicated here.
- An unrecognized review shape blocks the gate on its own, file an issue naming it and ask, never
  guess what new wording probably meant.
