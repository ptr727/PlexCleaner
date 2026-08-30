---
name: resync-a-repo
description: >-
  Drives RESYNC.md's procedure for bringing a ptr727/ProjectTemplate fleet repo that is already
  stood up back into line with the current hub, run from a hub checkout against a named target
  repo. Use this whenever asked to resync, sync, converge, or bring a specific repo up to date
  with the hub, or to run a conformance sweep against a named repo and apply what it finds. Needs
  a hub checkout and a named target repo to mean anything, so it does not usefully trigger from
  inside a downstream repo's own session with no target named and no hub checkout present, that
  case is fleet-conformance-check instead. Triggers even when the request sounds routine, such as
  "just copy AGENTS.md over" or "make repo X match the hub," because that phrasing is exactly how
  the AGENTS.md-overwrite incident happened.
---

# Resync a Repo

## Why this exists

RESYNC.md's own apply order already sequences the remedies so the rules land before the files
they govern and a deletion lands before the re-vendor that would otherwise refresh it. The
AGENTS.md-overwrite incident happened inside that same procedure, on the step that looked most
routine. This skill exists so the mandatory check survives contact with a real, time-pressured
resync instead of depending on an agent remembering to run it unprompted. It is a driver over
RESYNC.md, not a replacement for it. Read RESYNC.md itself for the deletion sweep, the
letters-versus-drift routing, the settings and ruleset step, and everything else that does not
change from one resync to the next.

## Confirm the procedure before starting

Read RESYNC.md section 0. A repo with no instruction set at all, or a partial one, is not this
skill's job, it is STANDUP.md sections 1A and 2 instead, since an absent carried file is a
baseline that never arrived rather than drift to converge. Run `spec/audit.py <RepoName>`, the
target's `registry/repos.json` `name` field rather than an `owner/repo` slug or a checkout path,
and read whether the findings are letters (absent) or drift (present but stale) before doing
anything else. The finding kind names the procedure the repo is owed.

## Reach the hub and measure before changing anything

Fetch a hub checkout of your own immediately before reading it, per RESYNC.md section 1, since a
stale clone answers confidently instead of failing. Never operate against an existing checkout
already present at a known or shared path, the maintainer's own primary checkout included, even
one that looks current -- always fetch into a private worktree of your own, per `repo-worktree`.
On Claude Code this is now also a mechanical stop for most such commands (a `PreToolUse` hook
denies a mutating git operation run directly in a primary checkout), though this prose is still
the only enforcement for a non-Claude-Code agent, and for the narrow shapes the hook itself
exempts, so following it here is not optional even where no hook can catch a lapse. Verify the
host with `python3 scripts/host_gate.py --repo <path-to-target-checkout>`. Then run the audit end
to end,
RESYNC.md section 2, against the target's `main` branch, never `develop`. A finding is a snapshot,
so quote the run stamp in anything derived from it and re-run before acting on a finding read
earlier in the session.
File any hub defect this work exposes against `ptr727/ProjectTemplate`.
Examples include bugs, conflicting sources, unclear or incomplete instructions, missing capabilities, and Copilot findings about any of them.
Search open and closed issues first, then update the matching issue or file a new one.
Preserve the evidence RESYNC.md section 2 requires, and do not leave the finding only in chat, a review thread, the downstream repo, or agent memory.

## Apply, in this order

1. **The instruction set first.** `CLAUDE.md`, then `AGENTS.md` and `GOVERNANCE.md` verbatim sections, then `CODESTYLE.md` and `WORKFLOW.md`, including the `AGENTS.md` skill-dependency pointer paragraph (naming `scripts/skills_install.py` and where the fleet's Skills live) as one more verbatim unit carried in this same step, not a separate pass. `CLAUDE.md` is the single `@AGENTS.md`-import file that gets `AGENTS.md` into a Claude Code session's context at all, a separate baseline entry from `AGENTS.md` itself, so carrying one without the other still leaves that provider unconfigured. **Before touching `AGENTS.md`, `GOVERNANCE.md`, `CODESTYLE.md`, or `WORKFLOW.md` in this step, run the `carried-instruction-file-guard` skill's distinctive-phrase probe against the target file, every time, without exception, regardless of whether the update is a verbatim re-vendor or an intent-fidelity edit.** This is not advisory language to weigh against how routine the diff looks, a diff that looks routine is exactly the shape the AGENTS.md-overwrite incident took. Do not proceed to the re-vendor until the probe has run and any local addition it finds has a destination, per that skill's own procedure. `CLAUDE.md` is outside that guard's scope: it carries no mixed or repo-specific content by design, so its re-vendor is an ordinary verbatim-fidelity copy, no probe needed.
2. **Deletions second, before any re-vendor.** Only a `retire` disposition in
   `spec/divergences.json` authorizes removing a file, and the removal is swept tree-wide, per
   RESYNC.md section 4, before the deletion counts as done.
3. **Verbatim re-vendors** for `CLAUDE.md` and everything else the probe in step 1 cleared. A
   finding classified modified rather than stale gets its diff read before being overwritten,
   since it may be an improvement the hub should adopt instead of a mistake to erase.
4. **Interface workflows.** Honor the named contract, required jobs, the ruleset-bound check name,
   the artifact-name handoff, rather than copying bytes.
5. **Settings, rulesets, and secrets.** Run
   `repo-config/configure.sh check "<owner>/<repo>" release` (substitute `operational` for an
   operational repo) from the hub at `main`, then `apply` for what it reports, never from a
   carried copy. Run `spec/audit.py [RepoName]` from the same checkout for secrets.
6. **Intent files last, and by hand,** since nothing mechanical judges these.

Reconcile the registry entry (`status`, `types`, `releaseTrigger`, `workflowModel`,
`driftNotes`) in the same pass, and delete a `driftNote` describing work this pass just finished
rather than leaving it standing.

## Ship it

One focused pull request per drift class, branched from the target's `develop`, never a direct
push to a protected branch and never a hand edit outside a pull request. Close the review loop,
per the `pr-review-conduct` skill, before asking the maintainer for merge permission. The
maintainer merges, the agent drives to green and stops. Re-run the audit after the merge and
commit the report once authorized, per `git-commit-conventions`, done means measured, not
applied.
