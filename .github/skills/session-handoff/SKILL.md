---
name: session-handoff
description: >-
  Writes and resumes the ptr727/ProjectTemplate fleet's session handoff, a link in a chain of
  issues rather than a file: one open issue per track per repository, carrying the `handoff` label
  and naming its predecessor, which is then commented on and closed. Use this whenever ending a
  session or a round of work, whenever starting or resuming one, whenever asked for a handoff or
  for what the previous round did, whenever the maintainer says only "resume the handoff" or names
  none, which stands for the whole attended procedure this skill states, and whenever about to
  re-attempt something a previous round may already have tried, the moment that earns this skill,
  since a session that does not know a chain exists never goes looking for one. Every session
  writing one, except an `unattended-handoff` seat, ends in the same order: lessons recorded, the
  link, the parked decision queue presented, then memories saved last. Triggers even when the
  session feels too short to be worth a handoff, because the rounds that produce nothing worth
  writing down are exactly the rounds a later session repeats. The rule is `AGENTS.md` "Session
  Scope" and the mechanics are the hub's `scripts/handoff.py`. Where a sibling skill owns the
  moment, it wins: `backlog-burndown` owns a multi-round run's reporting, `unattended-handoff`
  owns a run with no maintainer present, and `repo-worktree` owns the worktree the handoff names.
  `agent-conduct` co-fires at the parked-decision obligation and neither defers to the other.
---

# Session Handoff

## Why This Exists

A handoff written to a scratch file fails three ways the chain closes. The file is not found where
the next session looks. More than one candidate file is found and nothing says which one is
current. And there is no history at all, so a later round re-runs a path an earlier round already
tried and already wrote down. The value of a handoff is precisely the record of what has already
been attempted, and the shape the handoff actually had, one overwritten file on one machine, is a
shape that cannot hold it.

The chain closes each of those rather than making it impossible. The label and the metadata block
are what make a link findable from any machine, the one-open-per-track invariant is what answers
which link is current, and every closed link stays readable to every session after it. Where the
invariant is broken the chain refuses and says so, which is a state a reader can act on rather than
one that reads as an answer.

The rule is `AGENTS.md` "Session Scope", which keeps it, and it names the handoff's sections and
sets the size rule over them. This skill is the judgment that rule cannot state: what actually earns
a place in each of those sections, what belongs somewhere durable instead, and how to read a handoff
without trusting the parts of it that have gone stale.

## What a Handoff Is, and Is Not

- **It points at the durable record rather than restating it.** A defect becomes an issue, a rule
  becomes documented rule text, and a lesson becomes governance prose, per `GOVERNANCE.md` "Durable
  Knowledge and Self-Improvement". The handoff names each of those in a line and a link. The one
  exception is what a round attempted and what that cost, which has no home outside the chain, so
  there the chain is the durable record rather than a pointer to one.
- **It records work to do next, and it is not itself backlog work.** It carries none of the labels
  that classify work to be done, and every enumeration that ranks or counts the open backlog filters
  the `handoff` label out, since a track in use always has an open link and counting it inflates the
  backlog by one per track forever.
- **It is not a place to ask a question.** A question waiting on the maintainer is an issue carrying
  the `decision` label. The handoff records that queue, and "The Parked Decision Queue" below
  requires the session to put the questions themselves to the user in the same act, since a
  question recorded and never asked is the failure that section exists for.

## The Chain

- **One open handoff issue per track per repository.** A track is a short kebab-case slug naming a
  lane of work, and a session that names no track uses `default`. Parallel lanes each name their
  own, so each closes its own predecessor and neither reads the other's state as current.
- **The title is for humans**, shaped `Session Handoff [<track>]: <subject>`, which `new` composes
  from the track and the subject it is given, so what a session writes is the subject alone.
  Nothing parses the title, so a maintainer is free to rename one.
- **The chain is machine-readable from one HTML comment on the body's last line**, rendered
  invisible, so a retitled or hand-edited issue still chains:
  `<!-- handoff: v1 track=<slug> round=<n> previous=<issue number or none> -->`.
- **Closing the loop runs in one order**: create the new issue, comment the forward link on the
  previous one, then close the previous one. Creating first means a failure at any later step leaves
  a discoverable new issue rather than a closed chain with no successor, and `link` is what finishes
  a run that stopped between those steps.
- **The invariant a reader checks** is exactly one open issue carrying `handoff` for a given track in
  a given repository, read from each issue's metadata block rather than from the label, since an
  issue carrying the label and no block belongs to no readable track and blocks the count rather
  than joining it. Two or more on one track is a defect to report, never one to resolve by picking,
  and a run interrupted between the three steps above is the one shape of it that `link` settles
  rather than a human.
- **Zero open on a track is two different states.** The track has never had a handoff, or its lane
  was closed out and its newest link is closed. `new` chains onto the newest link either way, open
  or closed, because starting a second chain beside one that exists orphans every link already
  written.
- **The handoff lives in the repository holding the work the next session resumes.** A session that
  spanned repositories writes it there and names the others in the state section "What Goes in the
  Body" below describes, rather than filing one handoff per repository.

## What Goes in the Body

A fixed section order and fixed section names, the bold name opening each of items 1 through 8
below being the heading the handoff writes. Item 9 is an HTML comment the tool writes rather than a
section anyone types. Fixed names are what let a reader find a given fact in the same place every
time and what let one round's section be compared against another's. `AGENTS.md` "Session Scope" sets the size rule, and it is per section: an entry
earns its place by being specific enough to change a later session's behavior, and a section ranks
what it keeps and drops whatever does not meet that bar. `handoff.py` warns above 12 KB and refuses
above 60 KB, the refusal being a backstop against a body GitHub would reject and the warning a hint
that the per-section rule broke several sections earlier. Neither is the rule.

1. **Next steps, in priority order.** The most valuable section, and first for that reason. Each item
   names what to do and what "done" looks like. At most a handful, ranked. A step blocked on
   something says so and names the blocker below.
2. **External blockers.** Anything the next session cannot resolve on its own: a maintainer decision,
   a third-party quota or outage, an upstream release, a credential, a reviewer bot that is not
   running. Each names who must act, what unblocks it, and what is safe to do meanwhile.
3. **Internal dependencies.** The ordering constraints among the next steps, each stated as the
   dependency and its reason, so a later session can re-rank when circumstances change rather than
   following an order it cannot audit.
4. **State.** Branch, worktrees and whose they are, open pull requests, what merged, whether a
   release was dispatched, whether the primary checkout is clean, and any other repository the
   round touched, named so a resume knows to look there too. These are facts a resume re-derives,
   listed so the resume knows what to re-derive.
5. **The parked decision queue.** The count and the ranked list "The Parked Decision Queue" below
   requires, which also requires the questions to be presented in the same act rather than only
   recorded.
6. **What the last round did.** Issues closed and filed, pull requests landed, and the peripheral
   issues filed along the way.
7. **What not to repeat.** The section a file could never carry: paths explored that led nowhere,
   approaches that failed and why, problems discovered during execution, and the cost of each where
   it is known. An entry here is a claim about a specific attempt rather than a general lesson.
8. **New learnings.** Only what is not already durable somewhere else, each with a pointer to where
   it was recorded. A lesson that belongs in governance goes to governance and appears here as one
   line and a link.
9. **The metadata block**, which `scripts/handoff.py` writes and no author types.

Sections 7 and 8 are what the chain exists for, and they are also the two most likely to be padded.

A handoff issue outlives the session that wrote it and is readable by everyone who can read the
repository, which on a public one is everyone, so it quotes no data observed in the maintainer's
environment, per `GOVERNANCE.md` "Representative Data in Agent-Authored Text". That rule binds on a
private repository exactly as it does on a public one, and the visibility only changes who the
audience is. The routine case here
is an absolute home path, since a handoff naturally wants to name a worktree, and the rule for it is
to name the worktree by its branch and its repository-relative role rather than by its path. A next
session re-derives the path from `git worktree list` anyway, which section 4 already tells it to
do.

## Resuming

**Re-derive live state, and do not trust the handoff's copy of it.** Branches, worktrees, open pull
requests, and issue counts are read again from git and from GitHub at resume. The handoff's state
section is the pointer to what to read rather than the answer, and `AGENTS.md` "Session Scope"
already says stale context is worse than absent. A handoff is context by construction, so this is
that rule applied to the one artifact built to outlive the session that wrote it.

Read the current link's comments too, with `gh issue view "<n>" --comments`, since `resume` prints
only the body, and a parking comment or a closing session's answers land in the comments.

Read the chain before re-attempting anything. `resume` prints the current body and indexes the
closed links behind it, and `chain --grep` searches the bodies it walks for a regular expression,
which is how a session answers whether a path has already been tried without reading every round.
Three things bound that answer. The walk starts at one track's newest link and follows each
marker's `previous=` from there, which is normally that one lane and is in fact whatever the markers
name, so every link prints its own track and a predecessor on another lane shows up rather than
passing unseen. The walk stops at `--limit` and names the link it stopped short of, so a capped
search never reads as an exhaustive one. And `--grep` takes a regular expression, so a literal
string carrying a regex character is escaped or it matches something other than what was typed. On
a track with no open handoff `resume` refuses rather than printing an empty body, and `chain` falls
back to that track's newest closed link, so the read side of a lane that was closed out is `chain`
rather than `resume`.

Re-derive a count rather than copying one, and read it with an explicit page size. `gh issue list`
returns 30 rows unless told otherwise, and a truncated count reads exactly like a repository with
30 issues, which is worse than an absent count because it gets stated.

## The Attended Session

A maintainer resuming work says little, often only "resume the handoff", and that phrase stands for
the whole procedure below. Run every step without being reminded of any of them.

1. **Pick the link.** Where the maintainer names an issue, take it. Where none is named, read
   `gh issue list --label handoff --state open --limit 100 --json number,title,labels,updatedAt`,
   since `tracks` prints neither labels nor exact update times. Take a link carrying `blocked`
   first, newest update first among them, since its blocker is a decision only the maintainer can
   make and the maintainer is now present. Otherwise take the newest update among the rest. An
   `auto-*` link not carrying `blocked` is skipped unless named, since an `unattended-handoff`
   worker may hold it right now, and where one is named, confirm with the maintainer that no
   unattended run is live before working it. Where every open link is skipped, say so and ask the
   maintainer which to take. Say which link was picked in one line before anything else, so a wrong pick costs one reply
   rather than a round.
2. **Resume it** per "Resuming" above, comments included, re-deriving live state rather than
   trusting the body.
3. **Ask what it is blocked on first.** Where the link carries `blocked`, the parking comment names
   a `decision` issue. Read it first, since the maintainer may have answered it there already.
   Otherwise that question goes to the maintainer before any work starts, since the rest of the
   link waits on it. Once answered, record the answer on the decision issue, take its `decision`
   label off, and close it where it held nothing but the question. Leave `blocked` on the handoff
   while this session works the link, so a running `unattended-handoff` loop does not pick it up
   underneath the session, and settle it in step 6.
4. **Work the next steps** in a worktree of the session's own, per `repo-worktree`, and drive each
   pull request with `drive-pr` to a mergeable develop -> main promotion pull request. The phrase
   already states that target, so `drive-pr` does not ask how far. Merging that promotion pull
   request and dispatching a release stay `merge-and-release`, each on an explicit go-ahead asked
   for as a prompt whose option names the action.
5. **Ask every question as a dialog.** Where the interface has a prompt mechanism, each decision is
   its own question with its answers as the options, the recommended one first and marked as the
   recommendation, and every one carrying its reason, per "The Parked Decision Queue" below. The
   numbered list is the fallback where no prompt exists.
6. **Close the session** per "Closing a Session" below, which records lessons, writes the next link
   with `new`, presents the parked decision queue, and saves memories last. An `auto-*` lane is the
   exception, since it holds one issue. Where that issue is done, comment the outcome on the link
   and close it with no successor, as `unattended-handoff` closes such a lane out. Where work
   remains, write its next link on the same track without `blocked`, its next steps naming the
   decision issue and the answer, which hands it back to the unattended loop. Either way the
   `blocked` label left on in step 3 goes with the link it was on, and the parked decision queue
   is still presented, since the exception covers only the link.

## Closing a Session

Every session that writes a handoff ends in this order, except an `unattended-handoff` seat, which
closes as that skill states. Each step leaves the chain whole if the session is interrupted after
it.

1. **Record what was learned** per `GOVERNANCE.md` "Durable Knowledge and Self-Improvement", which
   says where a lesson goes, before the link is written, so its "New learnings" section points at a
   record that exists.
2. **Write the link**, per "Running the Chain" below, or close the lane out where its work is done.
   The link is written before the closing questions are put, since a prompt blocks until someone
   answers and an unanswered one must not cost the round its handoff.
3. **Present the parked decision queue** in the same act, per "The Parked Decision Queue" below.
   Record each answer given on its issue, where one was asked and answered. Where a link was
   written, comment those answers onto it together with the queue's count and list after them,
   since the body keeps the count it was written with and a resume reads the link's comments.
4. **Save memories last.** Where the host keeps a per-user memory, what goes there is the
   environment-specific nuance "Durable Knowledge and Self-Improvement" leaves to memory, such as a
   quirk of this machine or this account, saved as the session's final act. Never the round's
   state, which the link holds, and never a lesson, which step 1 already recorded.

## The Parked Decision Queue

The rules below are `GOVERNANCE.md` "Communicating with the User", carried whole so this skill works
in isolation. Two of them state the obligation: the one opening "A question filed as an issue is
parked rather than asked" and the one opening "The session that writes a handoff presents the parked
queue in the same act". Three more state the form the questions take, the one opening "Ask every
question through the interface's prompt, never in prose", which also names the numbered list as the
fallback where no prompt exists, the one opening "Raise work blocked on the user as a direct
interactive prompt", which shapes the options, and the one opening "Lead every choice with a
recommendation and its reason", which binds every question put either way. A reader who stops after
the obligation has it with no shape to put it in.

Recording the queue is not asking it. The handoff's "The parked decision queue" section is the
recording half, and what the other half is depends on what the session can reach: a prompt where one
is available, the numbered list where a user is present and no prompt is, and nothing at all where
no user is present, in which case the handoff names the whole queue by issue number and the asking
falls to the next session that has one. The rules below settle which case applies.

<!-- include: GOVERNANCE.md > Communicating with the User -->

- **Reference every pull request as a clickable link.** When you mention a PR on a surface that renders Markdown (chat, a summary, a report), render it as a Markdown link to the PR (`[#N](https://github.com/OWNER/REPO/pull/N)`), never a bare `#N`. The same applies to issues and commits. **The form follows the surface.** Some surfaces link neither a Markdown link nor a bare URL, an interactive prompt's question and option text among them, and pasting a full URL into one of those does not rescue it, since the reader gets a string to copy, which is the outcome this rule exists to prevent. There the reference is a bare `#N`, and the clickable link goes in the message that comes **before** the prompt rather than merely alongside it, because the prompt blocks on an answer and a message emitted after it is read once that answer is already given, which is the one moment the link is no longer any use. The test is whether the reader can click it where it is read, not whether it was written in the syntax that works elsewhere.
- **Ask every question through the interface's prompt, never in prose.** When you need the user to decide, answer, or approve anything, put it to them through the interface's own prompt mechanism, whether or not work is blocked on it, and an offer to do more work ("want me to file that?") is a question like any other. A question written into a message is lost in the report around it however short it is, and one closing a long report sits where the reader is least likely to reach it. Where no prompt mechanism is available, the questions go as a numbered list opening the message rather than closing it, so the user can reply per number. This bullet settles how a question is put once it is asked, and whether a question is asked now or recorded for later is the parking bullet's to settle, the one opening "A question filed as an issue is parked rather than asked".
- **Raise work blocked on the user as a direct interactive prompt.** When progress needs a decision, an authorization, or an answer only the user can give, ask for it through the interface's own prompt mechanism, at the point the work stops. Never leave it as prose in a summary: a handoff buried in a paragraph is a handoff that did not happen, because a summary reads as a report of finished work and the one line still waiting on the user is the easiest in it to skim past. The blocked item is the message, not a closing remark on a message about something else. **The options offered are the actions themselves**, and the one that unblocks the work names the action it authorizes ("squash and merge it"), so selecting it is the go-ahead rather than a note to act on later. Offering only ways to wait is the same failure in interactive clothing, since a prompt whose every choice is inaction reports the block rather than clearing it, and where the agent may not perform the authorized action itself, the option says who does it. Where no interactive prompt is available, the numbered list the bullet above names is the fallback.
- **Lead every choice with a recommendation and its reason.** Wherever the user is asked to choose, in a prompt or in a numbered list, the option the agent recommends comes first and is marked as the recommendation, and every option states the reason for it. A user who takes the recommendation then reads one line, and one who does not sees what the other options trade away. A question with no defensible recommendation says so rather than inventing one. Where an open question has no options and the agent does have an answer, that answer and its reason go in the question's text, never as an invented option.
- **A question filed as an issue is parked rather than asked, and it stays owed.** Where the work cannot continue without the answer, the interactive-prompt bullet governs and the question is asked at the point the work stops. Wherever the question is recorded rather than asked, whether because the work can continue without the answer or because the question was asked once and deferred, recording it is the right thing to do and recording it is still not asking it, so the issue carries the fleet's `decision` label and, when the filing session knows the choices the question is between, states them, which is what lets a later session, one that was not there when the issue was filed, find the question and put it to the user without inventing its answers. **The parked queue is the failure, not the parking.** An issue holding a question the user has never seen reads to every later session as tracked work rather than as a block, so each session files correctly and moves on, and presenting the accumulation is the step nobody owns.
- **The session that writes a handoff presents the parked queue in the same act.** This binds at the moment the session writes the handoff that `AGENTS.md` "Session Scope" defines, rather than at the moment the session ends, because a session also ends by interruption, where no agent acts at all and no rule reaches it. The session enumerates this repository's open issues carrying that label, states how many issues the queue holds, and puts the highest-ranked of them to the user as questions, **one question per parked decision, carrying that decision's own answers as its options**, which is the interactive-prompt bullet's own shape applied per decision rather than a single prompt whose options are topics. An issue that states no choices is put as the open question it is, never as invented options, since the label marks every decision waiting on the user rather than only the ones filed under this rule, and the issues that already carry the label predate the rule. Rank them longest-waited first. Every issue records how long it has waited, so any session can reproduce that order. Promote one ahead of that order where it blocks work in flight, and say in the handoff that it was promoted, since two sessions order the same queue differently where the key is subjective and nobody records it. Where more are parked than one round of questions can carry, the stated count still covers every one of them and the handoff names the remainder by issue number in that order, a list of numbers rather than of questions, which is what keeps the handoff inside the size rule that `AGENTS.md` "Session Scope" sets for it. **A count nobody states is a queue nobody can see**, which is how a backlog reported as healthy hides the questions inside it. Where a user is present but no prompt mechanism is available, the questions go as the numbered list the interactive-prompt bullet already names as its fallback, which reaches a reader who is there to read it. Where no user is present at all, the session asks nothing, names the whole queue by issue number in the handoff, and leaves the asking to the next session that has one. An item leaves the queue when the user answers it, and also at triage where a later change has already answered or overtaken its decision. Either way it leaves by losing the label, with the answer or the reason recorded on the issue, and the issue itself closes only where it held nothing but the question, since the queue holds issues that outlive their decision, and closing such an issue just to clear the queue discards the other work that issue tracks.

`GOVERNANCE.md` "Communicating with the User" keeps the full rules, and the `agent-conduct` and `session-handoff` Skills at `.agents/skills/agent-conduct/SKILL.md` and `.agents/skills/session-handoff/SKILL.md` in the hub, not repo-relative links since those paths are hub-local and not carried into every fleet repo, each carry it whole as a generated include and surface it at its own decision moment.

<!-- /include -->

## Running the Chain

The mechanics are the hub's `scripts/handoff.py`, run from a hub checkout, per `GOVERNANCE.md`
"Hub-Hosted Tooling". Every subcommand takes `--repo`, with no default, because an issue number
resolves in every repository and a chain read out of the wrong one is well formed. `current`,
`resume`, `chain`, and `new` each take an optional `--track` defaulting to `default`, and `link`
and `tracks` take none, since a pair of issue numbers and a whole-repository survey each name
their own scope. A session working a named lane passes `--track` on each of the four that accept
it. Omitting it does more than read the wrong chain, since `new` then files onto `default` and
comments on whatever link that lane has, closing it too where it was open.

- **`current` and `resume`** answer where a track stands, `resume` adding the body itself and an
  index of the closed links behind it.
- **`chain`** starts at one track's newest link and walks `previous=` backwards from there,
  following whatever the markers name and saying so where that leaves the track, `--grep`
  filtering the listing by a regular expression over the bodies while the notices print
  regardless.
- **`tracks`** surveys every open handoff, one carrying no metadata block included, and it is the
  one command that reports that state rather than refusing over it. That state is settled by a
  hand edit, adding the block to the issue's body or taking the label off it, since no read can
  place an issue on a track until its own block says which track that is.
- **`new`** performs as many of the chain's three steps as the track has links for. On a track
  with no link at all it creates and stops, since there is nothing to comment on or close. On one
  whose newest link is closed it creates and comments, since the close already happened. Only a
  track with an open head takes all three. **`link`** finishes a run that stopped between them,
  and separately repairs a successor whose block names no predecessor, so its own writes are the
  comment, the close, and that body edit. The edit reaches only the repair case, since `new`
  embeds the predecessor in the block at create time, so a run interrupted after the create
  already names it.

```sh
python3 scripts/handoff.py current --repo OWNER/NAME --track "<slug>"
python3 scripts/handoff.py resume  --repo OWNER/NAME --track "<slug>" --history 5
python3 scripts/handoff.py chain   --repo OWNER/NAME --track "<slug>" --grep "an escaped regex"
python3 scripts/handoff.py new     --repo OWNER/NAME --track "<slug>" --title "<subject>" \
  --body-file "<path>" --dry-run
python3 scripts/handoff.py link    --repo OWNER/NAME --new "<successor>" --previous "<predecessor>"
python3 scripts/handoff.py tracks  --repo OWNER/NAME
```

Read `--dry-run` output before the first real `new` of a session, since the run can close an issue.
It is accepted by `new` and `link`, the two subcommands that write, and by no other.

Exit `0` is success, `1` a refusal the caller can act on, a usage error included, and `2` the
command not having run to an answer, so a refusal and a failure to reach one never share a code. A
repository missing the `handoff` label is a refusal rather than a degraded empty answer, and it
names the command that applies the fleet label set except where the label read filled its window,
which is the one case where the label's absence is unproven rather than established.

Creating an issue, commenting on one, closing one, and editing a body are each outward-facing
writes. `new` creates, comments, and closes, the label riding inside the one create call rather than
being a write of its own. `link` edits a body, comments, and closes. Each of them is bound by
`GOVERNANCE.md` "Repository Boundaries and Write Safety" exactly as any other write is. Point them
at the repository `AGENTS.md` "Session Scope" sends the link to, the one holding the work the next
session resumes, and at no other. `link` also reaches an issue this chain never created, since the
caller names both numbers and its refusals ask for a block and a label to be added by hand first, so
the two issues it is given are chosen deliberately rather than swept up.

Where the caller names an issue, which is `link` alone, it reads both live before writing and
writes only what those reads returned. Every other identifier a write targets is captured from a
read in the same run, and the one identifier no read could have supplied, the new issue's own
number, is parsed from the create's confirmation rather than constructed. No write's output is suppressed or forced to success, and a close is
confirmed by reading the state back, because a write that appears to have failed may have succeeded
on the server.
