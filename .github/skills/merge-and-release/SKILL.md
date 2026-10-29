---
name: merge-and-release
description: >-
  Merges a ready develop -> main promotion PR for any ptr727/ProjectTemplate fleet repo and, when
  asked, dispatches the release, in this hub always refreshing this machine's installed Skills
  from the newly promoted content as part of that release step, never as a separate ask. Use this
  whenever asked to merge main, ship a release, cut a release, or finish a promotion once its PR
  is already green and fully resolved (produced by drive-pr or by hand). When the request does not
  say how far ("merge main", "ship it"), ask once whether to merge only or merge and release,
  rather than guessing which the maintainer wants this time. Triggers even when the phrasing is as
  short as "merge main and release", because that already states the scope and is itself the
  explicit, current go-ahead this skill acts on without asking again, though it never substitutes
  for the pr-review-conduct Merge Gate, a promotion PR that is not actually green and fully
  resolved gets reported and stopped on, not merged. Where a merge or dispatch is actually
  performed this skill wins over `branching-and-release-model`, which supplies the policy it
  follows, and an `unattended-handoff` run invoked with scope main or release is the one standing
  go-ahead it accepts in place of asking.
---

# Merge and Release

## Why This Exists

Once drive-pr (or a maintainer by hand) leaves a promotion PR ready, the same steps follow every
time. The steps are to merge it, and usually dispatch the release it unblocks. In this hub a promotion can
also change `.agents/skills` content this very session depends on. So the release step always
carries a Skills refresh with it there, never a separate branch to ask about. This is because an
ambiguous "merge and release" on the hub must not leave the maintainer unsure whether Skills got refreshed. One
skill covers all of it, scoped down by what the maintainer actually asks for.

## How Far to Go

- Read the invocation for an explicit scope first. "Just merge" or "merge only" means stop after
  the merge. "Merge and release", "ship it", or "cut a release" means also dispatch, and in this
  hub also refresh Skills as part of that same step. Act on either without asking.
- When the request names no scope ("merge main"), ask once, before merging: merge only, or merge
  and release. Recommend "merge and release" as the default on a release-model repo, a promotion
  merged without its release is the more common regret there. Recommend "merge only" as the
  default on an operational repo (registry `workflowModel: operational`). There a release is a
  separate, deliberate dispatch rather than an automatic follow-on to a promotion, per
  branching-and-release-model's "Operational repositories" delta.
- Detect the hub automatically, `git remote get-url origin` or `gh repo view --json
  nameWithOwner` naming `ptr727/ProjectTemplate`. There the release scope silently includes the
  Skills refresh, a downstream repo never sees it, it has no `.agents/skills` of its own to
  refresh.

## What Invoking This Skill Authorizes

- Naming this skill, and answering its how-far question, is the maintainer's explicit, current
  go-ahead. That go-ahead is to merge the promotion PR and to perform the scope chosen, for the one
  repo and PR in front of the agent. It is never a standing mode carried to the next PR.
- The one standing grant is an `unattended-handoff` run the maintainer invoked with scope `main`
  or `release`. That invocation names in advance each promotion that run's workers make, in that session
  only. A worker handing a promotion here under it asks no how-far question, since the scope
  states it. The Merge Gate is still re-verified per promotion.
- It is never permission to merge a PR that fails the Merge Gate. Re-verify the gate at
  invocation time, a check from earlier in the session can be stale.

## The Procedure

1. Identify the open develop -> main promotion PR for this repo, stop and report if none is open.
2. Record the promotion's live head, `gh pr view [number] --repo owner/repo --json headRefOid
   --jq .headRefOid`. Then, from a hub checkout, `scripts/` is not carried into downstream repos,
   run `uv run --no-project --script scripts/pr_review.py status [number] --repo owner/repo` on it and confirm the
   pr-review-conduct Merge Gate. Stop and report exactly what is missing rather than merging on a
   partial gate. A digest reading `coverage=none` is not a partial gate where the promotion meets the
   one case that skill's Merge Gate item 2 states. Check that case again on every re-run below, and record
   its evidence for the head step 3 merges, as that item says.
   Confirm the digest's `head=` is a prefix of the recorded SHA, re-running both where it is not.
   That way the SHA step 3 merges is the one the gate verified. Where drive-pr's ready report named a
   head and it is not a prefix of the recorded one, stop and re-ask with the commits added,
   `gh api repos/owner/repo/compare/<reported>...<recorded> --jq '.commits[] | .sha[:8] + " " +
   (.commit.message | split("\n")[0])'`. The reason is that a feature PR squashed into `develop` after the
   report moves the promotion's head onto content the maintainer never saw. Under an
   `unattended-handoff` standing grant this comparison is skipped, even where the worker holds a
   ready report. Content another session merged meanwhile rides along, as that skill's grant
   accepts.
   Where no report named a head, a promotion made ready by hand, the go-ahead covers the head
   this step recorded.
3. `gh pr merge [number] --merge --match-head-commit <sha> --repo owner/repo`, where `<sha>` is
   the head the go-ahead covers. Attended, that is the head `drive-pr`'s ready report named, or
   the moved head the maintainer approved after step 2's re-ask. Under an `unattended-handoff`
   grant, or where no report named a head, it is the head step 2 recorded. The server then
   refuses the merge if the head moved past that SHA. Never `--delete-branch`, the promotion
   PR's head is `develop`.
4. Confirm the merge landed, `mergedAt` set, `main`'s tip matching the merge commit.
5. When the chosen scope includes a release, first bring the hub checkout used for this procedure
   current, `git fetch origin main`. Then read this repo's `releaseTrigger` from that fetched tip
   rather than a possibly-stale working tree copy. That matters when the target repo is the hub itself
   and this exact promotion changed its own registry entry. Select the one matching entry
   explicitly, falling back to the registry's own default when that entry sets no
   `releaseTrigger` of its own. Stop and report rather than guessing when selection is not
   exactly one match. On a non-1 count exit non-zero rather than returning empty with success. An
   ambiguous or missing match must fail loud, not read as an empty value still safe to act on: `git
   show origin/main:registry/repos.json | jq -r --arg name '<repo-name>' '(.repos | map(select(.name
   == $name))) as $m | if ($m | length) == 1 then ($m[0].releaseTrigger // .defaults.releaseTrigger)
   else error("expected exactly one registry entry for \($name), got \($m | length)") end'`. Two
   cases, `none` versus anything else. When it
   reads `none`, report that no
   release is configured, dispatch and run-correlation (step 6) do not apply. Otherwise, dispatch
   explicitly, `gh workflow run publish-release.yml --ref main --repo owner/repo`, or `--ref
   develop` only when the maintainer explicitly asked for a prerelease dispatch instead.
6. Correlate the specific run this dispatch produced rather than assuming the newest one is it.
   `gh run list --repo owner/repo --workflow publish-release.yml --branch main --event
   workflow_dispatch --json databaseId,createdAt,headSha` (or `--branch develop` for a prerelease
   dispatch). Match it by `headSha` against the dispatched ref's tip (`main`'s tip confirmed in
   step 4, or `develop`'s current tip for a prerelease). Also match it by `createdAt` against the
   dispatch time. `gh run list` can momentarily omit a just-created run, so a single query reporting zero
   candidates is not yet "never started". Poll the list itself, within a bounded interval, until
   exactly one candidate matches. A concurrent run of a different event on the same branch must
   never be mistaken for this one. More than one candidate is as inconclusive as zero. A run whose
   `headSha` does not match the expected tip at all (rather than simply being absent) means the
   dispatched ref moved. That move happened between step 4's confirmation and the dispatch itself.
   Report that distinctly, the ref changed mid-dispatch, rather than folding it into an ordinary absent-run
   timeout. Report and stop rather than guessing once the interval elapses with zero or more than
   one candidate still matching. Only once exactly one candidate is confirmed, poll that one run
   id to completion in one further bounded background wait with an explicit, finite timeout.
   The timeout is 2700 seconds (45 minutes, matching `scripts/pr_review.py`'s own default) unless
   the maintainer states a different bound for this specific release. Use `timeout 2700 gh run
   watch <run-id> --repo owner/repo --exit-status` on a host with GNU `timeout`, with the maintainer's bound in place of 2700 where they state
   one. On a host without
   it (macOS without coreutils, native Windows), use the equivalent bounded-wait mechanism
   enforcing the same bound. Never pipe `gh run watch` into another command unless the shell running it sets
   `pipefail`. That is because without it a pipeline reports its last stage's exit status and
   `gh run watch ... | tail` reports whether `tail` succeeded rather than whether the run
   did. Read the watch's own exit status, or read the conclusion back with
   `gh run view <run-id> --repo owner/repo --json status,conclusion`.
   Report a timeout separately from a completed run's own conclusion, the tag or
   version it produced. A run that fails, times out, or never starts is reported, never silently
   retried.
7. In the hub, when the chosen scope includes a release, refresh this machine's Codex and opencode
   copy from the promoted `main`. Do it in a detached worktree of its own, never by switching an existing
   checkout to `main`. The reason is that the checkout Claude Code loads in place would move with it. Run `git fetch origin main`, then `git worktree add --detach
   <worktree> origin/main` at a new path in the fleet worktree layout. From that worktree run
   `python3 scripts/skills_install.py --snapshot-only`, then `python3 scripts/skills_install.py
   --report`, and confirm its snapshot reads current. A fresh worktree holds nothing uncommitted
   or ignored under `.agents/skills/` or `.claude-plugin/`, the paths the installer reads, so
   the copy holds exactly the promoted commit. `--snapshot-only` leaves the Claude Code
   registration untouched, and that channel needs no refresh here. It loads the registered
   directory in place, the one `--report` names under `live`, and serves whatever it holds at
   read time. So where that is the primary checkout on `develop`, Claude Code sessions on this
   machine load `develop`. Where `live.vcs` reads `archive`, that directory is the tree the hub's
   `host-setup/bootstrap.sh` or `bootstrap.ps1` keeps rather than a checkout. There it holds what
   that bootstrap fetched, the commit `live.commit` names where it is not null. It holds that until
   `bootstrap.sh --skills` or `bootstrap.ps1 -Skills` runs again, which this step does not do.
   From the same worktree, also run `python3 host-setup/agent-safety/claude/install.py --report`,
   and report any result other than CURRENT to the maintainer. The host-safety kit, its rendered
   `CLAUDE.md` included, is refreshed only by its own installer. That installer rewrites this
   machine's hooks and settings, so this step reports it rather than running it.
   Then remove the worktree with
   `git worktree remove <worktree>`, whatever either report said. Removing it leaves the skills
   snapshot and its stamp unchanged, and a result that does not read current is reported to the
   maintainer. The skills `--report` exits on the snapshot alone. This step runs whether step 5
   or 6 dispatched, skipped, or failed a release. That is because it is gated only on the chosen scope,
   never on the release outcome. This refreshes only the machine running
   this session, per skill-lifecycle, every other machine still refreshes on its own next run or
   `docs/host-setup.md` "Fleet Skills Install" cadence.
8. Run cleanup regardless of how steps 5 through 7 ended. No release configured, a dispatch
   failure, an ambiguous run match, a timeout, a failed run, or a hub Skills refresh all still
   reach this step. The merge in step 3 already landed by then. Every part below is required:
   - The promotion PR's own worktree: fetch and prune, remove the worktree, then bring the base
     clone to current `develop`. To do so, check `develop` out first only where it sits on another
     branch, and fast-forward it with `git merge --ff-only origin/develop`. Where that checkout
     is needed, removing first, not after, matters. The reason is that the base clone cannot check out `develop`
     while the promotion worktree still has it checked out. That is because one branch checked out
     in two worktrees at once is refused outright. Never delete `develop`: it is the promotion PR's
     own head. The repo's auto-delete-head-branches setting is kept off fleet-wide for exactly
     this reason, so nothing does this automatically.
   - A defensive sweep for anything drive-pr's own cleanup should already have removed but might
     not have. The causes include an interrupted loop, a fix landed by hand outside that skill, or
     a maintainer merge in the GitHub UI. `git worktree list` for any worktree still registered under this
     task's feature branches, `git branch -vv` for any local feature branch, `git ls-remote
     --heads origin` for any matching remote feature branch. For each such branch, read the one
     merged pull request into `develop` it headed. Fail loud on any count other than one rather
     than reading an empty answer as safe: `gh pr list --head "<branch>" --base develop --state
     merged --repo owner/repo --json number --jq 'if length == 1 then .[0].number else
     error("expected exactly one merged PR for this head, got \(length)") end'`. Then run
     `uv run --no-project --script scripts/pr_review.py cleanup <number> --repo owner/repo --checkout <base-clone>`
     from a hub checkout outside the worktree it removes, `<base-clone>` being the repository's
     primary clone, per `repo-worktree` "Listing and Cleanup". It re-reads that pull request and
     repeats the merged, base, head-repository, and tip checks in code. It refuses a permanent or
     protected branch, deletes the remote branch, and confirms it by reading the ref back. It treats
     a remote branch already gone as done, so a prior interrupted attempt finishes rather than
     stops. It then removes the worktree and local branch. Its local refusals, a dirty worktree
     among them, are decided before the remote delete. `repo-worktree` "Listing and Cleanup"
     says what a refusal after it and a `base=kept` line each leave to do. Exit `0` is done, and
     any other exit stops and reports. `1` is a refusal, and `2` is an unanswered read or an unconfirmed
     delete. `64` is a target under an owner other than the hub checkout's own `origin`, or an
     unreadable `origin`. A registry refusal is a `1`. It runs under the host-safety
     installer's `Bash(uv run --no-project --script scripts/pr_review.py:*)` allow rule, which is what lets auto mode
     complete it.
     It also refuses while another open pull request in the repository has the branch as its head or base, naming each.
     Where this seat cannot run `cleanup`, report the cleanup to the maintainer instead of running its steps by hand. That report names the pull request number and `--repo`.
     No direct `git push origin --delete`, ref API delete, `git worktree remove`, or `git branch -D` runs on a feature branch from this skill. That is because `cleanup` is the one place holding the checks.
     Never apply this sweep to `develop` or `main`
     themselves, only to feature branches a drive-pr loop created.
   - What no single pull request names: run
     `uv run --no-project --script scripts/pr_review.py cleanup --sweep --repo owner/repo --checkout <base-clone>`
     and report what it lists. It writes nothing but a fetch, and `--apply` runs only on the
     maintainer's go-ahead, since the report reaches other sessions' finished work too.

## Mechanics Live Elsewhere

- The Merge Gate itself: pr-review-conduct.
- Never delete develop, no-op republish, the operational repos' dispatch-only model:
  branching-and-release-model.
- What the dispatch actually builds and publishes: workflow-ci-contract.
- Skills install and report semantics: skill-lifecycle.
- Cleanup mechanics: repo-worktree.

## Stop and Report, Never Guess

- A merge conflict, a newly failing check, or a gate item that regressed since drive-pr finished
  are each a stop. Report the exact state, never force or retry blindly.
- A merge refused by `--match-head-commit` means the head moved past the SHA it was given.
  Attended, stop and report the commits added, named as step 2 names them, and never retry with
  the new SHA on the old go-ahead. Under an `unattended-handoff` grant, re-run step 2 on the new head instead.
- `gh pr merge` or `gh workflow run` failing is reported with its actual output, never
  suppressed, never assumed harmless on the agent's side alone.
