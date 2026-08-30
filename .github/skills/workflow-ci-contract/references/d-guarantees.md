# The D-Guarantees, Condensed

Each guarantee is a MUST from `WORKFLOW.md` section 4, stated as input to output plus the failure mode it prevents. This is the condensed catalog for working from, and `WORKFLOW.md` keeps authority, so read the section there when a guarantee's exact wording decides a verdict.

## D1: PR Fast-Feedback (Smoke)

- **D1.1** Only changed targets build: each target has a paths-filter entry, unchanged targets skip. Prevents a changed target slipping through unbuilt.
- **D1.2** A validation job always runs on any PR, and a non-.NET repo replaces it (never deletes it), re-pointing every `needs:` on it, the aggregator and `smoke-build` both. Prevents a PR merging with no validation, or a dangling `needs:` failing the workflow to load.
- **D1.3** Smoke never publishes and never uploads: full compile/lint/test, no pushes, every `upload-artifact` gated `!smoke`. Prevents a PR publishing and orphaned artifacts.
- **D1.4** Workflow-file changes are not smoke-built (the filter excludes `.github/workflows/**`), actionlint still validates them.
- **D1.5** One required aggregator gates merge: `needs:` the changes and validation jobs, passes on skipped smoke, blocks on failure or cancelled, and its name is ruleset-bound (job `name:` equals ruleset `context:`, renamed together).
- **D1.6** Coverage reports to Codecov for C# and Python repos with tests, best-effort so an outage never reds the gate, with a `codecov.yml` setting statuses informational and `.gitignore` excluding coverage output.

## D2: Validation at Entry

- **D2.1** A dedicated entry job asserts each cross-input invariant before expensive work, downstream jobs `needs:` it.
- **D2.2** The release gate fails loud when the default branch carries a prerelease suffix or a non-default branch carries none, strips `+buildmetadata` first, and on smoke skips the check while the job still succeeds (a job-level `if:` would skip dependents with it).
- **D2.3** A dispatch publish from any ref other than `main` or `develop` fails fast.
- **D2.4** Mutually-exclusive or must-pair inputs are validated, a half-filled combination fails fast.

## D3: Versioning and Classification

- **D3.1** One branch per run: `github.ref` names the built branch, NBGV classifies it directly, no `IGNORE_GITHUB_REF`.
- **D3.2** Default branch yields `X.Y.Z`, every other branch `X.Y.Z-g<sha>`, and the default-branch literal in the gate, the `prerelease` expression, and `version.json`'s `publicReleaseRefSpec` all name the repo's real default branch.
- **D3.3** `version.json` sets the major.minor floor, NBGV appends git height as the patch, and both are retained even by a no-compiler repo, since they own the tag.
- **D3.4** Registry versions follow the classification per registry: NuGet.org derives prerelease from the SemVer2 suffix, PyPI builds from `AssemblyFileVersion` with `.dev0` appended on `develop` only, and the develop build stays `--pre`-selectable above the released version.
- **D3.5** A wrapper repo drives its image version from a committed `name -> version` state file, and the leaf must actually read it, since a leaf still tagging off NBGV means the wrapper is not pinned to upstream.

## D4: Release and Publish

- **D4.1** Gated single-branch publish: a human merge never auto-publishes, the `plan` job decides once, publishes come from a code-affecting bot push to `main`, a dispatch of `main`/`develop`, or the main-only weekly Docker schedule.
- **D4.2** `target_commitish` is the built commit's SHA (NBGV `GitCommitId`), never a branch name and never `github.sha`.
- **D4.3** Every release is a tag plus source zip, README, and LICENSE, file targets attach `release-asset-*`, and a no-file-target caller passes `expect_release_assets: false` or the release-create step fails on unmatched files.
- **D4.4** No-op republish: an unchanged version re-pushes nothing, the release-create skips when the tag exists (refreshed only on `workflow_dispatch`), registries dedupe server-side, and Docker always re-pushes by design.
- **D4.5** A failed build blocks every publish target: `github-release` needs every build, the terminal registry pusher guards `!failure() && !cancelled()`, so nothing partial ships.
- **D4.6** A deploy check asserts which release and which environment answer, waiting for convergence to a bounded timeout, with an unreachable host reported distinctly from an HTTP status.

## D5: Resource Cleanup

- **D5.1** A cross-job transfer artifact is deleted at its point of consumption. An in-run intermediate may rely on the retention backstop.
- **D5.2** The delete runs under the same condition as its consumer, so a no-op re-run skips the release-asset delete while the PyPI build-artifact delete still runs.
- **D5.3** Cleanup is best-effort (`continue-on-error`, tolerate a failed listing, delete all matching ids).
- **D5.4** Every `upload-artifact` sets `retention-days: 1`.
- **D5.5** Never blanket-delete the run's artifacts, which destroys diagnostics and auto-emitted build records.
- **D5.6** A durable deploy destination's retention is bounded by a declared count with one side recorded as owning the prune: the deploy where its credential can observe the destination, the host where the credential is deliberately write-only.

## D6: Seam Conformance

- **D6.1** The release job downloads by `pattern:`/`merge-multiple:`, never `artifact-ids:`, canonical for single-target repos too.
- **D6.2** Branch-derived config reads `inputs.branch`, never `github.ref_name`.
- **D6.3** Artifact names are branch-suffixed.
- **D6.4** A target add or drop updates the whole surface together: `enable_<target>` input, `build-<target>` job, `github-release` `needs:` entry, paths-filter entry and output, and the `smoke-build` enable-forward.

## D7: Concurrency, Permissions, Safety

- **D7.1** The publisher serializes: global ref-independent concurrency group, `cancel-in-progress: false`.
- **D7.2** Every reusable job declares valid `permissions:` (validated before `if:`), a callee's extra scope granted by the caller.
- **D7.3** Boolean inputs are declared in both trigger blocks and compared against both forms.
- **D7.4** Optional-dependency chaining allowlists `success`/`skipped` explicitly.

## D8: Bots and Automation

- **D8.1** The merge-bot enables auto-merge on `opened`/`reopened` for every Dependabot tier, dispatches squash or merge by base ref, disables on a maintainer-pushed `synchronize`, and keys concurrency on the PR number, not `github.ref`.
- **D8.2** Codegen runs a deterministic matrix over both branches, Dependabot targets both branches.
- **D8.3** The upstream tracker writes a committed `name -> version` state file via a rolling per-branch bump PR the merge-bot auto-merges, and its branch prefix must match the merge-bot's head-ref pairs or auto-merge silently never fires.
- **D8.4** An identity allowlist used as a gate emits a `::warning::` on the non-matching branch rather than falling through silently, since a renamed App slug otherwise turns the gate off invisibly.

## D9: Style and Static

SHA pins with version comments, the name-suffix rules, `set -Eeuo pipefail`, `if: >-`, registry-tag Docker cache with `cache-to` only the built branch on push and `cache-from` both branches, line endings per `.editorconfig`.
