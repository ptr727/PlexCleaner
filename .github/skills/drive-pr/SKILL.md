---
name: drive-pr
description: >-
  Drives a ptr727/ProjectTemplate fleet pull request through its review loop, feature branch into
  develop and, when asked, on to a mergeable develop -> main promotion PR, disposing of every
  reviewer finding under pr-review-conduct's outcomes and escalating to whoever dispatched the drive
  where its seat cannot reach the maintainer. Use this whenever asked to drive, land, take, chase,
  or push a PR toward develop or main, or to run the review loop hands off. When the request does
  not say how far ("drive this PR", "land it"), ask once whether the target is develop or a
  mergeable main promotion PR, rather than guessing. Triggers even when only one PR is named,
  because a promotion-PR finding routinely needs its own feature -> develop fix cycle, and stopping
  at the first one is the early exit this skill prevents. Ends at develop merged, or at a promotion
  PR meeting every Merge Gate item but the maintainer's permission to merge. It never merges main,
  which is merge-and-release's, its own go-ahead.
---

# Drive PR

## Why This Exists

The same request repeats every time a change is ready. It asks to drive the change through review,
resolve whatever a reviewer raises, and keep going until develop, or main, actually has it.
Re-explaining the finding-disposition policy and the promotion-PR wrinkle each time is the cost
this skill removes. The wrinkle is that a finding raised against the develop -> main promotion PR
usually cannot be fixed on that PR directly. Its diff is develop's diff against main, so the fix
lands as its own feature -> develop PR first. Stopping at the first such finding is the early exit
this skill exists to prevent. So is forgetting to loop back to the promotion PR once the fix lands.

## How Far to Drive

- Read the invocation for an explicit target first. "To develop" or "to dev" means stop once
  merged into develop. "To main", "through to main", or "all the way" means continue to a
  mergeable promotion PR. Act on either without asking. `session-handoff`'s attended session,
  invoked by "resume the handoff", states the second, and naming that procedure names this skill.
- When the request names no target ("drive this PR", "land it", "take this PR"), ask once,
  before the first push. The question is develop only, or all the way to a mergeable main
  promotion PR. Recommend "all the way to main" as the default. That is because a promotion PR left
  to go stale once develop is ready is the more common regret than driving one step too far.
- A drive dispatched as part of a larger run takes its target from the brief and asks no one.
  That is because a subagent stopping to ask stalls a run designed to keep moving without one. It
  is also because the seat that dispatched it is the seat that holds the maintainer's answer.
  `backlog-burndown` is such a run, and it briefs develop only. It drives the develop -> main
  promotion pull request in its own seat under "The Drive Loop"'s promotion steps. A brief naming
  no target at all is one to stop and ask its dispatcher about. Asking the dispatcher is the whole
  of what a dispatched drive does about an authorization question. A dispatched drive is not the
  seat that can verify a grant, so it does not try. The responsibility for having the maintainer's
  go-ahead sits with the dispatcher. A worker inventing a check it cannot perform would only
  launder that responsibility rather than discharge it.
- **A brief is never itself the authorization**, which binds the dispatching seat. What authorizes
  a merge is what the maintainer said, recorded where the skill that carries the grant states its
  scope. `backlog-burndown`'s own "What Invoking This Skill Authorizes" is one such place. An agent
  cannot widen its own permission by writing itself an approval. A merge is also the outward-facing
  act this skill's own "What Invoking This Skill Authorizes" keeps tied to something the maintainer
  actually said. So writing an approval into a brief creates none.
- A repo on the operational workflow model (registry `workflowModel: operational`) has no
  standing promotion PR expectation. Confirm whether a promotion PR is even wanted before opening
  one, per branching-and-release-model's "Operational repositories (the complete delta)"
  section.

## What Invoking This Skill Authorizes

- Naming this skill, and answering its how-far question, is the maintainer's explicit, current
  go-ahead. It covers every feature -> develop squash merge the drive performs to reach that target.
- A dispatched drive answers no such question. So what stands in its place is the go-ahead the
  dispatching seat holds, per "How Far to Drive" above. The drive performs the same merges on it.
  Reading this bullet list is not how such a drive establishes that. That is because a worker
  cannot verify a grant made in a seat it has no access to.
- It is never authorization to merge the develop -> main promotion PR, or to dispatch a release.
  Those stay in merge-and-release, invoked on its own so the maintainer keeps a checkpoint before
  the harder-to-reverse step.
- The pr-review-conduct Merge Gate still gates every merge this skill performs on its own. The
  go-ahead removes the "may I merge to develop" question, not the gate itself, a feature PR with
  an open finding does not merge regardless of target.

## The Drive Loop

1. Isolate into a worktree per repo-worktree before the first edit. Base it on the branch that skill's base rule names, develop unless the task is explicitly about main-only content.
2. Commit the work, then run `local-strict-review` and record its pass in the order that skill
   gives, its diff receipt following the commit. Where the change also carries the canonical ledger,
   that skill's carried-content records instead precede the commit, because that ledger is tracked.
   A driver that cannot dispatch a subagent follows that skill's "Running It".
   Then push the branch and open the feature -> develop PR if it does not exist yet. Attest the
   pass on it, per `pr-review-conduct` "Expected review loop" step 1. Every draft of a pull
   request's body, the opening one and each later rewrite, goes in a file named for its
   repository and branch. A shared name such as `pr-body.md` lets a concurrent driver's draft
   land on the wrong pull request. A pass's
   edit budget can be spent on this push or a later one. Where it is, write each `introduced`
   finding still open into the pull request's body with its class, per `local-strict-review`
   "Disposing of Findings". A later push adds its own to any list already there. Step 3 gives
   each one an outcome under the Merge Gate's item 3. Where the
   change writes a code or config comment line, add the `comments` label after creating the PR, or
   the prose gate refuses it. Where a public repository's lint log says "Read the pull
   request's labels live", a rerun of the failed job picks the label up. Otherwise it applies to
   the next push.
   A push refused by a `.husky/pre-push` hook is that gate working rather than an obstacle to
   route around. The hub carries such a hook, and a repository has one only if it adds one. That
   skill's refusal table says what each refusal means and what clears it.
3. Drive pr-review-conduct's review loop on it to the Merge Gate, disposing of every finding per
   "Disposing of Every Finding" below. Request no review by hand. `wait` sends each request the review
   policy asks for. Under the hub's default it reads step 2's attestation as `coverage=local` here. It
   then requests no Copilot round, unless a partial round or a truncated review history is on record. Exit `49` means the head carries no attestation, so run and record a
   pass over it, push, and attest. Exit `46` means a thread is open on the head, so dispose of each
   one per "Disposing of Every Finding" and wait again. Read `state=` in every digest `wait` or `status` prints.
   A peer can merge or close the pull request mid-drive. A digest carrying `state=MERGED` on a feature PR into develop means the merge has landed.
   Skip the merge in step 4 and go on to its post-merge path.
   On a promotion PR it means the drive's end state is passed, so stop and report it merged.
   A digest carrying `state=CLOSED` is a stop to report, with its `closed_at=`.
   On such a PR `state=` decides the next step over any exit code `wait` or `status` returns. A promotion re-reviews content its feature PRs already carried.
   So under the hub's default review policy, the hosted review runs once per change, at the
   promotion in step 6. `wait` sends each request the repository's review policy asks for, so
   the drive never asks whether a reviewer starts on its own.
4. Merge the feature PR into
   develop with `uv run --no-project --script scripts/pr_review.py merge <number> --repo <owner>/<repo> --cleanup --checkout <base-clone>`,
   run from a hub checkout outside the task's worktree, `<base-clone>` being the repository's
   primary clone. It reads the Merge Gate's mechanically readable items in code, through `wait`'s
   own decision and a fresh read after it. It merges nothing while one reads unmet. The items no
   code reads stay this seat's per pr-review-conduct's Merge Gate. A finding the pull request's
   body lists is one. A no-thread finding on an earlier round is one too, since `FINDINGS WITH NO
   THREAD` reads the head's round alone. Dispose of them before running `merge`. A finding no
   thread carries and a part under `REVIEWER OUTPUT NOTED` still refuse it until each carries the
   `--ack` record pr-review-conduct "Answering a suppressed finding" states. A `qodo_open`
   finding refuses it the same way, even where Qodo also opened a thread for it, since resolving
   that thread answers nothing. For a disproved one, that comment's body mentions the Qodo login
   the digest's remedy line names. It asks Qodo to dismiss the finding by the number the remedy
   line gives, with the reason. That answer is the redrive an `UNTHREADED_FINDINGS` line naming
   `qodo_open` owes, rather than a blocked merge for the maintainer. It squash-merges only an open pull request into `develop`, headed in
   that same repository on a branch that is not permanent. The merge is pinned to the head the gate
   read and confirmed by reading it back.
   Every run ends on one `next=` line, and the drive follows it rather than reading each status
   line's route here. The hub's `scripts/README.md` "`pr_merge.py`" gives each route in full.

   - `next=done` is merged and cleaned up.
   - `next=cleanup` is a merge that landed with its cleanup still owed. Run the `cleanup` command
     below. Report a pull request another hand merged as a merge this run cannot vouch for. The
     head that merged may be one the gate never read.
   - `next=readback` is a merge that may have landed. Read it back with `gh pr view <number> --repo
     <owner>/<repo> --json state`. `MERGED` means run `cleanup`, and `OPEN` means treat it as
     `redrive`. Only a read that still cannot answer is reported, saying the merge may have landed.
   - `next=redrive` is a refusal that merged nothing and that this seat can clear. Do what its
     `status=` lines name, per step 3 and pr-review-conduct, giving each finding they count an
     outcome per "Disposing of Every Finding". A check still running clears by waiting for it,
     with `timeout 1800 gh pr checks <number> --repo <owner>/<repo> --watch`. A hosted
     reviewer's review still running on the head is such a check, refused as `CHECKS_PENDING`.
     Then run `merge` once more.
   - `next=retry` is a read that did not answer, or a merge `gh` refused on an unchanged head. Run
     `merge` once more as it is.
   - `next=maintainer` is a line no action of this seat can change, or a merge whose `cleanup`
     stopped. Report it without a rerun.

   One rerun after `redrive` or `retry` is the bound. Report what still stands then to the
   maintainer as a blocked merge, naming the pull request, each line, and each counted finding's
   outcome. Name each check the refusal's `pending=` line lists too, since a pull request with a
   check or a review still running is not finished. The maintainer can then merge it by hand. A refusal this seat reports is never routed
   around, and it hands the maintainer no command to run instead.
   A harness refusing the `merge` call itself is a refused write
   rather than a gate refusal. That happens on a machine with no allow rule for it, or under
   another provider. Raise it as a blocked decision per `GOVERNANCE.md` "Repository Boundaries and
   Write Safety". Name the maintainer's routes, adding the permission rule or running the command
   themselves. Once the merge is
   confirmed, `--cleanup` runs repo-worktree's post-merge cleanup,
   `uv run --no-project --script scripts/pr_review.py cleanup <number> --repo <owner>/<repo> --checkout <base-clone>`,
   and its exit is the command's. It deletes the remote branch, then removes the task's worktree and local branch.
   It decides its local refusals before the remote delete. `repo-worktree` "Listing and
   Cleanup" says what a refusal after the remote delete and a `base=kept` line each leave to do.
   It takes the pull request rather than a branch name and does the verify-then-delete in
   code. It refuses unless the pull request merged into `develop` from a head in that same
   repository. It also refuses a permanent branch or one any ruleset or branch protection covers. It
   checks the tip is still at the pull request's `headRefOid` before deleting, and reads the ref
   back to confirm.
   These exits are `cleanup`'s, read once a `status=MERGED` line printed or a standalone `cleanup`
   ran. Exit `0` is done, the branch deleted or already gone. Any other exit stops and reports rather
   than deleting by hand. `1` is a refusal, a moved tip among them. `2` is a read that did not
   answer or a delete it could not confirm. `64` is a target under an owner other than the hub
   checkout's own `origin`, or that `origin` unreadable. A registry refusal is a `1`. It runs
   under the host-safety installer's `Bash(uv run --no-project --script scripts/pr_review.py:*)` allow rule,
   which is what lets auto mode complete it.
   It also refuses while another open pull request in the repository has the branch as its head or base.
   It names each, since the delete would close or retarget it.
   Where this seat cannot run `cleanup`, report the delete to the maintainer instead of deleting by hand.
   Name the pull request number and `--repo` so the maintainer runs the same command.
   No direct `git push origin --delete`, ref API delete, `git worktree remove`, or `git branch -D` runs here, since `cleanup` holds the checks.
   A pull request opened from a fork follows `upstream-contribution-workflow` instead, and `cleanup` refuses it.
   The
   repo's auto-delete-head-branches setting is kept off fleet-wide (to protect `develop` and
   `main` from it, GitHub has no per-branch exception). So nothing deletes an ordinary feature
   branch automatically. Stop here and report the merged PR when the target is develop only.
5. Open the develop -> main promotion PR if it does not exist yet, or find the existing one.
6. Drive its review loop the same way. A finding that needs a code change never gets pushed to
   the promotion PR directly, since its head is develop. So land the fix as a fresh pass through
   steps 1 to 4 in its own worktree and branch. Then return here.
7. The fix landing on develop updates the promotion PR's diff and head SHA on its own. Re-request
   a review on the new head and continue the loop.
8. Repeat 6 and 7 until the promotion PR meets every pr-review-conduct Merge Gate item except the
   maintainer's explicit permission to merge.
9. Report the promotion PR number, its ready state, and its full head SHA, read as `gh pr view
   <number> --repo <owner>/<repo> --json headRefOid --jq .headRefOid`, the one `merge-and-release`
   compares against before merging. Do not merge it.

## Disposing of Every Finding

The rule below is a generated include, so a defect in it is fixed in `pr-review-conduct` and
regenerated rather than edited here. A drive that cannot reach the maintainer directly, a
dispatched one being the ordinary case, escalates per `pr-review-conduct` "Escalate to the
maintainer when".

<!-- include: .agents/skills/pr-review-conduct/SKILL.md > Every finding ends in a listed outcome -->

A finding that committed text holds observed data takes outcome 4's observed-data bullet,
whatever its class, ahead of every other outcome. That applies once the data is confirmed as
observed. A value the repository constructed is disproved under outcome 2 instead.

1. **Real, so fix it, and fix the class rather than the instance.** A reviewer samples rather
   than enumerates, so sweep for the finding's siblings before replying. Fix each one sitting
   in a file the diff already touches or that this change itself made wrong. Each of the rest takes
   the outcome a `pre-existing` finding takes under outcome 4, per `GOVERNANCE.md` "Verification Discipline". That sweep is owed the first time the finding is
   raised, not once it recurs. Take the fix through `local-strict-review` the same way the push
   that opened the pull request went, per `pr-review-conduct` "Expected review loop". Then reply
   with the fixing commit SHA. A branch already reviewed once has not been reviewed for the fix.
   That is the round the `local-strict-review` pass gets dropped on, and the churn
   `local-strict-review` exists to stop. For a finding on platform-specific code (PowerShell, a macOS- or WSL-only
   path), "fixed" means executed on that platform, per
   `agent-conduct` "Before Claiming Done". A fix reasoned out by analogy to a tested equivalent
   elsewhere is not yet fixed. The reply says so rather than claiming the SHA closes it.
2. **Not real, or real but structurally out of scope, so decline in the thread with evidence.**
   Disprove a wrong finding with the command and its output, the code path that makes it
   impossible, or the rule that governs it. A finding that is factually correct but not this
   repo's to fix declines the same way. A verbatim-fidelity manifest entry byte-locking the section
   is one such case, and ownership that sits elsewhere is another. Name the boundary and cite what
   proves it. A hardening finding that `GOVERNANCE.md` "Trust Boundaries and Hardening Effort"
   rules out declines the same way, citing that section and the input or deployment it turns on. Each shape closes the thread on
   its own evidence, and the agent resolves such a thread itself rather than leaving it for the
   maintainer. What makes that safe is the evidence being checkable by anyone,
   a command and its output, the code path, the quoted rule, a byte-identical diff. So a decline
   resting on anything weaker is not one of these. An assertion ("this is fine") does not close a
   finding. Outcome 3's value call is the maintainer's, so that thread stays open until they
   answer it.
3. **Real, fixable here, but deliberately left as is, a value call rather than a scope
   boundary, so it is the maintainer's, not the agent's.** Reach for this only once outcome 2 is
   ruled out, since a scope boundary declines on its own evidence and never needs this outcome. State the finding and why the fix is unwanted, and get an explicit answer in the same
   turn, before moving to other work. A plan to ask later is resolution by silence the moment
   attention moves elsewhere. If the maintainer is not reachable right now, leave the thread open
   and say so, rather than treating the intention to ask as the asking.
4. **Deferred past this pull request, or on text it did not touch, so record it by its class.**
   An `introduced` finding fixable here and deliberately left as is takes outcome 3 instead.
   An `introduced` finding leads to an issue only where the last bullet below says so. A spent edit budget hands any other `introduced` finding to this pull request's review loop, never past the
   pull request. An edge case on code this change wrote is `introduced`. Where `GOVERNANCE.md`
   "Trust Boundaries and Hardening Effort" lets it earn a fix or an issue, it takes the fix.
   Otherwise it is declined under outcome 2 with that section's reason.
   The class is the one `local-strict-review` "Disposing of Findings" defines, and the first
   bullet that fits decides:
   - A finding that committed text holds observed data takes the route `GOVERNANCE.md`
     "Representative Data in Agent-Authored Text" sets, and the first case that fits decides.
     Sweep for its siblings as outcome 1 does, since a reviewer samples, and give each one the
     route its own case sets. Read the visibility live. Data no pushed commit holds is removed from the unpushed commits
     before the push, and needs no record. A spent edit budget does not end that case, since the
     push waits on it. Two kinds are escalated per "Escalate to the maintainer when", and their
     threads stay open. One is data in a public repository's pushed history, the hub's included.
     The other is data in a carrying repository's copy of hub-authored text. In a private repository, pushed data is
     replaced here, even in a file this change does not otherwise touch. Where the current text
     no longer holds it, nothing is replaced, and the record below is still owed. On a develop ->
     main promotion pull request, a replacement lands in a feature pull request of its own, with
     its record. Where nothing is replaced, the record goes in the promotion's own body. Its body line goes under an `Observed data recorded` heading with the location
     and the kind, never the value. Where another pull request's review raised it, the line names
     that one too. The pull request holding the record takes the `history-data` label, and so
     does the one whose review raised it. Where the repository lacks that label, the maintainer is
     told. A thread raising a recorded finding is answered with a pointer to its body line and
     resolved.
   - A local pass before the push that opens the pull request can raise a `pre-existing`
     correctness defect in a file the diff already touches. That defect is fixed here under
     outcome 1. That covers wrong behavior and a false or unfollowable instruction, and not a
     remark on wording. The reach is the file, as outcome 1's class sweep reaches it. Its fix is
     text this change wrote, so a finding on that fix is `introduced` and spends the same edit
     budget. One that budget leaves unfixed takes the first bullet below that fits. One raised by a
     pass after the opening push, or by a PR-hosted review, takes it too. So a push after the opening one is
     never owed for one, and the bound holds once per pull request. This bullet
     does not apply on a develop -> main promotion pull request. Its diff is a batch of merged
     changes, and each fix there needs a feature pull request of its own. Nor does it apply to a
     canonical unit in a carrying repository, which `pr-review-conduct` "Merge Gate" item 3 routes
     to the hub.
   - A `pre-existing` finding on a canonical Markdown unit keeps the per-unit tracker
     `pr-review-conduct` "Merge Gate" item 3 describes, since that already costs one link per unit.
   - A `pre-existing` correctness defect in code that runs, with a stated failure, is filed once
     as its own issue, labeled `pre-existing`. Such a finding names an input or a state and the
     wrong output or the crash it produces. Its body line goes under a `Pre-existing findings,
     filed` heading and links that issue. A thread raising it is answered with that issue's link
     and resolved. Where its class has other sites, that one issue names each. An open issue for that class takes the new sites instead of a new one, unless a
     fix for it is already merged or claimed.
     A near-duplicate with no defect behind it is a consolidation finding, which the dropped
     bullet below takes.
   - A `style`, wording, consolidation, or restatement finding on text this change did not touch
     is dropped. It gets one line under a `Dropped findings` heading and nothing else. It gets no
     decline text, no issue, and no reply beyond a pointer to that line, which resolves any
     thread.
   - Any other `pre-existing` finding, from a local pass or a PR-hosted review, is listed in the
     pull request body with its location and its class. No issue is filed for it. The body line
     is its outcome, and a thread raising it is answered with a pointer to that line and resolved.
   - Nothing else is deferred to an issue. A question for the maintainer takes outcome 3 or
     "Escalate to the maintainer when", whichever fits. A fix that belongs to another repository
     declines under outcome 2. It is filed there where `GOVERNANCE.md` "Repository Boundaries and
     Write Safety" and the session's own scope both allow that write. It goes to the maintainer
     otherwise. Where work here waits
     on that fix, it also
     takes the local record `GOVERNANCE.md` "Durable Knowledge and Self-Improvement" requires of
     a blocker filed elsewhere. A deferral noted only in a thread is lost the
     moment the PR merges, so each of these leaves its record where its outcome says.

   The body keeps every other unfiled `pre-existing` line, the "Any other" bullet's, under one
   heading, `Pre-existing findings, not filed`. Each line under these headings carries its
   location and its class. The headings stay apart from the open `introduced` findings a spent
   edit budget puts in the body, per `local-strict-review` "Disposing of Findings". A reviewer's
   suppressed finding has no thread, so its pointer is an answer per `pr-review-conduct`
   "Answering a suppressed finding". That answer is `Listed in the body` for a listed or dropped
   finding, and `Deferred to <issue>` for a filed one. A local-pass finding owes no pointer
   beyond its body line. A sibling that outcome 1's class sweep leaves unfixed takes the
   bullet a `pre-existing` finding would take. An issue goes in the repository where
   the fix has to land. For a finding against carried content, that is the repository authoring
   the content rather than the one carrying it. An issue filed where nobody may make the fix is
   a deferral nobody can close. Filing every `pre-existing` finding is the expensive path. From
   2026-10-16 to 2026-10-23, 108 of 329 non-handoff issues filed across four fleet repositories
   carried `pre-existing`. Each one costs a lane, a pull request, and a review once worked. A fix
   inside a pull request already open costs only rounds that pull request already pays for.
5. **Keeps recurring although the class was swept, so the rule is what needs fixing.** A finding
   raised repeatedly against correct code means the code is not communicating something. Add the
   comment, sharpen the name, narrow the interface, or fix the rule if the rule is wrong.
   Bouncing the same point across rounds is the signal to escalate the rule itself, not to keep
   re-arguing it. This is not where the class sweep lives. Outcome 1 already owes that on the
   first instance. Reaching here means the sweep ran and the finding came back anyway.

**A disposition decided on one PR does not carry to the next.** The same finding shape recurring
on a sibling repo or PR, even within one batch or one session, gets its own outcome. That is its own
evidence-backed decline (outcome 2) or its own explicit maintainer answer (outcome 3). A prior
instance's outcome is context for the new one, never a standing answer to reuse in its place.

`pr-review-conduct` "Every finding ends in a listed outcome" keeps the full rule, and the
`drive-pr` Skill carries it whole as a generated include, applying it while driving.

<!-- /include -->

## Mechanics Live Elsewhere

- Review loop mechanics, the Merge Gate, and `scripts/pr_review.py`: pr-review-conduct.
- Branch rules, never delete develop, the EOL-only conflict, issue-closing keywords belonging on
  the promotion PR: branching-and-release-model.
- Worktree isolation and post-merge cleanup: repo-worktree.

## Stop and Ask, Beyond the How-Far Question

- A genuine design trade-off, a recurring finding pattern, or an architectural redesign proposal
  each escalate per pr-review-conduct's own list, restated there, not duplicated here.
- A part of the review the digest prints under `UNPLACED ON THE HEAD` holds the gate until its
  outcomes are acknowledged per pr-review-conduct's Merge Gate item 4. Never guess what the new
  wording probably meant. An item that cannot be told to be a finding or not goes to the
  maintainer. So does any part that pr-review-conduct's Merge Gate item 4 lead bullet
  covers.
