---
name: audit-a-repo
description: >-
  Drives AUDIT.md's read-only measurement of a named ptr727 fleet repo against the fleet ground truth. It ends in a committed report, never an edit to the repo being measured. Use this whenever asked to audit, measure, or verify conformance of a named repo. Use it also whenever asked to judge a conformance claim someone else made, or to decide whether an onboarding is actually complete. Run from a hub checkout of ptr727/ProjectTemplate against the named target. Triggers even when the repo believes it is conformant, because conformance asserted without a committed report is conformance nobody can check. It is also because that case, a repo believing it is conformant, is the one most often skipped. This completes the procedure triangle: standup-a-repo creates a repo, resync-a-repo applies findings to one already stood up, and this skill measures. By contrast, check-this-repo is the in-repo self-check with no named target and no standing hub checkout. AUDIT.md keeps authority over the procedure, this skill is the summary that routes into it.
---

# Audit a Repo

## Why This Exists

The audit is the fleet's measurement procedure. The failure shapes it guards against are all silent. They are a repo judged conformant with no committed evidence, and an audit that quietly edits what it was supposed to measure. `AUDIT.md` in the hub is the procedure and keeps authority. This skill carries the rules that get skipped in practice and says which section owns each step.

## Before Measuring Anything

- **Route first.** A repo with no carried instruction set, or a partial one, has a baseline that never arrived rather than drift to report (`AUDIT.md` section 0). It therefore goes to `STANDUP.md` sections 1A and 2 first (`AUDIT.md` section 0). Auditing it anyway produces a report that is all absences and reads as catastrophe.
- **Verify the host.** Run `python3 scripts/host_gate.py --repo <target-checkout>` from the hub checkout before any hub tool, and pass `--repo`, since a bare run skips the target's own `host-tools.json` overlay. A stale tool answers `--version`, looks healthy, and produces a wrong answer.
- **Read `main` as ground truth**, for every workflow model, and read `develop` only to detect divergence (`AUDIT.md` section 1). An `operational` repo's `develop` is mid-flight by design. Conformance work sitting there is therefore un-promoted work, not a defect, and it counts when it reaches `main`. Use `uv run --no-project --script spec/audit.py --branch <ref>` to preview in-flight work, which stamps the override so the finding cannot be mistaken for one against ground truth.

## Measuring

- **Resolve the repo's types from `registry/repos.json`** and classify a `classificationPending` entry from the tree (`AUDIT.md` section 2). The applicability gate is `WORKFLOW.md` section 1, extended to `AUDIT.md`'s own checks (`AUDIT.md` section 3). An item or check governing an absent construct is N/A, excluded from the verdict, and never a defect (`AUDIT.md` section 3).
- **Know what the runner does and does not prove.** `spec/audit.py` mechanizes the deterministic subset only: settings, rulesets, secret names, file and section presence, verbatim hashing, interface wiring, Dependabot coverage, branch facts. It evaluates no check under a type in `spec/project-types.json` (`AUDIT.md` section 4). So every per-type check is judged by hand, and a clean run is no evidence for them (`AUDIT.md` section 4). Silence from a tool that was never looking reads exactly like a pass.
- **Judge letter and intent per check** and keep the vocabulary. A letter miss with intent satisfied is a drift finding. Both missing is a defect. Operational is binary over the applicable set. That vocabulary is in `AUDIT.md` sections 4 and 7. Do not invent a parallel scheme.
- **Assert the Actions implement `WORKFLOW.md`** by outcome, not by matching catalog snippets byte for byte (`AUDIT.md` section 5). The method is the 5A static audit, each applicable guarantee cited in the form 5A sets out, then the 5B trace scenarios (`AUDIT.md` section 5). Read a workflow the repo only calls at the SHA it pins, throughout. The `workflow-ci-contract` skill summarizes that contract.
- **Check live settings, rulesets, and secrets from a hub checkout at `main`** with `AUDIT.md` section 6. Run `repo-config/configure.sh check` with the target repository and model for settings and rulesets, and `uv run --no-project --script spec/audit.py [RepoName]` for secrets, rather than constructing a local comparison. The hub payloads are the only repository-configuration source.

## Reporting

- **Write `reports/<repo>/audit.md` from `reports/_template.md`**, findings ranked most severe first, each with the `file:line` it was judged against, and quote the run stamp. The stamp is quoted because findings are a point-in-time snapshot. All of this is in `AUDIT.md` section 8.
- **The hub authors the report.** A downstream repo never opens a hub pull request to write its own, which would be self-certification. Downstream context goes into issues filed against the hub instead.
- **Generate a convergence issue, never compose one**: `uv run --no-project --script spec/audit.py --issue <repo>` emits it from live findings. An agent picking such an issue up re-runs the audit first and acts on the live result, not the pasted findings.
- **Reconcile registry `driftNotes` in the same pass**: a resolved deviation's note is deleted, not left describing finished work. A note naming a check id is retired by a person, not by a run. All of this is in `AUDIT.md` section 8.
- **Stale-versus-modified classification and the intent staleness advisory need a full hub clone with git history.** In a shallow clone the audit reports one ERROR per repo and exits non-zero, so run `git fetch --unshallow origin` in the hub checkout first. To verify one finding without history, compare against the current hub canonical on `main`, which decides current-match only.

## After the Report

Measuring and fixing are separate phases. Converging is `AUDIT.md` section 10. In that section, fixes ship as pull requests on the target repo, one focused pull request per drift class. In that section too, the Copilot loop is driven to green per the `pr-review-conduct` skill, and the maintainer merges. For a repo already stood up, `RESYNC.md` sequences the findings, since order matters (a deletion lands before the re-vendor that would refresh it). Systemic drift shared by many repos is fixed in the hub spec, not hand-patched per repo. Spec questions are escalated rather than resolved silently. Both are in `AUDIT.md` section 9.

An audit leaves no checkout behind it, since one left standing is what a later sweep has to tell apart from live work. Each is removed when the run ends, per `repo-worktree` "Listing and Cleanup". The report's pull request is cleaned up once it merges, with `uv run --no-project --script scripts/pr_review.py cleanup <number> --repo ptr727/ProjectTemplate --checkout <hub-base-clone>`. A detached checkout at `main` that only read the hub is removed by hand once it is clean.
