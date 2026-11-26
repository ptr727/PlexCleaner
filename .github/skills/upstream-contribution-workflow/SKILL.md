---
name: upstream-contribution-workflow
description: >-
  Governs how the maintainer contributes to a third-party repository he does not control, distinct
  from the fleet's own branching model: a dirty work branch on his fork for the iteration, its
  fork-internal PR kept open and never merged, squashed once clean to a second branch carrying only
  the intended history, that clean branch opened as the upstream PR, and reviewer feedback applied
  to the dirty branch first. Use this whenever about to open a
  pull request against a repository outside the ptr727 fleet, whenever forking a third-party project
  to contribute, whenever an upstream reviewer requests changes on a PR opened this way, whenever a
  fork-internal iteration PR looks ready to merge or close, whenever the upstream base branch moves
  under a contribution, whenever an upstream's CI rejects merge commits or it forbids
  rewriting a published PR, and whenever choosing such a repository's issue or PR template.
  Triggers regardless of the target repo's type or workflow model, never assumed to match the fleet's.
---

# Upstream Contribution Workflow

## Why this exists

The fleet's own branching model (`branching-and-release-model`) governs repos the maintainer
controls end to end: squash-only feature branches, merge-commit promotions, signed commits under
his own identity. None of that applies to someone else's repository. A PR into a third-party
project answers to that project's own maintainers, on their own timeline, with their own review cycles. The history that lands there should read as a deliberate, minimal contribution, not as
the maintainer's own iteration log. This skill is that different shape, kept separate from the
fleet's internal model so the two are never conflated.

## The two-branch shape

1. **Fork the upstream repo**, if not already forked. The fork's copy of the upstream base branch
   (`main`, `dev`, whichever upstream PRs target) is the **mirror branch**. It moves only by
   syncing from upstream, and nothing of the maintainer's is ever merged into it.
2. **Do the actual work on a dirty work branch**, on the maintainer's own fork. This branch is
   allowed to be messy: false starts, fixup commits, back-and-forth in response to review, whatever
   the real work looks like while it's happening. Open a PR from this branch into the mirror
   branch on the maintainer's **own fork** (not upstream). So all the iteration happens on that fork,
   visible and reviewable by bots and the maintainer, without touching the upstream repo at all.
   **That fork-internal PR is the iteration record, never a merge candidate.** It stays open for
   the life of the contribution, however green it gets, and is never merged, per "The iteration PR
   is never merged" below.
3. **Once the dirty branch is clean and the change is ready, squash it to a second branch** that
   carries only the intended, minimal commit history. That history is one commit (or a small, deliberate set) that states what the change is, not how it was arrived at. Cut that branch fresh from the mirror
   branch's current tip and squash the dirty branch's changes onto it. It is a plain branch, not a
   PR on the fork, since the review it needs already happened on the iteration PR. One dirty branch
   may feed several clean branches when upstream wants the change split, each carrying only its
   own slice.
4. **Open the PR against the upstream repo from that second, clean branch.** This is the only
   branch upstream ever sees. An upstream draft may be opened only after that clean presentation
   branch exists and is published. When more preparation is needed, continue on the dirty branch and re-squash it into the clean branch. In that case, update the same draft by the step 5 procedure. Never
   iterate directly on the published presentation branch. Mark the draft ready when preparation
   finishes. Open it ready immediately when no preparation remains.
5. **If upstream reviewers ask for changes, apply them to the dirty branch first**, and iterate there the same way as step 2. After that iteration, re-squash the updated dirty branch into the clean branch that
   actually reaches upstream. Updating the same upstream PR rather than opening a new one each
   round rewrites the clean branch's history. Pushing a rewritten branch that is already published requires `git push --force-with-lease`. Prefer `--force-with-lease` over a bare `--force`, since `--force-with-lease` refuses the push if the remote moved since the last fetch. **`git-commit-conventions`'s never-force-push
   rule governs this fleet's own repos, where a branch is shared with bots, other branches, and
   required-check history a rewrite would orphan. That rule stays absolute there, with no exception.
   It has no jurisdiction here**. That is because this clean presentation branch lives on the maintainer's own
   fork, outside the fleet entirely, and carries nobody's work but this squash. Force-with-lease
   is scoped just as tightly regardless. It applies only to this one branch, only on the maintainer's own fork. The dirty work branch takes it only in the one case "The iteration PR is never merged" below names. That branch is the append-only iteration log this whole workflow exists to preserve. **Each force-with-lease push still waits for the maintainer's explicit, current go-ahead.** Force-pushing is an irreversible step that stays his, per `GOVERNANCE.md` "Communicating with the User", and this skill decides only where one may happen. If the clean branch's force-with-lease push is refused or unavailable, open a fresh PR from a newly
   named clean branch rather than fighting the push. A dirty-branch lease push that is refused or unavailable never takes this route. "The iteration PR is never merged" below says what it takes instead.

   **Where the upstream forbids rewriting a published pull request branch, keep the clean branch append-only instead.** Its contribution guidelines or its reviewers say so. Commit each round's change on top of the clean branch as a new commit, and push it without force. The squash then happens through the upstream's own squash merge, never as a rewrite of the published branch. Where the upstream does not squash on merge and wants one commit, stop and ask the maintainer for an approved route. On every other upstream, the re-squash above is unchanged.

**The dirty branch is always the working copy. The clean branch is always the presentation copy.**
Never reverse this: never iterate directly on the branch that's open against upstream, and never
skip the squash step because the dirty branch "looks clean enough."

## The iteration PR is never merged

Merging the iteration PR into the mirror branch looks like the natural finish once it is green, and
it breaks the fork. The mirror branch then carries a commit upstream does not, so it stops
being a mirror. It can no longer be fast-forwarded from upstream. Every later sync has to merge
upstream into the maintainer's own change, and any sync touching the same lines conflicts. And the
iteration PR is the place where upstream drift is absorbed and its fallout fixed before the
upstream PR has to absorb that drift. So merging or closing the iteration PR removes that place.

When the upstream base moves, sync the mirror branch from upstream and merge the mirror branch into
the dirty branch. In that case, also fix whatever breaks in the iteration PR. Bringing the mirror branch in is a merge rather than a rebase, since the dirty branch is append-only. A dirty branch stacked on another takes the lower branch's changes the same way. Then re-squash onto the new mirror tip whenever the clean branch needs to follow, per step 5. An append-only clean branch, per step 5, has no recorded way to follow a moved base. So stop and ask the maintainer before it follows one. Stop too before it takes a round from a dirty branch that has merged the moved mirror, since that round carries upstream's code.

**Where the upstream's own CI rejects merge commits, rebase the dirty branch instead.** Such a check fails any pull request whose commits include a merge, and stops the rest of the upstream's checks from running. So a merge there costs the iteration PR the CI coverage it exists to provide. The rebase applies only in that case, and only to the dirty branch on the maintainer's own fork. Rebase it onto the mirror branch. On a stack, rebase the lower branch first. Then move the upper branch with `git rebase --onto "<lower>" "<lower-old-head>" "<upper>"`, its old head read from the lower branch's backup. Before each rebase, push the branch's old head to a named backup branch on the fork, and confirm the fork holds it. That backup keeps the replaced iteration log readable. A rebase also drops every merge commit, and with it any manual edit or conflict resolution the merge held. The chain below stops after the rebase where the old head carried one. Read each with `git show --remerge-diff "<merge>"` and reapply what it holds beyond the sync, or stop and ask the maintainer. Then run only the lease push. Otherwise push the rebased branch with `--force-with-lease`, pinned to that old head, under the go-ahead step 5 requires:

```sh
old=$(git rev-parse --verify work/x) && bak=refs/heads/backup/work-x-1 &&
  git push origin "$old:$bak" &&
  { [ "$(git ls-remote origin "$bak" | cut -f1)" = "$old" ] || { echo "backup not confirmed: $bak" >&2; false; }; } &&
  git rebase "$mirror" work/x &&
  { merges=$(git rev-list --merges "$mirror..$old") && [ -z "$merges" ] || { echo "merge scan failed or the rebase dropped merge commits, read each with git show --remerge-diff" >&2; false; }; } &&
  git push --force-with-lease="work/x:$old" origin work/x
```

PowerShell equivalent:

```powershell
if (-not $mirror) { throw 'set $mirror to the mirror branch first' }
$old = git rev-parse --verify work/x
if ($LASTEXITCODE -ne 0) { throw 'branch not found: work/x' }
$bak = 'refs/heads/backup/work-x-1'
git push origin "${old}:$bak"
if ($LASTEXITCODE -ne 0) { throw "backup push failed: $bak" }
if ("$(git ls-remote origin $bak)".Split("`t")[0] -ne $old) { throw "backup not confirmed: $bak" }
git rebase $mirror work/x
if ($LASTEXITCODE -ne 0) { throw 'rebase stopped, resolve it before pushing' }
$merges = git rev-list --merges "$mirror..$old"
if ($LASTEXITCODE -ne 0) { throw 'merge scan failed, so the check did not run' }
if ($merges) { throw 'the rebase dropped merge commits, read each with git show --remerge-diff' }
git push --force-with-lease="work/x:$old" origin work/x
if ($LASTEXITCODE -ne 0) { throw 'lease push refused: work/x' }
```

Set `mirror` to the mirror branch's name first. Number the backup branch per rebase, raising the suffix each time, so a later rebase never overwrites an earlier backup. Stop where the confirmation fails, since the force-push would then leave the old head on no remote branch. Where the rebase stops on a conflict, resolve it and run `git rebase --continue`. Then read the old head back from the backup branch with `git ls-remote origin "<backup-ref>"`. The merge check did not run in that case, so run `git rev-list --merges "$mirror..<old-head>"` with that value. A scan that exits non-zero checked nothing, so fix its cause and rerun it before anything else. Read any merge it names, as above. Where the merge check stops the chain instead, first fix a failed scan's cause and rerun it. Then read and reapply any merge it names. Run only the lease push, pinned to the old head.

A lease push that fails, or that the fork does not allow, stops the chain. Re-fetch with `git fetch origin`. A fetch that fails leaves `origin/work/x` stale, so stop and report that rather than comparing against it. Then list the fork's branch against the old head with `git log --oneline --left-right "<old-head>...origin/work/x"`. Read the old head back from the backup branch as above. A `>` entry is a commit the fork gained that the rebase never saw, so the fork moved under you. `<` entries alone are old-head commits the fork's branch lacks now, such as ones never pushed or ones a reset dropped. An empty list means the push failed for another reason, such as a fork refusing force pushes or a failed connection. Whatever the list shows, stop and ask the maintainer, showing it. Never push the branch again in the meantime. A repeat of the pinned push is refused again. An unpinned `--force-with-lease` after that fetch, or a bare `--force`, overwrites whatever the fork gained. Never open a fresh pull request from a new dirty branch either, since that drops the iteration log this workflow keeps. The rebased local branch and the backup branch both stay as they are until he answers.

For example, the fork's mirror branch and upstream both sit at commit `A`, with the iteration PR
from `work/x` into the mirror branch green. Upstream advances to `B`. The fork syncs its mirror
branch to `B`, merges `B` into `work/x`, and fixes the breakage in the iteration PR. Had `work/x`
been merged into the mirror branch at `A`, the mirror branch could not have fast-forwarded to `B`.

The contribution ends once the PR of every clean branch the dirty branch feeds is merged or
closed, with no slice left to cut. Only then close the iteration PR, unmerged, and delete the dirty and clean branches. Leave each backup branch for the maintainer to delete. A merged change then reaches the
mirror branch through the next sync from upstream, the same way anyone else's change does.

## Use the upstream repo's own conventions, not the fleet's

Always use the upstream repo's own issue and PR templates, contribution guidelines, and
commit-message and code-style conventions when they differ from this fleet's. The fleet's
`comment-and-doc-style`, `git-commit-conventions`, and `pr-review-conduct` skills describe how
*this fleet* does things, and none of them are the target repository's own rules. Read the target
repo's `CONTRIBUTING.md` (or equivalent) and follow it. Where the target repo states no
convention of its own, matching the surrounding code's existing style in that file is the better
default. Falling back to the fleet's own convention by habit is not.

## What stays governed by the fleet's own rules

Signing commits and using the correct git identity are host configuration, not project
convention. So `git-commit-conventions`'s signing and identity rules still apply on both the dirty
and clean branches. They are properties of the committer, not of the target repository. The
write-safety rules (never write to a repository outside explicit authorization, never fabricate a
GitHub id) also still apply in full. A fork the maintainer owns is within scope to push to.
The upstream repository itself is written to only through the PR the maintainer explicitly asked
for.

The fork is not a fleet repository. It carries none of the fleet's instruction files or repository
settings, and a resync or the fleet label set is never applied to it. A session working
the contribution can still keep its state in the `session-handoff` chain on the fork. That needs
issues turned on and the one `handoff` label, and nothing else of the fleet's
configuration. The hub's `scripts/handoff.py new
--create-label` creates that label on a fork under the fleet's owner.
