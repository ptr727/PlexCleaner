---
name: backlog-burndown
description: >-
  Burns a ptr727/ProjectTemplate fleet repository's open-issue backlog down by rounds: rank the
  open issues, group them so no two groups touch the same file, dispatch one subagent per group to
  drive its own feature -> develop pull request to merge, open at most one develop -> main
  promotion pull request per round for the maintainer to merge, then re-rank and go again,
  because every review round files new issues that change what the next round should pick. Use
  this whenever asked to work the backlog, burn the backlog down, clear the open issues, resolve
  or cull the backlog, or run issues in parallel until they are gone, and whenever the ask is a
  standing one rather than a single named issue. Triggers even when the backlog looks small enough
  to work by hand, because the failure it exists to prevent is two agents editing the same
  prose-heavy Markdown file in the same round, which surfaces as a merge conflict long after both
  branches are already deep in review. Drives one repository, the one the session is in, never a
  fleet-wide sweep. Ends when a re-rank finds nothing left it can act on, and never merges main,
  which stays the maintainer's own step through merge-and-release.
---

# Backlog Burndown

## Why This Exists

Asking for one issue to be fixed is `drive-pr`'s job and needs no skill above it. Asking for a
whole backlog to be worked down is a different problem, and three things about it are not obvious.
Parallelism is bounded by file overlap rather than by agent count, so the grouping decides the
throughput. The backlog is not a fixed list, since every review round files deferral issues that
belong in the next round's ranking, so a plan made once is stale by its second round. And a
prose-heavy repository conflicts on content rather than on syntax, so two agents rewording the
same section produce a conflict no tool resolves and no reviewer catches early.

## The Two Seats

Everything below turns on which seat is acting, so both are named once here.

- **The orchestrator** is the session this skill runs in. It ranks, groups, dispatches, and drives
  the promotion pull request. It opens no feature branch and fixes no issue itself, which is what
  keeps it out of every worker's files. It does write: it comments on issues, it drives and
  amends the promotion pull request, and it owns worktree and branch cleanup, which "Dispatching a
  Worker" states in full.
- **A worker** is one dispatched subagent holding one group, one worktree, and one feature branch,
  the dispatched task `AGENTS.md` "Session Scope" describes. It drives its own pull request into
  develop and ends there.

## Scope

One repository, the one the session is in, resolved from its own `origin`. A run staying inside the
one repository it was invoked for is narrower than `GOVERNANCE.md` "Repository Boundaries and Write
Safety" requires, deliberately. Reading another repository's issues is governed there and not here,
working them is out of this skill's scope, and a fleet-wide backlog sweep is a different request.

## What Invoking This Skill Authorizes

- Naming this skill is the maintainer's explicit go-ahead for the feature -> develop squash merges
  this run performs, in every round of it. A per-round merge question would idle every agent at
  every boundary, which is the thing this skill exists to avoid.
- **The grant is bounded by the session it was named in.** A run interrupted and resumed in a new
  session needs the skill named again, which costs one sentence and is the difference between a
  grant and a mode. A grant read back from a note is one nobody gave.
- The grant does not weaken the `pr-review-conduct` Merge Gate. It answers that gate's explicit-permission item for
  this run's feature -> develop merges and nothing else, so a pull request with one open finding
  still does not merge.
- It is never authorization to merge a develop -> main promotion pull request, to dispatch a
  release, to close an issue on judgment, or to touch another repository. Each stays the
  maintainer's, and merging a promotion pull request is `merge-and-release`, invoked on its own.

## The Round

A round is the unit. Each one runs these steps in order.

1. **Rank** every open issue, per "Ranking".
2. **Group** the top of that ranking, per "Grouping and File Claims".
3. **Verify** each group's predicted file set against everything in flight before dispatching
   anything. A group whose files are already claimed waits for the next round.
4. **Dispatch** at most four workers, one per group, per "Dispatching a Worker".
5. **Collect** each worker's outcome: merged to develop, stopped on a question only the
   maintainer can answer, parked behind another group's file claim, or abandoned, which is what
   the adjudication in "Grouping and File Claims" and a confirmed-gone worker both produce. Bound
   this wait per "Bounding the Wait on a Worker".
6. **Clean up** the worktrees, local branches, and merged remote branches of every group that has
   finished or been abandoned, per "Dispatching a Worker".
7. **Promote**, per "The Promotion Boundary".
8. **Re-rank from scratch**, and note that the next round prepares under the freeze "The Promotion
   Boundary" describes whenever a promotion pull request is still waiting on the maintainer, so it
   ranks, groups, and verifies claims, and dispatches nothing until that merge lands. Do not carry
   the previous round's ranking forward. The deferral issues this round's reviews filed are now
   open issues with a claim on the next round's attention, and an issue that ranked low last round
   can rank high once a sibling fix lands.

## Ranking

Where the repository carries no priority label, and the hub does not, the ranking is the
orchestrator's judgment against stated criteria rather than a field read off the issue. Where a
repository does carry one, that label is the first input and these criteria order what it leaves
tied. Write the ranking, and the reason for the top of it, into the report this skill makes at
each round boundary, per "Ending the Run".

Rank on these, highest first where they conflict:

- **It blocks other work.** An issue whose fix changes a rule, a gate, or a shared contract that
  other issues' fixes must then obey is worth doing before them, not after.
- **It is a correctness or safety defect** in something that runs, over an improvement to
  something that reads.
- **It is a root cause rather than a leaf.** A parent issue grouping several filed symptoms is
  worth more than any one of its children, and fixing it may close them.
- **It is new.** An issue filed by a recent review round is evidence of something the current
  content actually got wrong, and it is the freshest context anyone has on it.
- **It is small and self-contained**, as a tie-break only. Size breaks a tie between two issues of
  equal value, and it never promotes a trivial issue over a real defect.

An issue that asks a question rather than states a defect is not ranked and is never guessed at.
It has no group, no worker, and no claim, so nothing in "Raising a Blocked Question" applies to it
except how the question travels. It goes to the maintainer at the end of ranking, per
`GOVERNANCE.md` "Communicating with the User", batched with any other question the run is sending
at that moment and in a prompt of its own otherwise, rather than waiting for a stop that may not
come. It stays unranked until answered.

## Grouping and File Claims

Group so that **no file is claimed by two live groups at once.** This is the rule the skill exists
for, and it binds harder than any throughput target.

- **Group by the files a fix will touch**, not by the issues' subject matter. Two issues that read
  as unrelated but both edit a shared governance file are one group. Two issues that read as near
  duplicates but touch different files are two groups.
- **Genuine duplicates are one group.** Never close an issue during triage on the orchestrator's
  own judgment. Comment to cross-link the pair, and let the fix close both.
- **Closing keywords go on the promotion pull request**, not the feature pull request, per
  `operational-vs-release-workflow`. A feature pull request merging into develop fires no
  auto-close, so a `Fixes #N` line there closes nothing. **The feature pull request body instead
  carries a line reading `Closes on promotion: #N`**, listing every issue that pull request
  actually fixes and nothing it merely mentions, which is the line the promotion body is assembled
  from, per "The Promotion Boundary". Without that line nothing records which issues a merged pull
  request closes, since the closing keyword is deliberately absent and a body's other issue
  references are not the same set.
- **Predict each group's file set** by reading the issues, not by guessing from their titles.
- **Record every claim on the issue itself**, as a comment naming the predicted file set **and the
  branch that holds it**, before dispatching. The branch name is what lets a later session walk
  from a worktree it found back to the claim explaining it, which is the direction the next bullet
  actually travels. Working notes do not survive the session, so a claim living only in them is
  invisible to the round that has to respect it, and recording it on the issue is what makes the
  next bullet a read of durable state rather than of the orchestrator's memory.
- **Verify the prediction before dispatching**, against everything in flight, which is wider than
  this round: the files changed by every open **feature** pull request on this repository (`gh pr
  diff <number> --name-only` per open pull request), and the claim comments of every group still
  holding a branch, parked groups from earlier rounds included. `git worktree list` reports the
  registered worktrees and the branch checked out in each, which is not the same as every branch
  that exists, so pair it with `git branch -r`, after `git fetch --prune origin`, for one that was
  pushed and whose worktree is already gone, and with `git branch` for one that was never pushed
  and whose worktree is already gone. That third read is not optional here: the worktree-only
  disposition retires a tree and leaves its branch standing, so this skill produces exactly that
  state, and a local branch holding commits no remote has is invisible to both other reads. Prune
  rather than plain fetch because `--prune` is what drops a remote-tracking ref whose branch is
  gone from the remote, deleted there by another session or through the web interface, and a
  plain fetch leaves that ref in `git branch -r` to defer valid groups forever. Stop and report a
  failed fetch rather than reading `git branch -r` anyway, per `GOVERNANCE.md` "Verification
  Discipline" on what a local clone answers for: here the scan would miss a branch pushed since
  the last successful fetch and keep one deleted since it. The round
  stops there and reports, rather than dispatching against a stale answer, and stopping rather
  than deferring is what the cleanup and promotion steps need too, since both read the same
  remote.
  Those three enumerate the branches, with one gap: a branch in the standalone clone `repo-worktree` allows as a fallback is
  reached only once it is pushed, since `git branch -r` inventories the remote rather than this
  repository's checkouts. The claim comments are what say which files each branch holds, since a branch name says nothing about a file set, an unpushed branch has no
  pull request diff to read, and no diff of any branch reports the predicted set a claim records
  before the work is committed. **Exclude the open promotion pull request from that enumeration.**
  Its diff is all of `origin/main..origin/develop`, so counting it claims nearly every file any
  earlier round touched, and a round run during the freeze would defer every group it formed. A
  predicted claim that collides with a real one is a group deferred to the next round, never one
  dispatched hoping the overlap stays small.
- **A branch no claim comment covers still has to yield a file set.** The maintainer's own
  worktree and a hand-driven task's branch are both enumerated above and neither carries a claim
  comment, so reading only the comments records them as holding nothing, which is the collision
  this section exists to prevent rather than the absence of one. Read the branch itself instead:
  `git diff --name-only origin/develop...<branch>` for what it has committed, and, for a
  registered worktree, `git -C <worktree> status --porcelain` for what it holds uncommitted. Where
  neither read is available the set is unknown rather than empty, and an unknown set collides with
  every group, so ask the maintainer what that branch holds per "Raising a Blocked Question".
- **Re-verify when a worker reports that its real file set grew** beyond its claim. A worker
  needing a file another group holds stops and reports rather than editing it, and the
  orchestrator decides which group keeps the file, then tells the loser which of three things to
  do rather than leaving it to choose: narrow its change to drop that file, park until the holder
  merges, or abandon its branch and return the issue to the next round's ranking. Where the loser
  has already opened a pull request, say whether it closes or waits, since one left open on an
  abandoned branch reads to every later round as a live claim. **Update the claim comment whenever
  the adjudication changes what a group holds**, in either direction, or the durable record drifts
  from the real claim it exists to report.
- **Cap the round at four workers**, whatever the grouping allows.

## Bounding a Prose Group

A group whose files are prose-heavy Markdown bloats in a way a code group does not, and it needs
its own bound stated in the worker's brief.

- **The change stays inside the units the issue names.** Rewording an adjacent section because it
  now reads inconsistently is the next issue, filed, not this one's diff.
- **Deleting a claim beats qualifying it.** Where a review disproves something a rule leaned on,
  remove the rule that leaned on it. A narrowed qualifier is where a new false claim gets
  introduced, and it is the most common way a prose round produces the finding the following round
  then fixes.
- **The review-round budget is `local-strict-review` "Disposing of Findings"'s.** The brief names
  it and states no second one.

## Dispatching a Worker

Brief on `AGENTS.md` "Context and Delegation Discipline"'s subagent shape.

- **The worker drives its group to a develop merge**, by invoking `drive-pr` with the target
  stated as develop only. That skill owns the review loop, the finding disposition, and the merge,
  so brief the group and the bounds rather than restating the loop.
- **The worker creates its own worktree**, always, as `drive-pr`'s worktree isolation and
  `repo-worktree`'s task-start mandate already require of the task itself. No worker inherits another's worktree,
  which is why "Bounding the Wait on a Worker" either removes a dead worker's tree and its
  branch or leaves that tree untouched for the maintainer, and never passes it on.
- **The worker does no cleanup**, which is this skill's one stated override of `drive-pr`'s
  post-merge cleanup and of `repo-worktree`'s post-merge procedure. Say so in the brief, because
  a worker following either alone will clean up. The worker still performs the merge itself, and
  what the override moves is the two cleanup halves `drive-pr` runs after it, the worktree
  procedure and the verify-then-delete of the merged remote branch, **both** rather than only the
  first. "Cleanup Is the Orchestrator's" below, in this
  same section, says why and what it covers.
- **The worker runs `local-strict-review` before every push**, per `GOVERNANCE.md` "Verification
  Discipline". That pass dispatches a reviewer of its own, so a harness where a subagent cannot
  dispatch one leaves the worker unable to run it and unable to push. It reports that rather than
  pushing, and its worktree is then retired, since git refuses to attach that branch anywhere else
  while the reporting tree holds it. The branch is left standing for its own reason, that the
  commits it already carries are what the re-dispatched worker continues from. This is the
  worktree-only disposition "Cleanup Is the Orchestrator's" separates out, so a clean tree is the
  whole test. A clean
  tree is retired and the group re-dispatched to a seat that can dispatch. A dirty one is left
  exactly as it stands and the group stopped for the maintainer, as is a group for which no seat
  that can dispatch exists. That retire-and-re-dispatch case presumes the branch is reachable from
  this repository, which the standalone clone `repo-worktree` allows as a fallback breaks: a worker that never
  pushed holds its commits only in that clone, where this repository has no ref to hand a
  replacement and nothing to retire, so re-dispatching loses the work rather than continuing it.
  That group stops for the maintainer with the clone named, and no seat this skill defines resumes
  it, since a worker never inherits another's checkout and the orchestrator opens no branch and
  edits nothing. Neither the worker nor the orchestrator pushes around the missing pass.
- **The brief names the branch the worker will use**, which is what lets the claim comment record
  it before dispatch. The worker still creates its own worktree, on that named branch rather than
  one of its choosing, since a claim naming a branch nobody used points at nothing.
- **The brief requires the `Closes on promotion:` line** in the pull request body, listing exactly
  the issues this group fixes. A worker never reads this skill, and `drive-pr` does not ask for the
  line, so a brief that omits it produces a pull request nothing can derive a closing set from.
- **The brief names the files this group owns and the files it must not touch.** A subagent never
  reads this skill, so a claim it was never given is a claim it cannot respect. State this group's
  claimed set, state that any other group's file is out of bounds, and state the duty that makes
  the re-verification path work: a worker needing a file outside its claim stops and reports rather
  than editing it, and waits for the orchestrator to adjudicate.
- **The worker never merges to main** and never resolves a thread it did not actually dispose of.

### Cleanup Is the Orchestrator's

`repo-worktree`'s post-merge procedure returns the base clone to current develop before proving
the cleanup, and `operational-vs-release-workflow` states that requirement independently. Four workers doing
that concurrently mutate one shared checkout, which `GOVERNANCE.md` "Repository Boundaries and
Write Safety" forbids. A worker also cannot
finish the procedure from inside its own worktree, since removing that worktree leaves it with no
working directory in which to delete its branch.

So the whole procedure moves to the orchestrator, which runs it from the base clone at the round's
cleanup step, while no worker is live in a tree it touches. It carries the remote half of `drive-pr`'s
post-merge cleanup too, verifying the merged branch's tip against the pull request's `headRefOid` before
`git push origin --delete`, since taking that step from the worker without naming a new owner
would leave a live remote branch behind every group. It covers every group that is done with its tree,
which is the finished ones **and the abandoned ones**: a group told to abandon its branch keeps a
registered worktree until something removes it, and that worktree holds a live claim that would
collide with the very group the next round re-forms for the same issue. For a merged group the
procedure is deferred rather than changed: `repo-worktree`'s verify-before-removing and
prove-the-cleanup steps run unchanged, just later and in one seat.

**Retiring a worktree and deleting its branch are two dispositions with two tests**, and citing
one for the other is how a removal that discards nothing gets routed to the maintainer, or a
removal that discards commits gets waved through. Retiring a worktree alone, the branch left
standing, risks only what is uncommitted in it: a clean tree is the whole test, the branch's own
contents do not enter it, and a tree that is not clean is left exactly as it stands while the
group goes to the maintainer per "Raising a Blocked Question". Deleting the branch as well risks what is committed, so it carries
whichever branch check the group's state calls for, `repo-worktree`'s verify-before-removing for a
group whose pull request merged and the no-merge substitute below for one whose has not. Which
check that is matters: a squash merge never makes the feature tip an ancestor of develop, so the
substitute would fail on every merged group if it were read as covering them. Every disposition in
this skill names which of the two it is.

An abandoned group, and a dead worker's clean tree, have no merged pull request for
`repo-worktree`'s verify-before-removing step to read, so the check that step gives way to here is
what it exists to establish, that nothing unmerged is
being thrown away: confirm the branch carries no commit that is not already on develop, and that
its worktree is clean. Both hold, and the worktree and branch go the same way a merged group's do,
with no remote branch to delete where none was pushed. Either fails, and cleanup stops there and
the group goes to the maintainer per "Raising a Blocked Question", since past that point removal
discards work. A worker still live, a promotion fix included, keeps its worktree until the next
round's cleanup step.

### Choosing the Worker's Model Tier

`AGENTS.md` "Delegation" owns the model-tier rule. Read it there. This is only what it leaves to
judgment here: the tier is chosen per group rather than defaulted, because a stronger tier
produces better work up front and takes fewer review rounds to land it, which often costs less
than a cheaper worker looping. Three kinds of group are never tiered down:

- One touching **carried canonical content**, as `GOVERNANCE.md` "Verification Discipline" bounds
  it, since a wrong rule propagates to every carrier.
- One touching **anything `AGENTS.md` "Delegation" calls a design change**, however small the diff
  looks.
- One whose issues are **complex or entangled**, where the fix depends on reasoning across several
  files or on a contract not stated in the file being edited.

State the chosen tier and its reason in the round's report.

## Bounding the Wait on a Worker

`AGENTS.md` "Delegation" binds this wait as it binds any other, and this section is how the bound
is met here. A worker
reports merged, parked, or stopped. A worker that reports nothing at all is the case needing a
bound, since it is indistinguishable from a slow one and dying mid-drive is ordinary here.

The bound is a state read rather than a clock: when the other workers in the round have reported,
read the silent worker's branch and pull request directly, `git log` on that branch and
`gh pr view <branch>`, passing the branch as the positional argument that command takes, since a
bare `gh pr view` resolves the pull request of whatever branch the caller is standing on and never
the worker's. Let what they show decide. A pull request that is merged, or a branch whose work is
complete, means the worker died after doing the work and the group is finished.

Anything else needs one thing established before anything is touched: whether that worker is gone
or merely slow. No git read answers that, and the two call for opposite actions, so the answer
comes from the dispatch mechanism itself, which knows whether the subagent it started is still
running. Nothing about the worktree is acted on while the answer is "still running", however long
that is. Waiting costs a round's latency and guessing costs another task's uncommitted work.

Once the worker is confirmed gone, its worktree decides what follows. **A clean one** is cleaned up
as an abandoned group's is, per "Cleanup Is the Orchestrator's", which confirms the branch carries
no commit that is not already on develop before anything is removed. A worker that committed its
fix and then died leaves a clean tree standing over commits develop has never seen, so that check
is what separates the two. It holds, and the issue returns to the next round's ranking to be
dispatched fresh, its claim comment released with the worktree. It fails, and cleanup stops there
and the group goes to the maintainer, since past that point removal discards work. **A dirty one is left exactly as it stands**
and the group is stopped for the maintainer per "Raising a Blocked Question", naming the worktree
and what is uncommitted in it. The orchestrator does not commit that work, hand the tree to a
replacement to commit, or remove it, per `GOVERNANCE.md` "Repository Boundaries and Write Safety".
Where no other worker remains to bound the wait, the same liveness answer bounds it alone.

## Raising a Blocked Question

A group reaching a question only the maintainer can answer stops that group and nothing else.
`pr-review-conduct`'s "Escalate to the maintainer when" list is what makes a finding a question
rather than a decision.

- **The group stops, and nothing about it is disposed of.** No thread is resolved, no finding is
  answered on the orchestrator's own judgment, and no pull request merges.
- **The other groups keep driving.** One stopped group never idles the round.
- **The question travels worker to orchestrator to maintainer, and is asked when the group
  stops.** A worker escalates to whoever dispatched it, per `pr-review-conduct`, since a
  dispatched subagent is not the seat that can prompt anyone. The orchestrator is that seat, and
  it asks then and there, per `GOVERNANCE.md` "Communicating with the User". Holding the question
  for a round boundary is what that section forbids, and a boundary can be a long way off or, for
  a group blocking the promotion pull request, never arrive at all. Where several groups stop
  close together, their questions go in one prompt, which is batching without deferral.
- **The question is also written on its issue**, so it survives the session that asked it.
- **A stopped group keeps its branch and its claim**, and its worktree is left exactly as it
  stands while the question is open, since the answer may be that the work in it continues. Say in
  the prompt whether that worktree holds uncommitted work, because what becomes of it is part of
  what is being asked rather than something to settle while waiting.
- **Resuming retires that worktree first, then dispatches a fresh worker.** Git refuses to attach a
  branch already checked out somewhere, per `repo-worktree`, so a fresh worker cannot take the
  branch while the stopped tree holds it. Once the answer is in, remove that worktree if it is
  clean, or apply what the answer said about its uncommitted work and then remove it, and only then
  dispatch. This is the same retire-then-dispatch shape "Bounding the Wait on a Worker" uses, and
  no worker ever inherits another's tree.

## The Promotion Boundary

Each round ends with at most one develop -> main promotion pull request, driven to green and left
for the maintainer, so that one carries a single round rather than accumulating several.

**This section assumes the release workflow model**, where feature work reaches develop through
squash-merged pull requests and a promotion pull request carries develop to main. A repository
whose registry `workflowModel` reads `operational` reaches develop differently, per `GOVERNANCE.md`
"Operational Repositories". Confirm with the maintainer whether a promotion pull request per round
is wanted there.

That difference changes nothing about how this run's own work is read. Every worker invokes
`drive-pr` whatever the model, so this run's fixes still arrive as squash-merged feature pull
requests carrying the `Closes on promotion:` line, and the two hops still read them. What the
operational model adds is a second kind of commit in the same range, a direct push to develop that
never had a pull request, whose issues are recoverable only from the commit message itself. Read both, the pull requests for this
run's work and the commit messages for the direct pushes, since reading either alone returns a
partial set, and the range rather than this round is still what covers earlier work no promotion
has carried.

1. **Open it whenever develop is ahead of main**, which `git fetch origin` and then
   `git rev-list --count origin/main..origin/develop` answers, and this round's own outcome does
   not. Fetch first every time: a stale remote-tracking ref reports zero and the round would report
   nothing to promote while develop carries work. A round in which every group deferred or parked
   can still owe a promotion pull request, for work an earlier round landed and no promotion has
   yet carried. A count of zero is the only case with nothing to promote, and the round reports
   that instead of attempting one.
2. Drive its review loop per the promotion half of `drive-pr` "The Drive Loop", **with a
   review-round budget set before the first round**. That loop repeats until the promotion pull
   request meets every `pr-review-conduct` Merge Gate item except the maintainer's explicit
   permission to merge, and nothing in that loop terminates on its own, so when the budget is
   reached, stop and put the state to the maintainer rather than continuing to spend the run's only
   forward gear on one pull request.
3. Put the ready pull request to the maintainer, per `GOVERNANCE.md` "Communicating with the
   User", with its merge as the action asked for. The maintainer's merge is the run's clock, so one
   reported in a closing paragraph and never actually asked about stalls every round behind it.
   Do not merge it.
4. **While it waits, develop takes only what that pull request itself needs.** A finding against
   it lands as its own feature -> develop pass, and that landing moving its head is expected, since
   its head **is** develop. **That pass is dispatched as a worker like any other**, which is the
   one push the freeze permits and the reason the orchestrator still opens no branch of its own.
   `drive-pr` "The Drive Loop" sends the seat driving a promotion pull request back through its
   own feature -> develop pass for such a fix, and here that seat dispatches rather than drives it.
5. **A promotion fix outranks any file claim.** A group holding a file it needs yields, because the
   promotion pull request is what the whole run is queued behind. A holder that is merely parked
   yields by handing the file over. A holder that already pushed and has an open pull request
   yields by having that pull request wait, its branch untouched, and by the promotion fix taking
   the file, since the two must not be in flight on one file at once. Once the fix lands, that
   pull request waits untouched until the freeze lifts. Reconciling its content is the next round's
   worker's job rather than the orchestrator's, which opens no branch and edits nothing. The
   orchestrator retires that group's worktree, the branch and its pull request left standing,
   which is the worktree-only disposition "Cleanup Is the Orchestrator's" separates out and the
   retire-then-dispatch shape "Raising a Blocked Question" uses, and then dispatches a fresh
   worker on that same branch, briefed either to merge develop in to pick the fix up or to narrow
   the change to drop the file. Never rebase it,
   since its branch is already pushed and a rebase there needs what `GOVERNANCE.md` "Git and Commit
   Rules" forbids.
6. **Nothing else pushes, and nothing else is dispatched.** The promotion fix of step 4 is the one
   exception to both, and everything in this step is said of the next round's work rather than of
   it. That round's preparation is orchestrator work and continues: rank, group, and verify claims.
   Its dispatch waits, because a worker has exactly one procedure, `drive-pr`, which
   pushes and opens a pull request, so a next-round worker dispatched under the freeze would either
   break it or sit in a state that procedure does not describe. None is left running across the
   wait either, since a worker held idle for an unbounded maintainer wait is one doing nothing at a
   cost, and dispatching it after the merge starts it against the state that merge produced rather
   than the state it was briefed on.
7. The merge unfreezes the run, and the prepared round dispatches then.

The run advances no faster than the maintainer merges promotion pull requests. That is the human
gate, stated plainly rather than left for a stalled round to reveal.

### Assembling the Promotion Body

The body carries one `Fixes #N` per issue whose fix is on develop and not yet on main. Two hops
are needed rather than one, because the commits in `origin/main..origin/develop` are squash merges
whose subjects carry the **pull request** number and not the issue number, and this skill
deliberately keeps the closing keyword off the feature pull request, so nothing in the range names
an issue directly. Read the pull request numbers out of that range, freshly fetched, then read each
of those pull requests for its `Closes on promotion:` line, the one "Grouping and File Claims"
requires every feature pull request to carry and every worker brief to ask for.

**That line exists because the set has to be stated rather than inferred**, distinct from any
issue a body merely mentions. A body routinely references an issue it
does not fix, the deferral issues its own review round filed most of all, and those have to stay
open as the next round's ranking input. Sweeping in everything a body mentions would close them at
the promotion merge and delete the next round's backlog, so the promotion body reads the explicit
line and never the mentions. An issue named nowhere is one nothing closes, which is a missed
closure a later round notices, where the opposite error destroys work.

Deriving the set from the range rather than from what this round dispatched is what covers a group
that deferred or parked, contributing none, and an earlier round's work that no promotion has yet
carried. A fix landing during the freeze adds its issue to a body already written, so amend the
body when it lands rather than leaving the issue to be closed by hand.

## Run State

- **Working notes outside the repository hold the round**: the ranking, the working groups, the
  tier choices, and the worker assignments. A scratch file the harness gives a session serves
  where there is one, and any note kept out of the tree serves where there is not. It is the
  in-flight session state `GOVERNANCE.md` "Durable Knowledge and Self-Improvement" describes, and
  nothing about it is committed.
- **GitHub holds what outlives the session.** A claim comment records a group's file set, a pull
  request body records what a round carried, a `Fixes #N` line records what the promotion closes, a
  deferral issue records what was put off and why, a thread reply records how a finding was
  disposed of, and a stopped group's question is a comment on its issue.
- **No tracker file is committed for the run.** A committed tracker is a file every round rewrites,
  which is the contention the file-claim rule exists to prevent.

## Ending the Run

The run ends at either of two points, and they are different endings.

- **The backlog is worked out**, meaning a full re-rank finds no open issue this skill can act on.
  That is not the same as zero open issues, since a backlog of nothing but maintainer questions is
  a finished run. Report it as finished, with the questions put to the maintainer.
- **The session ends**, for a context limit or because the maintainer stops it. The run ends with
  it, since the merge authorization was bounded to that session. What the rounds already landed
  stands on its own in GitHub, and the branches, claim comments, and questions left behind are
  what a later run reads to pick the work up. That later run is a new run, named again, not this
  one continuing.

Report at every round boundary and at either ending: what merged to develop, what the promotion
pull request carries, what was newly filed, what is stopped and on which question, and what the
next round would pick.

## Mechanics Live Elsewhere

- The review loop, the Merge Gate, the five finding outcomes, and when a finding is a question:
  `pr-review-conduct`.
- Driving one pull request, and the promotion-pull-request wrinkle: `drive-pr`.
- Worktree isolation, the base branch, and the cleanup procedure this skill re-seats:
  `repo-worktree`.
- Closing keywords, branch protection, and the promotion trap: `operational-vs-release-workflow`.
- The pre-push adversarial pass and its recorded receipt: `local-strict-review`.
- Merging the promotion pull request and dispatching a release: `merge-and-release`, invoked
  separately.
- Delegation briefing shape, model-tier rules, wait discipline, and session scope: `AGENTS.md`
  "Context and Delegation Discipline".
