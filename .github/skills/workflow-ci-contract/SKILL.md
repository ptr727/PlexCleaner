---
name: workflow-ci-contract
description: >-
  Governs the WORKFLOW.md CI/CD behavioral contract for every ptr727/ProjectTemplate fleet repo: the D1-D9 guarantees stated as the failure mode each prevents, the seam contract for release assets, the artifact lifecycle, NBGV versioning and classification, validate-at-entry, and the 5A/5B/5C test methodology with its per-type walkthroughs. Use this whenever writing or editing anything under .github/workflows/, adding or dropping a release target, auditing a repo's workflows, or reasoning about why a publish did or did not fire. This is the YAML half of the pipeline, and the operational-vs-release-workflow skill keeps the git half (branching, promotion, publish policy), so branch choice questions go there. Triggers even when the edit looks mechanical, such as bumping an action, renaming a job, or adding one upload step, because SHA pinning, the ruleset-bound aggregator name, smoke gating on uploads, and retention-days are each easy to break in a one-line diff that no smoke build exercises, since workflow-only changes are deliberately not smoke-built. WORKFLOW.md keeps authority, and GOVERNANCE.md wins where the two overlap.
---

# Workflow CI Contract

## Why This Exists

`WORKFLOW.md` in the hub is the largest law doc, a behavioral contract stating required outcomes rather than a required implementation, and it had no skill surface, so agents edited workflow YAML without the contract in view. This skill is the summary plus the binding rules, with the guarantee catalog and the test methodology split into `references/`. `WORKFLOW.md` keeps authority for the contract and methodology, and `GOVERNANCE.md` ("Workflow YAML Conventions", "Release Model") wins where the two overlap.

## How the Contract Is Read

- **Outcomes, not bytes.** A workflow is correct when it satisfies the section 4 contract against the expected inputs and outputs, not when it matches a catalog snippet byte for byte. Two repos may implement one guarantee with different YAML.
- **Applicability.** A guarantee governing a construct the repo does not contain is N/A: recorded, excluded from the verdict, never a defect. A source-only pipeline is mostly N/A and that is fine.
- **Operational is binary.** Every applicable guarantee holds, or the workflow is not operational. A single applicable input-output mismatch is a defect regardless of how clean the YAML looks.
- **Reached, not carried.** A standard workflow whose job graph is identical across repos of a type is a `workflow_call` task the hub hosts once, and a repo carries only a caller stub pinned to a hub release commit plus a composite-action hook at `.github/actions/<hook>` for what is its own. A hub task reaches its own actions and sibling tasks through `$/`, which resolves at that pinned commit. The merge-bot is the first, and `docs/reusable-workflows.md` in the hub carries the model, the hook contract, and the phase each workflow migrates in. Until a workflow's phase ships, its copy is graded as below.
- **Two layers.** Orchestration (the PR entry workflow, publisher, version/release/badge jobs) is generic and standard at the job level. Build leaves (`build-<target>-task.yml`) are repo-owned. Inputs like `github`/`nuget`/`dockerhub`/`expect_release_assets` live on the orchestrator, a leaf only receives `ref`/`branch`/`smoke` and a derived `push`, so assert each input in the layer that declares it. What a repo curates is the list of targets, and adding or dropping one edits the whole surface together: the `enable_<target>` input, the `build-<target>` job and its `github-release` `needs:` entry, the `changes` paths-filter entry and output, and the `smoke-build` enable-forward (D6.4).

## Style Rules That Break in One-Line Diffs

- **Pin every action to a commit SHA** with a trailing `# vX.Y.Z` comment, first-party included. The one documented no-pin exception is `dotnet/nbgv@master`. Invent no others.
- **Names carry meaning**: `-task.yml` files and "task" names are reusable (`on: workflow_call`), entry points end in what they do and their names end in "action", every job `name:` ends in "job" and every step in "step". A ruleset-bound required check's job `name:` and the ruleset `context:` are one string renamed together, in the live ruleset and the hub's `repo-config/` payloads in lockstep, or required-check enforcement silently breaks.
- **Concurrency**: top-level workflows use `group: '${{ github.workflow }}-${{ github.ref }}'` with `cancel-in-progress: true`. The publisher is the documented exception: a global ref-independent group with `cancel-in-progress: false`, so publishes serialize and never cancel mid-push.
- **Shells**: every multi-line bash `run:` starts `set -Eeuo pipefail`. Multi-line `if:` uses `>-`, never `|`.
- **Boolean inputs** are declared in both trigger blocks and compared against both forms, `${{ inputs.foo == true || inputs.foo == 'true' }}`, since `workflow_dispatch` delivers strings.
- **Permissions validate before `if:`**, so even a skipped job needs valid `permissions:`, and a callee's extra scope (`actions: write`, `id-token: write`) is granted by the caller at the one entry point that needs it.
- **Chaining across optional jobs** allowlists `success`/`skipped` explicitly, because `!= 'failure'` lets `cancelled` through.
- **Docker layer cache** targets a registry tag (`buildcache-<branch>`), never `type=gha`.
- **Workflow YAML is LF.** Preserve endings on every edit.

## The Core Behavioral Spine

- **PRs validate fast and never publish**: a paths-filter smoke-builds only changed targets, a type-appropriate validation job always runs, and one required aggregator gates the merge, treating skipped smoke as pass and blocking on failure or cancelled. Smoke does a full compile/lint/test but pushes nothing and uploads nothing, every `upload-artifact` gated `!smoke`.
- **A human merge never auto-publishes**: a `plan` job decides once and every job gates on it. Publishes come from a code-affecting bot push to `main`, a manual dispatch of `main` or `develop`, or the main-only weekly Docker schedule. Each run builds the one trigger branch, `main` a clean `X.Y.Z`, anything else a prerelease `X.Y.Z-g<sha>`, with NBGV owning the patch from git height. The release tags the built commit's SHA (`GitCommitId`), never a branch name.
- **Validate at entry**: cross-input and input-versus-derived-state invariants are asserted once in a dedicated entry job the downstream jobs `needs:`, failing fast with `::error::` before expensive work. The release gate checks branch-versus-prerelease in both directions, strips `+buildmetadata`, and on smoke skips the check while the job still succeeds.
- **The seam contract**: a target contributes a release file by uploading `release-asset-<branch>-<target>`, and the release job collects by `pattern:` plus `merge-multiple:`, never `artifact-ids:`, canonical even for a single target. A repo with no file target passes `expect_release_assets: false` at the caller.
- **Artifacts are an intra-run handoff**: consume-then-delete at the point of consumption, gated to the consumer's condition, best-effort, `retention-days: 1` on every upload as the backstop, and never a blanket delete of the run's artifact set, which destroys the diagnostics you need when the run fails.
- **No-op republish**: an unchanged version re-pushes nothing, the release-create step skips when the tag exists, registries dedupe server-side (`--skip-duplicate`, `skip-existing: true`), and Docker alone always re-pushes by design.
- **A build failure blocks every publish target**: `github-release` needs every build, and the terminal registry pusher guards with `!failure() && !cancelled()`, so nothing partial ships.

The full catalog, each guarantee with the failure mode it prevents, is in `references/d-guarantees.md`. Auditing, tracing, and probing a repo's workflows is `references/test-methodology.md`.

## After Any Workflow Edit

Workflow-only changes are not smoke-built, so run actionlint locally (the Docker invocation in `GOVERNANCE.md` "Running the Linters Locally", which bundles shellcheck for `run:` blocks) before pushing, and remember a workflow change is only fully exercised by CI, since `secrets: inherit`, `permissions:`, and `needs:` wiring resolve only in a real run.
