# Instructions for AI Coding Agents

**PlexCleaner** is a C# .NET utility that optimizes media files for Direct Play in Plex, Emby, Jellyfin, etc.

For comprehensive coding standards and detailed conventions, refer to [`.github/copilot-instructions.md`](./.github/copilot-instructions.md) and [`CODESTYLE.md`](./CODESTYLE.md).

## Git and Commit Rules

**These rules are absolute — no exceptions:**

- **Never make git commits.** AI coding agents cannot produce cryptographically signed commits. All commits must be signed (SSH/GPG) and must be made by the developer. Stage changes with `git add` and leave the commit to the developer.
- **Never force push.** Do not run `git push --force` or `git push --force-with-lease` under any circumstances. Force pushing rewrites shared history and can cause data loss.
- **Never run destructive git commands** (`git reset --hard`, `git checkout .`, `git restore .`, `git clean -f`) without explicit developer instruction.
- **Staging is the limit.** Prepare and stage file changes; the developer runs `git commit` in their own environment where signing keys are available.

## Branches and merging

- Pipeline is `feature → develop → main`. Both branches are protected by branch rulesets; everything lands via PR.
- **Feature → develop PRs squash-merge** (single commit on develop, PR title becomes the commit message; never rebase-merge).
- **Develop → main PRs merge-commit** (one merge commit on main per release, develop's tip becomes a second parent and stays in main's ancestry — see [Develop → Main Promotion](#develop--main-promotion)).
- Open feature PRs against `develop`. `develop → main` is how stable releases are cut.

Repo settings reflect this: `allow_merge_commit=true`, `allow_squash_merge=true`, `allow_rebase_merge=false`, `allow_auto_merge=true`. The `develop` ruleset enforces `allowed_merge_methods=["squash"]` and `required_linear_history`. The `main` ruleset enforces `allowed_merge_methods=["merge"]` and intentionally omits linear-history (the develop → main merge commit is non-linear by design).

## Merging a PR

**Never merge a PR without `copilot-pull-request-reviewer` having posted a clean re-review on the latest commit** — defined as a review submitted *after* the head commit's `committedDate`, with no new unresolved inline threads (Copilot in this repo posts `COMMENTED` reviews, not `APPROVED`, so a clean COMMENTED review with zero open threads is the "no issues found" outcome). `mergeStateStatus: CLEAN` only confirms ruleset gates (thread resolution, status checks, signatures); it does not confirm Copilot has re-evaluated the latest changes.

After resolving Copilot's threads or pushing fixes:

1. Wait for Copilot to post a fresh review on the new head commit. The `copilot_code_review` rule on both `develop` and `main` rulesets has `review_on_push: true` configured (verify with `gh api repos/<repo>/rulesets/<id> --jq '.rules[] | select(.type=="copilot_code_review")'`), so a re-review normally lands within a few minutes.
2. A fresh review is identified by comparing `submitted_at` (from `gh api repos/<repo>/pulls/<n>/reviews`) against `committedDate` of the last commit (from `gh pr view <n> --json commits --jq '.commits[-1].committedDate'`). Fresh ⇔ `submitted_at > committedDate`.
3. If the fresh review is `COMMENTED` with zero unresolved inline threads (or `APPROVED`), the PR is good to merge.
4. If the fresh review introduces new concerns (inline threads or body-level objections), address them and loop.
5. **If Copilot does not auto re-review within a reasonable window after the latest push (~5 min), do not merge — ask the maintainer.** Silence is not approval.

This applies to every human-authored PR (feature → develop, develop → main). The merge-bot workflow's auto-merge of dependabot bumps is the only exception and is governed separately by the `update-type` filter.

## Develop → Main Promotion

Use the **"Create a merge commit"** option on develop → main PRs. Repo rulesets are split: PRs into `develop` are squash-only (linear history); PRs into `main` are merge-commit only. Clicking "Create a merge commit" on a develop → main PR produces a merge commit on main whose second parent is develop's tip — so develop becomes a real ancestor of main, and the *next* develop → main PR has a clean merge base (no recurring conflicts, no behind-base churn).

Under any squash-only setup this would be a recurring pain point: each develop → main squash drops develop's ancestry and forces a per-cycle admin-bypass merge commit on develop to resync. With merge-commit on main, that resync is unnecessary — main's history shows one merge commit per release (a feature, not a defect: each promotion is visible as a single auditable node), and develop stays linear.

**Immediately after a develop → main merge lands and main's publish workflows complete, bump the minor version in [version.json](version.json) on develop.** Open a small isolated feature PR `bump-version-X.Y` (e.g. `"version": "3.16"` → `"version": "3.17"`), squash into develop, and continue feature work from there. Without this bump, develop's next NBGV-computed prerelease (`3.16.<height>-g{sha}`) is *numerically lower* than the stable that just shipped (`3.16.<N>`), which is visibly confusing in HISTORY.md, `--version` output, and consumer update prompts. Bumping ensures every develop prerelease is `3.17.<height>-g{sha}` — visibly newer than main's `3.16.<N>`. Don't bundle the bump with other work; keep the PR isolated so the version change is unambiguous in git blame.

## Release flow

PlexCleaner is a "pull" project: consumers (`docker pull ptr727/plexcleaner:latest`, `docker pull ptr727/plexcleaner:develop`, GitHub Releases) track both branches. **Both `main` and `develop` auto-publish on every push** — there is no manual `workflow_dispatch` gate.

[publish-release.yml](.github/workflows/publish-release.yml) drives both prereleases and stable releases off the same [build-release-task.yml](.github/workflows/build-release-task.yml). It triggers on `push: [main, develop]`:

- **Push to `develop`** — automatic prerelease. Merging any PR into `develop` (feature, bug fix, dependabot) calls [get-version-task.yml](.github/workflows/get-version-task.yml) for an NBGV-computed version like `3.16.42-g1a2b3c4` (because develop does not match `publicReleaseRefSpec` in [version.json](version.json)) and creates a GitHub Release with `prerelease: true`. The Docker image is tagged `develop` by [publish-periodic-docker-release.yml](.github/workflows/publish-periodic-docker-release.yml).
- **Push to `main`** — automatic stable release. NBGV produces a clean version like `3.16.42` and creates a GitHub Release with `prerelease: false`. The Docker image is tagged `latest`.

Branch-aware logic lives in three places:

- [build-release-task.yml](.github/workflows/build-release-task.yml) — `prerelease: ${{ github.ref_name != 'main' }}` and `target_commitish: ${{ github.sha }}` (the latter is critical: without it, softprops creates the tag against the repo's default branch, mis-tagging develop builds onto main's tip).
- [publish-periodic-docker-release.yml](.github/workflows/publish-periodic-docker-release.yml) — `tag: ${{ github.ref_name == 'main' && 'latest' || 'develop' }}`.

Bot-merged PRs (Dependabot) trigger the publish workflows automatically because the merge-bot uses an App token — see the merge-bot section below.

## Dependabot

[.github/dependabot.yml](.github/dependabot.yml) targets **both `main` and `develop`** with two ecosystems each (`nuget`, `github-actions`), grouped per ecosystem, daily. The duplication is intentional: because both branches auto-publish, develop must not drift from main's dependency baseline. A NuGet major bump landing on develop should land on main on the next promotion cycle, not weeks later.

Major NuGet bumps are not auto-merged by [merge-bot-pull-request.yml](.github/workflows/merge-bot-pull-request.yml) — they require human review. Major GitHub Actions bumps are auto-merged because the workflow execution itself is the validation surface.

## GitHub Actions pinning

Every third-party action in `.github/workflows/*.yml` is pinned to a full commit SHA with a trailing comment matching the upstream release tag, e.g. `uses: actions/checkout@de0fac2e4500dabe0009e67214ff5f5447ce83dd # v6.0.2`. The comment is whatever tag the action's repo actually publishes — typically `# vX.Y.Z`, but use `# v3` if upstream only publishes major-only tags (e.g. `addnab/docker-run-action`) and `# master` if the action ships only a moving branch (rare). Floating refs without a SHA (`@v6`, `@main`, `@master`) are never used. Local reusable workflows (`./.github/workflows/*.yml`) are referenced by path and don't need pinning.

**Why:** Floating tags can be silently re-pointed by the action's owner (or by a compromised account) to malicious code; a SHA pin is immutable. Matching the comment to upstream's actual release tag (rather than fabricating one) lets dependabot rewrite both the SHA and the comment together when bumping.

When adding a new `uses:` line, resolve the latest release's commit SHA (`gh api repos/<owner>/<repo>/releases/latest`) and copy its `tag_name` into the comment verbatim. Don't ship a floating tag and "pin it later".

## Merge bot

[merge-bot-pull-request.yml](.github/workflows/merge-bot-pull-request.yml) auto-merges Dependabot PRs. Two key design choices:

- **Branch-aware merge method**: the script picks `--squash` for PRs targeting develop and `--merge` for PRs targeting main, matching each ruleset's `allowed_merge_methods`. An unknown base branch is a hard error.
- **App token, not GITHUB_TOKEN**: the merge step uses a token minted by `actions/create-github-app-token` from `CODEGEN_APP_CLIENT_ID` / `CODEGEN_APP_PRIVATE_KEY` secrets. Pushes authored by `GITHUB_TOKEN` are blocked from triggering downstream workflows by GitHub's recursion guard; without the App token, a Dependabot merge to develop would silently skip `publish-release.yml` and `publish-periodic-docker-release.yml`, leaving the develop Docker tag and prerelease stale.

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
