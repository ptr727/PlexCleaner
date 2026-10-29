---
name: upstream-contribution-workflow
description: >-
  Governs how the maintainer contributes to a third-party repository he does not control (for
  example esphome/esphome), distinct from the fleet's own internal branching model: a dirty work
  branch on his own fork for the actual work and review iteration, its fork-internal PR kept open
  and never merged, squashed once clean to a second branch that carries only the intended minimal
  history, that clean branch opened as the PR against the upstream repo, and reviewer feedback
  applied to the dirty branch first, then re-squashed into the clean one. Use this whenever about
  to open a pull request against a repository outside the ptr727 fleet, whenever forking a
  third-party project to contribute a fix or feature, whenever an upstream reviewer requests
  changes on a PR opened this way, whenever a fork-internal iteration PR looks ready to merge or
  close, whenever the upstream base branch moves under a contribution in flight, and whenever
  deciding which issue or PR template to use for a third-party repository. Triggers regardless of
  the target repo's own type or workflow model, since this skill is about the shape of a
  contribution to someone else's repo, not the target repo's own internal conventions, which this
  skill does not attempt to state and are never assumed to match the fleet's.
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
   is scoped just as tightly regardless. It applies only to this one branch, only on the maintainer's own fork,
   and never to the dirty work branch. That branch is the append-only iteration log this whole workflow exists
   to preserve. If force-with-lease is ever refused or unavailable, open a fresh PR from a newly
   named clean branch rather than fighting the push.

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
the dirty branch. In that case, also fix whatever breaks in the iteration PR. Bringing the mirror branch in is a merge rather than a rebase,
since the dirty branch is append-only and is never force-pushed. Then re-squash onto the new mirror
tip whenever the clean branch needs to follow, per step 5.

For example, the fork's mirror branch and upstream both sit at commit `A`, with the iteration PR
from `work/x` into the mirror branch green. Upstream advances to `B`. The fork syncs its mirror
branch to `B`, merges `B` into `work/x`, and fixes the breakage in the iteration PR. Had `work/x`
been merged into the mirror branch at `A`, the mirror branch could not have fast-forwarded to `B`.

The contribution ends when upstream merges or declines the upstream PR. Only then close the
iteration PR, unmerged, and delete the dirty and clean branches. A merged change then reaches the
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
