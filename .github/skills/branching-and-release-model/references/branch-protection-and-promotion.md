# Branch Protection and Promotion Mechanics

Full detail for the "Branching" rules in `SKILL.md`. Load this when configuring or reconstructing
branch protection on a fleet repo, or when executing a `develop -> main` promotion. Also load it
when recovering a lost `develop`, cutting a conflicted promotion, the EOL-only case included, or
recovering a squash into `main`. Load it too when working on the dual-target bot wiring (Dependabot, codegen, the merge-bot). It is not for an ordinary
feature-branch PR, which the SKILL.md summary covers.

## Configuring branch protection: don't hand-build the rules

From a hub checkout at `main`, create **exactly two rulesets named `develop` and `main`** from the
hub's `repo-config/*.json` payloads. Use a hub checkout at `develop` instead
while the repository's registry entry has merged there but not yet reached `main`. Run
`repo-config/configure.sh apply "<owner>/<repo>" release` (substitute `operational` for an operational repo) from that checkout. The names
are load-bearing because governance content and workflows reference them. The registry
`workflowModel` selects the `develop` payload for a registered repository. `apply` refuses a
repository outside the registry, writing nothing. Once `apply` has created both rulesets, delete
**all** classic branch-protection rules and stray rulesets, because rulesets are the only protection
mechanism. Deleted first, they would leave the branches unprotected wherever `apply` then refuses.

## Executing a `develop -> main` promotion safely

These traps were learned the hard way:

- **Never delete `develop`.** A promotion PR's head *is* `develop`, so `gh pr merge --delete-branch`
  (and a repository's "Automatically delete head branches" toggle, which the fleet keeps off for
  exactly this reason) deletes `develop` itself. Merge a promotion
  with a plain `gh pr merge --merge`, no `--delete-branch`. If `develop` is ever lost this way,
  restore it to the merged PR's head SHA. That SHA is still reachable as the merge commit's second parent:
  `gh api -X POST "repos/<owner>/<repo>/git/refs" -f ref=refs/heads/develop -f sha="$(gh pr view "<n>" --json headRefOid --jq .headRefOid)"`.
- **Spurious EOL-only conflicts resolve by taking `develop`.** When `develop`'s `.editorconfig`
  line-ending default has changed while `main` hasn't caught up yet, `develop -> main` conflicts
  *whole-file* on every renormalized path. An example of that default changing is the fleet-wide CRLF-to-LF flip.
  `develop`'s `required_linear_history` plus PR rulesets forbid resolving on `develop` (no merge
  commit, no force-push). So resolve on a throwaway branch off `main`:
  `git checkout -b promote/develop-to-main origin/main && git merge origin/develop`. Take
  `develop`'s side for the EOL-conflicted files (`git checkout --theirs "<file>"`) **after
  confirming each is content-identical modulo EOL, or that `develop` is a strict superset**
  (`diff <(git show ":2:<file>" | tr -d '\r') <(git show ":3:<file>" | tr -d '\r')`). Then open
  that branch into `main`. Verify no genuine `main`-only content is dropped (build/test where the repo supports it).
- **A conflicted promotion lands from a branch cut from `main` that merges `develop`.** The EOL-only case above is one instance. A bot bump that landed on `main` and `develop` separately, beside a line `develop` changed, is another. This procedure covers a `develop` that is a strict superset of `main`'s content. The steps:
  1. Run `git fetch origin`, then cut the branch in a worktree of its own: `git worktree add -b promote/develop-to-main "<worktree>" origin/main`. Run `git merge origin/develop` there.
  2. Take `develop`'s side on each conflict only after the strict-superset check above shows `main` holds nothing `develop` lacks there. Stage each resolved file and commit the merge, so `HEAD` holds it.
  3. Confirm the merged tree equals `develop`'s before the push: `git diff origin/develop HEAD --stat` prints nothing.
  4. A path it prints holds content `develop` lacks. Redo a resolution that kept `main`'s side. Land genuine `main`-only content on `develop` through a feature pull request first. Then run `git fetch origin develop` and merge `origin/develop` again.
  5. Open the branch as the promotion pull request in place of the one headed by `develop`. Carry the superseded pull request's whole body, its closing set included.
  6. Carry its labels too, `comments` among them. Without that label, a local strict pass on the new head reports every comment line the feature pull requests landed as `comment-added`.
  7. Close the superseded pull request with a comment naming the new one.
  8. Land each later `develop` fix on the branch in the same worktree. Run `git fetch origin develop`, then `git merge --no-ff origin/develop`. Re-run the tree check, then push the branch.
  9. Merge it like any promotion, with `--merge`. The merge commit restores the merge base, so the next promotion runs from `develop` again.
- **A squash into `main` is recovered with that same cut.** The `main` ruleset allows only merge commits, yet its admin bypass lets a squash through. A squashed promotion leaves `main` with `develop`'s content but not its history. The merge base stays at the previous merge commit. So the next promotion conflicts wherever a later `develop` change edits a line the squash already carried. Where it merges cleanly instead, it keeps any such line a later `develop` change removed. GitHub runs no pull request workflow while the promotion conflicts. So the hub's `validate-task.yml` `Check promotion history step` fails the next promotion that merges cleanly. It names each single-parent commit on `main`'s first-parent chain since its previous promotion merge. Recover with the conflicted-promotion cut above, resolving each conflict as its step 2 states. The cut merges from the same stale base, so it can keep a removed line too. Its tree check then prints that path. Restore `develop`'s version of each such path the squash carried with `git checkout origin/develop -- "<path>"`. Remove one `develop` has since deleted with `git rm "<path>"`. Then commit. The step passes a head that already holds each commit it named, and the merge then restores the merge base. Never rewrite or force-push `main` to undo the squash.

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
  GitHub's auto-merge will not fire while strict is on. The merge-bot arms auto-merge on
  `opened`/`reopened` and never auto-updates bot branches, and Dependabot's rebase isn't real-time.
  So the second PR sits OPEN with all checks green indefinitely. Squash mechanics still rebase the
  diff onto `develop`'s tip on merge, and `required_linear_history` still enforces linearity.
  Textual conflicts still block `mergeable: CONFLICTING`, and the required `Check pull request workflow
  status job` still gates merges. The only thing lost is pre-merge detection of
  *semantic-but-not-textual* conflicts, which the post-merge `develop` CI run catches anyway.

## Dual-target bots

**Dependabot and codegen target both `main` and `develop` in parallel.**
`.github/dependabot.yml` duplicates every ecosystem entry (one per branch) and the codegen
workflow runs as a matrix over both branches with branch names `codegen-main` and
`codegen-develop`. Each branch absorbs its own bot PRs independently, so neither falls behind.
The forward-only rule still holds: nothing is back-merged from `main` to `develop`, and both branches
receive their updates directly. The merge-bot (`.github/workflows/merge-bot-pull-request.yml`)
dispatches `--squash` or `--merge` from each PR's base ref via a `case` statement so the form
matches the ruleset on either base. Dependabot **security** PRs (CVE-driven) always open against
the repo default branch (`main`) regardless of `target-branch`, and the same `case` statement
covers them. The merge-bot auto-merges **every** Dependabot tier including semver-major (no
ecosystem or update-type guard). The required CI checks are the gate, not the bump magnitude, so a
major that breaks the build fails its checks and never merges.

**Why parallel dual-target rather than develop-only with eventual flow-through:**
Push-distribution channels consume `main` directly.
Examples include HACS for Home Assistant integrations, Linux distros that vendor from `main`, and others. A develop-only model would leave `main` running stale code
during long-running develop features. Codegen content can also be production-critical
(live API-derived data, language lists, build catalogs) rather than just sample/demo content.
So both branches need fresh codegen on their own cadence.

**Maintainer-pushed commits on a bot PR auto-disable auto-merge.** The merge-bot's
`merge-dependabot` and `merge-app` jobs arm auto-merge only on `opened`/`reopened` events. Its
`rearm-auto-merge` job leaves the disable below in place, since the head is then not the bot's own push. A maintainer may push commits to a bot's branch. Such a push is a `synchronize`
event with an actor that isn't the same bot. When it happens, the merge-bot's
`disable-auto-merge-on-maintainer-push` job fires and calls `gh pr merge --disable-auto`. The
maintainer's commits stay in the PR but won't auto-merge with the bot's content. Re-enable
auto-merge manually (`gh pr merge --auto "<PR>"` or the GitHub UI) when ready.

**A disable GitHub makes is re-armed once, then fails visibly.** GitHub can disable an armed
auto-merge with every check green. It does so for a merge that would write workflow content the
App may not write. The caller stub triggers on `auto_merge_disabled`, so the merge-bot's
`rearm-auto-merge` job answers such a disable. It re-arms once the base branch has settled. Where
every check is green, `gh` merges at once instead, so a refused merge fails the job. That makes the
stall visible rather than recovering it. WORKFLOW.md D8.1 is the contract.

## Codegen determinism

The codegen workflow is a mechanism to refresh files that are checked into the repo. It runs a
matrix over `main` and `develop`. Each leg regenerates against its own checkout and opens its
own PR (`codegen-main -> main`, `codegen-develop -> develop`). For the two legs not to conflict on
`develop -> main`, the generated output must depend only on its inputs. It must never depend on per-invocation
state (timestamps, GUIDs, build IDs), which would diverge every run and conflict on every release.
**What** a repo regenerates (data files, source, or both) is entirely its own concern.
So is **how** (download and process an external source, transform local inputs, whatever). The constraint is
only that the output be input-deterministic, not how it is produced. A repo adopting codegen
supplies its own input-deterministic generator and wires the codegen reference workflow
(`run-codegen-pull-request-task.yml` and its scheduler).

## App-token workflows use Client ID, not App ID

`actions/create-github-app-token` deprecated the numeric `app-id` input in v3.0.0. Use
`client-id: ${{ secrets.CODEGEN_APP_CLIENT_ID }}`. When adding new App-token call sites, use the
same form, and do not reintroduce `app-id` / `CODEGEN_APP_ID`. A mechanism needing a secret this
repository does not hold is a configuration question for the maintainer. It is not something to
work around in the workflow.
