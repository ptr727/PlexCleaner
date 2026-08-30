---
name: standup-a-repo
description: >-
  Drives STANDUP.md's procedure for taking a ptr727/ProjectTemplate fleet repo from nothing (or a
  partial state) to operational against the fleet ground truth, run from a hub checkout for a
  named target repo the maintainer is standing up. Use this whenever asked to stand up, create,
  bootstrap, or onboard a new fleet repo, or to onboard a new repo type. Needs a hub checkout and
  a target repo, new or partially started, to mean anything, so it does not usefully trigger
  inside an already-operational downstream repo's own session with no hub checkout present, that
  case is resync-a-repo for drift or fleet-conformance-check for a self-check instead. Triggers
  even when the request sounds like "just copy the template over" or "spin up a quick repo,"
  because skipping the ordered signing, branch, and instruction-set steps below is exactly how a
  repo ends up unsigned, unrecoverable, or authored against unknown rules.
---

# Stand Up a Repo

## Why this exists

STANDUP.md's own section order exists because several of its steps close a window that cannot be
reopened cheaply: commit signing has to be correct before the first commit, the long-lived
branches have to exist before any standup commit lands on one, and the instruction set has to be
carried before anything else is authored against it. This skill exists so that order survives
contact with a real, time-pressured standup instead of depending on an agent remembering to run
each gate unprompted. It is a driver over STANDUP.md, not a replacement for it. Read STANDUP.md
itself for the full text of every step, the onboarding-a-new-repo-type procedure, and the
cold-start self-test.

## Before starting

Read STANDUP.md section 0A first. Nothing in this procedure creates the GitHub repository, its
App, or its secrets, each an outward-facing write that needs the maintainer's explicit permission
and inputs, so hand that checklist over before step 1 rather than discovering the gap partway
through. A repo with no remote is not partially stood up, it is not started, and only the
maintainer can supply what section 0A lists.

## Apply, in order

1. **Signing, before the first commit.** STANDUP.md section 0: verify, never set, the inherited
   `--global` commit identity and signing configuration, and the host tool floors via
   `python3 scripts/host_gate.py`. The window closes at the first commit, since a repo committed
   under the wrong identity or unsigned cannot be cleanly repaired afterward.

2. **Branches, before the first standup commit.** STANDUP.md section 0B: create `main` and
   `develop` empty, off one signed empty root commit, then run every step below on a feature
   branch off `develop`. Never commit standup work directly onto `develop`. `non_fast_forward` on
   both branch payloads, or the missing blocking rule on an operational repo's `develop` ruleset,
   makes that mistake either unrecoverable or silently unprotected.

3. **Classify and catalog.** STANDUP.md section 1: resolve the repo's type(s) against `AUDIT.md`
   section 2, then write or repair its `registry/repos.json` entry and confirm it with
   `spec/validate.py`.

4. **The instruction set, before authoring anything.** STANDUP.md section 1A: carry `CLAUDE.md`,
   `AGENTS.md`, `GOVERNANCE.md`, `CODESTYLE.md`, `WORKFLOW.md` and `AUDIT.md`, adapted rather
   than cloned for the ones that describe a repo, plus `.markdownlint-cli2.jsonc` and
   `cspell.json`. `CLAUDE.md` is the fixed, verbatim `@AGENTS.md`-import file that gets
   `AGENTS.md` into a Claude Code session's context at all, a separate baseline entry from
   `AGENTS.md` itself, so carrying one without the other still leaves that provider unconfigured.
   Read `CODESTYLE.md` and the `GOVERNANCE.md` documentation-style rules before writing any repo
   content of your own, the same window-closes shape as signing in step 1.

5. **Capture the source, if one exists.** STANDUP.md section 1B, only when the repo's content
   replaces a live external system: capture it and verify the capture against the source before
   anything is scaffolded from it, since the source is not under version control and cannot be
   re-derived once it stops serving.

6. **The baseline files.** STANDUP.md section 2: copy every `spec/files.json` entry whose
   `appliesTo` matches the repo's selector set, adapted rather than cloned, and choose
   `version.json`'s version floor deliberately rather than propagating the template's. Carry
   `AGENTS.md`'s skill-dependency pointer paragraph, naming `scripts/skills_install.py` and where
   the fleet's Skills live, as one more verbatim unit in this same step, not a separate pass, the
   identical requirement `RESYNC.md` places on a repo already stood up.

7. **The workflows.** STANDUP.md section 3: implement the Actions `WORKFLOW.md` requires for the
   repo's type, reusing `catalog/snippets/workflows/` as the reference implementation rather than
   inventing a shape.

8. **Settings, rulesets, and secrets.** STANDUP.md section 4: confirm the remote and the GitHub
   repository agree before running anything else here, then run
   `repo-config/configure.sh check owner/repo release` (substitute `operational` for an
   operational repo) from the hub at `main`. A non-zero exit there means drift was found, not a
   command failure. Review what it reports. Then run the same command's `apply` subcommand, which
   idempotently reconciles the repo to the full committed configuration regardless of what `check`
   reported, never from a hand-built or carried copy.

9. **Verify with the audit.** STANDUP.md section 5: run `AUDIT.md` end to end. The repo is stood
   up only when it passes for its type, or its residual deltas are tracked in
   `reports/<repo>/audit.md` plus an issue.

## Onboarding a new repo type

When a repo matches no existing type in `spec/project-types.json`, that is a type to onboard, not
a repo to force into the nearest existing one. STANDUP.md's "Onboarding a New Repo Type" section
covers the manifest additions (`spec/project-types.json`, `spec/files.json`, `spec/secrets.json`,
`spec/scope-model.md`, `spec/type-model.md`, and the `registry/repos.schema.json` target enum for
a new publish destination) and the cold-start self-test that proves the result usable by a
context-free agent, not just by the one that wrote it.

## Ship it

One pull request per standup, branched from `develop` per step 2 above, into `develop`, never a
direct push to a protected branch. Close the review loop, per the `pr-review-conduct` skill,
before asking the maintainer for merge permission. The maintainer merges, the agent drives to
green and stops.
