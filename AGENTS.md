# Instructions for AI Coding Agents

**PlexCleaner** is a .NET 10 CLI utility that optimizes media files for Direct Play in Plex/Emby/Jellyfin (converting containers to MKV, re-encoding incompatible codecs, managing tracks and language tags, verifying and repairing media, and monitoring folders for changes). It orchestrates external media tools - FFmpeg, HandBrake, MkvToolNix, MediaInfo, and 7-Zip - through CLI wrappers. It ships two release targets: a multi-arch Docker image (Docker Hub `ptr727/plexcleaner`) and standalone executables attached to GitHub Releases; consumers pull from Docker Hub or the GitHub releases on their own cadence. The repo also contains an xUnit test project (`PlexCleanerTests/`).

This file is the canonical reference for cross-cutting AI-agent rules. The CI/CD workflow contract and conventions live in [`WORKFLOW.md`](./WORKFLOW.md); C# code-style conventions live in [`CODESTYLE.md`](./CODESTYLE.md). Copilot review *mechanics* are owned by [`.github/copilot-instructions.md`](./.github/copilot-instructions.md) - this file delegates them there explicitly (see "PR Review Etiquette" below). PlexCleaner's architecture, processing pipeline, and design patterns live in [`ARCHITECTURE.md`](./ARCHITECTURE.md). High-level summaries in other docs (e.g. README's Contributing section) are allowed when they link back here; don't duplicate the rules themselves. The app's **project-specific conventions** also live here and in `ARCHITECTURE.md`, **not** in `.github/copilot-instructions.md` - that file targets GitHub Copilot / VS Code specifically, while this file is the agent-agnostic one every coding agent reads, so any rule a reviewer must honor has to live here to be provider-independent.

**Where rules live.** A durable project, code, or style rule belongs in this file (or `WORKFLOW.md` / `CODESTYLE.md` as appropriate), so it is versioned and read by every session and every agent. An agent's own session memory or scratch state is private and lost on restart, so it is never the system of record for a rule: when you learn or are corrected on a rule, write it into the right doc in the same change. Memory may also note it, but the committed docs are the source of truth.

## Git and Commit Rules

- **Default to staging, not committing.** Stage changes with `git add` and leave `git commit` to the developer unless the developer has explicitly authorized the agent to commit for the current ask ("commit this", "open a PR", etc.). Authorization is scope-bound - it covers the commits needed for that specific task, not a blanket commit license for the rest of the session.
- **All commits must be cryptographically signed (SSH or GPG).** Branch protection enforces this on both branches; unsigned commits are rejected on push. Signing depends on environment configuration - `git config commit.gpgsign true`, a configured `user.signingkey`, and a working signing agent (loaded `ssh-agent` for SSH, or `gpg-agent` for GPG). If signing is not configured in the environment, **do not commit** - surface the missing config to the developer and stop at `git add`. Verify before any agent-authored commit (`git config --get commit.gpgsign && ssh-add -L` or the GPG equivalent). **Signing must be live before the *first* commit, not retrofitted.** Turning on `Require signed commits` against a branch that already has unsigned commits forces a rewrite of that entire history to re-sign it - changing every commit SHA and making whoever does the rewrite the committer and signer of every commit (a rebase preserves the `author` field but not the original signatures; you cannot sign another contributor's commits for them). During new-repo setup, never create commits until signing is verified.
- **Never force push.** Do not run `git push --force` or `git push --force-with-lease` under any circumstances. Force pushing rewrites shared history and can cause data loss.
- **Never run destructive git commands** (`git reset --hard`, `git checkout .`, `git restore .`, `git clean -f`) without explicit developer instruction.

### Git and Commit Rules - Repo-Specific Notes

- **The `develop -> main` release merge is maintainer-only.** Drive `feature -> develop` PRs end-to-end when authorized (commit, push, Copilot review loop, squash-merge), but never self-merge a release to `main`.

## Branching Model

- `develop` is the integration branch. Feature branches -> `develop` is **squash-only**; develop is kept linear.
- `develop` -> `main` is **merge-commit only** (no squash, no rebase). Merge commits preserve develop's commit list as a real second-parent reference on main, which lets the release model attribute releases to the develop commits that produced them (relevant to the weekly publish - see "Release Model" below). Branch protection enforces this: the develop ruleset allows only `squash`, the main ruleset allows only `merge`.
- All commits on both branches must be cryptographically signed (SSH or GPG). Squash and merge commits created via the GitHub UI are signed by GitHub's web-flow key.
- **`develop` is forward-only - no `main -> develop` back-merges.** The develop ruleset's squash-only setting physically blocks merge commits on develop. Historical back-merge commits visible in `git log` predate this rule and must not be repeated.
- **Both rulesets intentionally omit "Require branches to be up to date before merging" (`strict_required_status_checks_policy: false`), for two distinct reasons:**
  - *Main* - the check is graph-based; it asks whether main's tip commit is reachable from develop, not whether the two branches have the same content. After any develop -> main release, main's tip is a brand-new merge commit that develop's history doesn't contain. Forward-only develop never adds it (no back-merge of main into develop), so the check would fail on every subsequent release.
  - *Develop* - bot auto-merge incompatibility. When two bot PRs against develop land in the same minute (e.g. two grouped Dependabot PRs from the same daily run), the first to merge pushes the second into `mergeStateStatus: BEHIND`. GitHub's auto-merge will not fire while the strict flag is on, and nothing in the workflow set auto-updates a bot branch in that window - the merge-bot enables auto-merge via `gh pr merge --auto` but never rebases a stalled branch onto base (see [`merge-bot-pull-request.yml`](./.github/workflows/merge-bot-pull-request.yml)). Real file-level conflicts are still caught textually (`mergeable: CONFLICTING` blocks merge regardless); semantic-but-not-textual conflicts that combine cleanly are caught by the post-merge develop CI run rather than pre-merge. Do not reintroduce the strict flag on develop thinking it's hygiene - it breaks bot auto-merge.
- **Dependabot targets both `main` and `develop` in parallel.** [`.github/dependabot.yml`](./.github/dependabot.yml) duplicates every ecosystem entry (one per branch). Each branch absorbs its own bot PRs independently, so neither falls behind, and the forward-only rule still holds (nothing is back-merged from main to develop - both branches receive their updates directly). Parallel auto-merge across same-batch bot PRs is race-proof only because both rulesets have the strict "up to date" flag off (see bullet above). The merge-bot ([`.github/workflows/merge-bot-pull-request.yml`](./.github/workflows/merge-bot-pull-request.yml)) dispatches `--squash` or `--merge` from each PR's base ref via a `case` statement so the form matches the ruleset on either base. Dependabot **security** PRs (CVE-driven) always open against the repo default branch (`main`) regardless of `target-branch` - the same `case` statement covers them. Every tier auto-merges, semver-major included - the required checks are the gate, not the version bump.
- **Maintainer-pushed commits on a bot PR auto-disable auto-merge.** The merge-bot's `merge-dependabot` job only fires on `opened` / `reopened` events (auto-merge is enabled exactly once per PR, for Dependabot-authored PRs that originate from this repository, not forks). When a maintainer pushes commits to the bot's branch (a `synchronize` event with a non-bot actor), the `disable-auto-merge-on-maintainer-push` job fires and calls `gh pr merge --disable-auto`; the maintainer's commits stay in the PR but won't auto-merge with the bot's content. Re-enable manually (`gh pr merge --auto <PR>`) when ready. The merge-bot is on `pull_request_target` with per-PR concurrency; it carries only `merge-dependabot` + `disable-auto-merge-on-maintainer-push`.
- **App-token workflows use Client ID, not App ID.** `actions/create-github-app-token` deprecated the numeric `app-id` input in v3.0.0; the merge-bot uses `client-id: ${{ secrets.CODEGEN_APP_CLIENT_ID }}` (with `private-key: ${{ secrets.CODEGEN_APP_PRIVATE_KEY }}`). The App token - not `GITHUB_TOKEN` - is required so the merge push is committed by the App and fires downstream workflows (`GITHUB_TOKEN` pushes are blocked from triggering further runs by GitHub's recursion guard). When adding new App-token call sites, use the same form - do not reintroduce `app-id`.
- **Why parallel dual-target rather than develop-only with eventual flow-through:** consumers pull the Docker image and the release executables from `main` directly. A develop-only model would leave `main` running stale code during long-running develop features, so both branches receive their own bot updates on their own cadence and each stays current.
- **Mirror to `develop` any change that lands on `main` outside the feature -> develop -> main flow.** "Mirror" means landing the same fix directly on `develop` via a follow-up PR targeting `develop` - never a `main -> develop` back-merge, which the forward-only rule forbids. A reconciliation-branch fix made to resolve a `develop -> main` promotion conflict, or a security PR that merges only to `main`, leaves `develop` behind on that content - and forward-only `develop` never back-merges to catch up (the same parallel-target principle as the bots). Before basing new work on `develop`, or diagnosing a defect from it, compare content and not commit history: run `git diff origin/main origin/develop` and inspect its `-` lines - the `main`-side of each difference, to check for staleness. A `-`/`+` pair within one hunk is usually just `develop` modifying that code as normal unpromoted work (occasionally `develop` is reworking a `main`-side fix differently - worth a glance). The stronger staleness signal is a deletion-only hunk (`-` lines, no `+` lines): content on `main` that `develop` lacks entirely, i.e. a `main`-only fix `develop` never received, so the defect may already be fixed on `main`. Prefer this over a commit-log check like `git log origin/develop..origin/main`, which is noisy here because it also lists routine promotion merges and the `main`-direct bot commits whose content `develop` already carries via its own parallel bot PRs.
- **Put issue-closing keywords (`Closes #N`) where they fire on merge to the default branch (`main`).** GitHub closes an issue from a *PR description* only when that PR merges to `main`, so a `Closes #N` in a PR that targets `develop` never fires - put it in the `develop -> main` promotion PR instead. A closing keyword in a *commit message* does close the issue once that commit reaches `main` via promotion, but that is fragile across squash-merges, so prefer the promotion PR's description or close the issue manually once the fix lands on `main`.

## Release Model

The publish behavior - the **scheduled + on-demand** publisher (one branch per run: the weekly schedule rebuilds `main`, and a dispatch publishes the branch it is started from - native binaries + multi-arch Docker image + a GitHub release that anchors the version), branch-scoped versioning (`main` = stable / `latest`, `develop` = prerelease / `develop`), and the rule that **merges do not publish** (changes accumulate and ship in the next scheduled run, which also refreshes the Docker base image; release `develop` by dispatching from `develop`) - is specified in [`WORKFLOW.md`](./WORKFLOW.md), the canonical CI/CD guide. Do not duplicate those rules here.

Versioning is the one release rule that is a **human process**, not a workflow outcome, so it lives here:

- The `version` (major.minor) in [`version.json`](./version.json) is the version floor; NBGV appends the git height as the SemVer patch. `main` (the public release ref, `publicReleaseRefSpec = ^refs/heads/main$`) builds a stable `X.Y.<height>`; `develop` builds a prerelease `X.Y.<height>-g<sha>`. The maintainer edits `version.json`; *routine* dependency bumps, CI/workflow fixes, and doc edits leave it untouched.
- **Bump `version.json` only by maintainer instruction**, for a functional change (a new feature, a behavior change, a breaking change) or a significant one-time overhaul of the build/release process (such as a CI/CD migration), in the PR that introduces it (typically on `develop`). Do not bump on a cadence, for routine CI/workflow or dependency or doc edits, or mechanically after a release.
- **No post-release bump; no develop-ahead requirement.** NBGV advances the patch on every commit, so a release always gets a fresh build version with no `version.json` edit and there is no `bump-version-X.Y` PR after a release. A `develop -> main` promotion carries whatever `version.json` is current.
- **`dotnet/nbgv` is consumed via `@master`, never SHA-pinned.** Its tag stream lags `master` such that Dependabot tag-tracking would only propose downgrades to stale tags; this is the sole WORKFLOW.md D9.1 exception (rationale inline in the workflow). Do not SHA-pin it.

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

- Comment only when the code is non-obvious or important. Self-evident code needs no comment.
- Judge "obvious" in context, not line by line. A note that reads as redundant on its own line can be essential in the larger flow - a comment marking a workflow step's exit condition, for example, even though the line itself plainly does a `return` or `exit`.
- State the non-obvious *why*, not what the code already shows. No cross-project references (do not name other repos), no historic or design narrative, no rule citations - governance lives in this file, not echoed inline.
- **One line if it fits in ~120 columns.** Do not wrap a comment at 75-80 columns; a short two-line comment that would fit on one line looks sloppy - collapse it. Go multi-line only when the content genuinely exceeds ~120, filling each line rather than narrow-wrapping. For a multi-point comment, prefer short structured lines or `-` bullets over one prose paragraph.
- **Workflows: prefer one short summary description at the top of the file** over scattering rationale across steps; comment an individual step only when its purpose is non-obvious.
- **Do not accumulate comments.** When you change code or a comment, rewrite the whole comment fresh; never bolt a new comment onto an existing one or layer explanations across edits. Comment volume should stay flat or shrink over time, not grow.
- **Leave human-authored comments and emojis exactly as written** - do not reword, trim, reflow, or "clean" them, even if they seem to bend a rule. Revise only agent-authored comments, and match the surrounding voice when you do.

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

- [`.editorconfig`](./.editorconfig) is the single source of truth for line endings: CRLF for `.md`, `.cs`, XML/`.csproj`/`.props`, `.yml`/`.yaml`, `.json`, `.cmd`/`.bat`/`.ps1`; LF for `.sh` and Dockerfiles. The `[*.cs]`/ReSharper style block applies because this repo ships .NET.
- **Always honor the `.editorconfig` ending.** Create a file with its spec ending; when editing a file, bring the whole file to spec (a file-wide EOL fix alongside the content change is expected, not a violation); if you come across a file with the wrong ending, fix it. [`.gitattributes`](./.gitattributes) (`* -text`) governs git's own normalization - it is not a license to leave a file on the wrong ending. Verify with `file <path>` after writing.

### Quantitative Claims

- Any quantitative claim in `README.md` (counts, sizes, version floors, supported platforms) must be verified against current code. If a doc number is derived from a code constant, mark the dependency in a source-code comment so the next editor knows to update both.

## PR Review Etiquette

> This "PR Review Etiquette" section is the provider-agnostic review-loop *contract*; the [`.github/copilot-instructions.md`](./.github/copilot-instructions.md) "GitHub Copilot Review Runbook" implements its mechanics. Without both in-repo, an agent has no pointer to the reliable Copilot mechanics and falls back to ad-hoc (and known-broken) behavior.

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

## Shared Configuration and Tooling

- **Config files.** [`.editorconfig`](./.editorconfig) (per-file-type EOL plus the C# / ReSharper style block), [`.gitattributes`](./.gitattributes) (`* -text`, with the `.husky/pre-commit` LF pin), [`.markdownlint-cli2.jsonc`](./.markdownlint-cli2.jsonc), and [`CODESTYLE.md`](./CODESTYLE.md) hold the repo's formatting, linting, and code-style rules. Keep [`.github/copilot-instructions.md`](./.github/copilot-instructions.md) narrow (Copilot / VS Code review mechanics plus the commit/PR-title summary); project-specific conventions live in this file and the architecture deep-dive in [`ARCHITECTURE.md`](./ARCHITECTURE.md).
- **Clean-compile tasks.** [`.vscode/tasks.json`](./.vscode/tasks.json) defines the canonical `.NET Build`, `CSharpier Format`, and `.NET Format` tasks (the last chains the first two then `dotnet format style --verify-no-changes`); their names are owned by the `CODESTYLE.md` ".NET" section - do not loosen them. Husky.Net runs the same checks as a local pre-commit hook, and CI's `lint` job is the authoritative backstop.
- **Brownfield analyzer relaxations.** `Directory.Build.props` sets strict analysis; because this is a pre-existing console app, a specific set of analyzer rules are relaxed to suggestion in [`.editorconfig`](./.editorconfig), each documented inline. Prefer fixing new violations over adding relaxations.
- **Spell check.** The cspell word list and path exclusions live in [`cspell.json`](./cspell.json), the single source shared by the editor and CI. Do not keep a parallel word list in the `.code-workspace` file.
- **Run CI CLI tooling via Docker.** The linters CI uses (actionlint, markdownlint-cli2, shellcheck, cspell, etc.) need not be installed on the host - run them from their official images (e.g. `docker run --rm -v "$PWD:/repo" -w /repo rhysd/actionlint`) to reproduce a CI check locally before pushing.
- **Release notes.** Keep a short summary in [`README.md`](./README.md) and the full history in [`HISTORY.md`](./HISTORY.md); update both when cutting a release.

## Workflow YAML Conventions

The conventions for everything under [`.github/workflows/`](./.github/workflows/) - action pinning, file/workflow/job/step naming, concurrency, shells, conditionals, boolean inputs, permissions, artifact handling, Docker layer cache, and release tagging - are specified in [`WORKFLOW.md`](./WORKFLOW.md), the canonical CI/CD guide. New and modified workflows must respect it; do not duplicate those rules here.

## Project Structure

- **PlexCleaner** (`PlexCleaner/PlexCleaner.csproj`)
  - The CLI application - orchestrates FFmpeg, HandBrake, MkvToolNix, MediaInfo, and 7-Zip to optimize media for Direct Play.
  - Target framework: .NET 10.0. AOT is opt-in (`<PublishAot>false</PublishAot>` by default, matching the shipped builds); the plugin loader compiles only in non-AOT builds. Internals are exposed to the test project via `InternalsVisibleTo`.
- **PlexCleanerTests** (`PlexCleanerTests/PlexCleanerTests.csproj`)
  - xUnit v3 test suite. Assertions via AwesomeAssertions.
- **`Docker/`** - multi-arch Linux container build (`ubuntu:rolling`, `linux/amd64` + `linux/arm64`); runs as a `nonroot` user, mounts media under `/media`.
- **Build configuration**:
  - Common MSBuild properties (`TargetFramework`, `Nullable`, `ImplicitUsings`, `AnalysisLevel`, etc.) live in `Directory.Build.props` at the solution root. Do not duplicate these in individual `.csproj` files - only add a property to a `.csproj` when it is project-specific or overrides the shared default.
  - All NuGet package versions are centralised in `Directory.Packages.props`. `PackageReference` elements in `.csproj` files must not include a `Version` attribute. Asset metadata (`PrivateAssets`, `IncludeAssets`) stays in the `.csproj` `PackageReference` element.
- **Style guide / further reading**: [`CODESTYLE.md`](./CODESTYLE.md) for C# code conventions; [`.github/copilot-instructions.md`](./.github/copilot-instructions.md) for the Copilot review runbook; and [`ARCHITECTURE.md`](./ARCHITECTURE.md) for the architecture, processing pipeline, and design patterns - read it before changing processing, sidecar, media-tool, or language-tag code.
