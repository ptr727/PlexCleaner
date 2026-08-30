# Branch Protection and Promotion Mechanics

Full detail for the "Branching" rules in `SKILL.md`. Load this when configuring or reconstructing
branch protection on a fleet repo, executing a `develop -> main` promotion, recovering a lost
`develop`, resolving an EOL-only promotion conflict, or working on the dual-target bot wiring
(Dependabot, codegen, the merge-bot), not for an ordinary feature-branch PR (the SKILL.md summary
covers that case).

## Configuring branch protection: don't hand-build the rules

Delete **all** classic branch-protection rules and stray rulesets because rulesets are the only
protection mechanism. From a hub checkout at `main`, create **exactly two rulesets named `develop`
and `main`** from the hub's `repo-config/*.json` payloads. Run
`repo-config/configure.sh apply <owner>/<repo> release|operational` from that checkout. The names
are load-bearing because governance content and workflows reference them. The registry
`workflowModel` selects the `develop` payload for a registered repository. Pass the model
explicitly for a repository outside the registry. See the hub's `repo-config/README.md`
"Rulesets" for the configured state.

## Executing a `develop -> main` promotion safely

Two traps, both learned the hard way:

- **Never delete `develop`.** A promotion PR's head *is* `develop`, so `gh pr merge --delete-branch`
  (and a repo's "Automatically delete head branches" toggle, kept off in the hub's
  `repo-config/settings.json` for exactly this reason) deletes `develop` itself. Merge a promotion
  with a plain `gh pr merge --merge`, no `--delete-branch`. If `develop` is ever lost this way,
  restore it to the merged PR's head SHA, which is still reachable as the merge commit's second parent:
  `gh api -X POST "repos/<owner>/<repo>/git/refs" -f ref=refs/heads/develop -f sha="$(gh pr view <n> --json headRefOid --jq .headRefOid)"`.
- **Spurious EOL-only conflicts resolve by taking `develop`.** When `develop`'s `.editorconfig`
  line-ending default has changed (for example the fleet-wide CRLF-to-LF flip) while `main` hasn't
  caught up yet, `develop -> main` conflicts *whole-file* on every renormalized path.
  `develop`'s `required_linear_history` plus PR rulesets forbid resolving on `develop` (no merge
  commit, no force-push), so resolve on a throwaway branch off `main`:
  `git checkout -b promote/develop-to-main origin/main && git merge origin/develop`, take
  `develop`'s side for the EOL-conflicted files (`git checkout --theirs <file>`) **after
  confirming each is content-identical modulo EOL, or that `develop` is a strict superset**
  (`diff <(git show ":2:<file>" | tr -d '\r') <(git show ":3:<file>" | tr -d '\r')`), then open that branch into
  `main`. Verify no genuine `main`-only content is dropped (build/test where the repo supports it).

## Why both rulesets omit "Require branches to be up to date before merging"

The flag is off on `main` and on `develop`, for related but distinct reasons.

- **Main**: the check is graph-based, it asks whether `main`'s tip commit is reachable from
  `develop`, not whether the two branches have the same content. After any `develop -> main`
  release, `main`'s tip is a brand-new merge commit that `develop`'s history doesn't contain.
  Forward-only `develop` never adds it (no back-merge of `main` into `develop`), so the check
  would fail on every subsequent release. Other technical workarounds (rebasing `develop` onto
  `main`, or rewriting `develop`'s history) exist but contradict the squash-only `develop` ruleset
  and the linearity invariant.
- **Develop**: the check stalls bot auto-merge when two bot PRs against `develop` land within the
  same window. As soon as the first merges, the second flips to `mergeStateStatus: BEHIND` and
  GitHub's auto-merge will not fire while strict is on. The merge-bot only *enables* auto-merge on
  `opened`/`reopened` and never auto-updates bot branches, and Dependabot's rebase isn't real-time,
  so the second PR sits OPEN with all checks green indefinitely. Squash mechanics still rebase the
  diff onto `develop`'s tip on merge, `required_linear_history` still enforces linearity, textual
  conflicts still block `mergeable: CONFLICTING`, and the required `Check pull request workflow
  status job` still gates merges. The only thing lost is pre-merge detection of
  *semantic-but-not-textual* conflicts, which the post-merge `develop` CI run catches anyway.

## Dual-target bots

**Dependabot and codegen target both `main` and `develop` in parallel.**
`.github/dependabot.yml` duplicates every ecosystem entry (one per branch) and the codegen
workflow runs as a matrix over both branches with branch names `codegen-main` and
`codegen-develop`. Each branch absorbs its own bot PRs independently, so neither falls behind, and
the forward-only rule still holds, nothing is back-merged from `main` to `develop`, both branches
receive their updates directly. The merge-bot (`.github/workflows/merge-bot-pull-request.yml`)
dispatches `--squash` or `--merge` from each PR's base ref via a `case` statement so the form
matches the ruleset on either base. Dependabot **security** PRs (CVE-driven) always open against
the repo default branch (`main`) regardless of `target-branch`, and the same `case` statement
covers them. The merge-bot auto-merges **every** Dependabot tier including semver-major (no
ecosystem or update-type guard), the required CI checks are the gate, not the bump magnitude, so a
major that breaks the build fails its checks and never merges.

**Why parallel dual-target rather than develop-only with eventual flow-through:**
push-distribution channels (HACS for Home Assistant integrations, Linux distros that vendor from
`main`, etc.) consume `main` directly. A develop-only model would leave `main` running stale code
during long-running develop features. Codegen content can also be production-critical (live
API-derived data, language lists, build catalogs) rather than just sample/demo content, so both
branches need fresh codegen on their own cadence.

**Maintainer-pushed commits on a bot PR auto-disable auto-merge.** The merge-bot's
`merge-dependabot` and `merge-codegen` jobs only fire on `opened`/`reopened` events (auto-merge is
enabled exactly once per PR). When a maintainer pushes commits to a bot's branch (a `synchronize`
event with an actor that isn't the same bot), the merge-bot's
`disable-auto-merge-on-maintainer-push` job fires and calls `gh pr merge --disable-auto`. The
maintainer's commits stay in the PR but won't auto-merge with the bot's content. Re-enable
auto-merge manually (`gh pr merge --auto <PR>` or the GitHub UI) when ready.

## Codegen determinism

The codegen workflow is a mechanism to refresh files that are checked into the repo: it runs a
matrix over `main` and `develop`, each leg regenerating against its own checkout and opening its
own PR (`codegen-main -> main`, `codegen-develop -> develop`). For the two legs not to conflict on
`develop -> main`, the generated output must depend only on its inputs, never on per-invocation
state (timestamps, GUIDs, build IDs), which would diverge every run and conflict on every release.
**What** a repo regenerates (data files, source, or both) and **how** (download and process an
external source, transform local inputs, whatever) is entirely its own concern. The constraint is
only that the output be input-deterministic, not how it is produced. A repo adopting codegen
supplies its own input-deterministic generator and wires the codegen reference workflow
(`run-codegen-pull-request-task.yml` and its scheduler).

## App-token workflows use Client ID, not App ID

`actions/create-github-app-token` deprecated the numeric `app-id` input in v3.0.0. Use
`client-id: ${{ secrets.CODEGEN_APP_CLIENT_ID }}`. When adding new App-token call sites, use the
same form, and do not reintroduce `app-id` / `CODEGEN_APP_ID`. See the hub's
`repo-config/README.md` "Secrets" for which secrets each mechanism needs.
