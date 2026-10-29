---
name: pr-review-conduct
description: >-
  Governs opening, driving, and merging a pull request review loop in a ptr727/ProjectTemplate fleet
  repo: requesting a review at its defined moments, triaging findings, replying and resolving
  threads, and deciding whether a PR is actually mergeable. Use this whenever about to open a PR,
  immediately after creating one, about to merge a PR, enable auto-merge, ask the maintainer for
  merge permission, push a fix and move on without re-checking review state, or judge a PR "green"
  or "clean" from CI or mergeStateStatus alone. Triggers even when the request sounds routine,
  such as "open a PR," "merge this," or "it's all green, go ahead," because mergeStateStatus:
  CLEAN can go clean once checks pass and every known thread is resolved, while still saying
  nothing about whether the review covered the current head SHA, read the full diff, or left a
  suppressed low-confidence finding, which opens no thread at all, unanswered. Also triggers when
  a review loop looks stuck (no review landing, findings that keep reappearing) or when deciding a
  finding is real, false, deferred, or a deliberate decline, or when a review looks missing or
  skipped. This skill is the contract that `scripts/pr_review.py`, `drive-pr`, and
  `merge-and-release` implement: running the loop hands-off is `drive-pr` and merging main is
  `merge-and-release`, each winning for its own action while this skill still binds the gate.
---

# PR Review Conduct

## Why this exists

`mergeStateStatus: CLEAN` reflects required status checks and any review thread the ruleset's
conversation-resolution requirement already tracks as resolved. It says nothing about whether the
review that resolved those threads actually covered the **current** head SHA. Nor does it say
whether that review read the full diff rather than part of it. Nor does it say whether a
suppressed low-confidence finding, which never opens a thread for the ruleset to see, was ever
answered. A PR that looks done, green checks, no
visible comments, routinely still carries a finding nobody has answered. Treating "green" as
"mergeable" is the single most common way this loop gets skipped.

## Merge Gate, check this before merging or enabling auto-merge

**Do not merge, and do not enable auto-merge, unless ALL of these hold:**

1. Required status checks are green, and where they are not, the reason is **read**, never
   inferred. `BLOCKED` covers a failed check, a required check nothing is running, an unresolved
   thread, and a missing approval alike, and the response differs by cause.
2. A review is confirmed on the **current head SHA**. On a pull request into a branch other than the
   default, every head can carry an attested local pass instead, `coverage=local` in the digest.
   That holds where the whole review history is in view and no round on record states partial coverage. A promotion re-reviews content its feature pull requests already carried, so
   the hosted review runs once per change, at the promotion. A pull request into the default
   branch, a promotion among them, always carries a Copilot round on its head. A review is matched
   by commit SHA rather than assumed from a green merge-state. A push makes checks go green *before* the
   re-review lands, and the matched review is **read**, not just counted. A review can carry the
   head SHA and still decline the PR outright, or say it read only part of the changed files.
   `pr_review.py` reads coverage into the digest's `coverage=` field, and only partial coverage
   is an exit code, `42`. That is a head round stating partial, or a head round reading `UNKNOWN`
   beside an earlier partial round. So is a head with non-refusal rounds, none stating coverage,
   beside one. A round covering the head that states full coverage satisfies this item where no
   other head round reads `UNKNOWN` or partial. Where none states any, a round covering the head
   whose own file table names exactly the changed files covers it, `coverage=table`. That is the
   reading a Copilot round at Balanced review effort gives. Where no round covering the head carries a table of its own, the newest round
   that does stands in as `coverage=carried:table`. Its table has to name exactly the changed files.
   A table stands in only where no Copilot round on the pull request, on any commit, states
   partial coverage. So a pull request that ever had a partial round goes to the maintainer.
   `coverage=none` and `coverage=UNKNOWN` leave this item unsatisfied and decide no exit code,
   so read the field rather than the exit.
   The coverage this item requires is Copilot's, or the attested local pass above, and CodeRabbit
   and Qodo are advisory. That is because the hub's `docs/pr-reviewer-evaluation.md` "Status"
   names Copilot the incumbent and says no candidate is a required reviewer. An advisory
   reviewer's absence blocks nothing, while its findings owe item 3 exactly as Copilot's do.
   `pr_review.py`'s `review_on_head` names Copilot's own coverage specifically, not "no review of
   any kind covers this head". An advisory reviewer carrying the exact head under
   `other_reviewed`, with an empty review body and no new threads, is not a missing review. It is
   its own ordinary "reviewed, nothing to flag" shape.
   A refusal is not that coverage, so this item stays unsatisfied under one, and the loop clears
   it where it can. A file-count refusal is cleared by splitting the pull request, which is the
   only cause on record that the loop can clear. `pr_review.py wait` exits `41` on every refusal
   and quota state, its `status=` line naming which. The digest's `refusal=` field carries a
   refusal on this pull request.
   `refusal=QUOTA` or `refusal=ERROR` means the pull request's newest Copilot review is an
   account-quota refusal or an error refusal read as a possible quota hit. That review can sit on
   this head or an earlier one. Nothing the loop does clears it, which is the case "Which
   Reviewers a Repository Actually Has" below states. An error refusal whose `RUN LOG READING` line says
   transient is the exception. `wait` re-requests it once and exits `30` while that request is
   pending. A second error refusal in a row, or a re-request made with no round answering it,
   reads as possible quota. A held head exits `41` as `COPILOT_ERROR_TRANSIENT`, its request
   waiting on `--request`.
   `status=COPILOT_QUOTA_EXHAUSTED_REPO_WIDE` is that same
   account state read from the reviewer's activity elsewhere when this head carries none of its
   own. A refusal holding across several heads with no cause its body names reaches it the
   slower way. That exit is `wait`'s alone. `status` exits 0 over a refusal, carrying it as
   `refusal=` in the digest line instead. So reading `status`'s exit code as the absence of a
   refusal would falsely satisfy this item on the exact state it exists to catch. That is where
   the unsatisfied item goes to the maintainer. The coverage the other reviewers gave that head is
   read rather than counted, and named to the maintainer. The maintainer's permission under item 5
   is what allows the merge. Item 2 is never waived, and a merge over an unsatisfied one is theirs to authorize.

   One further case satisfies this item where no coverage reaches the head, and it is the only
   such case. The maintainer adopted it as a standing rule. It applies to a develop -> main
   promotion on which `pr_review.py` reads `coverage=none`. That promotion satisfies item 2 where each
   of these holds on the live head:

   - Every change it carries had full coverage on its own feature -> develop pull request, from its
     attested local pass or a Copilot round. That pull request met item 2 at its merged head by
     coverage, not by permission. Failing that, each file it changed has positive evidence of a
     full read, at the head that last changed the file or a later one. A Copilot table row with no
     note, on that pull request or on the promotion, is such evidence. It counts only where no
     Copilot round on the pull request holding it, on any commit, states partial coverage. A round stating full coverage is evidence for every file. So is the
     attestation `pr_review.py attest` published on the feature pull request, for each file the attested
     head's diff held. That head is held to the same rule, the head that last changed the file or a
     later one. The attestation counts only from an owner, member, or collaborator, with the marker
     on its own line outside any code span or fence. The attestation's base has to be the one the
     pull request merged into. A local receipt does not count, since removing its worktree deletes
     it.
   - Every review thread on the promotion is resolved, and every check passes.
   - `coverage=none` is the only reason `pr_review.py` does not report the promotion ready, read
     from the digest's lines. A `VERDICT WITH NO COUNTED FINDING` headline, exit `50`, is a second
     reason until it is answered under item 3, and not after. A `shapes=HELD` hold is another
     until its acknowledgment posts under item 4.

   The merge then runs with `--match-head-commit` set to the head those were checked on. The
   evidence names each constituent pull request and how its coverage was stated. An
   `unattended-handoff` worker records it on the handoff, and an attended merge reports it to the
   maintainer. Any other shortfall still goes to the maintainer. One example is a promotion
   carrying a change whose own pull request lacked that coverage. A pull request another lane
   merged with no attested local pass and no review round is one such. The case answers item 2 alone, so item 5's permission is still owed.
   Every pull request other than a promotion owes this item's coverage in full.
3. **Every** finding on that head SHA is closed. Threads are resolved, and issue-level comments
   (which have no resolve action) are triaged and replied to. **And** the low-confidence findings
   collapsed in the review body are investigated and answered. Those appear in no thread, so
   polling threads alone reports a clean pass while they stand. The same holds for CodeRabbit's
   own "outside diff range" comments (`cr_outside_diff` in `pr_review.py`'s digest) and for
   Qodo's comment-only findings (`qodo_open`). These open no `reviewThreads` entry either, so
   give each one the same triage the low-confidence findings above already get. Copilot's own
   section for findings against code the pull request did not change is a fourth such class and
   takes that same triage. It is `Previously missed` in the review body and `previously_missed`
   in the digest. It raises no thread for the same reason the others do not. A finding it holds
   is raised outright rather than withheld. So "the branch did not touch that code" is a reason
   to decline one with evidence rather than a reason to leave it unanswered. Qodo's own
   `Resolved`/`Dismissed` self-tracked badge is a fast pre-triage signal, not a substitute for
   reading the finding, spot-verify against `gh pr diff` rather than trusting it outright.
   A Copilot round states its own count of open findings, which the digest reads as
   `overview=T/M` beside the number of review threads that round opened. The digest's
   `FINDINGS WITH NO THREAD` block counts the findings the round names that no open entry links
   to a thread on the pull request. The round can name them in that count or in its findings
   list. What the block counts is usually
   findings the round withheld, the same blind spot under a different name, and sometimes the
   digest undercounting instead. Reading the review body is what tells the two apart, and nothing
   else does. So read it and dispose of what it holds as this section's outcomes require. `T`
   reads `?` where no count was found, which is a round stating none and equally one the digest
   could not locate. So a `?` leaves this item unsatisfied and sends you to the body exactly as a
   shortfall does. A round with a verdict other than the
   clean one that counts no finding prints its headline under `VERDICT WITH NO COUNTED FINDING`,
   and `status` and `wait` exit `50`. The
   headline is the one place that round names what it flags, so answer it as a suppressed finding.
   The exit code repeats on that head after the answer. A clean verdict on the same terms prints
   its headline under `CLEAN VERDICT HEADLINE`, which changes no exit code. Read that headline,
   since it can name a nit no thread carries, and answer any such nit with `comment`.
   A finding `suppressed=` counts may be counted in the shortfall too. So answer what the body
   holds rather than what the two numbers add up to.
   What closing a finding owes turns on whether it is `pre-existing`, under the classes
   `local-strict-review` "Disposing of Findings" defines, applied here to a PR-hosted finding. A
   `pre-existing` finding on text inside a canonical Markdown unit takes the first bullet of
   outcome 4 of "Every finding ends in a listed outcome" below. Such a unit is one the hub's
   `scripts/canonical_review.py list` names. That bullet applies once per unit rather than once
   per finding. The round gathers that unit's such findings onto the unit's tracker, an open hub
   issue whose title carries the unit key. The change that moves the key retitles it, and
   whichever round first needs it files it. The round answers each finding with that issue's
   link, resolving a thread on that reply. So a `pre-existing` remark on a sentence the change
   never touched costs one link rather than a decline or an issue per finding. The batch runs in
   the hub, which authors the text of every verbatim unit.
   A carrying repository routes a finding on a verbatim unit by fidelity rather than by class,
   since a resync writes the whole text there. It declines the finding under
   that section's outcome 2, ownership sitting elsewhere, and files it on the same tracker.
   A finding on an intent unit is filed there too, since the defect is still fixed at the source.
   The carrier adapts its own copy of an intent unit meanwhile. Every other finding, a `style`
   remark on untouched text included, takes its own outcome in that section.

   A finding the pull request's own body lists also needs an outcome before merge, under
   "Every finding ends in a listed outcome" below. Per the `local-strict-review` section
   "Disposing of Findings", the body lists each one still open once a push's edit budget is spent.
   It needs that outcome whichever head it was found on and whether or not a reviewer raises it
   again. `pr_review.py status` and `wait` do not read the pull request's body, so read the body
   live for that list before judging this item. It has no thread, so answer it as
   "Answering a suppressed finding" below states. Cite the body's list where that section links
   the review round, and the list's length where it states the block's `(N)` count. A line under
   a heading that outcome 4 of that section names is not on that list. It is already its
   finding's outcome, and owes only the pointer that outcome names.
4. Every part of a round covering the head that the tooling could not place is answered. The
   digest prints an excerpt of each under `UNPLACED ON THE HEAD` with a fingerprint for the set.
   It reads `shapes=HELD`, and `status` and `wait` exit `50`.
   - Read each part whole in the review body, since the excerpt only locates it. Give every item
     in it item 3's outcome: fixed with its SHA, declined with evidence, or listed as pre-existing
     in the pull request body.
   - Post the outcomes with `pr_review.py comment <number> --repo <owner>/<repo> --fingerprint
     <fp> --body <outcomes>`. One from an owner, member, or collaborator clears the hold,
     `shapes=ACKNOWLEDGED`. New unplaced text on a new head owes a new acknowledgment.
   - The spelling is a data edit to the matching `scripts/reviewers/*.json` in the hub plus one
     fixture. Whoever acknowledged the part makes it in a pull request of its own, never an issue.
   - An item that reads as neither a finding nor not one goes to the maintainer with its raw text
     quoted, per "Escalate to the maintainer when" below.

   A part on an earlier round prints under `REVIEWER OUTPUT NOTED` and decides no exit code. So
   does a label nested in a list, a quote, or a placed part. Read that block rather than the
   `shapes=` field, which names a head hold first. Read each such part by hand, and give any
   finding in it item 3's outcome.
5. The maintainer has given **explicit** permission to merge.

The agent never merges on its own. A green or CLEAN PR with one open finding is not mergeable,
full stop, whatever the merge-state field says.

## Which Reviewers a Repository Actually Has

Whether a reviewer covers a repository at all is decided by product terms this fleet observes
rather than sets. Whether it reviewed this pull request is decided by those terms together
with configuration a repository commits itself. So respond to what the reviewers actually did on
the pull request in front of you. Do not decide from a repository property what a reviewer
must have done.

- **A reviewer that posted a skip notice is available for the asking.** It says it did not review
  automatically, which is not the same as not reviewing at all. Comment the reviewer's documented
  review command, such as `@coderabbitai review`, and wait for the result as with any other
  requested review. The agent driving the loop posts that comment itself, on the same standing as
  requesting a Copilot round. It posts it at most once per pull request, on the head the drive
  judges final, and never after a rate-limit notice. That is because the reviewer caps its reviews
  per pull request and its absence blocks nothing.
- **A notice naming when the reviewer can next run is a rate limit, and asking does not clear
  it.** It reads like the skip notice above and is the opposite case. The trigger returns the same
  notice rather than a review. So a loop that keeps asking waits on something no amount of asking
  produces. Do not ask again on that pull request, and proceed on the reviewers that did run,
  since an advisory reviewer blocks nothing.
- **Silence is not evidence, and is never read as one on its own.** A reviewer that has posted
  nothing may not have started yet, or may not cover this repository at all. Or it may have
  reviewed and had nothing to say. That case posts no comment to read, and Merge Gate item 2
  describes it as its own ordinary shape. Read the reviews themselves rather than the comments alone, since the third
  case appears only there.
- **Copilot's absence blocks where no local pass stands in, and is answered elsewhere.**
  Merge Gate item 2 requires Copilot's own coverage of the current head. On a pull request into a
  branch other than the default, an attested local pass stands in for it. The loop's own request
  step below is where a missing one is answered, on the terms stated there. A refusal naming the
  account quota is its own case rather than a review. So is one saying only that Copilot
  encountered an error, which is what the
  weekly rate limit posts. Each covers no head, so the gate stays unsatisfied. Nothing the
  loop does clears it, since re-requesting returns it again and spends quota doing so. Where the
  reviewer's run log names a reset time `pr_review.py` reports it. Where it names a transient
  fetch failure instead, `pr_review.py wait` re-requests once. Every other refusal, a repeat of
  that one included, goes to the maintainer rather than into a wait.

The hub's `docs/pr-reviewer-reference.md` records what each reviewer does, what shapes it, and
which repositories its plan covers. That is the record for a reviewer whose behavior still
surprises you after reading what it posted.

## Expected review loop

Open every fleet-owned pull request ready for review. Draft state delays the loop and causes
reviewers to skip, so it has no place in the internal feature-to-develop or develop-to-main
workflow. The separately documented `upstream-contribution-workflow` may use a draft while a
third-party contribution is still being prepared for upstream review.

Opening a pull request starts this loop by default. Creating the PR is not a terminal handoff.
Only an explicit maintainer instruction may stop, defer, or alter the loop. Silence or a request
that says only "open a PR" is not such an instruction.

Run every `scripts/pr_review.py` command below from a hub checkout. The script is hosted there and
is never carried into a downstream repository.

Run `local-strict-review` against the branch's current diff before every push this loop makes. The pass is owed before the push that opens the pull request in step 1 and each one after it. It is owed whatever finding a push answers and whether or not the branch was reviewed once already. A push that delivers content no pass has read is the case the rule is about. So a re-push of a tree a recorded pass already covers needs no second pass. That is because the receipt is keyed on the branch's net content rather than on its commit series. A rebase that leaves the tree alone keeps the receipt only while the merge base holds. Rebasing onto a target that has moved retires the receipt although no file changed. That push owes a pass like any other. Follow that skill's own ordering and record each pass, which is what a capture point reads. The hub's own `pre-push` hook is one, and a repository has none until such a hook is carried to it. A push that hook refuses, where one is present, is the gate working rather than an obstacle to route around. That skill's refusal table says what each refusal means and what clears it.

1. Push changes to the PR branch and open the pull request when it does not exist. Once the push
   lands, run `uv run --no-project --script scripts/pr_review.py attest <number> --repo <owner>/<repo> --checkout <worktree>`.
   `attest` refuses unless the checkout is the pushed head and a recorded pass covers that head.
   Attest after every push a recorded pass covers, the opening push included. The comment `attest`
   posts is the record Merge Gate item 2's promotion case reads, since the receipt goes when its
   worktree does.
2. Run `uv run --no-project --script scripts/pr_review.py status <number> --repo <owner>/<repo>` once in the foreground and read its output.
3. Request a Copilot round at one defined moment: the head of a pull request into the default
   branch, a promotion among them. Auto-trigger is unreliable, so request it explicitly, which
   step 4's `wait` is what does. On a pull request into any other branch, `wait` reads step 1's
   attestation as `coverage=local` where Merge Gate item 2 allows that reading. There `wait`
   requests a round only where a partial round or a truncated review history is on record. Pass `wait --request` where a change
   deserves a Copilot round anyway. `wait` also skips the request where
   a review already covers the head, where the answer came outside a formal review, or where it
   detects drift. It skips it under a quota or error refusal too, and where something is already in
   the request set. Something in the request set is the condition the recovery below clears. An error refusal whose run log reads transient is
   the exception, owed one re-request per Merge Gate item 2. Requesting in the pull request UI is
   the maintainer's route rather than this loop's.
4. Run a bounded `uv run --no-project --script scripts/pr_review.py wait <number> --repo <owner>/<repo>` in a background process and read its terminal output.
   A completed review raising **no findings** is a valid terminal outcome, so do not re-trigger it
   or read silence as a missing review. A review whose body says it declined to review is the one
   exception, and it is terminal the other way. Nothing follows it, and re-requesting the same
   head only repeats the decline. A transient error refusal under Merge Gate item 2 is owed its one
   re-request first.
5. Triage findings (see below).
6. Apply fixes or write a rationale for declines.
7. Reply to each thread, and resolve what was addressed and what was declined on evidence the
   reviewer could check for itself, per outcome 2 below.
8. Re-run the loop after every fix push until the checks are green, the current head is covered,
   and no finding remains open.

The review effort setting is user-controlled. The workflow never selects or changes it. `status` reports `effort=lite`, `effort=balanced`, or `effort=max`, lowercased, when the completed review exposes that metadata. It names an inherited setting apart from a chosen one in a separate `effort_source=default|explicit` field. Both fields read `unknown` when no effort line parses. Missing effort metadata reports `unknown` and does not change coverage or completion. A pending effort-labeled request can complete without a `copilot_work_started` timeline event, so absence of that event never proves the request is abandoned. The bounded timeout reports `PENDING` when no review or terminal answer arrives. `requested=yes` reports that the request was accepted rather than that a round is coming. An accepted request can sit unpicked, printing the same digest as one about to be served. So a driver reading that field as progress is waiting on evidence it does not hold. After a timeout carrying it, rerun `wait` for another bounded interval by default, because the request may still be active. Where a second bounded wait times out as well, read the pending set. In that case, clear it only where no human or team reviewer is requested alongside the bot. That is because the clear replaces that set rather than adding to it, and nothing restores a request it drops. A stall on a pull request that has a human or team reviewer requested goes to the maintainer instead. So does one still pending after the wait that follows a clear. The clear leaves the next `wait` nothing outstanding to defer to, so that run requests afresh. Its own auto-request line is what says so. That is because `wait` reads the reviewer's node id out of the repository's recent reviews, and polls without requesting where it finds none. The hub's `docs/pr-reviewer-reference.md` carries the mutation, and an agent seat can run it. By contrast, removing and re-adding the reviewer in the pull request UI is a step only the maintainer can take. This recovery replaces only the review request and never changes the effort setting. A `wait` ending `REQUEST_NOT_RECORDED`, exit 40, is a different state and takes none of this recovery. The request returned success and left neither a pending reviewer nor a review-request event. That is how an exhausted Copilot allowance has shown itself. So clearing and requesting again does not clear this state, and it goes to the maintainer.

Drive to green, a review confirmed on the latest head SHA and every actionable finding closed,
then apply the Merge Gate above. **Never exit this PR-hosted loop early.** Its pre-push
counterpart is bounded instead by `local-strict-review` "Disposing of Findings". A round count
is not a stopping condition here, and neither is patience running out. Reporting only that the
PR was opened is an early exit unless the maintainer explicitly instructed the agent not to
monitor or drive its review.

After an authorized merge, run the `repo-worktree` post-merge cleanup procedure unless the user explicitly asks to retain the checkout or branch. For a merge into `develop` that procedure is `uv run --no-project --script scripts/pr_review.py cleanup <number> --repo <owner>/<repo> --checkout <base-clone>`, run from a hub checkout. The pull request loop is incomplete while its finished worktree or local task branch remains. A promotion branch merged into `main` is the exception, which `repo-worktree` leaves for its sweep. The loop is also incomplete until the base clone returns to fetched and fast-forwarded `develop`.

## Every finding ends in a listed outcome

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
   The class is the one `local-strict-review` "Disposing of Findings" defines, and the first
   bullet that fits decides:
   - A `pre-existing` finding on a canonical Markdown unit keeps the per-unit tracker
     `pr-review-conduct` "Merge Gate" item 3 describes, since that already costs one link per unit.
   - A `pre-existing` correctness defect with a stated failure is filed once as its own issue,
     labeled `pre-existing`. Such a finding names an input or a state and the wrong output or the
     crash it produces. Its body line goes under a `Pre-existing findings, filed` heading and
     links that issue. A thread raising it is answered with that issue's link and resolved.
   - A `style`, wording, consolidation, or restatement finding on text this change did not touch
     is dropped. It gets one line under a `Dropped findings` heading and nothing else. It gets no
     decline text, no issue, and no reply beyond a pointer to that line, which resolves any
     thread.
   - Any other `pre-existing` finding, from a local pass or a PR-hosted review, is listed in the
     pull request body with its location and its class. No issue is filed for it. The body line
     is its outcome, and a thread raising it is answered with a pointer to that line and resolved.
   - Any other deferral is filed as an issue first, and the reply carries its link. A deferral
     noted only in a thread is lost the moment the PR merges.

   The body keeps every other unfiled `pre-existing` line, the fourth bullet's, under one
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
   carried `pre-existing`. Each one costs a lane, a pull request, and a review once worked.
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

## Triaging findings

**A low-confidence (suppressed) finding is not a low-value one.** Judge each against the code,
never against its confidence label. Classify before responding:

- **Bug**, wrong behavior, missing coverage, a real code or doc divergence. Fix it.
- **Hardening that `GOVERNANCE.md` "Trust Boundaries and Hardening Effort" rules out**, including
  a test it says not to write. Decline it under outcome 2, even where it reads as missing coverage.
- **Design finding that `GOVERNANCE.md` "Design Before Code" backs**, such as logic an existing
  helper provides or a per-site fix for a class seen elsewhere. It is not an architectural
  opinion. Fix it, extending the existing helper where this repository holds it, and move the
  class's sites in the touched files onto it. File the other sites as one issue naming each, and
  an extension a helper in another repository needs as an issue in that repository.
- **Style or convention**. If the cited rule matches the existing tree, fix the code. If the rule
  contradicts the tree or industry norm, **fix the rule, not the code**. Take such a rule to the
  maintainer (outcome 5) rather than bouncing the same code across rounds.
- **Architectural opinion**, a proposed redesign. Surface it with a recommendation, never apply
  it unilaterally.

## Answering a suppressed finding

A suppressed finding has no thread and no resolved or unresolved state, so an answer needs to
carry its own context. Quote the finding with its `file:line` anchor and enough of the reviewer's
own words to identify it. Give one bold verdict per finding: `Fixed in <SHA>`, `Disproven`,
`No change needed`, `Deferred to <issue>`, `Listed in the body`, or `Awaiting maintainer`. An
`Awaiting maintainer` answer leaves the finding open until a later answer records the
maintainer's decision. State the `(N)` count the block gave so answers can be checked against
findings, and link the review round.
**Read every round, not only the head.** A suppressed finding does not retire when a later push
supersedes it, it just stops showing up in a head-scoped query while still unanswered. Post the answer with `uv run --no-project --script scripts/pr_review.py comment <number> --repo <owner>/<repo> --body <text>`
from a hub checkout. Do not use a provider connector or reconstruct the GitHub mutation.

## Escalate to the maintainer when

- A genuine design trade-off surfaces (fail-open vs. fail-closed, refactor scope).
- A finding keeps recurring. Bring the pattern and a recommended fix (rule change or code
  change), don't keep silently re-declining it.
- A finding is judged real but should not be fixed. That decision is never the agent's alone.
- An architectural redesign is proposed rather than a bug fix.
- An item in a part the digest prints under `UNPLACED ON THE HEAD` cannot be told to be a finding
  or not. Quote its raw text in the escalation.

An agent that cannot reach the maintainer directly escalates to whoever dispatched it and stops
that unit of work there. A dispatched subagent is the ordinary case. Such an agent never substitutes its own
judgment for the escalation because asking is inconvenient from where it sits. It never resolves
the thread to keep moving either. A dispatcher receiving one puts it to the maintainer at the point
that work stopped, per `GOVERNANCE.md` "Communicating with the User". This skill's own
ask-the-maintainer outcome forbids resolution by silence. Deciding the escalated question instead, so
the dispatcher's own work keeps moving, is that same resolution, one seat further from the
maintainer. The escalation may travel through several seats. What stays stopped is the
escalated unit of work, in whichever seat holds it, until the answer arrives. A dispatcher's other
work is not stopped by it.

## Mechanics Live Elsewhere

This skill is the provider-agnostic contract. Use `scripts/pr_review.py` from a hub checkout for
the GitHub-specific API operations, each taking `<number> --repo <owner>/<repo>`. `claims` checks the pull
request description against the branch it describes, catching a commit or `uses:` ref the head no
longer carries. `status` reports coverage, threads, body-only findings, and
shapes in one call. `wait` requests and polls in-process. `comment` posts a PR-conversation
answer after it reads the PR node ID. `reply` answers a thread by matching the finding's own
words instead of a line number a fix push can move. It resolves the thread only when `--resolve`
is given. A finding posted twice on one file, as two threads with the same text, is answered with
`--all-identical`, which the refusal names. The repository's
`.github/copilot-instructions.md` bootstraps Copilot into the `fleet-code-review` skill and its stable
coverage marker. Do not reconstruct the API operations by hand.
