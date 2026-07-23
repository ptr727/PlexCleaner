# Instructions for AI Coding Agents

**PlexCleaner** is a .NET 10 CLI utility that optimizes media files for Direct Play in Plex/Emby/Jellyfin (converting containers to MKV, re-encoding incompatible codecs, managing tracks and language tags, verifying and repairing media, and monitoring folders for changes). It orchestrates external media tools - FFmpeg, HandBrake, MkvToolNix, MediaInfo, and 7-Zip - through CLI wrappers. It ships two release targets: a multi-arch Docker image (Docker Hub `ptr727/plexcleaner`) and standalone executables attached to GitHub Releases; consumers pull from Docker Hub or the GitHub releases on their own cadence. The repo also contains an xUnit test project (`PlexCleanerTests/`).

This file is the canonical reference for cross-cutting AI-agent rules. The CI/CD workflow contract and conventions live in [`WORKFLOW.md`](./WORKFLOW.md); C# code-style conventions live in [`CODESTYLE.md`](./CODESTYLE.md). Copilot review *mechanics* are owned by [`.github/copilot-instructions.md`](./.github/copilot-instructions.md) - this file delegates them there explicitly (see "PR Review Etiquette" below). PlexCleaner's architecture, processing pipeline, and design patterns live in [`ARCHITECTURE.md`](./ARCHITECTURE.md). High-level summaries in other docs (e.g. README's Contributing section) are allowed when they link back here; don't duplicate the rules themselves. The app's **project-specific conventions** also live here and in `ARCHITECTURE.md`, **not** in `.github/copilot-instructions.md` - that file targets GitHub Copilot / VS Code specifically, while this file is the agent-agnostic one every coding agent reads, so any rule a reviewer must honor has to live here to be provider-independent.

**Where rules live.** A durable project, code, or style rule belongs in this file (or `WORKFLOW.md` / `CODESTYLE.md` as appropriate), so it is versioned and read by every session and every agent. An agent's own session memory or scratch state is private and lost on restart, so it is never the system of record for a rule: when you learn or are corrected on a rule, write it into the right doc in the same change. Memory may also note it, but the committed docs are the source of truth.

## Repository Boundaries and Write Safety

A state-changing GitHub call is the highest-blast-radius thing an agent does here: it runs under the maintainer's identity, so one wrong target writes to another owner's repository as the maintainer - an outward-facing, hard-to-reverse act. These rules bound every write - a git push, an API mutation, a comment, a label, a merge - on any platform. Reads are unrestricted. The bounds below are on writes.

- **Write only to the current project's own repository.** Every state-changing call targets this project's `origin` and nothing else. A broad or logged-in identity is capability, not permission - a token that *can* reach another repository does not authorize writing to it. Writing to any other repository needs explicit, per-session human permission for that specific repository, and a "harmless test" write is still a write, so there is no probe exception. Reads from anywhere are fine.
- **Never fabricate, guess, or reuse an identifier passed to a write.** Every id a state-changing call consumes - a node id, a numeric id, a thread or comment id - is captured from a live query in the **same** session into a variable and passed from there. Do not hand-type an id, guess it, recall it from memory or an earlier session, or copy it from documentation or an example. Ids commonly resolve **globally**, so a wrong-but-valid id does not fail - it writes to the wrong target, in someone else's repository. If a query returns no id, stop rather than invent one to proceed.
- **A write is never a probe, and a write's output is never suppressed.** Never fire a state-changing call to see whether it works: decide it should happen, make it happen, and read the result. Never append output-discarding redirection or a force-success tail to a mutation (for example `>/dev/null`, `2>/dev/null`, `&>/dev/null`, `|| true`, `|| :`, `|| echo`) - the write's output is exactly what must be read. A write that appears to fail is **verified, not assumed harmless** - the operation may have succeeded on the server while the client reported an error - so confirm the actual state before retrying or moving on. The ban targets hiding a *failure*. An ad-hoc call's response is the only signal you get, so `>/dev/null 2>&1`, `|| true`, or `|| echo` - which swallow the error stream or force success - are never acceptable on one. A committed script under `set -e` is a narrow exception: it may send a write's *stdout* to `/dev/null` to drop the success-response noise, because stderr stays visible and a failed write still aborts loudly (`repo-config/configure.sh` does exactly this). The exception is stdout-only suppression inside a reviewed, fail-loud script, never `2>&1` or a force-success tail, and never an ad-hoc command.

## Git and Commit Rules

- **Default to staging, not committing.** Stage changes with `git add` and leave `git commit` to the developer unless the developer has explicitly authorized the agent to commit for the current ask ("commit this", "open a PR", etc.). Authorization is scope-bound - it covers the commits needed for that specific task, not a blanket commit license for the rest of the session.
- **Check the working tree for the maintainer's own uncommitted edits before committing.** The maintainer hand-edits files live (often `README.md`/`HISTORY.md`, sometimes with the editor's LF->CRLF flip on top). Review `git status` first. If there are changes you did not make, ask whether to include them rather than bundling half-finished work or stranding it in an unrelated commit.
- **All commits must be cryptographically signed (SSH or GPG).** Branch protection enforces this on both branches, and unsigned commits are rejected on push. Signing depends on environment configuration - `git config commit.gpgsign true`, a configured `user.signingkey`, and a working signing agent (loaded `ssh-agent` for SSH, or `gpg-agent` for GPG). If signing is not configured in the environment, **do not commit** - surface the missing config to the developer and stop at `git add`. Verify before any agent-authored commit (`git config --get commit.gpgsign && ssh-add -L` or the GPG equivalent). **Signing must be live before the *first* commit, not retrofitted.** Turning on `Require signed commits` against a branch that already has unsigned commits forces a rewrite of that entire history to re-sign it - changing every commit SHA and making whoever does the rewrite the committer and signer of every commit (a rebase preserves the `author` field but not the original signatures - you cannot sign another contributor's commits for them). During new-repo setup, never create commits until signing is verified.
- **Commit under the committing account's own GitHub `noreply` identity - never a private, personal, or invented address.** The `author` and `committer` on every agent-authored commit are the GitHub `noreply` address of the account whose key signs the commit (above) - GitHub issues these in a `username@users.noreply.github.com` or `ID+username@users.noreply.github.com` form, and for this single-maintainer fleet it is the owner's `ptr727@users.noreply.github.com`. Do not set `user.name`/`user.email` to a fabricated persona, bot name, or product name, and do not commit under whatever identity the environment happens to carry: verify `git config --get user.email` is that GitHub `noreply` address before committing, and fix it if not. A wrong identity is not cosmetic - a private email trips GitHub's email-privacy push protection (GH007), and an unrecognized or invented author pollutes history. Identity is separate from signing: a wrong author does not by itself fail the signature rule, but the ad-hoc identities that produce it are typically also unsigned, which the signing rule above then rejects on push.
- **Never force push.** Do not run `git push --force` or `git push --force-with-lease` under any circumstances. Force pushing rewrites shared history and can cause data loss.
- **A history rewrite includes only the commits that must change, and re-identifies any commit it rewrites that is not yours.** Filtering history (`git filter-repo` / `filter-branch`, e.g. to strip PII) rewrites the touched commits and you re-sign them with your key, while the tooling preserves each commit's original `author` and `committer` unless told otherwise. GitHub verifies a signature against the commit's `committer` identity, so a your-key signature over a commit still committed by a bot (`dependabot[bot]`, `github-actions[bot]`) or by GitHub's web-flow does not match its committer and is marked `unknown_key`/unverified, which a require-signed-commits ruleset then rejects. Two gates keep committer and signature aligned. **First, scope the rewrite to only the commits that must be modified** - by default those are your own, whose committer is already your identity, and a commit that does not need changing is kept out of the rewrite so its identity and signature are never touched. **Second, if a commit that must change is not yours, set its `committer` to the signing identity before re-signing** (and its `author` too, since a rewrite that alters the content should not keep attributing it to the bot), so the committer GitHub verifies matches your key - the original bot attribution is deliberately given up as the cost of having to rewrite it. Never leave your signature over a commit committed by another identity. Verify after the rewrite that every rewritten commit is signed and committed under your identity (`git log --show-signature`).
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

- [`.editorconfig`](./.editorconfig) is the single source of truth for line endings: CRLF for `.md`, `.cs`, XML/`.csproj`/`.props`, non-workflow `.yml`/`.yaml`, `.json`, `.cmd`/`.bat`/`.ps1`; LF for `.sh`, Dockerfiles, and workflow YAML (`.github/workflows/*.{yml,yaml}`). Workflow YAML is pinned LF because Dependabot and Actions rewrite it with LF, so declaring LF keeps it consistent instead of mixed; git still leaves endings alone (`* -text`) and CI's `editorconfig-checker` enforces it. The `[*.cs]`/ReSharper style block applies because this repo ships .NET.
- **Always honor the `.editorconfig` ending.** Create a file with its spec ending; when editing a file, bring the whole file to spec (a file-wide EOL fix alongside the content change is expected, not a violation); if you come across a file with the wrong ending, fix it. [`.gitattributes`](./.gitattributes) (`* -text`) governs git's own normalization - it is not a license to leave a file on the wrong ending. Verify with `file <path>` after writing.
- **Python (`.py`) and `.toml` are CRLF.** They have no `[*.py]`/`[*.toml]` override, so they inherit the `[*]` CRLF default (matching the audited convention that keeps Python on the repo default rather than pinning LF). Only the `.sh` harness is LF.

### Quantitative Claims

- Any quantitative claim in `README.md` (counts, sizes, version floors, supported platforms) must be verified against current code. If a doc number is derived from a code constant, mark the dependency in a source-code comment so the next editor knows to update both.

## Verification Discipline

The checks that separate work actually done from work that merely reports success. Their unifying property: **every failure below is green.** A skipped job and a passing job are indistinguishable in the aggregated required check, a pattern that matches less still exits zero, and a gate that stops gating still reports success. No linter, status check, or review layer catches any of them.

- **A test must assert the mechanism it names.** Label each case by the behavior it proves, and satisfy yourself it would fail if that mechanism broke. A case that passes for an incidental reason - the right answer reached by the wrong path - is worse than no case, because it is later cited as evidence.
- **Gates, filters, and gate-like watchers fail loud, never narrow quietly.** A pattern that silently matches less, an allowlist that silently stops matching, or a gate that silently stops gating all report success while doing nothing. When a construct exists to notice something, make the not-noticing case produce an error or an annotation. An identity allowlist used as a gate, for one, must raise an error when its list stops matching, not silently pass everything through.
- **Run the repo's whole lint gate before every push, not the parts that look relevant.** CI runs all of them, so a partial local run only defers the failure - and the tool most likely to catch a given change is often the one it seems least about (an edit that manipulates line endings is exactly when `editorconfig-checker` matters). The invocations are in "Running the Linters Locally", which documents *how* to run each. This rule is that **all** of them run.
- **Editing CRLF files programmatically: `.` matches `\r` in a regex**, so a captured line keeps its carriage return and rejoining with `\r\n` yields `CRCRLF`. A text-mode rewrite has the mirror failure, silently flattening CRLF to LF. Prefer line-based edits (`splitlines(keepends=True)`) or literal replacement over regex reassembly. This is the mechanism behind the Line Endings warning above, and it is worth naming because the corruption is invisible in a rendered diff.
- **Never edit an active `.code-workspace` file.** A workspace file rewritten on disk can make VS Code reload the window, and a reload destroys the running agent session's context - the work in flight is lost with nothing to catch it, and the trigger is not fully characterized (an agent's edit has caused the reload where a human's identical edit did not). Surface the needed change for the maintainer to apply by hand.
- **A green check is not evidence the work happened.** A skipped job and a passing job are indistinguishable in the aggregated required check. When a job exists to exercise something, confirm from its log that it ran and produced the output it promises.
- **A workflow change is only fully exercised by CI.** Extracting a `run:` block and executing it locally validates the script and nothing else - `secrets: inherit`, `permissions:`, `needs:` wiring, and reusable-workflow inputs resolve only in a real run.
- **A review flags an instance - fix the class.** When a reviewer cites one stale claim, one silent-narrowing pattern, or one mis-worded contract, sweep for its siblings before replying. Reviewers sample - they do not enumerate.

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
- **`dotnet format style` gates at info severity, and its auto-fixes bite.** The pre-commit `.NET Format` task runs `dotnet format style --verify-no-changes --severity=info`, so info-level IDE analyzers gate the commit; CSharpier alone is not enough. Two to watch: `IDE0072` populates an enum `switch` expression with `throw new NotImplementedException()` for unlisted members - a `_ =>` discard arm does **not** satisfy it, so map values with explicit arms or a ternary chain instead of relying on the fixer; and `IDE0046` rejects `if (!cond) return false; return expr;` - write the combined `return cond && expr;` form. Run the `.NET Format` task before committing to surface these, and review any file it rewrote rather than staging it blind.
- **Brownfield analyzer relaxations.** `Directory.Build.props` sets strict analysis; because this is a pre-existing console app, a specific set of analyzer rules are relaxed to suggestion in [`.editorconfig`](./.editorconfig), each documented inline. Prefer fixing new violations over adding relaxations.
- **Spell check.** The cspell word list and path exclusions live in [`cspell.json`](./cspell.json), the single source shared by the editor and CI. Do not keep a parallel word list in the `.code-workspace` file.
- **Run CI CLI tooling via Docker.** The linters CI uses (actionlint, markdownlint-cli2, shellcheck, cspell, etc.) need not be installed on the host - run them from their official images (e.g. `docker run --rm -v "$PWD:/repo" -w /repo rhysd/actionlint`) to reproduce a CI check locally before pushing.
- **Release notes.** Keep a short summary in [`README.md`](./README.md) and the full history in [`HISTORY.md`](./HISTORY.md); update both when cutting a release. `README.md` carries the summary for the **current version only** - when bumping the version, replace the previous version's summary rather than appending; prior versions live in `HISTORY.md`.

## Communicating with the User

- **Reference every pull request as a clickable link.** When you mention a PR - in chat, a summary, or a report - render it as a markdown link to the PR (`[#123](https://github.com/<owner>/<repo>/pull/123)`), never a bare `#123`. The same applies to issues and commits.
- **Ask for input as a numbered list.** When you need the user to decide or answer, present the questions - and any options - as a numbered list so they can reply per number. A single inline question is fine; two or more are always numbered.

## Workflow YAML Conventions

The conventions for everything under [`.github/workflows/`](./.github/workflows/) - action pinning, file/workflow/job/step naming, concurrency, shells, conditionals, boolean inputs, permissions, artifact handling, Docker layer cache, and release tagging - are specified in [`WORKFLOW.md`](./WORKFLOW.md), the canonical CI/CD guide. New and modified workflows must respect it; do not duplicate those rules here.

## Logging Conventions

Serilog log levels describe the **nature** of an event, applied uniformly across the whole app - never "which command am I in". When adding or reviewing a log call, pick the level from what the event *is*, and keep the pipeline reading as a coherent story: *inspect -> decide to act -> do the work -> call the tool -> succeed or fail*.

- **Error** - an operation failed and could not complete (tool returned non-zero, IO/parse/verify failure, a step that aborts the file). Every early-exit failure path.
- **Warning** - the **trigger**: the orchestration layer inspected the file, interpreted the result, and has **decided to modify the media** (or detected a noteworthy non-fatal condition - unknown codec, cover art, language fallback, non-convergent repair, an interruption). Emitted **once**, at the decision point, *before* the modification. This is the event that elevates the per-file log from Warning to Information (see `PerFileLogLevel`), so `--loglevel Warning` shows every file that gets changed and why.
  - A "modification" is a write to the **media file**, including in-place metadata edits (MkvPropEdit flags/language/title) and container remuxes/renames. Sidecar cache writes and the results file are bookkeeping, not media modifications - they are Debug/Information, not Warnings.
  - **The media-manipulation code itself does not emit Warning.** Doing a remux or re-encode is that code's job, not a warning. Only the decision to run it is the Warning. Do not sprinkle Warnings through `Convert`, the media-tool wrappers, or the worker methods.
- **Information** - the high-level narrative of what the app is doing, readable end to end at the default level with no low-level mechanics: startup (banner, settings, tool versions), discovery (`Discovered N files`), batch lifecycle (`Starting {Command}, processing N files`, progress, `Completed`, the run summary), the per-file entry, read-only outcomes of note (skips), a worker **doing its job** (e.g. `Convert.ReMux` logging `Remux using MkvMerge`), and the intended output of read-only commands (`getmediainfo` / `getsidecarinfo` / `gettagmap` dumps).
- **Debug** - troubleshooting detail; *how* the work is done: raw tool invocations and command lines (`Executing MkvMerge : GetMediaPropsJson : args`, which carry the operation so a per-method "doing X" line is not needed), read/probe mechanics (`Reading media info from sidecar`, temp files, packet probes), per-track structural dumps during normal processing, inspection sub-steps (verify, bitrate, idet counting), and sidecar cache bookkeeping.
- **Verbose** - very granular: filesystem-watcher events, per-packet/byte-level progress.

The elevation trigger (Warning) must be preserved: keep exactly one decision-Warning per media modification, with the action at Information and the underlying tool at Debug.

### Tool execution and failure logging

- **Always consume a tool's output.** A subprocess whose stdout/stderr is not read can deadlock once it fills the pipe buffer, so never run a tool without consuming its pipes: `MediaTool.Execute` buffers them (summarize when the output is huge), and `ExecuteStreamStdErr` streams stderr line by line for the unbounded `-f null` verify pass. `Execute`, its cancellation path, and `LogFailedResult` record the **operation** (the calling method, captured via `[CallerMemberName]`, rendered with `:l`) so a command line ties to its purpose in a parallel log without correlating separate lines.
- **Tools write errors to different streams.** ffmpeg, ffprobe, HandBrake, and 7-Zip use **stderr**; the mkvtoolnix tools (mkvmerge, mkvpropedit) write everything including errors to **stdout** (confirmed from the mkvtoolnix source - all output goes through the one stdout object) and override `GetErrorOutput` to it. MediaInfo also emits to stdout but keeps the stderr default; its errors are caught by the `LogFailedResult` fallback, which reads the other captured stream when the tool's declared stream is empty, so an error is never lost.
- **Do not add a per-method debug line that just restates the command about to run** (e.g. `Getting media info`); the `Executing {Tool} : {operation} : args` line from `Execute` already covers it.

### Failure-handling philosophy

An **expected, recoverable** failure escalates through the standard repair tiers (detect -> surgical -> remux -> re-encode -> fail); an **unexpected or logic** failure (e.g. tool output that will not parse) aborts the file and stays a hard error, so the bug surfaces and gets fixed rather than being masked by a fallback that silently mis-processes at scale.

## Project Structure

- **PlexCleaner** (`PlexCleaner/PlexCleaner.csproj`)
  - The CLI application - orchestrates FFmpeg, HandBrake, MkvToolNix, MediaInfo, and 7-Zip to optimize media for Direct Play.
  - Target framework: .NET 10.0. AOT is opt-in (`<PublishAot>false</PublishAot>` by default, matching the shipped builds); the plugin loader compiles only in non-AOT builds. Internals are exposed to the test project via `InternalsVisibleTo`.
- **PlexCleanerTests** (`PlexCleanerTests/PlexCleanerTests.csproj`)
  - xUnit v3 test suite. Assertions via AwesomeAssertions.
- **`Docker/`** - multi-arch Linux container build (`ubuntu:rolling`, `linux/amd64` + `linux/arm64`); runs as a `nonroot` user, mounts media under `/media`.
- **`RegressionTests/`** - regression harness and tooling: a ZFS-clone Bash harness plus standalone stdlib-only Python utilities (catalog / reduce / locate / audit) that verify processing decisions stay consistent across versions against a curated media collection. The Python tooling is linted with ruff and type-checked with mypy (config in `RegressionTests/pyproject.toml`); it is the only Python in the repo. No media or media filenames are committed - media-specific reduction rules live with the media as an external JSON file, and the repo ships only a synthetic example. See [`RegressionTests/README.md`](./RegressionTests/README.md).
- **Build configuration**:
  - Common MSBuild properties (`TargetFramework`, `Nullable`, `ImplicitUsings`, `AnalysisLevel`, etc.) live in `Directory.Build.props` at the solution root. Do not duplicate these in individual `.csproj` files - only add a property to a `.csproj` when it is project-specific or overrides the shared default.
  - All NuGet package versions are centralised in `Directory.Packages.props`. `PackageReference` elements in `.csproj` files must not include a `Version` attribute. Asset metadata (`PrivateAssets`, `IncludeAssets`) stays in the `.csproj` `PackageReference` element.
- **Style guide / further reading**: [`CODESTYLE.md`](./CODESTYLE.md) for C# code conventions; [`.github/copilot-instructions.md`](./.github/copilot-instructions.md) for the Copilot review runbook; and [`ARCHITECTURE.md`](./ARCHITECTURE.md) for the architecture, processing pipeline, and design patterns - read it before changing processing, sidecar, media-tool, or language-tag code.
