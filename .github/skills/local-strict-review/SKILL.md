---
name: local-strict-review
description: >-
  Runs a read-only, adversarial review pass against this branch's current diff against its
  target branch, full file context included, on the strongest model tier the session can reach,
  before a unit of work is pushed toward a pull request or claimed done. Use this whenever staged,
  committed, or untracked work is about to be pushed on a PR-bound branch, and whenever
  `agent-conduct`'s "about to claim work is done, verified, green, or fixed" trigger fires for
  PR-bound work. Triggers even when the change looks small or the same session already judged its
  own diff ready, because a self-review pass judging its own diff inherits its own blind spots,
  the exact gap this skill exists to close before a PR-hosted reviewer closes it instead. Reuses
  `code-review`'s "Review the Change" criteria rather than restating them, and owns only this
  local, pre-PR moment. Once a pull request exists, `pr-review-conduct` and `drive-pr` own
  triaging and disposing of what a PR-hosted reviewer finds. Also triggers whenever a change
  edits rule text, a Skill, or any other canonical content this repository authors and other
  repositories carry, because that content reaches a reviewer whole only when a repository
  carries it for the first time, and a second pass reading each changed unit's whole text is
  what moves that read into the repository that can act on what it finds.
---

# Local Strict Review

## Why This Exists

A coding agent that finishes a unit of work, judges it ready, and opens the pull request is judging its own diff with the model, and often the blind spots, that wrote it. CodeRabbit, Qodo, and Copilot routinely find real defects that a local pass missed, and each round costs review latency and, for a rate-limited reviewer, shared account-wide quota. A local, full-file-context adversarial pass before the pull request exists catches the same class of defect for a fixed, smaller cost, the same reasoning that already runs local lint before a push instead of waiting for CI.

## What It Does

Dispatches one read-only subagent against this branch's full diff since it forked from its target branch. Resolve `<target>` once, `develop` unless `repo-worktree`'s base-branch rule put this branch on `main` instead, then fetch it, `git fetch origin <target>`, and diff against the merge-base, `git diff "$(git merge-base origin/<target> HEAD)"`. Stop and report a failed fetch rather than running the merge-base or diff commands anyway: an existing local `origin/<target>` ref can still resolve after a failed fetch, and reviewing against it silently trades the current target for a stale one. Use the same resolved `<target>` in every command below, never a literal `develop` alongside it. Naming the target branch explicitly matters: the branch's own `@{u}` tracking ref points at the branch's own remote once it has been pushed, not at the branch it targets, so anchoring there silently narrows a later run to only the diff since the last push instead of the full accumulated diff. That merge-base diff covers every commit already on the branch plus whatever is currently staged or unstaged, so it never reviews only the latest increment, at any of the moments this skill is invoked from. An empty diff is not the same as nothing to review, and it is never the signal to stop: it reports no untracked file at all, and it reports nothing for content a commit carries that the working tree has since put back. The untracked-file list below covers the first of those. The second is why the diff pass commits before reviewing, the carried-content pass below running against uncommitted content instead, since a removal or a restore that is committed leaves no net content to miss, and why the engine reads HEAD rather than this diff, its change set coming from the merge base against HEAD, the index and the working tree, so the two answer different questions. A fresh review of the full accumulated diff is what catches what per-push review misses, the exact evidence this skill exists to act on.

`git diff` never reports a path `git add` has not touched, so a newly created file sitting untracked would otherwise go unread. List it explicitly, `git ls-files --others --exclude-standard`, and read each result in full alongside the diff, the same as any other file the diff touches.

The subagent reads the full content of every file the diff and the untracked-file list touch, not just the hunks, since cross-file and whole-file context is exactly what incremental review misses. It reports findings only. It never fixes, stages, or commits anything.

Review criteria are `code-review`'s "Review the Change" section, reused rather than restated here, plus three traps worth calling out explicitly for a pass that runs before a human or a PR-hosted reviewer ever sees the diff: unguarded type coercions, TOCTOU/race conditions, and platform-specific behavior differences. `code-review`'s separate "Publish Every Finding" section does not apply here: this skill has no PR to post a comment on and no coverage marker to close a review with, so its own report contract below replaces that section rather than extending it.

## Running It

Follow `AGENTS.md` "Context and Delegation Discipline"'s subagent briefing shape:

```text
Task: adversarial review of this branch's diff against its merge-base with its target branch,
  read full surrounding files where the diff hunks alone do not give enough context.
Paths: the files `git diff --name-only "$(git merge-base origin/<target> HEAD)"` and
  `git ls-files --others --exclude-standard` list, mandatory floor. Reading a specific
  unchanged caller or consumer beyond that list is in bounds only where a candidate finding's
  proof actually depends on it, per code-review's own "follow data and control flow beyond the
  edited lines" instruction below, never as an open-ended exploration.
Rules that bind this task: quote `code-review`'s "Review the Change" section into the prompt,
  plus flag unguarded type coercions, TOCTOU/race conditions, and platform-specific behavior
  differences explicitly. Do not quote "Publish Every Finding", this task's report contract is
  the Return line below, not a PR comment or a coverage marker.
Return: one finding per line, file:line, the concrete failure scenario, no severity theater.
Bounds: read-only. No edit, no stage, no commit, no push, no PR-hosted write of any kind.
<AGENTS.md's own unresolved-rule closing line, quoted verbatim from "Context and Delegation Discipline", not restated here>
```

Before dispatching, grep the tree for other statements of each rule the diff adds or changes, and add each file holding one to the `Paths:` floor, so a statement the diff has put in disagreement is read rather than missed.

**Model tier:** the strongest tier this session can reach, per `AGENTS.md` "Match the model tier to the judgment" and "Never tier down the seat holding the judgment", applied here to the reviewer rather than the author. Run the pass on the same tier that authored the change when only one tier is reachable, a second, adversarially-prompted look still catches what the authoring pass's own "looks ready" judgment did not.

"This session can reach" means the tier this session can name when it dispatches the reviewer, rather than the tier this session is itself running on. A session deliberately tiered down for execution work, a worker dispatched by an orchestrator being the ordinary case, names a stronger tier for the reviewer where its harness lets it, since tiering down the author is the reason the reviewer must not follow it down. What a given harness and account actually permit varies, so treat this as the tier to ask for rather than one to assume. Where a dispatch reaches several tiers but exposes no way to name one, take what it gives and run the pass, on the same reasoning as the single-reachable-tier sentence above. A seat that cannot dispatch a subagent at all cannot perform this pass. Instead of pushing, it reports that it could not run the pass, to whoever dispatched it, or to the maintainer where nobody did. Either way it is a push that does not happen rather than a pass quietly skipped. The headless `run --backend` route under "Recording the Pass" is not the substitute: it runs a vendor CLI against its own review, which never carries the brief above, so it satisfies the rule this section states only where that separate route is what a capture point asked for.

## Recording the Pass

`scripts/local_review.py` is what makes this rule checkable rather than something each session has to remember. For the pass above, the engine only records that it happened, keyed on the content the reviewer actually saw, and its `run --backend <name>` subcommand is the separate case where a headless backend performs the review and records its own count. That receipt is what a capture point reads, the hub's own `.husky/pre-push` hook being the only one today, and a repository having none unless it adds one, since no manifest entry carries it.

Commit first, then read the digest, then dispatch the subagent, then hand that same value back. Nothing may change the tree between the read and the record. Staging a modified tracked file is such a change, moving the digest although the content did not, and a commit can move it too, since HEAD decides which paths are in the change set at all. Reading after the commit is what leaves neither of them between the read and the record.

```sh
engine="<hub-checkout>/scripts/local_review.py"   # in the hub itself, scripts/local_review.py
python3 "$engine" status --target <target>        # JSON, take contentDigest
# run the pass above, then:
python3 "$engine" record --reviewer agent-skill --target <target> --expect-digest <digest> [--findings N]
```

Every subcommand here, `run --backend <name>` included, runs with the repository under review as the working directory, whichever repository that is. The engine takes no `--repo` and reads whichever repository it is run in, so the path names where the script lives and the working directory names what it measures.

`<target>` is the same branch "What It Does" resolved for the review, passed to both commands. Leaving it off defaults them to `develop`, and on a `main`-based branch that computes the digest against a merge base the reviewer never read, so the receipt would attest to a change set nobody looked at. A receipt is only valid against the target it names, so the two have to agree.

`--expect-digest` is required rather than optional, and binding it to the earlier read is the whole point. A format-on-save or a hook autofix between the review and the record would otherwise be stamped as reviewed by a pass that never saw it. A refusal there is the content having moved, so the answer is another pass over the current content rather than another read of the digest.

Record the pass whatever it found, including nothing. The key covers the net content the branch introduces against its target rather than the commit series, so an interactive rebase that leaves the tree alone keeps the receipt valid, and changing one byte invalidates it.

**Why the commit comes first**, rather than being an ordering that could equally run the other way. A push delivers the commit, and the hook's tree check refuses a push whose tracked content differs from HEAD, so the record has to describe what HEAD holds. A commit that leaves the tree alone usually does not move the receipt's key, so diligence done before it still describes the same content, and a commit putting a path back to its base state drops it from the change set and does move it. Two reasons make the order matter anyway: staging a modified tracked file moves the key even though its content did not change, and a commit made after the record can carry content the pass never read. Reviewing earlier than this is still worth doing as ordinary diligence, and it does not substitute for the recorded pass: the digest read and the record bracket a window in which the tree holds still, and a commit inside that window ends it.

The engine is hub-hosted per `GOVERNANCE.md` "Hub-Hosted Tooling", so a downstream repository reaches a hub checkout's copy rather than carrying one, which is what the path above is for.

## The Carried-Content Pass

A second pass under the same rule, run in the repository that authors canonical content other repositories carry, which in this fleet is the hub. `GOVERNANCE.md` "Verification Discipline" states the rule and why the ordering it corrects is a defect, and is not restated here. What it requires of a run is below.

**The unit is what a reviewer reads whole**, and `spec/files.json` rather than the document decides which, down to which files carry units at all. `canonical_review.py list` names the whole set and is the authority on it, so the rules are not paraphrased here, where a paraphrase can only drift from them. In the ordinary case a unit is one level-two section of a carried Markdown canonical, and `check` names each one it wants exactly as `record` takes it. The pass reads that unit's whole current text rather than the diff that moved it, because reproducing the carrier's read is the entire point, and a diff with surrounding context is a different read the pass above has already done.

Run it at the same model tier and in the same delegation shape as the pass above. The brief, the engine, its flags, and the point in the sequence where the record is written each differ, and all four are below.

```text
Task: adversarial review of one canonical unit, read as a repository carrying it for the first
  time reads it, whole, knowing nothing about what this branch changed in it.
Paths: <the unit key, substituted here>, read in full out of the file that key names.
  Read the whole unit, never a diff of it.
Rules that bind this task: <quote code-review's "Review the Change" section>, and judge the text
  as a reader who has only this unit: a claim it makes about a tool, a path, a command, or
  another rule is a defect wherever that claim is false, stale, or unverifiable from the unit
  itself, and an instruction it gives is a defect wherever following it literally fails.
Return: one finding per line, the sentence quoted, and what is wrong with it. No severity theater.
Bounds: read-only. Report a rule that looks incomplete rather than guessing at what it meant.
<AGENTS.md's own unresolved-rule closing line, quoted verbatim from "Context and Delegation Discipline", not restated here>
```

```sh
git fetch origin <target>    # stop and report a failed fetch rather than measuring past it
python3 scripts/canonical_review.py check --target <target>   # each uncovered unit, with its digest
# run the pass above over each unit it named, then, per unit:
python3 scripts/canonical_review.py record --reviewer agent-skill --target <target> --unit '<key>=<digest>' [--findings N]
```

These run in the authoring repository itself, which is the only repository this pass ever runs in, so the engine path is the plain one and there is no downstream side needing the `<hub-checkout>/` form the pass above shows for its own reach. Point an engine in one checkout at another checkout's tree and the two mix, the engine's own section rules over the other tree's manifest and files.

`<target>` is the branch this work targets, resolved once as the pass above resolves it and passed to both commands explicitly. Left off it defaults to `develop`, so a branch based on `main` is measured from a fork point nobody read, and `record` stamps each pass with a merge-base against a branch the work never targeted. The fetch matters for the same reason it does above: the engine resolves `origin/<target>` if it already exists and never fetches it, so a stale remote-tracking ref moves the fork point without saying so. Lagging, which is the ordinary way to be stale, moves it back and gates units this change never touched, and the reverse case, where the branch restores text the target has since changed, drops one it did move. Neither is announced, so the fetch is what keeps the fork point meaning what the reviewer read against. `check` names each uncovered unit with the digest to hand back, so nothing has to be looked up separately, and `list` is there for reading the whole set rather than for this loop.

The digest is bound to the read for the same reason `--expect-digest` is above: recording a unit by name alone would stamp whatever the file holds at record time, so an edit between the review and the record would be attested to by a reviewer who never saw it. Record each unit whatever the pass found, including nothing. Fixing a finding is itself such an edit, so `record` then refuses the digest you were holding: that refusal is the content having moved rather than a fault in the record, and the answer is a read of the unit's new text, which is what a carrier will actually receive, recorded at its new digest.

**This pass records before the commit, where the pass above records after it**, and the two orders are opposite because the two records live in different places. A receipt sits in the worktree's git directory and can never be committed, so it is written once the commit has fixed what a push will deliver. This ledger, `reports/canonical-review.json`, is a tracked file the commit has to carry, so writing it after that commit leaves the tree differing from HEAD, which is a state the pre-push hook refuses before either gate runs. The shortest order meeting both, and the one the refusal table below assumes, is: run this pass and record each unit, commit that together with the change, then read the digest, run the diff pass, record its receipt, and push. Committing the change first and the ledger in a second commit satisfies the same constraint and costs a commit.

**A unit nothing has read here yet is not this branch's debt.** `check` refuses the units this change moved, meaning the ones whose text it edited and the ones it newly carried, since widening the manifest hands a carrier content for the first time exactly as writing it would. Everything else is a burn-down entry `canonical_review.py report` renders rather than a block on unrelated work. Working one of those off is worthwhile, and it is its own change rather than a tax on an unrelated one.

## Disposing of Findings

Each bullet is a rule down to its `Why:` line, which is rationale rather than rule, so a stale rationale is a cleanup rather than a defect.

- **Every finding ends in one of the outcomes that `pr-review-conduct` "Every finding ends in one of five outcomes" enumerates, reached here with no thread to reply in.**
  - `Why:` a local finding and a PR-hosted one deserve the same dispositions, and one home for the list is what stops two copies of it drifting apart.
- **The agent disposing of a pass's findings classes each one `style`, `introduced`, or `pre-existing`, in that order.** `style` is a preference between defensible forms. `introduced` is any other finding on text this change wrote, rewrote, or removed, on text this change should have written, on a precondition this change left false elsewhere, or load-bearing for a decision this change puts to the maintainer. `pre-existing` is every other finding.
  - `Why:` the reviewer is asked to omit preferences and returns some anyway, and `style` is classed first so that a preference on text this change wrote is not owed a fix.
- **Another round is owed only while an `introduced` finding is open.** Unless evidence disproves it, an `introduced` finding is fixed within the budget below, or escalated where `pr-review-conduct` "Escalate to the maintainer when" says so, a `pre-existing` one is filed once and blocks nothing, and a `style` one is declined with evidence, per `pr-review-conduct` "Every finding ends in one of five outcomes", the evidence being `code-review` "Review the Change"'s own rule to omit preferences.
  - `Why:` a finding count over prose never reaches zero, so a loop closing on "did it find anything" does not close, where one closing on the false claim, the unfollowable instruction, or the wrong behavior this change put there does.
- **A push allows two rounds of edits in answer to the passes it owes, one budget across both.** Where an `introduced` finding is still open after the second round, editing stops and what remains goes to the maintainer with its counts per class, per `pr-review-conduct` "Escalate to the maintainer when".
  - `Why:` past the second round nearly every finding is against text the previous round's fix wrote, so the rounds are producing the defects they find rather than removing them.
- **The pass is mandatory, and the count it records gates nothing.** A pass is recorded whatever it raised, so the record attests that a review ran rather than that the content is clean.
  - `Why:` a gate reading the count would make a pass raising nothing the cheapest way through it, the opposite of what recording one is for.

## When to Run It

- Before the first push toward a pull request, the push that opens it in `drive-pr` "The Drive Loop" and in `pr-review-conduct` "Expected review loop".
- Before pushing a fix for a reviewer finding, the same self-review blind spot applies to a fix as to the original diff (the fix outcome of `pr-review-conduct` "Every finding ends in one of five outcomes", which `drive-pr` "Disposing of Every Finding" carries).
- Whenever `agent-conduct`'s "about to claim work is done, verified, green, or fixed" trigger fires for work that will become, or already is, a pull request.
- Before pushing a change that edits canonical content other repositories carry, or that newly carries some by widening the manifest, over each unit `check` names, per "The Carried-Content Pass" above.

In the hub, `.husky/pre-push` checks the receipt, and the canonical-unit coverage beside it, at the push itself, so the moments above are where each pass is run rather than the only places it is noticed. A blocked push usually means one of those passes was skipped. Both capture points, that hook and the pull request one named below, are the hub's own, and a repository carrying this Skill has neither until one is carried to it, which is what makes the moments above the layer that actually binds everywhere. The hook is a backstop under this skill and not a replacement for it: it fires only in a clone that enabled `core.hooksPath`, it says nothing about a repository that carries no such hook, and it is bypassable by design, `--no-verify` being the documented route for a genuine pickle rather than for a diff nobody read. That route is not open in every seat. A Claude Code session running the fleet's agent-safety hook has the flag denied unconditionally, so where the rows below say a bypass is the answer, the answer in that seat is to report the state and hand the push to the maintainer rather than to force it. The hub's own `.github/actions/validate` composite action runs the canonical-unit half again as a step on every pull request into `main` or `develop`, which is what its workflow triggers on. That one needs no hooks path, runs whether or not any clone enabled one, and `--no-verify` does not reach it, which is what makes it the capture point a push cannot bypass where it applies.

**Read the refusal itself, which names its own case.** Some of the rows below are cleared by running a pass and some are cleared by nothing of the kind, and each row says which, so no count of either is kept here to go stale against the table. Some the hook decides before either engine runs, so there is no engine message under them, and the rows say where each one's detail comes from.

| The refusal says | What it means | What clears it |
| --- | --- | --- |
| No local review covers this branch's current content | The ordinary missing pass: no recorded receipt covers what this push delivers, either because none was recorded or because the content moved after one was | One pass over the branch's whole diff, recorded per "Recording the Pass" above |
| Tracked content differs from HEAD | A push delivers HEAD while a receipt covers the index and working tree, so the receipt does not describe this push. The hook prints the same headline for an unresolved merge and for a `git update-index --refresh` that exited above 1, naming each on its own line | Commit what is being pushed, then the pass, then the record. Where the change also moved a canonical unit, follow "The Carried-Content Pass" order instead, since committing first strands that ledger after the commit and each fix then lands on another row. Resolve the merge first where the hook names one, and run `git status` first where it names the refresh, since the content may not differ at all |
| The commit is not this worktree's HEAD | Any pushed branch ref carrying an object id that is neither this worktree's HEAD nor the all-zero id of a delete, which a push from a checkout sitting elsewhere reaches and so does a multi-ref push such as `git push --all` | Push one branch, the one this worktree holds. Where another branch is the one wanted, check it out in its own worktree first, per `repo-worktree` |
| Any wording saying the gate did not or could not run | An execution boundary rather than a verdict, which blocks because a gate that waves a push through when it could not run has stopped gating. The cause is named in that same message or in the engine error printed above it, and it is a missing Python interpreter, an unresolvable target, an unreadable receipt, a git command that failed, a manifest or ledger the engine could not read, or any unexpected failure | Whatever the message names, most often installing an interpreter per `docs/host-setup.md` or fetching the target branch. Never another pass |
| This branch changes N carried canonical unit(s) that no recorded pass covers | The carried-content pass was skipped for a unit this change moved or newly carried, and the refusal names each one with the digest to hand back | One carried-content pass per named unit, then `canonical_review.py record` for each, in the order "The Carried-Content Pass" above gives. The ledger that writes is tracked content, so the commit has to carry it and the diff pass comes after |
| A canonical refusal naming units this branch never touched | The fork point is not where the reader thinks it is. Either `origin/<target>` does not hold the commit this branch forked from, since neither engine ever fetches it, or the branch is based on something other than `develop` and the hook, which passes no `--target`, measured it against `develop` regardless. Unlike the row below it still prints a record command, and taking that one records passes over units nobody read | `git fetch origin <target>`, then `canonical_review.py check --target <target>` by hand for the real set, then pass and record what that names and commit the ledger with the change, per the carried-unit row above. Where the branch targets something the hook does not measure, no pass clears it, so the gate cannot judge that branch at all and the bypass is its answer, as in the row below |
| The recorded pass was run against X and this check measured Y, printed under the missing-pass headline | The hook reads `develop` and nothing else, so a branch based elsewhere is measured against `develop` whatever the pass targeted, and the engine deliberately prints no record command, since the one it would print records a pass over a diff nobody read | One more pass against the branch this work actually targets, where it does target the measured one. Where it does not, the gate cannot judge the branch at all and the bypass is its answer |

This table is the fleet's one enumeration of these, and every other surface states the principle and routes here rather than listing the shapes. That is deliberate: every review round that added a shape also left a restatement of it somewhere else, and keeping one table is what stops the next round doing the same.

## Mechanics Live Elsewhere

- Review criteria: `code-review`.
- Delegation shape and model-tier discipline: `AGENTS.md` "Context and Delegation Discipline".
- Branch base rule (`develop` unless the task is explicitly `main`-only): `repo-worktree`.
- Finding disposition once a pull request exists, the Merge Gate, `scripts/pr_review.py`: `pr-review-conduct`, `drive-pr`.
- The receipt's key, its backends, and the three-valued exit contract a capture point folds: `scripts/README.md` "`local_review.py`".
- The unit model, the coverage ledger, and the burn-down report: `scripts/README.md` "`canonical_review.py`".
