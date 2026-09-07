# Release Build and Publish Mechanics

Full detail for the "Publishing" rules in `SKILL.md`. Load this when adding or removing a release
target, wiring a new leaf build task, deciding where a build output belongs (a GitHub Release
asset, a package-registry push, an image push, a deploy), recovering a package push that failed
after the release was already cut, or setting up a wrapper repo that tracks an upstream release,
not for reading the release model's shape (the SKILL.md summary covers that).

## Reusable-task parameter contract

Every `build-*-task.yml` and `build-release-task.yml` takes `ref` (git ref to check out/version),
`branch` (logical branch driving config/tags/prerelease, where `main` => Release/`latest`/
non-prerelease, else Debug/`develop`/prerelease), and where relevant `smoke`.
**Branch-derived config keys off `inputs.branch`**: each run builds one branch, and the top-level
publisher passes `branch: ${{ github.ref_name }}`, which the tasks forward and read as
`inputs.branch` (not `github.ref_name`) for config/tags/prerelease. `get-version-task.yml` takes a
`ref` so NBGV versions the right branch.

## Per-target subsetting

`build-release-task.yml` is a hub-hosted task with per-target `enable_*` inputs, so a repo drops a
target by setting its `enable_<target>: false` at the caller stub rather than deleting a job: the
hub task carries the full job graph for every repo, and the caller stub's `with:` block is where
the target list is expressed. A repo still curates, in `test-pull-request.yml`, its
path-filter entry, that filter's output, and the `smoke-build` enable-forward, all three together
per D6.4, since an entry nothing consumes never smoke-builds the target. And, for a package
target, the `publish-nuget` or `publish-pypi` job in its own `publish-release.yml`, since
`id-token: write` belongs at that one entry point. CodeGen, versioning, merge-bot, and Dependabot
are target-agnostic.

## Orchestration vs. build: the override seam

The pipeline splits into two layers. The **orchestration** layer is generic and is the
standardization baseline: `publish-release.yml` (single-branch publish plan), the `get-version`
task plus `github-release` job inside `build-release-task.yml`, `get-version-task.yml`, and the
aggregator shape of `test-pull-request.yml`. Within
`test-pull-request.yml`, only the `changes -> smoke-build -> check-workflow-status` aggregator
wiring and the ruleset-bound job name are verbatim orchestration, while the `dorny/paths-filter`
entries are owned/per-target. The validation job is a call to the reusable validator, whose own
jobs a caller cannot address. The **build** layer is a hook: a composite
action at `.github/actions/build-<target>` the hub-hosted `build-release-task.yml` reaches. The
hub defaults require explicit project paths. A project needing more than a path override carries
its own hook.

The contract that keeps the seam clean: **a target contributes files to the GitHub release by
uploading a workflow artifact named `release-asset-<branch>-<target>`.** The `github-release` job
collects every `release-asset-<branch>-*` artifact by pattern, so its `download-artifact` step
uses `pattern:`/`merge-multiple:`, **never an `artifact-ids:` that names a build job's output**
(the producing build jobs still appear in `needs` for sequencing). That makes the tag-the-commit
plus create-the-release plus attach-the-assets logic reusable **as-is** across repos. **This
name-pattern handoff is canonical for every repo, single-target included**: name your one asset
`release-asset-<branch>-<target>` and the verbatim `github-release` globs it. Do not switch a
single-target repo to an `artifact-id` output plus `download-artifact` `artifact-ids:`, which
looks tidier for 1:1 but forks the `github-release` download and breaks its verbatim carry.

**What a repo still curates** (by design, not a leak): which `enable_<target>` inputs its caller
stub sets, per the per-target subsetting rule above. `build-release-task.yml` is hub-hosted
(the hub's `docs/reusable-workflows.md` "Stage 4: The Release Chain and the Docker Core"), so its job graph
and its `github-release` job are the hub's, not a per-repo file a caller edits. A repo adopting the
release chain carries only the caller stub in its own `publish-release.yml` and
`test-pull-request.yml`, naming the hub task by pin and setting the `enable_*`, `docker_image`,
and project-path inputs its targets need.

## Map your outputs to the right seam

Pick by where each artifact *goes*, not by language:

- **Files attached to the GitHub Release** (zips, binaries, packaged libraries): a dotnet-publish
  hook or a build-nuget hook per output, each uploading `release-asset-<branch>-<target>`. This is where the
  .NET `dotnet publish` or `dotnet build` lives, though a package push does not. The hub default takes an explicit
  project path, and a project needing different build behavior replaces the hook. A data-only
  repo's own output (e.g. a symbol library) is not yet
  expressible as a hub hook or an `enable_*` input, so it stays a carried leaf until the hub task
  grows one.
- **Package-registry pushes** (NuGet.org, PyPI): both are split, and the push never sits in the
  hook. OIDC trusted publishing validates the token's `job_workflow_ref` claim, which names the
  workflow the job actually ran from, so a push from a hub-hosted task is rejected at the token
  exchange, NuGet.org answering `HTTP 401` and PyPI under its own code. And because D7.2 has a callee
  declare `permissions:` only where every caller grants that scope at startup, the release task's
  jobs declare none and run under the calling job's whole grant, so a push anywhere inside that
  task would put `id-token: write` on every job in it. The
  build-nuget hook uploads a `nuget-build-<branch>` artifact for a separate `publish-nuget` job in
  the caller's own `publish-release.yml`, which authenticates through `NuGet/login` and therefore
  carries `id-token: write` (plus `actions: write` to delete the artifact it consumed), *and* also
  uploads a `release-asset-*` (.7z) for the GitHub release. PyPI is the same shape: the build-pypi hook only builds and uploads the
  `pypi-build-<branch>` artifact, and the separate `publish-pypi` job in the caller's own
  `publish-release.yml` does the OIDC Trusted-Publishing upload, behind an `environment: pypi`
  gate and with `skip-existing: true` (`id-token: write` is granted only at that one entry
  point), and PyPI contributes **no** `release-asset-*`.
- **Image-registry pushes** (Docker Hub): `build-docker-task.yml`, hub-hosted like
  `build-release-task.yml`, pushes the default branch multi-arch (amd64+arm64) and any other
  branch `amd64`-only, and contributes **no** `release-asset-*`. The image set comes from a docker-prepare hook (the hub default emits the
  single vanilla entry an `image` input implies). A multi-image or upstream-pinned repo carries its
  own hook, and a shared base layer comes from a required docker-build-base hook with no hub
  default. To publish the Docker Hub repository overview, the hub-hosted `publish-docker-readme-task.yml`
  pushes a readme via `peter-evans/dockerhub-description` (single-repo by default, matrix per
  image for multi-image repos), wired into `publish-release.yml` and gated to `main` both by the
  caller's `branch` input and inside the task itself. A `docker-readme-transform` hook sets a
  `readme-filepath` step output naming which file to push, defaulting to `Docker/README.md` if
  present else `README.md` as-is, so a repo needs a hook only to render the file first or to
  override that default.
- **Filesystem on a host the project owns** (a static site, a config tree): a deploy leaf builds
  the tree and ships it over the repo's own transport, contributing **no** `release-asset-*`. It
  is a **separate `workflow_dispatch`** from the release, so a redeploy of an unchanged commit
  mints no tag, and its credentials come from a **per-environment GitHub Environment** rather than
  the repository secret store. Its last step asserts what the host actually serves, the release id
  and the environment, never that the transport exited zero. Retention at the destination is
  bounded by a declared count, and one side is recorded as owning the prune: the deploy where its
  credential can observe the destination, the host where that credential is deliberately
  write-only.
- **Source-only / no build** (validate + tag + release): the repo has no leaf build tasks.
  Its dispatch-only `publish-release.yml` calls the hub-hosted `build-release-task.yml` after the repo's reusable validation task succeeds.
  The caller sets `github: true`, every `enable_*` input to false, and `expect_release_assets: false`.
  The reusable task runs NBGV and creates the release with the tag, automatic source archive, README, and LICENSE.

`get-version-task.yml` installs the .NET SDK only because NBGV needs the runtime to compute the
version/tag, which is heavyweight but expected even for a non-.NET repo, and acceptable as-is.

## No-op republish guarantee

A scheduled or push publish where NBGV `SemVer2` is **unchanged** (no new commit since the last
publish) re-pushes **nothing** to GitHub Releases (the `github-release` job's `release-exists`
check skips the create step, and a dispatch refreshes the release instead of skipping), NuGet (`dotnet nuget push --skip-duplicate`), or PyPI
(`gh-action-pypi-publish` `skip-existing: true`), since all three key on the version string.
**Docker always re-pushes** by design: it picks up upstream base-image refreshes (e.g.
`ubuntu:rolling`) that aren't visible in the repo. Boundary: `version.json` has **no
`pathFilters`**, so *any* commit, including a CI/workflow-only or docs-only change, advances the
NBGV git height and therefore `SemVer2`, and the next publish *does* create a fresh release for it
even when the shipped binary is byte-identical. This is accepted NBGV behavior, and `pathFilters`
are intentionally not added.

## Recovering a failed registry push

A package publish job is gated like everything else, `needs:` the release-task call, so a failed build skips it. The **push inside it** is what no gate can reach, because it runs after the whole release task and therefore after `github-release`. `WORKFLOW.md` D4.5 names the two recovery routes and leaves their mechanics here. A rejected token exchange, a registry outage, or a trusted-publishing policy naming the wrong workflow file leaves a published release and tag for a version that never reached the registry. The recovery is a re-dispatch or a full re-run rather than a cleanup. **A full re-run is always available inside its window, and a re-dispatch only while the branch tip has not moved**, so the tip decides whether there is a choice at all rather than which route to take. What re-dispatch buys, where it is available, is that it outlives the re-run window.

**Re-dispatch, available only while the tip has not moved.** A `workflow_dispatch` takes a ref rather than a commit, and D2.3 admits only `main` or `develop`, so what it builds is that branch's tip at dispatch time. While the tip is still the commit whose push failed, a re-dispatch rebuilds the same version and runs its push again, refreshing the release the way any dispatch does.

This is a time-of-check-to-time-of-use race rather than a guarded operation: nothing compares the tip against the failed run, so a push landing between the two mints a new version instead of erroring, and the operator sees a green publish that left the failed version unpublished. Confirm the failed run's own head commit still equals the branch tip immediately before dispatching, reading it as `gh run view <id> --json headSha` against `gh api repos/{owner}/{repo}/branches/<branch>` for the branch that run built rather than whichever branch is to hand. Where the two differ, or where the check is not worth making, prefer the re-run route, which is bound to that commit by construction, and fall back to re-dispatch only once the re-run window below has closed.

**Re-run all jobs, available inside the window whatever the tip has done.** `gh run rerun <id>` replays the run under the original event's `GITHUB_SHA` and `GITHUB_REF` and re-executes every job rather than only the failed ones. The publisher pins the release task to that commit with `ref: ${{ github.sha }}`, so `get-version` recomputes the same version from the same commit and history, each build leaf checks out the `GitCommitId` that job emits, the package artifact D5.2 deleted is rebuilt and re-uploaded rather than missing when `publish-<target>` downloads it, and that job retries the push it failed. The release itself needs nothing from the re-run, the failed run having already cut it, though on a dispatch-triggered run the re-run re-enters `github-release`, which refreshes the release per D4.4's dispatch leg and runs the `release-asset-*` delete with it per D5.2. A re-dispatch here would build the new tip instead, and NBGV derives the version from git height, so that is a further version and the one whose push failed never reaches the registry.

Three qualifications come with the re-run route.

- D4.4 and `WORKFLOW.md` 5B's S9 describe a re-run whose predecessor push **succeeded**, where the registry dedupes the second one. This is the case they do not cover, and its retried push is the first the registry ever receives for that version.
- GitHub offers a re-run only within **30 days** of the initial run, and a repository's own **log** retention setting can be shorter, so the usable window is the shorter of the two. This is the run's own retention and is unrelated to D5.4's `retention-days: 1`, which bounds an uploaded artifact rather than the run.
- **Re-run failed jobs** (`--failed`) does not serve here. D5.2's delete runs on the path that reaches this case, its gate being `!cancelled()` and the download having succeeded, so it has already removed the package artifact a `--failed` re-run would download, and only the full re-run rebuilds it.

Past the window, a moved tip leaves that version with no route to the registry. The release and tag already name it, and removing them is not the answer: leave them, and let the next publish carry a later version, recording the gap in `HISTORY.md`, since the release body is regenerated on any later dispatch refresh and cannot hold the record.

What no route settles in advance is whether the registry accepts the retried push.

## Wrapper repos that track an upstream release

A repo wrapping an upstream release uses the hub-hosted `check-upstream-version-task.yml`: a
required `resolve-upstream` hook sets a `versions` step output, a **JSON object of
`name -> version`**, written to a committed state file at the **repo root beside `version.json`**
(default `upstream-version.json`, since it is a build-input version source, not GitHub-platform
config, so it does not belong under `.github/`), and opens a rolling App-signed bump PR per branch
that the merge-bot auto-merges (`merge-upstream-version`). The object carries one key for the
common single-version case (`{"version": "X"}`) or N keys for a wrapper that pins several upstream
components (e.g. an image plus a companion tool), and the build reads each component by key, and
the bump PR's title/body name only the keys that actually moved. Call it from a scheduled
entry-point workflow and matrix only the branches that ship the version (a CI-only version uses
`["develop"]`). A merged bump ships on the **next publish**, not immediately, which is the
two-phase latency tradeoff. A tracker whose bump needs a human decision instead of auto-merge, for
example one that snapshots a package list to review rather than a version to adopt outright, sets
`auto-merge: false`, which prefixes the head so no merge-bot rule matches it.
