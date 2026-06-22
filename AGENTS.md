# Instructions for AI Coding Agents

**PlexCleaner** is a C# .NET utility that optimizes media files for Direct Play in Plex, Emby, Jellyfin, etc.

For comprehensive coding standards and detailed conventions, refer to [`.github/copilot-instructions.md`](./.github/copilot-instructions.md) and [`CODESTYLE.md`](./CODESTYLE.md).

## Git and Commit Rules

- **Default to staging, not committing.** Stage changes with `git add` and leave `git commit` to the developer unless the developer has explicitly authorized the agent to commit for the current ask ("commit this", "open a PR", etc.). Authorization is scope-bound — it covers the commits needed for that specific task, not a blanket commit license.
- **All commits must be cryptographically signed (SSH or GPG).** Branch protection enforces this on both branches; unsigned commits are rejected on push. Signing depends on environment configuration (`git config commit.gpgsign true`, a configured `user.signingkey`, and a loaded signing agent). If signing is not configured in the environment, **do not commit** — surface the missing config to the developer and stop at `git add`. Verify before any agent-authored commit (`git config --get commit.gpgsign && ssh-add -L`, or the GPG equivalent).
- **Never force push.** Do not run `git push --force` or `git push --force-with-lease` under any circumstances. Force pushing rewrites shared history and can cause data loss.
- **Never run destructive git commands** (`git reset --hard`, `git checkout .`, `git restore .`, `git clean -f`) without explicit developer instruction.
- **The `develop → main` release merge is maintainer-only.** An agent may drive `feature → develop` PRs end-to-end (commit, push, review loop, squash-merge) when authorized, but never self-merges a release to `main` — prepare it and hand it off.

## Branches and merging

- Pipeline is `feature → develop → main`. Both branches are protected by branch rulesets; everything lands via PR.
- **Feature → develop PRs squash-merge** (single commit on develop, PR title becomes the commit message; never rebase-merge).
- **Develop → main PRs merge-commit** (one merge commit on main per release, develop's tip becomes a second parent and stays in main's ancestry — see [Develop → Main Promotion](#develop--main-promotion)).
- Open feature PRs against `develop`. `develop → main` is how stable releases are cut.

Repo settings reflect this: `allow_merge_commit=true`, `allow_squash_merge=true`, `allow_rebase_merge=false`, `allow_auto_merge=true`. The `develop` ruleset enforces `allowed_merge_methods=["squash"]` and `required_linear_history`. The `main` ruleset enforces `allowed_merge_methods=["merge"]` and intentionally omits linear-history (the develop → main merge commit is non-linear by design).

## Merging a PR

**Never merge a PR without `copilot-pull-request-reviewer[bot]` (shown as "Copilot" in the GitHub UI; the `[bot]` suffix is its actual login) having posted a clean re-review on the latest commit** — defined as a review whose `commit_id` (or GraphQL `commit.oid`) equals the PR's `headRefOid`, with no new unresolved inline threads (Copilot in this repo posts `COMMENTED` reviews, not `APPROVED`, so a clean COMMENTED review with zero open threads is the "no issues found" outcome). `mergeStateStatus: CLEAN` only confirms ruleset gates (thread resolution, status checks, signatures); it does not confirm Copilot has re-evaluated the latest changes.

After resolving Copilot's threads or pushing fixes:

1. **Re-request a Copilot review programmatically** on the current head via the GraphQL `requestReviews` mutation — auto-review-on-push fires inconsistently, so don't wait on it. The full mechanics (bot node-id discovery, the mutation, head-SHA coverage check, thread reply/resolve, bounded retry) are in the [GitHub Copilot Review Runbook](./.github/copilot-instructions.md#github-copilot-review-runbook). The agent can drive this loop end-to-end without a maintainer clicking "re-request review" in the UI.
2. Verify Copilot's most recent review targets the current head — compare its `commit.oid`/`commit_id` to `headRefOid`, not timestamps (multiple reviews and authors clutter the list, and timestamp drift is unreliable).
3. If the fresh review is `COMMENTED` with zero unresolved inline threads (or `APPROVED`), the PR is good to merge.
4. If the fresh review introduces new concerns (inline threads or body-level objections), address them and loop.
5. **If Copilot does not re-review within a reasonable window (~5 min) after re-requesting**, retry per the runbook's bounded-retry workflow (up to three total); if still missing, mark the review blocked and escalate to the maintainer. Silence is not approval.

This applies to every human-authored PR (feature → develop, develop → main). The merge-bot workflow's auto-merge of dependabot bumps is the only exception and is governed separately by the `update-type` filter.

## Develop → Main Promotion

Use the **"Create a merge commit"** option on develop → main PRs. Repo rulesets are split: PRs into `develop` are squash-only (linear history); PRs into `main` are merge-commit only. Clicking "Create a merge commit" on a develop → main PR produces a merge commit on main whose second parent is develop's tip — so develop becomes a real ancestor of main, and the *next* develop → main PR has a clean merge base (no recurring conflicts, no behind-base churn).

Under any squash-only setup this would be a recurring pain point: each develop → main squash drops develop's ancestry and forces a per-cycle admin-bypass merge commit on develop to resync. With merge-commit on main, that resync is unnecessary — main's history shows one merge commit per release (a feature, not a defect: each promotion is visible as a single auditable node), and develop stays linear.

**Immediately after a develop → main merge lands and main's publish workflows complete, bump the minor version in [version.json](version.json) on develop.** Open a small isolated feature PR `bump-version-X.Y` (e.g. `"version": "3.16"` → `"version": "3.17"`), squash into develop, and continue feature work from there. Without this bump, develop's next NBGV-computed prerelease (`3.16.<height>-g{sha}`) is *numerically lower* than the stable that just shipped (`3.16.<N>`), which is visibly confusing in HISTORY.md, `--version` output, and consumer update prompts. Bumping ensures every develop prerelease is `3.17.<height>-g{sha}` — visibly newer than main's `3.16.<N>`. Don't bundle the bump with other work; keep the PR isolated so the version change is unambiguous in git blame.

A **maintenance** develop -> main promotion - dependency bumps, CI/doc fixes, template re-syncs, not a release - holds main's version: run `git checkout main -- version.json` on the promotion branch before opening the PR, so main advances only its git height (a patch), not its minor, and develop keeps its lead. Only a release promotion carries develop's bumped version to main.

## Release flow

PlexCleaner is a "pull" project: consumers (`docker pull ptr727/plexcleaner:latest`, `docker pull ptr727/plexcleaner:develop`, GitHub Releases) track both branches. It uses a **two-phase model** that decouples merging from publishing:

- **PRs smoke-test only.** [test-pull-request.yml](.github/workflows/test-pull-request.yml) always runs unit tests, then a [`dorny/paths-filter`](.github/workflows/test-pull-request.yml) `changes` job gates a **reduced** build of only the changed targets (Docker `linux/amd64` only, executable on a `linux-x64` + `win-x64` subset), never pushing. Build-workflow files are intentionally not in the path filters — a filter can't tell a logic change from an action-version bump — so a workflow-only change isn't smoke-built; the reusable workflows are exercised by the next run that uses them. There is no CI workflow-lint job; lint workflow edits with `actionlint` locally before pushing. The `changes` job is in the `Check pull request workflow status` aggregator's `needs` and **must succeed** (not just "not fail") — a paths-filter error must never let a target-changing PR merge with its smoke build silently skipped.
- **Merges don't publish by default.** [publish-release.yml](.github/workflows/publish-release.yml) is the **sole publisher**: its **weekly schedule** (Mondays 02:00 UTC) and **manual `workflow_dispatch`** always do the full multi-arch build/publish of **both** `main` and `develop` (a branch matrix in one run). Its `push` trigger publishes only when the **`PUBLISH_ON_MERGE` repository variable** is `true` (opt-in legacy continuous-release). Unset/`false` = two-phase: routine merges to `develop`/`main` only smoke-build, and `:latest`/`:develop` Docker tags + GitHub releases refresh on the weekly run instead of on every merge.

A `setup` job computes the plan: `push` ⇒ the pushed branch with `publish = (vars.PUBLISH_ON_MERGE == 'true')`; `schedule`/`dispatch` ⇒ both branches with `publish = true`. The `publish` job is a `matrix.branch` fan-out over [build-release-task.yml](.github/workflows/build-release-task.yml); `tool-versions`, `docker-readme` (main only), and `date-badge` (main only) run after it.

Branch-aware config keys off the **`branch` input** threaded through every reusable task — **never `github.ref_name`** (the publisher builds `develop` from a run whose `github.ref_name` is `main`, so a fallback would mislabel it). `main` ⇒ Release / `latest` / stable release; anything else ⇒ Debug / `develop` / prerelease. The GitHub release's `target_commitish` is pinned to NBGV's `GitCommitId` (the exact built commit) — not `github.sha` (wrong on the develop leg) and not a branch name (a moving ref); `get-version-task.yml` surfaces `GitCommitId` as an output. The release step is skipped when a release for the computed `SemVer2` tag already exists (no-op weekly republish), except on `workflow_dispatch` (which can refresh a partial release).

**Per-target subsetting (derived projects).** `build-release-task.yml` has per-target `enable_*` gates and self-contained leaf tasks, so a project that drops a target deletes: its `build-<target>-task.yml`, the matching job + `github-release` `needs` entry in `build-release-task.yml`, and its path-filter entry in `test-pull-request.yml`. Versioning, badge, tool-versions, Docker README, merge-bot, and Dependabot are target-agnostic. PlexCleaner's targets are the **Docker image** and the **console executable** only (no NuGet/PyPI).

Bot-merged PRs (Dependabot) still trigger `publish-release.yml` because the merge-bot uses an App token (see the merge-bot section) — under the default two-phase model that push run is a no-op publish unless `PUBLISH_ON_MERGE` is set.

## Dependabot

[.github/dependabot.yml](.github/dependabot.yml) targets **both `main` and `develop`** with two ecosystems each (`nuget`, `github-actions`), grouped per ecosystem, daily. The duplication is intentional: both branches ship from the weekly publisher (and on every merge when `PUBLISH_ON_MERGE` is set), so develop must not drift from main's dependency baseline. A NuGet major bump landing on develop should land on main on the next promotion cycle, not weeks later.

Major NuGet bumps are not auto-merged by [merge-bot-pull-request.yml](.github/workflows/merge-bot-pull-request.yml) — they require human review. Major GitHub Actions bumps are auto-merged because the workflow execution itself is the validation surface.

## GitHub Actions pinning

Every third-party action in `.github/workflows/*.yml` is pinned to a full commit SHA with a trailing comment matching the upstream release tag, e.g. `uses: actions/checkout@de0fac2e4500dabe0009e67214ff5f5447ce83dd # v6.0.2`. The comment is whatever tag the action's repo actually publishes — typically `# vX.Y.Z`, but use `# v3` if upstream only publishes major-only tags (e.g. `addnab/docker-run-action`) and `# master` if the action ships only a moving branch (rare). Floating refs without a SHA (`@v6`, `@main`, `@master`) are never used. Local reusable workflows (`./.github/workflows/*.yml`) are referenced by path and don't need pinning.

**Why:** Floating tags can be silently re-pointed by the action's owner (or by a compromised account) to malicious code; a SHA pin is immutable. Matching the comment to upstream's actual release tag (rather than fabricating one) lets dependabot rewrite both the SHA and the comment together when bumping.

When adding a new `uses:` line, resolve the latest release's commit SHA (`gh api repos/<owner>/<repo>/releases/latest`) and copy its `tag_name` into the comment verbatim. Don't ship a floating tag and "pin it later".

## Merge bot

[merge-bot-pull-request.yml](.github/workflows/merge-bot-pull-request.yml) auto-merges Dependabot PRs. Two key design choices:

- **Branch-aware merge method**: the script picks `--squash` for PRs targeting develop and `--merge` for PRs targeting main, matching each ruleset's `allowed_merge_methods`. An unknown base branch is a hard error.
- **App token, not GITHUB_TOKEN**: the merge step uses a token minted by `actions/create-github-app-token` from `CODEGEN_APP_CLIENT_ID` / `CODEGEN_APP_PRIVATE_KEY` secrets. Pushes authored by `GITHUB_TOKEN` are blocked from triggering downstream workflows by GitHub's recursion guard; without the App token, a Dependabot merge would silently skip `publish-release.yml` on the merge commit. Under the default two-phase model that push is a no-op publish (it only republishes when `PUBLISH_ON_MERGE` is `true`), but the App token keeps that opt-in path — and any future push-triggered workflow — working.

The App secrets (`CODEGEN_APP_CLIENT_ID`, `CODEGEN_APP_PRIVATE_KEY`) must exist in **both** secret namespaces: Settings → Secrets and variables → **Actions**, and Settings → Secrets and variables → **Dependabot**. Since Sept 2021, GitHub injects only the Dependabot-namespace secrets when a Dependabot-authored `pull_request` event fires; the regular Actions namespace is not visible to that run. Without the Dependabot duplicate the App-token step gets empty inputs and merge-bot silently fails to auto-merge. (The trigger remains `pull_request`, not `pull_request_target` — the merge-bot doesn't check out PR code, but `pull_request` plus duplicated secrets is the simpler, less-permissive setup.)

## Key Requirements for All Projects Derived from This Template

### Build & Quality Standards

- **Zero Warnings Policy**: All builds must complete without errors or warnings
  - Use `CSharpier Format`, `.Net Format`, and `Husky.Net Run` tasks

- **Code Analysis**: Enable all .NET analyzers
  - `<EnableNETAnalyzers>true</EnableNETAnalyzers>`
  - `<AnalysisLevel>latest-all</AnalysisLevel>`

### Project Configuration

- Common MSBuild properties (`TargetFramework`, `Nullable`, `ImplicitUsings`, `AnalysisLevel`, etc.)
  live in `Directory.Build.props` at the solution root. Do not duplicate these in individual `.csproj`
  files — only add a property to a `.csproj` when it is project-specific or overrides the shared default.
- All NuGet package versions are centralised in `Directory.Packages.props`. `PackageReference` elements
  in `.csproj` files must not include a `Version` attribute. Asset metadata (`PrivateAssets`,
  `IncludeAssets`) stays in the `.csproj` `PackageReference` element.

### Development Environment

- Target latest .NET SDK (currently .NET 10 with C# 14)
- Support Visual Studio Code (`.code-workspace`) and Visual Studio Community (`.slnx`)
- Support Linux, Windows, and macOS with correct line endings and permissions
- Use `.editorconfig` for style enforcement

### Project Structure

- **PlexCleaner**: CLI application
- **PlexCleanerTests**: Unit tests using xUnit and AwesomeAssertions
- **Sandbox**: Sandbox/testing utility project
- **Docker**: Multi-platform Linux containers

### Testing

- Use xUnit v3 and AwesomeAssertions
- Organize tests logically in separate files
- Follow Arrange-Act-Assert pattern
- Test naming: `MethodName_Scenario_ExpectedBehavior()`

## Authoritative References

For detailed specifications, see:

- [`.github/copilot-instructions.md`](./.github/copilot-instructions.md) - Complete coding conventions and style guide
- [`CODESTYLE.md`](./CODESTYLE.md) - Code style and formatting rules
- [`.editorconfig`](./.editorconfig) - Automated style enforcement
- Project task definitions - `CSharpier Format`, `.Net Build`, `.Net Format`, `.Net Outdated Upgrade`, `Husky.Net Run`
