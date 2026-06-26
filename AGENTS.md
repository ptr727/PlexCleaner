# Instructions for AI Coding Agents

**PlexCleaner** is a .NET 10 CLI utility that optimizes media files for Direct Play in Plex/Emby/Jellyfin (converting containers to MKV, re-encoding incompatible codecs, managing tracks and language tags, verifying and repairing media, and monitoring folders for changes). It orchestrates external media tools - FFmpeg, HandBrake, MkvToolNix, MediaInfo, and 7-Zip - through CLI wrappers. It ships two release targets: a multi-arch Docker image (Docker Hub `ptr727/plexcleaner`) and standalone executables attached to GitHub Releases; consumers pull from Docker Hub or the GitHub releases on their own cadence. The repo also contains an xUnit test project (`PlexCleanerTests/`).

This file is the canonical reference for cross-cutting AI-agent and workflow rules. C# code-style conventions live in [`CODESTYLE.md`](./CODESTYLE.md). Copilot review *mechanics* are owned by [`.github/copilot-instructions.md`](./.github/copilot-instructions.md) - this file delegates them there explicitly (see "PR Review Etiquette" below). PlexCleaner's architecture, processing pipeline, and design patterns live in [`ARCHITECTURE.md`](./ARCHITECTURE.md). High-level summaries in other docs (e.g. README's Contributing section) are allowed when they link back here; don't duplicate the rules themselves. The app's **project-specific conventions** also live here and in `ARCHITECTURE.md`, **not** in `.github/copilot-instructions.md` - that file targets GitHub Copilot / VS Code specifically, while this file is the agent-agnostic one every coding agent reads, so any rule a reviewer must honor has to live here to be provider-independent.

## Git and Commit Rules

- **Default to staging, not committing.** Stage changes with `git add` and leave `git commit` to the developer unless the developer has explicitly authorized the agent to commit for the current ask ("commit this", "open a PR", etc.). Authorization is scope-bound - it covers the commits needed for that specific task, not a blanket commit license for the rest of the session.
- **All commits must be cryptographically signed (SSH or GPG).** Branch protection enforces this on both branches; unsigned commits are rejected on push. Signing depends on environment configuration - `git config commit.gpgsign true`, a configured `user.signingkey`, and a working signing agent (loaded `ssh-agent` for SSH, or `gpg-agent` for GPG). If signing is not configured in the environment, **do not commit** - surface the missing config to the developer and stop at `git add`. Verify before any agent-authored commit (`git config --get commit.gpgsign && ssh-add -L` or the GPG equivalent). **Signing must be live before the *first* commit, not retrofitted.** Turning on `Require signed commits` against a branch that already has unsigned commits forces a rewrite of that entire history to re-sign it - changing every commit SHA and making whoever does the rewrite the committer and signer of every commit (a rebase preserves the `author` field but not the original signatures; you cannot sign another contributor's commits for them). During new-repo setup, never create commits until signing is verified.
- **Never force push.** Do not run `git push --force` or `git push --force-with-lease` under any circumstances. Force pushing rewrites shared history and can cause data loss.
- **Never run destructive git commands** (`git reset --hard`, `git checkout .`, `git restore .`, `git clean -f`) without explicit developer instruction.

### Git and Commit Rules - Repo-Specific Notes

- **The `develop -> main` release merge is maintainer-only.** Drive `feature -> develop` PRs end-to-end when authorized (commit, push, Copilot review loop, squash-merge), but never self-merge a release to `main`.

## Branching Model

- `develop` is the integration branch. Feature branches -> `develop` is **squash-only**; develop is kept linear.
- `develop` -> `main` is **merge-commit only** (no squash, no rebase). Merge commits preserve develop's commit list as a real second-parent reference on main, which lets the release model attribute releases to the develop commits that produced them (relevant both for the weekly publish and the opt-in `PUBLISH_ON_MERGE` mode - see "Release Model" below). Branch protection enforces this: the develop ruleset allows only `squash`, the main ruleset allows only `merge`.
- All commits on both branches must be cryptographically signed (SSH or GPG). Squash and merge commits created via the GitHub UI are signed by GitHub's web-flow key.
- **`develop` is forward-only - no `main -> develop` back-merges.** The develop ruleset's squash-only setting physically blocks merge commits on develop. Historical back-merge commits visible in `git log` predate this rule and must not be repeated.
- **Both rulesets intentionally omit "Require branches to be up to date before merging" (`strict_required_status_checks_policy: false`), for two distinct reasons:**
  - *Main* - the check is graph-based; it asks whether main's tip commit is reachable from develop, not whether the two branches have the same content. After any develop -> main release, main's tip is a brand-new merge commit that develop's history doesn't contain. Forward-only develop never adds it (no back-merge of main into develop), so the check would fail on every subsequent release.
  - *Develop* - bot auto-merge incompatibility. When two bot PRs against develop land in the same minute (e.g. two grouped Dependabot PRs from the same daily run), the first to merge pushes the second into `mergeStateStatus: BEHIND`. GitHub's auto-merge will not fire while the strict flag is on, and nothing in the workflow set auto-updates a bot branch in that window - the merge-bot enables auto-merge via `gh pr merge --auto` but never rebases a stalled branch onto base (see [`merge-bot-pull-request.yml`](./.github/workflows/merge-bot-pull-request.yml)). Real file-level conflicts are still caught textually (`mergeable: CONFLICTING` blocks merge regardless); semantic-but-not-textual conflicts that combine cleanly are caught by the post-merge develop CI run rather than pre-merge. Do not reintroduce the strict flag on develop thinking it's hygiene - it breaks bot auto-merge.
- **Dependabot targets both `main` and `develop` in parallel.** [`.github/dependabot.yml`](./.github/dependabot.yml) duplicates every ecosystem entry (one per branch). Each branch absorbs its own bot PRs independently, so neither falls behind, and the forward-only rule still holds (nothing is back-merged from main to develop - both branches receive their updates directly). Parallel auto-merge across same-batch bot PRs is race-proof only because both rulesets have the strict "up to date" flag off (see bullet above). The merge-bot ([`.github/workflows/merge-bot-pull-request.yml`](./.github/workflows/merge-bot-pull-request.yml)) dispatches `--squash` or `--merge` from each PR's base ref via a `case` statement so the form matches the ruleset on either base. Dependabot **security** PRs (CVE-driven) always open against the repo default branch (`main`) regardless of `target-branch` - the same `case` statement covers them. Semver-major NuGet bumps gate on human review; everything else auto-merges.
- **Maintainer-pushed commits on a bot PR auto-disable auto-merge.** The merge-bot's `merge-dependabot` job only fires on `opened` / `reopened` events (auto-merge is enabled exactly once per PR, for Dependabot-authored PRs that originate from this repository, not forks). When a maintainer pushes commits to the bot's branch (a `synchronize` event with a non-bot actor), the `disable-auto-merge-on-maintainer-push` job fires and calls `gh pr merge --disable-auto`; the maintainer's commits stay in the PR but won't auto-merge with the bot's content. Re-enable manually (`gh pr merge --auto <PR>`) when ready. The merge-bot is on `pull_request_target` with per-PR concurrency; it carries only `merge-dependabot` + `disable-auto-merge-on-maintainer-push` (no `merge-codegen` / `merge-upstream-version` - this repo has neither codegen nor an upstream-version tracker).
- **App-token workflows use Client ID, not App ID.** `actions/create-github-app-token` deprecated the numeric `app-id` input in v3.0.0; the merge-bot uses `client-id: ${{ secrets.CODEGEN_APP_CLIENT_ID }}` (with `private-key: ${{ secrets.CODEGEN_APP_PRIVATE_KEY }}`). The App token - not `GITHUB_TOKEN` - is required so the merge push is committed by the App and fires downstream workflows (`GITHUB_TOKEN` pushes are blocked from triggering further runs by GitHub's recursion guard). When adding new App-token call sites, use the same form - do not reintroduce `app-id`.
- **Why parallel dual-target rather than develop-only with eventual flow-through:** consumers pull the Docker image and the release executables from `main` directly. A develop-only model would leave `main` running stale code during long-running develop features, so both branches receive their own bot updates on their own cadence and each stays current.

## Release Model

This repo uses a **two-phase model by default**: PRs build fast, publishing is batched weekly. The load-bearing rules:

- **PRs smoke-test only.** [`test-pull-request.yml`](./.github/workflows/test-pull-request.yml) always runs unit tests, then a `dorny/paths-filter` `changes` job gates a **reduced, never-published** build of only the changed targets (Docker `linux/amd64` only, plus the executable on a representative runtime subset), with no push. Build-workflow files are intentionally not in the path filters - a filter can't tell a logic change from an action-version bump - so a workflow-only change isn't smoke-built; the reusable workflows are exercised by the next run that uses them. There is no CI workflow-lint job; lint workflow edits with `actionlint` locally before pushing.
- **Merges don't publish by default.** [`publish-release.yml`](./.github/workflows/publish-release.yml) is the sole publisher: its **weekly schedule** (Mondays 02:00 UTC) and **manual `workflow_dispatch`** always do the full build/publish of **both** `main` and `develop` (a branch matrix). Its `push` trigger publishes only when the **`PUBLISH_ON_MERGE` repository variable** is `true` (opt-in legacy continuous-release). Unset/`false` = two-phase.
- **Required check.** The `changes` job is in the `Check pull request workflow status` aggregator's `needs` and **must succeed** (not just "not fail") - a paths-filter error must never let a target-changing PR merge with its smoke build silently skipped. Skipped smoke jobs (no matching change) pass; `failure`/`cancelled` blocks.
- **Reusable-task parameter contract.** [`build-release-task.yml`](./.github/workflows/build-release-task.yml) and the leaf `build-*-task.yml` workflows take `ref` (git ref to check out/version), `branch` (logical branch driving config/tags/prerelease - `main` => Release/`latest`/non-prerelease, else Debug/`develop`/prerelease), and where relevant `smoke`. **Branch-derived config keys off `inputs.branch`, never `github.ref_name`** - the publisher's matrix builds `develop` from a run whose `github.ref_name` is `main`, so `ref_name` would be wrong. Artifact names are branch-suffixed so both matrix legs coexist in one run. [`get-version-task.yml`](./.github/workflows/get-version-task.yml) takes a `ref` so NBGV versions the right branch, and exposes `GitCommitId` so the release tag and built artifacts pin to the exact built commit.
- **The release-asset seam.** A target contributes files to the GitHub release by uploading a workflow artifact named `release-asset-<branch>-<target>`. The `github-release` job collects every `release-asset-<branch>-*` artifact by pattern and **never names a build job**, so the tag-the-commit + create-the-release + attach-the-assets logic is reusable **verbatim**. PlexCleaner's executable target ([`build-executable-task.yml`](./.github/workflows/build-executable-task.yml)) uses this seam - it `dotnet publish`es the standalone executables and uploads them as `release-asset-<branch>-*`. The Docker target ([`build-docker-task.yml`](./.github/workflows/build-docker-task.yml)) pushes multi-arch tags directly to Docker Hub (`latest` for main, `develop` for develop) and contributes **no** `release-asset-*`. The Docker Hub repository overview is pushed separately by [`publish-docker-readme-task.yml`](./.github/workflows/publish-docker-readme-task.yml), gated to `main`.
- **Versioning is semantic and maintainer-controlled.** The `version` (major.minor) in [`version.json`](./version.json) is the version floor; NBGV appends the git height (the SemVer patch position) for the build version. `main` (the public release ref) builds a stable `X.Y.<height>`; `develop` builds a prerelease `X.Y.<height>-g<sha>`. `version.json`'s `publicReleaseRefSpec` is `^refs/heads/main$`. The maintainer edits `version.json`; dependency bumps, CI/workflow fixes, doc edits, and template re-syncs leave it untouched.
  - **Bump `version.json` only for functional changes, by maintainer instruction.** Raise the major/minor when the work being introduced warrants a new semantic version - a new feature, a behavior change, a breaking change - and do it in the PR that introduces that work (typically on `develop`). Do **not** bump on a fixed cadence or mechanically after a release. NBGV advances the patch (git height) on every commit automatically, so a release always gets a fresh build version without any `version.json` edit.
  - **No post-release bump; no develop-ahead requirement.** NBGV advances the patch (git height) on every commit, so a release always gets a fresh build version with no `version.json` edit and there is no `bump-version-X.Y` PR after a release. A `develop -> main` promotion carries whatever `version.json` is current: a promotion with a functional bump releases that new version on `main`; a maintenance-only promotion (dependency bumps, CI/doc fixes, template re-syncs) carries the unchanged `version.json` and `main` advances only its NBGV height.

## Pull Request Title and Commit Message Conventions

### Format

- Imperative subject summarizing the change, <=72 characters, no trailing period. ("Add 24-hour PM2.5 average sensor", not "Added X" or "Adds X".)
- Optional body, blank-line separated, explaining *why* the change is being made when that's non-obvious. The diff shows *what*.

### Rules

- Don't write `update stuff`, `wip`, or other vague titles. (Dependabot's default `Bump X from Y to Z` titles are fine - keep them.)
- Don't add `Co-Authored-By:` lines unless the developer explicitly asks.
- Don't put release-bump magnitude in the title - no "minor", "patch", "release v0.2.0", etc. Nerdbank.GitVersioning computes the next release version from `version.json` + git history. Dependency versions in dependency-bump titles are fine and expected.
- Use US English spelling and match the existing heading style of the file you're editing: title case with lowercase short bind words (a, an, the, and, but, or, of, in, on, at, to, by, for, from); hyphenated compounds capitalize both parts unless the second is a short preposition (*Built-in*, *EPA-Corrected*, *24-Hour*).

### Examples

```text
Add Direct Play seek-index verification
Pin softprops/action-gh-release to commit SHA
Remove embedded closed captions during remux
Bump xunit.v3 from 3.2.2 to 3.3.0
Clarify HandBrake custom-options usage in README
```

## Documentation Style Conventions

### Markdown

- Use reference-style links for any URL referenced more than once or appearing in lists; alphabetize the reference definitions block.
- Inline single-use relative links (e.g. `[CODESTYLE.md](./CODESTYLE.md)`) are fine.
- One logical paragraph per line; no hard-wrap line-length limit. For an intentional hard line break within a block - stacked badges, status, or license lines - end the line with a trailing backslash (`\`); this explicit form is preferred over trailing whitespace and is not treated as a paragraph split.
- Headings follow the title-case-with-short-bind-words rule from the PR-title section.
- **Write docs in the current state, not as a change from a prior one.** The reader has no memory of the previous behavior, so describe what *is*: "X does Y", never "X *now* does Y", "X *no longer* does Z", or "changed/switched/restored to Y". Before/after framing belongs in changelogs, commit messages, and PR descriptions - not in `README.md` or other living docs.

### Comments

Applies to code and workflow (`#`) comments alike.

- Comment only when the code does not explain itself or the logic is genuinely complex. Self-evident code needs no comment.
- Write for the human reading *this* project's code now: state what the code does and only the non-obvious *why*. No cross-project references (do not name other repos), no historic or design narrative, no rule citations - governance lives in this file, not echoed inline.
- Match the surrounding code's line length (typically ~120), not an 80-column wrap.

### Character Set

- **Write ASCII in all agent-authored text** - documentation, code, comments, commit messages, and PR descriptions. The agent does not introduce non-ASCII characters. Replace typographic Unicode with its ASCII equivalent on sight:
  - em dash (U+2014) and en dash (U+2013) -> hyphen `-` (use a spaced ` - ` for an em-dash-style clause break)
  - right arrow (U+2192) -> `->`; double arrow (U+21D2) -> `=>`
  - less-than-or-equal (U+2264) -> `<=`; greater-than-or-equal (U+2265) -> `>=`
  - curly quotes (U+2018/U+2019/U+201C/U+201D) -> straight `'` and `"`; ellipsis (U+2026) -> `...`
- **Allowed non-ASCII (two narrow exceptions):**
  - **Scientific or technical symbols with no clean ASCII equivalent** - e.g. ohm, micro, degree, pi. Keep the symbol; do not approximate it away.
  - **Unicode the developer deliberately typed** - emoji used for emphasis or as callout markers (for example the warning/info markers a maintainer placed in `README.md`). Preserve it; never strip the developer's own characters. This carve-out is for developer-authored text, not a license for the agent to add emoji.

### Line Endings

- [`.editorconfig`](./.editorconfig) defines the correct ending per file type (CRLF for `.md`, `.cs`, XML/`.csproj`/`.props`, `.yml`/`.yaml`, `.json`, `.cmd`/`.bat`/`.ps1`; LF for `.sh`), and [`.gitattributes`](./.gitattributes) (`* -text`) stops git from normalizing. The defaults + per-extension EOL block is always-verbatim from the template; the `[*.cs]`/ReSharper style block is .NET-only and is carried because this repo ships .NET.
- **Editing an existing file: preserve its current line endings** - do not reflow them as a side effect of a content change, even if the file is already non-compliant. After any programmatic edit, verify with `git diff --stat` (only changed lines) and `file <path>` (expected ending). Bring a non-compliant file to its `.editorconfig` ending only as a deliberate, isolated EOL-only change.

### Quantitative Claims

- Any quantitative claim in `README.md` (counts, sizes, version floors, supported platforms) must be verified against current code. If a doc number is derived from a code constant, mark the dependency in a source-code comment so the next editor knows to update both.

## PR Review Etiquette

> **Mandatory in every derived repo.** This entire "PR Review Etiquette" section is the provider-agnostic review-loop *contract* and must be carried **verbatim** into every repo derived from this template, alongside the [`.github/copilot-instructions.md`](./.github/copilot-instructions.md) "GitHub Copilot Review Runbook" that implements it. Without both in-repo, an agent working in the derived repo has no pointer to the reliable Copilot mechanics and falls back to ad-hoc (and known-broken) behavior.

The repo runs a review loop on every PR: local agent iteration plus remote automated review (GitHub Copilot is the configured reviewer). Treat this as a contract regardless of which local agent authored the changes.

### Merge Gate (read this first)

**Do not merge - and do not enable auto-merge - unless ALL of these hold:**

1. Required status checks are green (`mergeStateStatus: CLEAN`), **and**
2. A Copilot review is confirmed on the **current head SHA** (not an earlier push), **and**
3. **Every** Copilot finding on that head SHA is closed out - all review threads resolved, **and** any issue-level Copilot comments (which have no resolve action) triaged and replied to - so zero outstanding findings remain, **and**
4. The maintainer has given **explicit** permission to merge.

`mergeStateStatus: CLEAN` reflects **only** required statuses - it never reflects open bot review comments, so `CLEAN` alone is **never** sufficient to merge. A green/`CLEAN` PR with an unresolved Copilot finding fails this gate; treat it as "not mergeable" no matter what the merge-state field says. The agent never merges on its own (consistent with "default to staging"; merging is maintainer-authorized).

**Merging is not releasing.** A merge to a release branch does **not** by itself publish; publishing is a separate, explicitly configured step in the repo's release pipeline (e.g. a scheduled run, a manual dispatch, or an opted-in publish-on-merge trigger), not an automatic consequence of merging. Never describe a merge as cutting a release, and never trigger a publish without explicit maintainer instruction.

### Expected Review Loop

1. Push changes to the PR branch.
2. Re-request a review for the **current head SHA**. Auto-trigger is unreliable, so request it explicitly via the `requestReviews` GraphQL mutation (now reliable end-to-end - see the runbook); the UI is only a fallback.
3. Wait for review activity on that head. A completed review that raises **no findings** is a valid terminal outcome for that head - proceed; do not re-trigger it or treat the absence of comments as a missing review.
4. Triage findings.
5. Apply fixes or write a rationale for declines.
6. Reply to each thread and resolve what was addressed.
7. Re-run the loop after every fix push until no actionable findings remain.

Drive the loop to green - review confirmed on the latest head SHA and every actionable finding closed - then stop and apply the **Merge Gate** above: all four preconditions must hold, and `mergeStateStatus: CLEAN` alone never satisfies it.

For provider-specific mechanics (how to request review, query review state, post replies, resolve threads), see the **GitHub Copilot Review Runbook** in [.github/copilot-instructions.md](./.github/copilot-instructions.md). This file owns the contract; that file owns the mechanics.

### Triaging Review Comments

For each comment, classify before responding:

- **Bug** - wrong behavior, missing test coverage, or a real divergence between code and docs. Fix it. Reply with the fixing commit SHA when done.
- **Style/convention** - the comment cites a rule from this file or a language-specific style guide. Two cases:
  - The cited rule matches what the existing codebase already does -> fix the offending code.
  - The cited rule contradicts what's in the tree, or industry norm -> **update the rule instead of the code**. The rule is wrong, not the code. Bouncing the same code across rounds is the symptom of a wrong rule. Heuristic: three rounds on the same style category means the rule needs adjusting and the user should authorize the rule change.
- **Architectural opinion** - the comment proposes a different design ("constrain this to disabled-by-default", "move it elsewhere", "add a runtime guardrail"). This is judgment, not a bug. Surface it to the user with a recommendation; don't apply unilaterally.

### Responding and Resolution Expectations

Reply inline with either the fixing commit SHA (for accepted issues) or a concise rationale (for declines). Resolve review threads when addressed or intentionally declined with rationale. Issue-level comments (those at `repos/.../issues/<N>/comments` rather than tied to a specific line) have no resolution action - acknowledge with a reply if needed and move on.

After the final push on a PR, sweep older threads from earlier rounds whose code paths no longer exist; otherwise stale unresolved markers remain in the review UI.

### Escalating to the User

Bring the user in when:

- **Genuine design trade-off** surfaces (fail-open vs fail-closed, narrow vs broad refactor scope, "should we add a guardrail or trust the docstring"). Triage, recommend, ask.
- **Repeated friction** across rounds without convergence - that's the rule-needs-updating signal. Stop, summarize the pattern, and let the user authorize the rule change.
- **Architectural redesign** is requested rather than a bug fix. Surface with a recommendation; never apply unilaterally.

Anti-pattern: don't keep flipping the code on the same style point. Flip the rule once and stick to the rule.

## Staying in Sync with the Template

This repo is derived from [`ptr727/ProjectTemplate`](https://github.com/ptr727/ProjectTemplate) and re-syncs against it periodically, not just at creation.

- **Verbatim carries.** Pull the current template version of each shared artifact and re-apply it, adapting only this repo's placeholders: [`.github/copilot-instructions.md`](./.github/copilot-instructions.md) (the Copilot review runbook - change only the `<owner>`/`<repo>`/`<N>` values in its API snippets), [`.markdownlint-cli2.jsonc`](./.markdownlint-cli2.jsonc), [`.editorconfig`](./.editorconfig), [`.gitattributes`](./.gitattributes), and this file's [PR Review Etiquette](#pr-review-etiquette) section. The `.editorconfig` EOL/per-extension block is always-verbatim; its `[*.cs]`/ReSharper block is .NET-only and is carried here. Keep `copilot-instructions.md` **narrow** (provider mechanics plus the commit/PR-title summary); project-specific conventions live in this file and the architecture deep-dive lives in [`ARCHITECTURE.md`](./ARCHITECTURE.md), not there - non-Copilot agents are not directed to that file.
- **CODESTYLE.md.** Re-sync the whole file from the template; it is carried whole, so the language sections this repo doesn't ship stay inert (this repo is .NET-only). Repo-root placement is load-bearing - `AGENTS.md` and `.github/copilot-instructions.md` link it by relative path. The file is genericized with neutral placeholders, so re-sync is a clean wholesale overwrite.
- **.vscode/tasks.json.** Carry the named **clean-compile** task definitions verbatim - `.Net Build`, `CSharpier Format`, and `.Net Format` (which chains the first two then `dotnet format style --verify-no-changes`). Their names are owned by the `CODESTYLE.md` ".NET" section and their command sequence + arguments are the canonical clean-compile spec; don't loosen them. Convenience tasks are the adapt zone.
- **Release notes.** Keep a short release-notes summary in [`README.md`](./README.md) and the full history in [`HISTORY.md`](./HISTORY.md); update both when cutting a release.
- **Report drift upstream.** When a re-sync surfaces a template gap, an outdated instruction, or something that bit this repo and would bite the next derived repo, open an issue in [`ptr727/ProjectTemplate`](https://github.com/ptr727/ProjectTemplate) rather than only patching locally - the template is the single source of truth, and this upstream-issue rule is this repo's only cross-repo obligation. Do not maintain or reference a "known downstream" registry, and do not name sibling repositories in docs, comments, or workflows - that registry and the maintainer fan-out duty live in the template hub only.

### Template adaptations

Intentional deviations from a literal verbatim carry, kept on purpose:

- **`.editorconfig`** carries the template verbatim plus a repo-wide block of CA-rule relaxations in `[*.cs]` (console-app, not a library; documented inline). The template's per-extension EOL block - including the `Dockerfile`/`*.Dockerfile` LF pins - is carried as-is.
- **`.gitattributes`** carries the template verbatim plus a `.husky/pre-commit text eol=lf` pin (this repo ships an extensionless Husky.Net hook, the exact case the template's `*.sh`/extensionless-script note calls out) and a `Docker/README.m4 text eol=lf` pin (m4 source rendered on Linux).
- **`.github/copilot-instructions.md`** keeps this repo's filled `ptr727`/`PlexCleaner` placeholders, its [`ARCHITECTURE.md`](./ARCHITECTURE.md) pointer, and the `.NET`-only language wording (no Python) - already adapted from the template's placeholder/multi-language form.
- **`CODESTYLE.md`** is carried whole from the template (genericized - generic project-name placeholders, both language sections); this repo's real project names live in `.csproj`/`.editorconfig`, not the style guide.
- **Husky.Net pre-commit hooks.** This repo runs Husky.Net (a `.husky/` hook runner + a `Husky.Net Run` VS Code task), inverting the template's no-hooks-by-default stance.
- **`.vscode/tasks.json`** carries the template's `.NET` clean-compile tasks verbatim (labels and command sequences) and adds this repo's Docker/Husky convenience tasks; the `.NET` task-label casing now matches the template (the former `.Net` casing was re-converged to the template's official casing per [`CODESTYLE.md`](./CODESTYLE.md)).

## Workflow YAML Conventions

These conventions describe the target state. New and modified workflows must respect them; the rest of the repo is expected to be brought up to the same standard.

- **Action pinning**: pin **every** action - first-party (`actions/*`) and third-party - to a commit SHA with a trailing `# vX.Y.Z` comment, so Dependabot can still bump it but a tag swap can't change the executed code. Use `# vX` (major-only) only when the upstream's floating major tag doesn't correspond to a specific patch/minor release SHA - pinning to the floating-tag SHA still gives the SHA guarantee, the version comment just records the major line. Every action in this repo, including [`dotnet/nbgv`](./.github/workflows/get-version-task.yml), is SHA-pinned with no exceptions.
- **Filename**: reusable workflows (those with `on: workflow_call`) end in `-task.yml`. Entry-point workflows (`on: push` / `pull_request` / `schedule` / `workflow_dispatch`) do NOT use the `-task` suffix; they end with what they do - `-pull-request.yml`, `-release.yml`, etc. The suffix carries semantic meaning: a `-task.yml` file is meant to be `uses:`-d, never triggered directly.
- **Workflow `name:`** (the top-level `name:` field): reusable workflow names end in **"task"** (e.g. `Build executable task`); entry-point workflow names end in **"action"** (e.g. `Publish project release action`, `Test pull request action`). The displayed action name in the GitHub Actions UI tells you at a glance whether you're looking at an orchestrator or a callee.
- **Job and step `name:` suffixes**: every job's `name:` ends in **"job"**; every step's `name:` ends in **"step"**. **Exception**: a job whose `name:` is also referenced as a required-status-check `context:` in a branch ruleset (currently `Check pull request workflow status` in `test-pull-request.yml`) keeps the ruleset-bound name verbatim - renaming would silently break required-status-check enforcement. Do not "fix" that name; if a future job becomes ruleset-bound, mark it the same way.
- **Concurrency**: top-level workflows declare `concurrency: { group: '${{ github.workflow }}-${{ github.ref }}', cancel-in-progress: true }` so a fresh push supersedes an in-flight run on the same ref. **Documented exception** (records the rationale inline in its header comment): [`publish-release.yml`](./.github/workflows/publish-release.yml) uses both a **global, ref-independent group** for real publishes (`group: ${{ github.workflow }}`, dropping the usual `-${{ github.ref }}`) and `cancel-in-progress: false`. Its schedule/dispatch runs publish both branches regardless of the triggering ref, so a ref-scoped group would let a scheduled run (ref `main`) and a manual dispatch (ref `develop`) run concurrently and double-publish; and cancelling a publish mid-flight can leave a half-created GitHub release or a partially pushed Docker tag set. Non-publishing (two-phase default) `push` runs get a unique per-run group so they never queue behind a real publish.
- **Shells**: multi-line `run:` blocks with bash start with `set -euo pipefail` - fail fast, fail on undefined vars, fail on a failed pipe segment.
- **Conditionals**: multi-line `if:` uses folded scalar `if: >-` so YAML preserves whitespace correctly. Literal block (`if: |`) is wrong because it embeds newlines inside the boolean expression.
- **Boolean inputs**: workflows triggered both via `workflow_call` and `workflow_dispatch` must declare each boolean input in *both* trigger blocks - one definition does not propagate to the other. `workflow_call` delivers booleans as actual booleans; `workflow_dispatch` delivers them as the *strings* `"true"`/`"false"`. Any `if:` consuming a boolean input must compare against both forms - `if: ${{ inputs.foo == true || inputs.foo == 'true' }}`.
- **Reusable workflows**: job-level `permissions:` are validated *before* the `if:` evaluates, so even a skipped job needs valid permissions declared. A `release` job with `permissions: contents: write` and `if: ${{ inputs.publish }}` will still cause `startup_failure` on a caller that doesn't grant `contents: write`. Either declare permissions at the call site, or omit the inner block and inherit.
- **Allowlist `success` and `skipped` explicitly** when chaining jobs across optional dependencies - `!= 'failure'` lets `cancelled` through (timeout, runner failure, manual cancel). Use `(needs.X.result == 'success' || needs.X.result == 'skipped')`.
- **Artifact retention**: intermediate build artifacts (`actions/upload-artifact`) are consumed by a later job in the same run, so set `retention-days: 1` - the default 90-day retention otherwise piles up against the account-wide artifact-storage quota. The durable copies live on the GitHub release, not in workflow artifacts.
- **Docker layer cache**: cache to/from a registry tag (`type=registry`, e.g. `buildcache-<branch>` on Docker Hub), not the GitHub Actions cache (`type=gha`), to keep large image layers off the 10 GB Actions cache.
- **Tag pinning on releases**: when using `softprops/action-gh-release` (or any tag-creating action), pass `target_commitish` explicitly - without it, GitHub's REST API defaults the new tag to the repository's default branch instead of the commit that built the artifact. Pin it to the **exact built commit's SHA** (the publisher uses NBGV's `GitCommitId` output), not `github.sha` (wrong branch in the publisher's branch matrix - a `develop` leg runs with `github.sha` = main's tip) and not a branch name (a moving ref that a mid-run commit could advance past the built tree).

## Project Structure

- **PlexCleaner** (`PlexCleaner/PlexCleaner.csproj`)
  - The CLI application - orchestrates FFmpeg, HandBrake, MkvToolNix, MediaInfo, and 7-Zip to optimize media for Direct Play.
  - Target framework: .NET 10.0, AOT compiled (`<PublishAot>true</PublishAot>`). Internals are exposed to the test project via `InternalsVisibleTo`.
- **PlexCleanerTests** (`PlexCleanerTests/PlexCleanerTests.csproj`)
  - xUnit v3 test suite. Assertions via AwesomeAssertions.
- **`Docker/`** - multi-arch Linux container build (`ubuntu:rolling`, `linux/amd64` + `linux/arm64`); runs as a `nonroot` user, mounts media under `/media`.
- **Build configuration**:
  - Common MSBuild properties (`TargetFramework`, `Nullable`, `ImplicitUsings`, `AnalysisLevel`, etc.) live in `Directory.Build.props` at the solution root. Do not duplicate these in individual `.csproj` files - only add a property to a `.csproj` when it is project-specific or overrides the shared default.
  - All NuGet package versions are centralised in `Directory.Packages.props`. `PackageReference` elements in `.csproj` files must not include a `Version` attribute. Asset metadata (`PrivateAssets`, `IncludeAssets`) stays in the `.csproj` `PackageReference` element.
- **Style guide / further reading**: [`CODESTYLE.md`](./CODESTYLE.md) for C# code conventions; [`.github/copilot-instructions.md`](./.github/copilot-instructions.md) for the Copilot review runbook; and [`ARCHITECTURE.md`](./ARCHITECTURE.md) for the architecture, processing pipeline, and design patterns - read it before changing processing, sidecar, media-tool, or language-tag code.
