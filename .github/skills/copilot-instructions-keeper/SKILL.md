---
name: copilot-instructions-keeper
description: >-
  Helps keep a repo's .github/copilot-instructions.md in sync with the ptr727/ProjectTemplate hub
  canonical, and stops the one mistake specific to this file: silently wiping its repo-local
  "Disproved Claims" ledger entries during a resync. Use this whenever about to edit, overwrite,
  re-vendor, or carry .github/copilot-instructions.md into a repo, whenever checking a repo for
  drift against the hub or running a conformance sweep that touches this file, whenever GitHub
  Copilot's review mechanics in this file look stale, wrong, or missing something the fleet
  runbook should cover, or whenever standing up a new repo and carrying this file for the first
  time. Also triggers on "why isn't the audit catching that this file is out of date," since the
  fleet's mechanical audit checks this file, at intent fidelity, for file presence and each named
  section's heading, never for content drift inside a section, so nothing else notices a stale
  section here except a live check like this one.
---

# Copilot Instructions Keeper

## Why this exists

`.github/copilot-instructions.md` is read directly by GitHub Copilot and bootstraps the shared
`AGENTS.md` instruction set and review-focused skills. Its Copilot-specific rules stay fully
intact in every repo that carries it. This skill maintains that carried copy, it does not replace
the bootstrap.

`spec/files.json` declares it `intent` fidelity, `whole: true`, covering four named sections
(`Commit Messages and Pull Request Titles`, `Reviewing Carried Fleet Content`, `GitHub Copilot
Review Runbook`, `When in Doubt`), with `<owner>`, `<repo>`, and `<N>` placeholders filled per
repo. **The fleet audit checks an `intent` file for file presence and each named section's
heading, never for content drift inside a section.** A section that is present but has fallen out
of date against the hub, the exact gap this skill exists to catch, produces no finding anywhere in
the mechanical audit. Noticing that has to happen in a live session like this one.

## The one thing this file has that others don't: repo-local ledger entries

The file's own "Disproved Claims" section states its rule plainly. **The section's shape and
governing rules are carried, but its entries are not.** Each entry records a finding that was
raised against this specific repository and disproved against this repository's code at a named
revision. A repository carrying a copy of this file carries the shape and rules, deletes any
entry whose subject it does not hold, and records what it has proved for itself.

This means a blind re-vendor of the hub's canonical `.github/copilot-instructions.md` over a downstream
repo's copy is wrong in both directions:

- Copying the hub's own "Disproved Claims" entries (about `ProjectTemplate` itself) into a
  downstream repo attaches proofs about code that repo does not carry.
- Overwriting a downstream repo's copy wholesale deletes any entries that repo itself has earned,
  a live disproof, run against that repo's own tree, thrown away with no record.

**Before touching this file in any repo other than the hub itself:**

1. Read the current "Disproved Claims" section in that repo's copy, if it has one, and preserve
   every entry that names a file or behavior that repo actually carries.
2. Update everything else, the runbook mechanics, the four named sections, the rule text, to
   match the hub canonical.
3. Never carry the hub's own repo-specific "Disproved Claims" entries downstream. They name
   `ProjectTemplate`'s own files and revisions, not the target repo's.
4. If in doubt whether an entry is still valid for the current tree, treat it per the guard skill
   below rather than guessing.

The `carried-instruction-file-guard` skill stops this same failure class for `AGENTS.md`,
`GOVERNANCE.md`, `CODESTYLE.md`, and `WORKFLOW.md`: a
routine-sounding overwrite silently deleting content that is not a stale copy of the hub. Run
that skill's distinctive-phrase probe against this file too before any full-file replace. It is
not in that skill's own file list because its failure mode, ledger entries rather than fleet
rules, is specific enough to warrant its own skill, but the underlying discipline, probe before
overwrite, give a local addition a destination rather than deleting it, is the same.

## Checking a repo's copy for drift

1. Fetch the hub (`github.com/ptr727/ProjectTemplate`) `main` branch fresh. A stale local clone
   answers confidently instead of failing.
2. Compare the target repo's `.github/copilot-instructions.md` against the hub's, section by
   section, at **intent** fidelity, judged by meaning, not by byte match. A content-identical
   file with different `<owner>`/`<repo>` placeholder fills is current, not drifted.
3. Read the "Disproved Claims" section separately from the rest. Judge its **shape and rules**
   against the hub, and judge its **entries** only against what that repo itself carries (see
   above), never against the hub's own entries.
4. Report what is actually stale (a runbook mechanic that changed, a rule that moved, a new
   section) versus what only looks different because it is correctly repo-specific.

## Carrying it fresh, new repo or full resync

Follow `RESYNC.md`'s general apply order for carried files, with the ledger rule above applied at
the point this file is touched: carry the hub's current rule text and runbook mechanics, keep the
target repo's own "Disproved Claims" entries (if any existed pre-resync) rather than replacing
them with the hub's, and start a new repo's ledger empty rather than seeded from the hub's own
proofs.

## What this skill does not cover

Content-style rules for other carried files (`AGENTS.md`, `GOVERNANCE.md`, `CODESTYLE.md`,
`WORKFLOW.md`) are `carried-instruction-file-guard`'s job. The review-loop contract this file's
runbook implements, the merge gate, triage, escalation, is `pr-review-conduct`'s job. This skill
is narrowly about keeping this one file's carried copy correct.
