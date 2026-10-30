---
name: local-strict-review
description: >-
  Runs a read-only, adversarial review pass against this branch's current diff against its
  target branch, full file context included, at the `frontier` model tier, before a unit of work
  is pushed toward a pull request or claimed done. Use this whenever staged, committed, or
  untracked work is about to be pushed on a PR-bound branch, and whenever
  `agent-conduct`'s "about to claim work is done, verified, green, or fixed" trigger fires for
  PR-bound work. Triggers even when the change looks small or the same session already judged its
  own diff ready, because a self-review pass judging its own diff inherits its own blind spots,
  the exact gap this skill exists to close before a PR-hosted reviewer closes it instead. Reuses
  `fleet-code-review`'s "Review the Change" criteria rather than restating them, and owns only this
  local, pre-PR moment. Once a pull request exists, `pr-review-conduct` and `drive-pr` own
  triaging and disposing of what a PR-hosted reviewer finds. Also triggers whenever the periodic
  canonical sweep is worked. That sweep names the carried units whose text has moved past the pass
  that read them. It also names a bounded slice of those nothing has read at all. This skill
  triggers there because the content a sweep names reaches a reviewer whole only when a repository carries it
  for the first time. It also triggers there because reading each named unit's whole text
  moves that first-carry read into the repository that can act on its findings. Editing such content owes no pass of its
  own, so a change that moves a unit pushes and merges like any other.
---

# Local Strict Review

## Why This Exists

A coding agent that finishes a unit of work, judges it ready, and opens the pull request is judging its own diff. It does so with the model, and often the blind spots, that wrote it. CodeRabbit, Qodo, and Copilot routinely find real defects that a local pass missed. Each round costs review latency and, for a rate-limited reviewer, shared account-wide quota. A local, full-file-context adversarial pass before the pull request exists catches the same class of defect for a fixed, smaller cost. That is the same reasoning that already runs local lint before a push instead of waiting for CI.

## What It Does

Dispatches one read-only subagent against this branch's full diff since it forked from its target branch. Resolve `<target>` once, `develop` unless `repo-worktree`'s base-branch rule put this branch on `main` instead. Then fetch it, `git fetch origin <target>`, and diff against the merge-base, `git diff "$(git merge-base refs/remotes/origin/<target> HEAD)"`. The target is named by its full remote-tracking ref because git resolves the short `origin/<target>` against local branches first. So a local branch of that name would define the diff the reviewer reads. In that case the engine, which resolves the full ref, would key its receipt on another diff. Stop and report a failed fetch rather than running the merge-base or diff commands anyway. That is because an existing `refs/remotes/origin/<target>` ref can still resolve after a failed fetch. Reviewing against it silently trades the current target for a stale one. Use the same resolved `<target>` in every command below, never a literal `develop` alongside it. Naming the target branch explicitly matters. The branch's own `@{u}` tracking ref points at the branch's own remote once it has been pushed, not at the branch it targets. So anchoring there silently narrows a later run to only the diff since the last push instead of the full accumulated diff. That merge-base diff covers every commit already on the branch plus whatever is currently staged or unstaged. So it never reviews only the latest increment, at any of the moments this skill is invoked from. An empty diff is not the same as nothing to review, and it is never the signal to stop. That is because it reports no untracked file at all. It also reports nothing for content a commit carries that the working tree has since put back. The untracked-file list below covers the first of those. The second is why the diff pass commits before reviewing. That is because a removal or a restore that is committed leaves no net content to miss. The sweep's own passes below run against uncommitted content instead, on the change that carries their ledger. The second is also why the engine reads HEAD rather than this diff. Its change set comes from the merge base against HEAD, the index and the working tree, so the two answer different questions. A fresh review of the full accumulated diff is what catches what per-push review misses, the exact evidence this skill exists to act on.

`git diff` never reports a path `git add` has not touched, so a newly created file sitting untracked would otherwise go unread. List it explicitly, `git ls-files --others --exclude-standard`, and read each result in full alongside the diff, the same as any other file the diff touches.

The subagent reads the full content of every file the diff and the untracked-file list touch, not just the hunks. That is because cross-file and whole-file context is exactly what incremental review misses. It reports findings only. It never fixes, stages, or commits anything.

Review criteria are `fleet-code-review`'s "Review the Change" section, reused rather than restated here. Before any other reviewer sees the diff, this pass also calls out these traps: unguarded type coercions, TOCTOU/race conditions, and platform-specific behavior differences. Each is called out only where the code's actual deployment can produce it, per `GOVERNANCE.md` "Trust Boundaries and Hardening Effort". The brief also asks the reviewer to search for an existing helper doing the job of each new one, per `GOVERNANCE.md` "Design Before Code". `fleet-code-review`'s separate "Publish Every Finding" section does not apply here. This skill's report contract below replaces it, since there is no PR to comment on and no coverage marker to close a review with.

## Running It

Follow `AGENTS.md` "Context and Delegation Discipline"'s subagent briefing shape:

```text
Task: adversarial review of this branch's diff against its merge-base with its target branch,
  read full surrounding files where the diff hunks alone do not give enough context.
  Run `python3 <hub-checkout>/scripts/prose_lint.py --diff "$(git merge-base refs/remotes/origin/<target> HEAD)"`.
  Add `--allow-comments` where the change carries, or will open carrying, the `comments` label.
  Then run the same command again with `--check sentence-length` added, and report each output as findings.
Worktree: <worktree>, the checkout holding the branch.
  Start every command with `cd <worktree> &&`, since a harness can reset the working directory between calls.
  Read each listed file at its path under `<worktree>`.
Paths: the files `git diff --name-only "$(git merge-base refs/remotes/origin/<target> HEAD)"` and
  `git ls-files --others --exclude-standard` list, mandatory floor. Reading a specific
  unchanged caller or consumer beyond that list is in bounds only where a candidate finding's
  proof actually depends on it, per fleet-code-review's own "follow data and control flow beyond the
  edited lines" instruction below, never as an open-ended exploration.
Rules that bind this task: quote `fleet-code-review`'s "Review the Change" section into the prompt,
  plus flag unguarded type coercions, TOCTOU/race conditions, and platform-specific behavior
  differences explicitly, each only where the code's actual deployment can produce it.
  For each function or helper the diff adds, search this repository and the hub for one doing
  the same job, and report a consolidation finding where one exists. Where the diff itself adds
  hardening the threat model rules out, report that code as an over-hardening finding. Never
  ask for such hardening.
  Do not quote "Publish Every Finding", this task's report contract is the Return line below,
  not a PR comment or a coverage marker.
Return: one finding per line, file:line, the concrete failure scenario, or for a design finding
  what fleet-code-review's item 7 names, no severity theater.
Bounds: read-only. No edit, no stage, no commit, no push, no PR-hosted write of any kind.
  Beyond what "Review the Change" allows, run only the exact commands, with their arguments,
  that Task above names.
<AGENTS.md's own unresolved-rule closing line, quoted verbatim from "Context and Delegation Discipline", not restated here>
```

Before dispatching, grep the tree for other statements of each rule the diff adds or changes. Before dispatching, also add each file holding one to the `Paths:` floor. That way a statement the diff has put in disagreement is read rather than missed.

**Model tier:** `frontier`, the tier "Model Tiers" below names for adversarial review, which this pass is. Where `frontier` is not reachable, run the pass on the strongest tier below it that is, even the one that authored the change. A second, adversarially-prompted look still catches what the authoring pass's own "looks ready" judgment did not.

Reachable means the tier this session can name when it dispatches the reviewer, rather than the tier this session is itself running on. A worker dispatched by an orchestrator is the ordinary case of a session deliberately tiered down for execution work. Such a session names `frontier` for the reviewer where its harness lets it. That is because tiering down the author is the reason the reviewer must not follow it down. What a given harness and account actually permit varies, so treat this as the tier to ask for rather than one to assume. Where a dispatch exposes no way to name a tier, take what it gives and run the pass, for the reason the fallback above states. A seat that cannot dispatch a subagent hands the pass to whoever dispatched it. The seat commits and runs `python3 <hub-checkout>/scripts/local_review.py status --target <target>` in its worktree. It then hands back its worktree path, target, and `contentDigest`. The dispatcher fills the brief's `<worktree>` slot and runs the reviewer. It then runs the record command from "Recording the Pass" in that worktree, with `--reviewer agent-skill --target <target> --expect-digest <digest>`. It returns the findings. Each fix commit repeats this cycle, and the seat pushes only when a record matches its content. The brief dispatching the seat states whether the dispatcher can continue that seat. Where the dispatcher cannot, or under `unattended-handoff`, the seat stops, reports, and does not push. Where nobody dispatched the seat, it reports to the maintainer that it could not run the pass, and does not push. Either way the push waits rather than the pass being quietly skipped. The headless `run --backend` route under "Recording the Pass" is not the substitute. That is because it runs a vendor CLI against its own review, which never carries the brief above. So it satisfies the rule this section states only where that separate route is what a capture point asked for.

## Recording the Pass

`scripts/local_review.py` is what makes this rule checkable rather than something each session has to remember. For the pass above, the engine only records that it happened, keyed on the content the reviewer actually saw. Its `run --backend <name>` subcommand is the separate case where a headless backend performs the review and records its own count. That receipt is what a capture point reads, the hub's own `.husky/pre-push` hook being the only one today. A repository has none unless it adds one, since no manifest entry carries it. The receipt lives in its worktree's git directory, and removing the worktree deletes it. The record a later reader needs is therefore the attestation `pr-review-conduct` "Expected review loop" publishes after every push a recorded pass covers.

Commit first, then read the digest, then dispatch the subagent, then hand that same value back. Nothing may change the tree between the read and the record. Staging a modified tracked file is such a change, moving the digest although the content did not. A commit can move it too, since HEAD decides which paths are in the change set at all. Reading after the commit is what leaves neither of them between the read and the record.

```sh
engine="<hub-checkout>/scripts/local_review.py"   # in the hub itself, scripts/local_review.py
python3 "$engine" status --target '<target>'      # JSON, take contentDigest
# run the pass above, then:
python3 "$engine" record --reviewer agent-skill --target '<target>' --expect-digest '<digest>' --findings '<count>'
```

Every subcommand here, `run --backend <name>` included, runs with the repository under review as the working directory. That holds whichever repository that is. The engine takes no `--repo` and reads whichever repository it is run in. So the path names where the script lives and the working directory names what it measures.

`<target>` is the same branch "What It Does" resolved for the review, passed to each command. Leaving it off defaults each to `develop`. On a `main`-based branch, that computes the digest against a merge base the reviewer never read. So the receipt would attest to a change set nobody looked at. A receipt is only valid against the target it names, so the two have to agree.

`--expect-digest` is required rather than optional, and binding it to the earlier read is the whole point. A format-on-save or a hook autofix between the review and the record would otherwise be stamped as reviewed by a pass that never saw it. A refusal there is the content having moved, so the answer is another pass over the current content rather than another read of the digest.

Record the pass whatever it found, including nothing. `--findings` is optional and records how many findings the pass raised. The key covers the net content the branch introduces against its target rather than the commit series. So a rebase that leaves the tree alone keeps the receipt valid while the fork point holds, and changing one byte invalidates it. The key holds that fork point too, so rebasing onto a target that has moved retires the receipt although no file changed.

**Why the commit comes first**, rather than being an ordering that could equally run the other way. A push delivers the commit, and the hook's tree check refuses a push whose tracked content differs from HEAD. So the record has to describe what HEAD holds. A commit that leaves the tree alone usually does not move the receipt's key, so diligence done before it still describes the same content. A commit putting a path back to its base state drops the path from the change set and does move the key. The order matters anyway, for these reasons. Staging a modified tracked file moves the key even though its content did not change. And a commit made after the record can carry content the pass never read. Reviewing earlier than this is still worth doing as ordinary diligence, and it does not substitute for the recorded pass. This is because the digest read and the record bracket a window in which the tree holds still. A commit inside that window ends it.

The engine is hub-hosted per `GOVERNANCE.md` "Hub-Hosted Tooling". So a downstream repository reaches a hub checkout's copy rather than carrying one. That is what the path above is for.

## The Carried-Content Sweep

A second pass under the same rule, run in the repository that authors canonical content other repositories carry, which in this fleet is the hub. `GOVERNANCE.md` "Verification Discipline" states the rule and why the ordering it corrects is a defect, and is not restated here. What it requires of a run is below.

**No change owes this pass.** Editing a carried unit refuses no push and fails no pull request. The passes are worked instead from the sweep's own issue. A scheduled workflow in the authoring repository files that issue with the units it asks for this round. Working that issue is the moment this section is for. What it produces is an ordinary pull request, carrying the ledger and whatever the passes had you fix, driven the ordinary way.

**The unit is what a reviewer reads whole.** `spec/files.json` rather than the document decides which, down to which files carry units at all. `canonical_review.py list` names the whole set and is the authority on it. So the rules are not paraphrased here, where a paraphrase can only drift from them. In the ordinary case a unit is one level-two section of a carried Markdown canonical. `sweep` names each unit it wants exactly as `record` takes it. The pass reads that unit's whole current text rather than the diff that moved it, because reproducing the carrier's read is the entire point. It also does so because a diff with surrounding context is a different read the pass above has already done.

Run it at the same model tier and in the same delegation shape as the pass above. The brief, the engine, and its flags each differ, and each is below.

```text
Task: adversarial review of one canonical unit, read as a repository carrying it for the first
  time reads it, whole, knowing nothing about what this branch changed in it.
Paths: <the unit key, substituted here>, read in full out of the file that key names.
  Read the whole unit, never a diff of it.
Rules that bind this task: <quote fleet-code-review's "Review the Change" section>, and judge the text
  as a reader who has only this unit: a claim it makes about a tool, a path, a command, or
  another rule is a defect wherever that claim is false, stale, or unverifiable from the unit
  itself, and an instruction it gives is a defect wherever following it literally fails.
Return: one finding per line, the sentence quoted, and what is wrong with it. No severity theater.
Bounds: read-only. Report a rule that looks incomplete rather than guessing at what it meant.
<AGENTS.md's own unresolved-rule closing line, quoted verbatim from "Context and Delegation Discipline", not restated here>
```

```sh
uv run --no-project --script scripts/canonical_review.py sweep     # the units it asks for this round, with their digests
# exit 1 where it named any, which is the sweep working rather than the command failing
# run the pass above over each unit it named, then, per unit:
uv run --no-project --script scripts/canonical_review.py record --reviewer agent-skill --target develop --findings '<count>' --unit '<key>=<digest>'
```

These run in the authoring repository itself, the only repository this pass ever runs in. So the engine path is the plain one, and no downstream side needs the `<hub-checkout>/` form the pass above shows for its own reach. Each resolves the repository from the working directory rather than from where the script sits. So the directory a command runs in is what decides which tree it measures. The unit model and the manifest reader come from the checkout the script itself lives in. Running one checkout's copy against another's tree therefore measures the second tree by the first's rules. So run them in the tree being measured. `record` additionally stamps each pass with the merge-base against `--target`, which is provenance rather than coverage. The line above names it rather than leaning on the default, since a sweep's own branch is based on `develop`. A `main`-based branch passes `--target main` instead.

The digest is bound to the read for the same reason `--expect-digest` is above. Recording a unit by name alone would stamp whatever the file holds at record time. So an edit between the review and the record would be attested to by a reviewer who never saw it. Record each unit whatever the pass found, including nothing. Fixing a finding is itself such an edit, so `record` then refuses the digest you were holding. That refusal is the content having moved rather than a fault in the record. The answer is a read of the unit's new text, which is what a carrier will actually receive, recorded at its new digest.

**The ledger this writes is tracked content, so the commit has to carry it**, where the receipt the pass above writes never can be. Record each unit, commit the ledger together with whatever the passes had you fix. Then read the digest, run the diff pass over that commit, record its receipt, and push. That is why the two records sit on opposite sides of the one commit.

**A unit nothing has read here yet reaches the list a slice at a time.** `sweep` names every unit whose text has moved past a pass, and beside them a bounded number of the never-read ones. The never-read ones are ordered by how recently the file each sits in was last committed. So recently authored content comes ahead of text that has sat unread for months rather than waiting behind the whole backlog. The key is the file rather than the unit, so committing to a file lifts every unread unit in it. A unit becomes carried without being lifted wherever the manifest is widened on its own, the manifest being a file of its own. Declaring a section in the same commit that writes it lifts it like any other. `canonical_review.py report` renders that backlog in full, and working more of it off than the sweep asked for is worthwhile and is its own change.

## Disposing of Findings

Each bullet is a rule down to its `Why:` line, which is rationale rather than rule. So a stale rationale is a cleanup rather than a defect.

- **Every finding ends in one of the outcomes that `pr-review-conduct` "Every finding ends in a listed outcome" enumerates, reached here with no thread to reply in.**
  - `Why:` a local finding and a PR-hosted one deserve the same dispositions. One home for the list is what stops two copies of it drifting apart.
- **The agent disposing of a pass's findings classes each one `style`, `introduced`, or `pre-existing`, in that order.** `style` is a preference between defensible forms. `introduced` is any other finding on text this change wrote, rewrote, or removed, or on text this change should have written. It is also any other finding on a precondition this change left false elsewhere. It is also any other finding load-bearing for a decision this change puts to the maintainer. `pre-existing` is every other finding.
  - `Why:` the reviewer is asked to omit preferences and returns some anyway. And `style` is classed first so that a preference on text this change wrote is not owed a fix.
- **Another round is owed only while an `introduced` finding is open.** Unless evidence disproves it, an `introduced` finding is dealt with in one of these ways. It is fixed within the budget below. It is handed to the pull request once that budget is spent. Or it is escalated where `pr-review-conduct` "Escalate to the maintainer when" says so. A `pre-existing` one blocks nothing, and neither does a `style` one on text this change did not touch. `pr-review-conduct` "Every finding ends in a listed outcome" outcome 4 says how each is recorded. A `style` one on text this change wrote is declined with evidence per that section. The evidence is `fleet-code-review` "Review the Change"'s own rule to omit preferences.
  - `Why:` a finding count over prose never reaches zero, so a loop closing on "did it find anything" does not close. One closing on the false claim, the unfollowable instruction, or the wrong behavior this change put there does close.
- **Two rounds of edits answer a pass, one budget per push and one per sweep issue.** Where an `introduced` finding is still open after the second round, editing stops. The pull request's body lists each remaining finding with its class. Its review loop then gives each one an outcome under `pr-review-conduct` before it merges, whether or not a hosted reviewer raises it again. A spent budget is not an escalation. A remaining finding goes to the maintainer only where `pr-review-conduct` "Escalate to the maintainer when" names its case.
  - `Why:` past the second round nearly every finding is against text the previous round's fix wrote. So the rounds are producing the defects they find rather than removing them.
- **The pass is mandatory, and the count it records gates nothing.** A pass is recorded whatever it raised, so the record attests that a review ran rather than that the content is clean.
  - `Why:` a gate reading the count would make a pass raising nothing the cheapest way through it, the opposite of what recording one is for.

## When to Run It

- Before the first push toward a pull request, the push that opens it in `drive-pr` "The Drive Loop" and in `pr-review-conduct` "Expected review loop".
- Before pushing a fix for a reviewer finding, because the same self-review blind spot applies to a fix as to the original diff. Such a fix is the fix outcome of `pr-review-conduct` "Every finding ends in a listed outcome", which `drive-pr` "Disposing of Every Finding" carries.
- Whenever `agent-conduct`'s "about to claim work is done, verified, green, or fixed" trigger fires for work that will become, or already is, a pull request.
- When the canonical sweep's issue is worked, over each unit it names, per "The Carried-Content Sweep" above. Editing such content is not itself one of these moments, and nothing refuses a push over it.

In the hub, `.husky/pre-push` checks the receipt at the push itself. So the moments above are where the pass is run rather than the only place it is noticed. A blocked push usually means it was skipped. That hook is the hub's own, and a repository carrying this Skill has none until one is carried to it. That is what makes the moments above the layer that actually binds everywhere. The hook is a backstop under this skill and not a replacement for it. It fires only in a clone that enabled `core.hooksPath`. It says nothing about a repository that carries no such hook. And it is bypassable by design, `--no-verify` being the documented route for a genuine pickle rather than for a diff nobody read. That route is not open in every seat. A Claude Code session running the fleet's agent-safety hook has the flag denied unconditionally. So where the rows below say a bypass is the answer, the answer in that seat is to report the state. It is also to hand the push to the maintainer rather than to force it. No capture point anywhere gates the carried-content sweep. The hub's `.github/actions/validate` composite action renders the coverage burn-down into every run's job summary and fails no pull request over what that rendering shows. That is the sweep being periodic rather than enforced at a push. That step does still fail where the engine could not read what it needs at all. That is a boundary rather than a verdict about coverage.

**Read the refusal itself, which names its own case.** Some of the rows below are cleared by running a pass and some are cleared by nothing of the kind. Each row says which, so no count of either is kept here to go stale against the table. Some the hook decides before the engine runs, so there is no engine message under them. The rows say where each one's detail comes from.

| The refusal says | What it means | What clears it |
| --- | --- | --- |
| No local review covers this branch's current content | The ordinary missing pass: no recorded receipt covers what this push delivers, either because none was recorded or because the content moved after one was | One pass over the branch's whole diff, recorded per "Recording the Pass" above |
| Tracked content differs from HEAD | A push delivers HEAD while a receipt covers the index and working tree, so the receipt does not describe this push. The hook prints the same headline for an unresolved merge and for a `git update-index --refresh` that exited above 1, naming each on its own line | Commit what is being pushed, then the pass, then the record. Where the change also carries the canonical ledger, record those passes before that commit, per "The Carried-Content Sweep" above, since a ledger written after it leaves the tree differing from HEAD again. Resolve the merge first where the hook names one, and run `git status` first where it names the refresh, since the content may not differ at all |
| The commit is not this worktree's HEAD | Any pushed branch ref carrying an object id that is neither this worktree's HEAD nor the all-zero id of a delete, which a push from a checkout sitting elsewhere reaches and so does a multi-ref push such as `git push --all` | Push one branch, the one this worktree holds. Where another branch is the one wanted, check it out in its own worktree first, per `repo-worktree` |
| Any wording saying the gate did not or could not run | An execution boundary rather than a verdict, which blocks because a gate that waves a push through when it could not run has stopped gating. The cause is named in that same message or in the engine error printed above it, and it is a missing Python interpreter, an unresolvable target, an unreadable receipt, a git command that failed, or any unexpected failure | Whatever the message names, most often installing an interpreter per `docs/host-setup.md` or fetching the target branch. Never another pass |
| The recorded pass was run against X and this check measured Y, printed under the missing-pass headline | The hook reads `develop` and nothing else, so a branch based elsewhere is measured against `develop` whatever the pass targeted, and the engine deliberately prints no record command, since the one it would print records a pass over a diff nobody read | One more pass against the branch this work actually targets, where it does target the measured one. Where it does not, the gate cannot judge the branch at all and the bypass is its answer |

This table is the fleet's one enumeration of these, and every other surface states the principle and routes here rather than listing the shapes. That is deliberate. Every review round that added a shape also left a restatement of it somewhere else. Keeping one table is what stops the next round doing the same.

## Model Tiers

`AGENTS.md` "Model Tiers" keeps the tier rule, carried here whole as a generated include. A tier
this skill names then resolves to its model even where a carried `AGENTS.md` predates the table.

<!-- include: AGENTS.md > Model Tiers -->

A rule, a Skill, or a brief names a model tier, never a model. A model release is then an edit to the table below and to the hub's `spec/model-tiers.json`. That file states the same mapping as data, and the hub's spec gate holds the two equal. Reasoning effort is a separate axis stated beside the tier, such as `bulk` at low effort.

- **`frontier`** is the fleet's default reasoning tier, for orchestrating, rule text, design, spec logic, and adversarial review. It is not the most capable model available, which is `highest`.
- **`standard`** is ordinary implementation and driving a pull request. Judging a review finding is never mechanical, however often it repeats, so it runs at `standard` or above. A `standard` driver's evidence-backed decline of a finding raised on its pull request stands, since that pull request's reviewers and its merge gate check it.
- **`bulk`** is the cheapest model that does the work correctly. It is for high fan-out mechanical work, such as an extraction or a status check.
- **`highest`** is the most capable model available, and only the maintainer selects it. The maintainer chooses it for the session itself, or allows it for the session's subagents at launch. The host's agent-safety kit reads that launch setting, `FLEET_ALLOW_HIGHEST=1` on Claude Code. Naming the tier in a prompt does not set it. A maintainer naming it partway through a session restarts the session with it allowed, and an agent never sets it. Without that setting, the kit refuses a dispatch that names it. A dispatch naming no model runs at its agent definition's model where one is set, and otherwise at the session's own. A fork always runs at the session's model, whatever it names. So a session launched on `highest` names a tier on every dispatch meant to run below it, and uses no fork for such work. A session never reaches for it because a task looks hard.

| Tool | `highest` | `frontier` | `standard` | `bulk` |
| --- | --- | --- | --- | --- |
| Claude Code | `fable` | `opus` | `sonnet` | `haiku` |

Each cell is the name that tool's subagent dispatch takes, which follows the latest model of its family. A dispatch on a tool with no row here, or on one that cannot name a model per subagent, names no model.

- **A tier resolves only through this table.** A word such as "frontier", "strongest", or "most capable" never resolves to `highest`. The industry's "frontier model" is the opposite of what this tier means here. A session that cannot see this table names no model rather than guessing one.
- **An orchestrating seat runs at `frontier` and names a tier on every dispatch.** It defaults a subagent to `frontier`, and picks `standard` or `bulk` deliberately for execution or mechanical work.

<!-- /include -->

## Mechanics Live Elsewhere

- Review criteria: `fleet-code-review`.
- Delegation shape and model-tier discipline: `AGENTS.md` "Context and Delegation Discipline".
- Branch base rule (`develop` unless the task is explicitly `main`-only): `repo-worktree`.
- Finding disposition once a pull request exists, the Merge Gate, `scripts/pr_review.py`: `pr-review-conduct`, `drive-pr`.
- The receipt's key, its backends, and the three-valued exit contract a capture point folds: `scripts/README.md` "`local_review.py`".
- The unit model, the coverage ledger, and the burn-down report: `scripts/README.md` "`canonical_review.py`".
