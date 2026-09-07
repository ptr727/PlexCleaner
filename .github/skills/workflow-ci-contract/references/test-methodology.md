# Testing a Repo's Workflows

The section below is `WORKFLOW.md` section 5, whole. Its items and scenarios answer to the D-guarantees in `WORKFLOW.md` section 4, carried whole in `d-guarantees.md` beside this file.

## The Test Methodology

<!-- include: WORKFLOW.md > 5. Test Methodology -->

An agent verifies a project in three escalating modes, then renders a verdict. **Skip N/A items** (`WORKFLOW.md` section 1): a guarantee or scenario for an absent construct is recorded N/A, not failed.

### 5A. Static Audit (No Execution)

Assert the structural fact each *applicable* D-guarantee implies, and record **pass**, **fail**, or **N/A** per item. This section says how an audit is run and recorded rather than what must hold: a guarantee names its own constructs, and the requirement is `WORKFLOW.md` section 4's item together with whatever that item defers to.

Most of the evidence is in the workflow files and the composite actions they reach. Where a guarantee's evidence lies outside them, it is in practice the repo's branch ruleset, its Actions and Dependabot secret names, a workflow the repo only calls, a project or dependency file, or a committed file such as `version.json`, `.github/dependabot.yml`, `global.json`, `codecov.yml`, `.gitignore`, or `.editorconfig`.

Cite what each verdict rests on. That is `file:line` for a file in the audited repo, its own name where a setting, a ruleset, or a secret name rather than a file is the evidence, and `<owner>/<repo>@<sha>` plus the `file:line` in that repo where the guarantee binds a workflow or composite action the audited repo only reaches, read at the SHA the caller pins. An **N/A** verdict names the absent construct instead, there being no line to cite.

### 5B. End-to-End Trace Scenarios (No Execution, Deterministic from the YAML)

For each *applicable* scenario, evaluate every job's `if:`/`needs:` against the inputs and emit the predicted **run/skip + version + release + artifact-end-state** table, then compare to the expected. A scenario governing a construct the repo does not contain is N/A, per `WORKFLOW.md` section 1, and an absent trigger is such a construct. Each scenario's trigger belongs to one workflow, so read that workflow's own `on:` block rather than the repo's type: S1 to S4 the pull request workflow's, S5 to S10 the publisher's, S11 the upstream tracker's, and S12 and S13 the deploy workflow's. A publisher carrying only `workflow_dispatch` therefore records S5, S6 and S9 N/A, their push and schedule paths never firing there, and a repo with no publisher at all records S5 to S10 N/A together. Where a scenario's path runs through a workflow or composite action the repo only **calls**, trace that callee as the repo reaches it, read at the SHA the caller pins rather than at the callee's current default branch, which is the same evidence rule 5A states. Predicting from the callee's `main` predicts a table for YAML the audited repo never runs. A local (`./`) or self-repository (`$/`) call carries no pin of its own and runs at the workflow commit, so it is traced at whatever SHA the outermost pinning caller fixed. Minimum set:

| # | Input | Expected output | Exercises |
| --- | --- | --- | --- |
| S1 | PR touching a build target | `changes` flags it; validation runs; that target's smoke build runs; no push, **no uploads**; validate-release **succeeds**, its check exiting early on smoke per D2.2; release **skipped**; aggregator **success**; version = prerelease; no release; no dangling artifacts | D1, D2.2, D3 |
| S2 | PR changing only docs | smoke-build **skipped**, validation runs, aggregator **success** | D1.1, D1.2, D1.5 |
| S3 | PR changing only `.github/workflows/**` | the filter marks no target -> smoke-build **skipped**, validation runs, aggregator **success** | D1.2, D1.4, D1.5 |
| S4 | PR base = default branch, carrying a build target | smoke versions as prerelease, validate-release **succeeds** with its check exited early per D2.2, so the default-branch arm does **not** fire, aggregator **success**, promotion not blocked | D1.5, D2.2, D3.2 |
| S5 | bot push to `main` not touching a release path (e.g. an Actions bump) | the paths filter excludes it, so nothing publishes | D4.1 |
| S6 | code-affecting **bot** push to `main` (a human push/promotion, or any develop push, does not) | the `plan` job gates it to the App/Dependabot actor, and `main` publishes a release | D3, D4 |
| S7 | publish run (schedule, a bot push to main, or a dispatch) | builds the **one** trigger branch: `main` -> `X.Y.Z`, `prerelease=false`, registry stable, readme run; `develop` -> `X.Y.Z-g<sha>`, `prerelease=true`, registry prerelease; `release-asset-*` consumed-then-deleted; each package build-artifact (`nuget-build-*`, `pypi-build-*`) deleted after its publish; **no dangling artifacts** | D3, D4, D5, D6, D7 |
| S8 | dispatch from a ref other than `main` or `develop` | **fails fast** | D2.3 |
| S9 | re-run publish on a schedule or push trigger, version unchanged (a dispatch re-run refreshes the release instead, per D4.4) | release-create **skipped**, `release-asset-*` delete **skipped**; NuGet/PyPI pushes no-op (server dedupe); **package build-artifacts still deleted** (their download succeeded); **Docker still re-pushes** the image; no duplicate release | D4.4, D5.2 |
| S10 | branch/version classification disagree | validate-release **fails loud**, build/publish skip | D2.2 |
| S11 | scheduled upstream-version bump (wrapper) | resolver detects a change -> commits the state file -> opens a per-branch bump PR -> the merge-bot auto-merges it, or leaves it for the maintainer where the tracker sets `auto-merge: false` (D8.3) -> the `main` pin publishes via the gate (a develop pin does not auto-publish, shipping instead via a develop dispatch or promotion) | D8.3, D3.5 |
| S12 | deploy dispatch naming an environment | the ref gate runs **first** (production from the default branch only, any ref to a non-production environment); validation runs; the callee re-asserts the environment name; a release installs under its own id; the pointer flips as a separate step; retention is bounded by whichever of the two D5.6 shapes the repo uses, so a deploy whose credential can observe the destination asserts the count converged and one confined write-only leaves it to the host; the live check asserts the environment and the release id, waiting out the reload, then the URL contract; **no tag and no release are created** | D2.1, D4.6, D5.6 |
| S13 | deploy dispatch of a production environment from a non-default ref | **fails fast**, before anything is installed or written | D2.1 |

### 5C. Live Probe (Where Warranted)

Every probe here that opens a pull request, dispatches a workflow, or re-runs a real publish is the maintainer's to run, with the agent preparing the command and reading the result back afterwards. A harness that refuses such a write is the harness working as intended, and the refusal is neither re-shaped into a raw API call nor talked around (`GOVERNANCE.md` "Repository Boundaries and Write Safety").

- Open a trivial-change PR touching one target and confirm S1. *Caveat: the Docker leg logs in to the registry even on smoke and reads the buildcache, so it needs `DOCKER_HUB_*` secrets and cannot run on a fork PR (same-repo only).*
- Per registry: after a real publish, query NuGet.org for the expected version + prerelease classification (and the `.snupkg` on the symbol server), and confirm a re-run added no duplicate. For PyPI read the built `dist/*` filenames out of the build job's log, `.dev0` off `develop` vs a plain version on the default branch.
- Inspect the latest real publish's logs for `PublicRelease`/`SemVer2` per leg and confirm the artifact lifecycle (uploaded, consumed, deleted, with none left behind).
- **The deploy ref gate (S13) is verified only by tripping it.** Dispatch the production environment from a non-default ref and expect the run to fail at the gate. The evidence is four things, and each of them matters: the gate job's conclusion, its error text naming the expected and the received ref, every downstream job recorded as **skipped** rather than passed, and the production environment's deployment list carrying no deployment from the dispatched ref. Capture all four, because a gate that fails open and a gate nobody tripped produce the same empty run history, so "we have never seen it fail" is not evidence about the one control standing between a mis-dispatch and the live site. **The agent prepares the command and reads all four back afterwards. It does not fire it.** The same split applies to any probe that acts on the deploy host directly, an outbound SSH exercising a forced command among them.

### Assessment

Record the workflow **operational** when every *applicable* 5A item passes, every *applicable* 5B scenario's predicted output equals the expected, and no 5C probe that was run contradicts either. N/A items are excluded, never counted as failures. Any *applicable* mismatch is a **defect** -> **not operational**. Procedure:

1. **Audit** with 5A, recording each item's verdict and its evidence in the form 5A sets out.
2. **Trace** the applicable S-scenarios with 5B. Diff predicted vs expected.
3. **Probe** with 5C where a live signal exists that the static trace cannot produce, running the probes that only read and preparing the writing ones for the maintainer: live version classification, registry state, the artifact lifecycle of a real run, and the deploy ref gate.
4. **Verdict:** operational / not operational, with the failing guarantee(s) and the triggering input for each, the list of items recorded N/A, and the 5C probes prepared but not run.

`WORKFLOW.md` section 5 keeps the test methodology, and the `workflow-ci-contract` Skill at `.agents/skills/workflow-ci-contract/references/test-methodology.md` in the hub, not a repo-relative link since that path is hub-local and not carried into every fleet repo, carries this section whole as a generated include.

<!-- /include -->
