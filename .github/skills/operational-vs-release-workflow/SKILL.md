---
name: operational-vs-release-workflow
description: >-
  Governs how a ptr727/ProjectTemplate fleet repo branches, promotes, and publishes: the
  feature -> develop -> main flow, squash-only vs. merge-commit-only branch protection, the two
  develop -> main promotion traps (never delete develop, EOL-only conflicts), the two-phase
  publish model (PRs smoke-test only, a human merge never auto-publishes), NBGV semantic
  versioning, and the operational-repo delta (direct-to-develop commits, advisory CI, dispatch-only
  release) that applies instead whenever the registry's workflowModel field for this repo reads
  operational rather than release. Use this whenever choosing a target branch for a change,
  promoting develop to main, resolving a develop -> main merge conflict, deciding whether a
  release repo's config change needs a PR versus an operational repo's config change can commit
  straight to develop, bumping version.json, adding or dropping a release target, or reasoning
  about why a merge did or didn't trigger a publish. Triggers even when the request sounds like
  ordinary git housekeeping ("just push this config fix", "merge develop into main", "cut a
  release"), because the two workflow models genuinely differ (a direct-to-develop commit that is
  correct in an operational repo is a rule violation in a release repo, and vice versa) and
  applying the wrong one is not obviously wrong to a reader who only knows one of the two.
---

# Operational vs. Release Workflow

## Why this exists

Two workflow models exist because the underlying repos are two different things. Most fleet repos
ship versioned units of delivery, so they earn a feature -> `develop` -> `main` flow with real
release gates. A handful of repos instead track a live service's running state (Home Assistant,
ESPHome, Vantage, home automation configs) where the "release" is the config already committed,
not something built and shipped later. Applying the release model's ceremony to an operational
repo, or skipping the release model's gates on a repo that actually ships versioned artifacts, is
each wrong in its own repo and correct in the other, which is why this is one skill keyed on which
repo you're in rather than two skills that never talk to each other.

## Which model this repo uses

Read the registry `workflowModel` field for this repo (`release`, the default, or `operational`).
The rest of this skill's "Branching" and "Publishing" sections describe the `release` model. The
"Operational repositories" section below is the complete delta for `operational` repos. Anything
not mentioned there is unchanged. When in doubt which one applies, check `registry/repos.json`
rather than guessing from the repo's contents.

## Branching (release model)

- **GitHub's repository setting for "default branch" reads `main`, but `develop` is where work starts and where in-flight content lives.** A worktree or clone that defaults to "the default branch" lands on `main` and can silently miss content that has merged to `develop` but not yet been promoted. Before branching off a change, or asserting something absent from this repo, check `develop`, not just whichever branch a tool defaulted to. See GOVERNANCE.md "Verification Discipline" on naming the branch a "does not exist" claim was checked against, and the `repo-worktree` skill, which owns the worktree-creation moment this base-branch choice is made at.
- `develop` is the integration branch. Feature branches -> `develop` is **squash-only**, which
  keeps `develop` linear.
- `develop -> main` is **merge-commit only** (no squash, no rebase). Merge commits preserve
  `develop`'s commit list as a real second-parent reference on `main`, which lets the release
  model attribute releases to the develop commits that produced them. Branch protection enforces
  this: the `develop` ruleset allows only `squash`, the `main` ruleset allows only `merge`.
- All commits on both branches must be cryptographically signed (SSH or GPG), see
  `git-commit-conventions`. Squash and merge commits created via the GitHub UI are signed by
  GitHub's web-flow key.
- **`develop` is forward-only, with no `main -> develop` back-merges.** The `develop` ruleset's
  squash-only setting physically blocks merge commits on `develop`. Any historical back-merge
  commits in `git log` predate this rule and must not be repeated.
- **Never delete `develop`, and take the EOL-only conflict by taking develop's side.** A
  promotion PR's head *is* `develop`, so `--delete-branch` deletes it. An EOL-only conflict on a
  workflow YAML file resolves on a throwaway branch off `main`, not on `develop`. Full recovery and
  conflict-resolution commands: `references/branch-protection-and-promotion.md`.
- **A merge or release ends with worktree cleanup and the base clone on current `develop`.** Run the `repo-worktree` post-merge procedure after a feature squash merge. Run it again after a promotion or release completes, unless the user explicitly asks to retain a checkout or branch. Remove finished task, conflict-resolution, installer, and release helpers. Never delete `develop`, and never leave the base clone on `main` merely because `main` was promoted or released.
- **Issue-closing keywords (`Closes #N`, `Fixes #N`) go in the `develop -> main` promotion PR, not
  the feature -> `develop` PR.** GitHub auto-closes an issue only when the closing keyword merges
  into the **default branch** (`main`), so a feature -> `develop` PR merge never fires it.
  Reference the issue in the `develop` PR body if useful, but the actual closing keyword belongs on
  the promotion PR. Closing by hand is the ordinary route wherever the keyword cannot fire (a
  promotion that already merged without it, or completed work with no promotion imminent), not a
  repair for a botched promotion, cite the squash SHA and re-read that commit before closing.
- **Neither ruleset requires branches to be up to date before merging**, for different reasons on
  each branch (a graph-based check that would fail every release on `main`, a check that stalls
  bot auto-merge on `develop`). Detail: `references/branch-protection-and-promotion.md`.
- **Configuring branch protection: import the committed ruleset payloads, don't hand-build them.**
  Exactly two rulesets, named `develop` and `main`. Full procedure, including the operational
  `develop` payload and the brownfield-repo signing caveat:
  `references/branch-protection-and-promotion.md`.
- **Dependabot and codegen target both `main` and `develop` in parallel**, each branch absorbing
  its own bot PRs independently so neither falls behind, with the merge-bot dispatching the merge
  form (`--squash`/`--merge`) that matches each PR's base ruleset. Codegen output must be
  deterministic from its inputs alone, never per-run state, or the two branches' legs conflict on
  every promotion. Full mechanics: `references/branch-protection-and-promotion.md`.
- **App-token workflows authenticate with Client ID, not the deprecated App ID.** Use
  `client-id: ${{ secrets.CODEGEN_APP_CLIENT_ID }}` at any new App-token call site.

## Publishing (release model)

- **The two-phase model is the default: PRs build fast, publishing is batched.** A PR only
  smoke-tests (unit tests plus a reduced build of the changed targets), it never pushes anything.
  `publish-release.yml` is the sole publisher, and each run builds a **single trigger branch**
  (`main` a release, `develop` a prerelease).
- **A human merge never auto-publishes.** Publishing fires on a **`workflow_dispatch`** of
  `main`/`develop` (a human-initiated release), a **code-affecting bot push to `main`** (the
  codegen App merging a Dependabot/codegen PR, gated on `github.actor` so a human
  merge/promotion skips it), or a **weekly `schedule`** (Docker only, to refresh the base image).
  A source-only repo publishes on dispatch only.
- **The changes-detection job is a required check that must succeed, not just not fail.** A
  paths-filter error must never let a target-changing PR merge with its smoke build silently
  skipped. A skipped smoke job (no matching change) passes, `failure`/`cancelled` blocks.
- **Versioning is semantic and maintainer-controlled.** `version.json`'s `major.minor` is the
  version floor, edited by the maintainer for functional changes only, in the PR that introduces
  the work, never on a fixed cadence or mechanically after a release. NBGV appends the git height
  automatically on every commit, so a release always gets a fresh build version with **no
  post-release bump** and no develop-ahead requirement.
- **Docs reference the 2-digit `major.minor` line, never a 3-digit build.** `README.md`,
  `HISTORY.md`, and release notes name the version as `Version 1.0` (the floor), never the concrete
  build height, which is both wrong (the real height differs) and a maintenance trap.
  "Correcting" `1.0` to `1.0.0` is a defect.
- **A no-op publish (unchanged NBGV `SemVer2`) re-pushes nothing to any target keyed on the
  version string, except Docker, which always re-pushes** to pick up upstream base-image
  refreshes. Full guarantee and the `version.json` `pathFilters` boundary:
  `references/release-publish-mechanics.md`.
- **Adding, dropping, or wiring a release target** (which leaf task, which artifact-naming
  contract, which seam a given output belongs to: a GitHub Release asset, a package-registry push,
  an image-registry push, a filesystem deploy, or a source-only repo with no build layer at all),
  and tracking an upstream release from a wrapper repo: `references/release-publish-mechanics.md`.
  See also `WORKFLOW.md` for the full CI/CD contract this section's rules are load-bearing
  excerpts of.

## Operational repositories (the complete delta)

Everything above is the `release` model. An `operational` repo (registry `workflowModel:
operational`) tracks a live service's running state rather than shipping versioned units of
delivery, and differs from the `release` model in exactly these ways, everything not listed here
stays the same:

- **Commit configuration directly to `develop`.** There is no feature branch requirement, the
  maintainer commits straight to `develop`, and only *occasionally* opens a `develop -> main` PR to
  bless a known-good snapshot. The `develop` ruleset drops the PR and status-check gate, so direct
  signed pushes are allowed (force-push, deletion, and unsigned commits are still blocked), and CI
  runs on the push as **advisory** feedback that never rejects a commit.
- **A PR into `develop` stays available, and CI runs on it, reported but not required.** Dropping
  the requirement permits the direct push, it does not withdraw the pull request, so a change worth
  reviewing takes one and both paths into `develop` are legitimate.
- **Take the pull request whenever the change is not one a reader takes in at a glance and
  reverts cleanly.** What decides it is the shape of the change, not a line count: restructuring
  rather than adjusting a value, touching several files at once, introducing a device, an
  integration, or an automation that did not exist before, and anything whose failure shows up on
  the live service rather than in a lint run are each the pull request case. So is a change the
  author cannot state in one sentence. This stays a judgment call by design, adding a
  `pull_request` rule to the operational `develop` ruleset would gate the direct push too and
  withdraw the allowance the model exists to give.
- **The `main` promotion gate is unchanged.** The shared `main` ruleset still **enforces** the
  required `Check pull request workflow status job` on the `develop -> main` PR. For an operational
  repo that check is lint/validation only (editorconfig/EOL plus a domain linter such as a Home
  Assistant or ESPHome config validation, never unit tests), so `develop` stays the live surface
  and a broken config can never reach `main`.
- **Release only by manual dispatch.** Operational repos carry `releaseTrigger: dispatch-only` and
  run no codegen or auto-publish bots, publishing **only** on a manual `workflow_dispatch` (the
  same source-only release the publisher already supports: tag, source zip, README, LICENSE,
  NBGV-versioned), never automatically. The `develop -> main` promotion just blesses a known-good
  snapshot, a release is a separate, deliberate dispatch.
- **Fleet sync still applies.** Dependabot's dual-target sync and the App-signed merge-bot run on
  **every** tier, operational included, so both branches stay in sync and a promotion stays a
  clean forward merge.
- **Line-ending policy differs too**, following the consuming app's native platform rather than the
  fleet LF default, per the registry `lineEndings` field. That rule belongs to
  `comment-and-doc-style`, not repeated here.
