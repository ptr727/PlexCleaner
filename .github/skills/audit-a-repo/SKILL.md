---
name: audit-a-repo
description: >-
  Drives AUDIT.md's read-only measurement of a named ptr727 fleet repo against the fleet ground truth, ending in a committed report, never an edit to the repo being measured. Use this whenever asked to audit, measure, or verify conformance of a named repo, to judge a conformance claim someone else made, or to decide whether an onboarding is actually complete. Run from a hub checkout of ptr727/ProjectTemplate against the named target. Triggers even when the repo believes it is conformant, because conformance asserted without a committed report is conformance nobody can check, and that is the case most often skipped. This completes the procedure triangle: standup-a-repo creates a repo, resync-a-repo applies findings to one already stood up, and this skill measures, while fleet-conformance-check is the in-repo self-check with no named target and no standing hub checkout. AUDIT.md keeps authority over the procedure, this skill is the summary that routes into it.
---

# Audit a Repo

## Why This Exists

The audit is the fleet's measurement procedure, and the two failure shapes it guards against are both silent: a repo judged conformant with no committed evidence, and an audit that quietly edits what it was supposed to measure. `AUDIT.md` in the hub is the procedure and keeps authority. This skill carries the rules that get skipped in practice and says which section owns each step.

## Before Measuring Anything

- **Route first.** A repo with no carried instruction set, or a partial one, has a baseline that never arrived rather than drift to report, so it goes to `STANDUP.md` sections 1A and 2 first (`AUDIT.md` section 0). Auditing it anyway produces a report that is all absences and reads as catastrophe.
- **Verify the host.** Run `python3 scripts/host_gate.py --repo <target-checkout>` from the hub checkout before any hub tool, and pass `--repo`, since a bare run skips the target's own `host-tools.json` overlay. A stale tool answers `--version`, looks healthy, and produces a wrong answer.
- **Read `main` as ground truth**, for both workflow models, and read `develop` only to detect divergence (`AUDIT.md` section 1). An `operational` repo's `develop` is mid-flight by design, so conformance work sitting there is un-promoted work, not a defect, and it counts when it reaches `main`. Use `spec/audit.py --branch <ref>` to preview in-flight work, which stamps the override so the finding cannot be mistaken for one against ground truth.

## Measuring

- **Resolve the repo's types from `registry/repos.json`** and classify a `classificationPending` entry from the tree (`AUDIT.md` section 2). The applicability gate is `WORKFLOW.md` section 1: a check governing an absent construct is N/A, excluded from the verdict, and never a defect (`AUDIT.md` section 3).
- **Know what the runner does and does not prove.** `spec/audit.py` mechanizes the deterministic subset only: settings, rulesets, secret names, file and section presence, verbatim hashing, interface wiring, Dependabot coverage, branch facts. It evaluates no check under a type in `spec/project-types.json`, so every per-type check is judged by hand, and a clean run is no evidence for them (`AUDIT.md` section 4). Silence from a tool that was never looking reads exactly like a pass.
- **Judge letter and intent per check** and keep the vocabulary: letter miss with intent satisfied is a drift finding, both missing is a defect, and operational is binary over the applicable set (`AUDIT.md` sections 4 and 7). Do not invent a parallel scheme.
- **Assert the Actions implement `WORKFLOW.md`** by outcome, not by matching catalog snippets byte for byte: the 5A static audit with a `file:line` citation per applicable guarantee, then the 5B trace scenarios (`AUDIT.md` section 5). The `workflow-ci-contract` skill summarizes that contract.
- **Check live settings, rulesets, and secrets from a hub checkout at `main`** with `AUDIT.md` section 6. Run `repo-config/configure.sh check` with the target repository and model for settings and rulesets, and `spec/audit.py [RepoName]` for secrets, rather than constructing a local comparison. The hub payloads are the only repository-configuration source.

## Reporting

- **Write `reports/<repo>/audit.md` from `reports/_template.md`**, findings ranked most severe first, each with the `file:line` it was judged against, and quote the run stamp, since findings are a point-in-time snapshot (`AUDIT.md` section 8).
- **The hub authors the report.** A downstream repo never opens a hub pull request to write its own, which would be self-certification. Downstream context goes into issues filed against the hub instead.
- **Generate a convergence issue, never compose one**: `spec/audit.py --issue <repo>` emits it from live findings. An agent picking such an issue up re-runs the audit first and acts on the live result, not the pasted findings.
- **Reconcile registry `driftNotes` in the same pass**: a resolved deviation's note is deleted, not left describing finished work, and a note naming a check id is retired by a person, not by a run (`AUDIT.md` section 8).
- **Stale-versus-modified classification needs a full hub clone with git history.** Without one, compare against the current hub canonical on `main`, which decides current-match only.

## After the Report

Measuring and fixing are separate phases. Converging is `AUDIT.md` section 10: fixes ship as pull requests on the target repo, one focused pull request per drift class, the Copilot loop driven to green per the `pr-review-conduct` skill, and the maintainer merges. For a repo already stood up, `RESYNC.md` sequences the findings, since order matters (a deletion lands before the re-vendor that would refresh it). Systemic drift shared by many repos is fixed in the hub spec, not hand-patched per repo, and spec questions are escalated rather than resolved silently (`AUDIT.md` section 9).
