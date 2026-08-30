---
name: upstream-contribution-workflow
description: >-
  Governs how the maintainer contributes to a third-party repository he does not control (for
  example esphome/esphome), distinct from the fleet's own internal branching model: a dirty work
  branch on his own fork for the actual work and review iteration, squashed once clean to a second
  branch that carries only the intended minimal history, that clean branch opened as the PR
  against the upstream repo, and reviewer feedback applied to the dirty branch first, then
  re-squashed into the clean one. Use this whenever about to open a pull request against a
  repository outside the ptr727 fleet, whenever forking a third-party project to contribute a fix
  or feature, whenever an upstream reviewer requests changes on a PR opened this way, and whenever
  deciding which issue or PR template to use for a third-party repository. Triggers regardless of
  the target repo's own type or workflow model, since this skill is about the shape of a
  contribution to someone else's repo, not the target repo's own internal conventions, which this
  skill does not attempt to state and are never assumed to match the fleet's.
---

# Upstream Contribution Workflow

## Why this exists

The fleet's own branching model (`operational-vs-release-workflow`) governs repos the maintainer
controls end to end: squash-only feature branches, merge-commit promotions, signed commits under
his own identity. None of that applies to someone else's repository. A PR into a third-party
project answers to that project's own maintainers, on their own timeline, with their own review
cycles, and the history that lands there should read as a deliberate, minimal contribution, not as
the maintainer's own iteration log. This skill is that different shape, kept separate from the
fleet's internal model so the two are never conflated.

## The two-branch shape

1. **Fork the upstream repo**, if not already forked.
2. **Do the actual work on a dirty work branch**, on the maintainer's own fork. This branch is
   allowed to be messy: false starts, fixup commits, back-and-forth in response to review, whatever
   the real work looks like while it's happening. Open a PR from this branch into a branch on the
   maintainer's **own fork** (not upstream), so all the iteration happens there, visible and
   reviewable, without touching the upstream repo at all.
3. **Once the dirty branch is clean and the change is ready, squash it to a second branch** that
   carries only the intended, minimal commit history, one commit (or a small, deliberate set) that
   states what the change is, not how it was arrived at.
4. **Open the PR against the upstream repo from that second, clean branch.** This is the only
   branch upstream ever sees. An upstream draft may be opened only after that clean presentation
   branch exists and is published. When more preparation is needed, continue on the dirty branch,
   re-squash it into the clean branch, and update the same draft by the step 5 procedure. Never
   iterate directly on the published presentation branch. Mark the draft ready when preparation
   finishes. Open it ready immediately when no preparation remains.
5. **If upstream reviewers ask for changes, apply them to the dirty branch first**, iterate there
   the same way as step 2, then re-squash the updated dirty branch into the clean branch that
   actually reaches upstream. Updating the same upstream PR rather than opening a new one each
   round rewrites the clean branch's history, and pushing a rewritten branch that is already
   published requires `git push --force-with-lease` (prefer it over a bare `--force`, it refuses
   the push if the remote moved since the last fetch). **`git-commit-conventions`'s never-force-push
   rule governs this fleet's own repos, where a branch is shared with bots, other branches, and
   required-check history a rewrite would orphan, and it stays absolute there, with no exception.
   It has no jurisdiction here**: this clean presentation branch lives on the maintainer's own
   fork, outside the fleet entirely, and carries nobody's work but this squash. Force-with-lease
   is scoped just as tightly regardless: only this one branch, only on the maintainer's own fork,
   never the dirty work branch, which is the append-only iteration log this whole workflow exists
   to preserve. If force-with-lease is ever refused or unavailable, open a fresh PR from a newly
   named clean branch rather than fighting the push.

**The dirty branch is always the working copy. The clean branch is always the presentation copy.**
Never reverse this: never iterate directly on the branch that's open against upstream, and never
skip the squash step because the dirty branch "looks clean enough."

## Use the upstream repo's own conventions, not the fleet's

Always use the upstream repo's own issue and PR templates, its own contribution guidelines, and
its own commit-message and code-style conventions when they differ from this fleet's. The fleet's
`comment-and-doc-style`, `git-commit-conventions`, and `pr-review-conduct` skills describe how
*this fleet* does things, and none of them are the target repository's own rules. Read the target
repo's `CONTRIBUTING.md` (or equivalent) and follow it. Where the target repo states no
convention of its own, matching the surrounding code's existing style in that file is the better
default, not falling back to the fleet's own convention by habit.

## What stays governed by the fleet's own rules

Signing commits and using the correct git identity are host configuration, not project
convention, so `git-commit-conventions`'s signing and identity rules still apply on both the dirty
and clean branches. They are properties of the committer, not of the target repository. The
write-safety rules (never write to a repository outside explicit authorization, never fabricate a
GitHub id) also still apply in full. A fork the maintainer owns is within scope to push to, and
the upstream repository itself is written to only through the PR the maintainer explicitly asked
for.
