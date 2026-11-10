# Testing a Repo's Workflows

The section below is `WORKFLOW.md` section 5, whole. Its items and scenarios answer to the D-guarantees in `WORKFLOW.md` section 4, carried whole in `d-guarantees.md` beside this file.

## The Test Methodology

<!-- include: WORKFLOW.md > 5. Test Methodology -->

An agent verifies a project in escalating modes, then renders a verdict. **Skip N/A items** (`WORKFLOW.md` section 1): a guarantee or scenario for an absent construct is recorded N/A, not failed.

### 5A. Static Audit (No Execution)

Assert the structural fact each *applicable* D-guarantee implies, and record **pass**, **fail**, or **N/A** per item. This section says how an audit is run and recorded rather than what must hold. A guarantee names its own constructs, and the requirement is `WORKFLOW.md` section 4's item together with whatever that item defers to.

Most of the evidence is in the workflow files and the composite actions they reach. Where a guarantee's evidence lies outside them, in practice it lies in one of these places. They are the repo's branch ruleset, its Actions and Dependabot secret names, a workflow the repo only calls, or a project or dependency file. They also include a committed file such as `version.json`, `.github/dependabot.yml`, `global.json`, `codecov.yml`, `.gitignore`, or `.editorconfig`.

Cite what each verdict rests on. That is `file:line` for a file in the audited repo. It is the evidence's own name where a setting, a ruleset, or a secret name rather than a file is the evidence. Where the guarantee binds a workflow or composite action the audited repo only reaches, it is `<owner>/<repo>@<sha>` plus the `file:line` in that repo. The callee is read at the SHA the caller pins. An **N/A** verdict names the absent construct instead, there being no line to cite.

### 5B. End-to-End Trace Scenarios (No Execution, Deterministic from the YAML)

For each *applicable* scenario, evaluate every job's `if:`/`needs:` against the inputs. Emit the predicted **run/skip + version + release + artifact-end-state** table, then compare to the expected. A scenario governing a construct the repo does not contain is N/A, per `WORKFLOW.md` section 1, and an absent trigger is such a construct. Each scenario's trigger belongs to one workflow, so read that workflow's own `on:` block rather than the repo's type. For S1 to S4 the block is the pull request workflow's, and for S5 to S10 and S14 the publisher's. For S11 the block is the upstream tracker's, and for S12 and S13 the deploy workflow's. A publisher carrying only `workflow_dispatch` therefore records S5, S6 and S9 N/A, their push and schedule paths never firing there. A repo with no publisher at all records S5 to S10 and S14 N/A together. A publisher whose schedule never publishes, per D4.1, records the schedule paths of S7 and S9 N/A as well. Where a scenario's path runs through a workflow or composite action the repo only **calls**, trace that callee as the repo reaches it. Read it at the SHA the caller pins rather than at the callee's current default branch, which is the same evidence rule 5A states. Predicting from the callee's `main` predicts a table for YAML the audited repo never runs. A self-repository (`$/`) call or a job-level local (`./`) reusable-workflow call carries no pin of its own. It resolves at the calling workflow file's commit, so it is traced at that file's own commit. That commit is the SHA fixed by the pin that reached that file. Where that file is one of the audited repository's own workflows, it is the audited commit instead. A local (`./`) action reference carries no pin either, but it resolves against whatever the job checked out. So it is traced at that checkout's commit. That is the caller's commit where a reusable workflow called from another repository checks out its caller. Minimum set:

| # | Input | Expected output | Exercises |
| --- | --- | --- | --- |
| S1 | PR touching a build target | `changes` flags it, validation runs, and that target's smoke build runs. No push and **no uploads**. The validate-release job **succeeds**, its check exiting early on smoke per D2.2. Release **skipped**, aggregator **success**. The version is a prerelease. No release and no dangling artifacts | D1, D2.2, D3 |
| S2 | PR changing only docs | smoke-build **skipped**, validation runs, aggregator **success** | D1.1, D1.2, D1.5 |
| S3 | PR changing only `.github/workflows/**` | the filter marks no target -> smoke-build **skipped**, validation runs, aggregator **success** | D1.2, D1.4, D1.5 |
| S4 | PR base = default branch, carrying a build target | smoke versions as prerelease, validate-release **succeeds** with its check exited early per D2.2, so the default-branch arm does **not** fire, aggregator **success**, promotion not blocked | D1.5, D2.2, D3.2 |
| S5 | bot push to `main` not touching a release path (e.g. an Actions bump) | the paths filter excludes it, so nothing publishes | D4.1 |
| S6 | code-affecting **bot** push to `main` (a human push/promotion, or any develop push, does not) | the `plan` job gates it to the App/Dependabot actor, and `main` publishes a release | D3, D4 |
| S7 | publish run (schedule, a bot push to main, or a dispatch) | builds the **one** trigger branch. `main` -> `X.Y.Z`, `prerelease=false`, registry stable, readme run. `develop` -> `X.Y.Z-g<sha>`, `prerelease=true`, registry prerelease. `release-asset-*` is consumed then deleted, and each package build-artifact (`nuget-build-*`, `pypi-build-*`) is deleted after its publish. **No dangling artifacts** | D3, D4, D5, D6, D7 |
| S8 | dispatch from a ref other than `main` or `develop` | **fails fast** | D2.3 |
| S9 | re-run publish on a schedule or push trigger, version unchanged (a dispatch re-run refreshes the release instead, per D4.4) | release-create **skipped**, `release-asset-*` delete **skipped**; NuGet/PyPI pushes no-op (server dedupe); **package build-artifacts still deleted** (their download succeeded); **Docker still re-pushes** the image; no duplicate release | D4.4, D5.2 |
| S10 | branch/version classification disagree | validate-release **fails loud**, build/publish skip | D2.2 |
| S11 | scheduled upstream-version bump (wrapper) | resolver detects a change -> commits the state file -> opens a per-branch bump PR. The merge-bot auto-merges it, or leaves it for the maintainer where the tracker sets `auto-merge: false` (D8.3). The `main` pin then publishes via the gate. A develop pin does not auto-publish, shipping instead via a develop dispatch or promotion | D8.3, D3.5 |
| S12 | deploy dispatch naming an environment | the ref gate runs **first**, allowing production from the default branch only and any ref to a non-production environment. Validation runs, and the callee re-asserts the environment name. A release installs under its own id, and the pointer flips as a separate step. Retention is bounded by whichever of the D5.6 shapes the repo uses. So a deploy whose credential can observe the destination asserts the count converged, and one confined write-only leaves it to the host. The live check asserts the environment and the release id, waiting out the reload, then the URL contract. **No tag and no release are created** | D2.1, D4.6, D5.6 |
| S13 | deploy dispatch of a production environment from a non-default ref | **fails fast**, before anything is installed or written | D2.1 |
| S14 | publish whose branch head gained different `.github/workflows` content after the build began | every update since a release bot's push or merge: release-create **skipped** and the publisher dispatched on the branch. In that case this run ends **cancelled** with its package publish jobs skipped (a Docker image already pushed). The dispatched run releases the head, queueing behind this run only where the head's publisher evaluates this run's group for a dispatch. A push landing after the branch is confirmed unmoved and before the dispatch is released as if it were such an update. That is an accepted, bounded limit per D4.7. Any other update: the step **fails**, nothing is released or dispatched | D4.1, D4.2, D4.5, D4.7 |

### 5C. Live Probe (Where Warranted)

Every probe here that opens a pull request, dispatches a workflow, or re-runs a real publish is the maintainer's to run. The agent prepares the command and reads the result back afterwards. A harness that refuses such a write is the harness working as intended. The refusal is neither re-shaped into a raw API call nor talked around (`GOVERNANCE.md` "Repository Boundaries and Write Safety").

- Open a trivial-change PR touching one target and confirm S1. *Caveat: the Docker leg logs in to the registry even on smoke and reads the buildcache. So it needs `DOCKER_HUB_*` secrets and cannot run on a fork PR (same-repo only).*
- Per registry: after a real publish, query NuGet.org for the expected version + prerelease classification (and the `.snupkg` on the symbol server). Also confirm a re-run added no duplicate. For PyPI read the built `dist/*` filenames out of the build job's log, `.dev0` off `develop` vs a plain version on the default branch.
- Inspect the latest real publish's logs for `PublicRelease`/`SemVer2` per leg and confirm the artifact lifecycle (uploaded, consumed, deleted, with none left behind).
- **The deploy ref gate (S13) is verified only by tripping it.** Dispatch the production environment from a non-default ref and expect the run to fail at the gate. The evidence is the following, and each item matters. It is the gate job's conclusion, and its error text naming the expected and the received ref. It is also every downstream job recorded as **skipped** rather than passed. And it is the production environment's deployment list carrying no deployment from the dispatched ref. Capture all of it, because a gate that fails open and a gate nobody tripped produce the same empty run history. So "we have never seen it fail" is not evidence about the one control standing between a mis-dispatch and the live site. **The agent prepares the command and reads the evidence back afterwards. It does not fire it.** The same split applies to any probe that acts on the deploy host directly, an outbound SSH exercising a forced command among them.

### Assessment

Record the workflow **operational** when every *applicable* 5A item passes and every *applicable* 5B scenario's predicted output equals the expected. The record also requires that no 5C probe that was run contradicts either. N/A items are excluded, never counted as failures. Any *applicable* mismatch is a **defect** -> **not operational**. Procedure:

1. **Audit** with 5A, recording each item's verdict and its evidence in the form 5A sets out.
2. **Trace** the applicable S-scenarios with 5B. Diff predicted vs expected.
3. **Probe** with 5C where a live signal exists that the static trace cannot produce. Where such a signal exists, run the probes that only read, and prepare the writing ones for the maintainer. The probes cover live version classification, registry state, the artifact lifecycle of a real run, and the deploy ref gate.
4. **Verdict:** operational / not operational, with the failing guarantee(s) and the triggering input for each. It also lists the items recorded N/A, and the 5C probes prepared but not run.

`WORKFLOW.md` section 5 keeps the test methodology. The `workflow-ci-contract` Skill carries this section whole as a generated include, at `.agents/skills/workflow-ci-contract/references/test-methodology.md` in the hub. That path is not a repo-relative link, since it is hub-local and not carried into every fleet repo.

<!-- /include -->
