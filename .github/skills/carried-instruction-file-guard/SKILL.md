---
name: carried-instruction-file-guard
description: >-
  Stops a blind overwrite of a downstream repo's AGENTS.md, GOVERNANCE.md, CODESTYLE.md, or WORKFLOW.md when resyncing or updating it to match the ptr727/ProjectTemplate hub template. Use this whenever about to edit, replace, re-vendor, or sync-to-match-the-hub any of those four files in a repository that is not ProjectTemplate itself, or whenever asked to bring a repo's instruction set up to date, run a conformance sweep, or fix drift against the hub. Triggers even when the request sounds routine, such as copying the hub's AGENTS.md over or resyncing a repo's docs, because that phrasing is exactly how a real incident happened, where a downstream repo's local rules were silently deleted by a full-file overwrite. Do not skip this just because the task looks mechanical.
---

# Carried Instruction File Guard

## Why this exists

A downstream repo's `AGENTS.md`/`GOVERNANCE.md`/`CODESTYLE.md`/`WORKFLOW.md` can hold two different kinds of content mixed in one file: sections that are stale copies of the hub's fleet-wide rules, and local rules the repo wrote for a fault the fleet has never seen elsewhere. Re-vendoring the hub's canonical version over the whole file deletes the second kind silently, because nothing about the diff looks wrong. This has actually happened: a resync replaced a repo's `AGENTS.md` wholesale with the hub's, and the repo's own local additions were gone with no error, no warning, and no review comment calling it out.

The fix is not "be careful." Being careful is what failed the first time. The fix is a mechanical check you run before any overwrite touches one of these four files, every time, regardless of how routine the request sounds.

## Before you touch any of these four files

1. **Check whether the file's content is declared `verbatim` or `intent`.** The hub's `spec/section-model.md` (fetch it from a hub checkout, `github.com/ptr727/ProjectTemplate`, if you don't have one) names, section by section, which parts of `AGENTS.md` and `GOVERNANCE.md` are universal fleet law (safe to byte-match against the hub) and which describe the repo itself (never safe to overwrite from another repo). `CODESTYLE.md` and `WORKFLOW.md` are carried whole at `intent` fidelity, judged by meaning, not hashed.
2. **If any part of the file is `intent`, or if the file predates a clean split into hub-governed sections, do not diff-and-replace. Probe instead.** For each rule or paragraph in the current file that is not obviously boilerplate:
   - Pick the phrase in it that is most peculiar to this repo, not generic governance vocabulary. A rule about "always sign commits" is generic. A rule about "this repo's Docker image pins Alpine 3.19 because 3.20 broke the s6 supervisor" is peculiar.
   - Grep the hub's canonical copy of the same file for that peculiar phrase.
   - **Absent from the hub canonical means it is a local addition.** It is never dropped because it looks similar to something else, and never dropped because a merge or overwrite would be simpler without it.
3. **A local addition found by the probe gets a destination, not a deletion.** Either it names a rule that should apply fleet-wide (flag it for the maintainer to promote into the hub), or it is genuinely specific to this repo and moves to the repo's own topical doc before the carried file is touched: `CODESTYLE.md` for a language/formatting convention, `ARCHITECTURE.md` for a design decision, `OPERATIONS.md` for a runbook or operational note, `TODO.md` for backlog. Move it, confirm it is not lost, and only then proceed with the carry.
4. **Do not trust a similarity or word-overlap check for step 2.** A repo-specific rule written in ordinary governance language reads as a reworded duplicate of an unrelated hub rule to that kind of check, and it will confidently tell you the local content is redundant when it is not. Exact phrase presence or absence is the only check that has held up.

## What is actually safe to overwrite without this procedure

A section `spec/section-model.md` names as `verbatim`, in a file that is already cleanly split (the file carries only that declared section, nothing else mixed in), can be re-vendored directly: byte-matching it against the hub canonical is the point of `verbatim` fidelity, and the audit already checks it that way. The guard above is for everything else: `intent`-fidelity content, a file that has not been split yet, or any file you are not certain is clean.

## If you are not sure which case you are in

Stop and say so, rather than guessing. Naming the uncertainty costs one sentence. Silently overwriting the wrong thing costs someone's local rules with no way to notice until much later.
