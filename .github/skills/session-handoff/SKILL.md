---
name: session-handoff
description: >-
  Writes and resumes the ptr727/ProjectTemplate fleet's session handoff, a standing issue rather
  than a file: one open issue per track per repository, carrying the `handoff` label, its body the
  current handoff, replaced in place at each session close once the outgoing body is archived on
  the same issue as a comment. Use this whenever ending a
  session or a round of work, whenever starting or resuming one, whenever asked for a handoff or
  for what the previous round did, whenever the maintainer says only "resume the handoff" or names
  none, which stands for the whole attended procedure this skill states, and whenever about to
  re-attempt something a previous round may already have tried, the moment that earns this skill,
  since a session that does not know a chain exists never goes looking for one. Every session
  writing one, except an `unattended-handoff` seat, ends in the same order: lessons recorded, the
  handoff written, the parked decision queue presented, then memories saved last. Triggers even when
  the session feels too short to be worth a handoff, because the rounds that produce nothing worth
  writing down are exactly the rounds a later session repeats. The rule is `AGENTS.md` "Session
  Scope" and the mechanics are the hub's `scripts/handoff.py`. Where a sibling skill owns the
  moment, it wins: `backlog-burndown` owns a multi-round run's reporting, `unattended-handoff`
  owns a run with no maintainer present, and `repo-worktree` owns the worktree the handoff names.
  `agent-conduct` co-fires at the parked-decision obligation and neither defers to the other.
---

# Session Handoff

## Why This Exists

The chain closes each way a handoff written to a scratch file fails. The file is not found where
the next session looks. More than one candidate file is found and nothing says which one is
current. And there is no history at all, so a later round re-runs a path an earlier round already
tried and already wrote down. The value of a handoff is precisely the record of what has already
been attempted. The shape the handoff actually had, one overwritten file on one machine, is a shape
that cannot hold it.

The chain closes each of those rather than making it impossible. The label and the metadata block
are what make a handoff findable from any machine. The one-open-per-track invariant is what answers
which handoff is current. And every archived round stays readable on the same issue to every
session after it. Where the invariant is broken the chain refuses and says so. That is a state a
reader can act on, rather than one that reads as an answer.

The rule is `AGENTS.md` "Session Scope", which keeps it, and it names the handoff's sections and
sets the size rule over them. This skill is the judgment that rule cannot state. It says what
actually earns a place in each of those sections, and what belongs somewhere durable instead. It
also says how to read a handoff without trusting the parts of it that have gone stale.

## What a Handoff Is, and Is Not

- **It points at the durable record rather than restating it.** A defect becomes an issue, a rule
  becomes documented rule text, and a lesson becomes governance prose, per `GOVERNANCE.md` "Durable
  Knowledge and Self-Improvement". The handoff names each of those in a line and a link. The one
  exception is what a round attempted and what that cost, which has no home outside the chain. So
  there the chain is the durable record rather than a pointer to one.
- **It records work to do next, and it is not itself backlog work.** It carries none of the labels
  that classify work to be done. Every enumeration that ranks or counts the open backlog filters
  the `handoff` label out. That is because a track in use always has an open issue, and counting
  it inflates the backlog by one per track forever.
- **It is not a place to ask a question.** A question waiting on the maintainer is an issue carrying
  the `decision` label. The handoff records that queue. "The Parked Decision Queue" below requires
  the session to put the questions themselves to the user in the same act. That is because a
  question recorded and never asked is the failure that section exists for.

## The Chain

- **One open handoff issue per track per repository.** A track is a short kebab-case slug naming a
  lane of work, and a session that names no track uses `default`. Parallel lanes each name their
  own, so none reads another's state as current. The issue is created when the track starts and
  closed when its work is complete. It is never closed and replaced per session.
- **A platform lane is for work that needs its platform.** A platform track is one kept for a single
  host platform, such as `wsl` or `windows`. It takes an issue only when its defect reproduces, or
  its fix can be verified, on that platform alone. It also needs that no Linux host outside WSL can
  both fix the issue and verify the fix. An issue meeting each condition goes to its platform's lane. Any
  other issue goes to the ordinary backlog, since a lane costs a separate chain and a host-bound
  session. A defect in WSL interop, the `WSLInterop` binfmt handler, `/mnt/<drive>` paths, or
  `wslpath` reproduces only inside WSL. So does running a Windows `.exe` from Linux, although WSL
  runs on Windows. Behavior that differs under Windows PowerShell 5.1 or `winget`, or in Windows
  path and ACL semantics, reproduces only on Windows. Each of these defects still goes to the
  backlog when a Linux host outside WSL can both fix it and verify the fix. A path-translation
  fix made and unit-tested on a Linux host outside WSL is one such case. An issue in
  systemd user scopes, apt, a bash script, or a `pwsh` script run in a Linux container gets no lane
  by its topic. The conditions decide it like any other issue. Where only part of an issue needs
  a platform, the session finding it files each such part as its own issue for its platform's lane.
  The original is narrowed to the rest, or closed in favor of those issues where nothing is left. A
  session that may not write issues names the unsplit issue in its report or handoff instead. An
  issue for a platform whose lane was closed reopens that lane with `new`, which files a new issue
  continuing from the closed one. A platform lane is closed with `close` once none of its next steps
  needs its platform. Before that, each remaining step with no issue of its own is filed as one. The
  closing comment carries the outcome, as `AGENTS.md` "Session Scope" closes a track whose work is
  complete.
- **The title is for humans**, shaped `Session Handoff [<track>]: <subject>`. `new` composes it
  from the track and the subject it is given at every round. So what a session writes is the
  subject alone. Nothing parses the title, so a maintainer is free to rename one.
- **The issue is machine-readable from one HTML comment on the body's last line**, rendered
  invisible, so a retitled or hand-edited issue still reads:
  `<!-- handoff: v1 track=<slug> round=<n> previous=<issue number or none> -->`. The round
  advances at each session close. `previous=` names the closed issue the track continues from, and
  stays fixed while the round moves. Each archived round ends in a line of its own,
  `<!-- handoff-round: v1 track=<slug> round=<n> closed=<YYYY-MM-DD> -->`, which is how a reader
  tells a round from an ordinary comment.
- **A session close runs in one order.** First append the outgoing body to the issue as a comment
  headed with its round and close date. Then replace the body and advance the round. Archiving
  first means a failure between the two leaves the outgoing round in both places rather than in
  neither. A rerun of `new` then finds it archived and only replaces the body.
- **The invariant a reader checks** is exactly one open issue carrying `handoff` for a given track in
  a given repository. It is read from each issue's metadata block rather than from the label. That
  is because an issue carrying the label and no block belongs to no readable track, and blocks the
  count rather than joining it. Two or more on one track is a defect to report, never one to
  resolve by picking.
- **Zero open on a track is two different states.** The track has never had a handoff, or it was
  closed when its work was complete. In the second state `new` continues from the newest closed
  issue on the track, naming it as `previous=` and starting one round above it. That is because
  starting a second chain beside one that exists orphans every round already written. In the first
  it starts at round 1 with `previous=none`.
- **Whatever open issue a track holds is its standing issue.** That includes one whose body has
  never been replaced in place, and whose `previous=` names a chain of closed issues behind it.
  `new` archives its body and edits it like any other, since it is the one open `handoff` issue on
  the track. The walk still reaches the closed issues through `previous=`.
- **The handoff lives in the repository holding the work the next session resumes.** A session that
  spanned repositories writes it there, rather than filing one handoff per repository. It names the
  others in the state section "What Goes in the Body" below describes.

## What Goes in the Body

A fixed section order and fixed section names, the bold name opening each of items 1 through 8
below being the heading the handoff writes. Item 9 is an HTML comment the tool writes rather than a
section anyone types. Fixed names are what let a reader find a given fact in the same place every
time. They are also what let one round's section be compared against another's. `AGENTS.md`
"Session Scope" sets the size rule, and it is per section. An entry earns its place by being
specific enough to change a later session's behavior. A section ranks what it keeps and drops
whatever does not meet that bar. `handoff.py` warns above 12 KB and refuses above 60 KB. The
refusal is a backstop against a body GitHub would reject. The warning is a hint that the
per-section rule broke several sections earlier. Neither is the rule.

1. **Next steps, in priority order.** The most valuable section, and first for that reason. Each item
   names what to do and what "done" looks like. At most a handful, ranked. A step blocked on
   something says so and names the blocker below.
2. **External blockers.** Anything the next session cannot resolve on its own. That covers a
   maintainer decision, a third-party quota or outage, an upstream release, a credential, and a
   reviewer bot that is not running. Each names who must act, what unblocks it, and what is safe to
   do meanwhile.
3. **Internal dependencies.** The ordering constraints among the next steps, each stated as the
   dependency and its reason. That lets a later session re-rank when circumstances change, rather
   than following an order it cannot audit.
4. **State.** Branch, worktrees and whose they are, open pull requests, and what merged. Whether a
   release was dispatched, and whether the primary checkout is clean. Any other repository the round
   touched, named so a resume knows to look there too. These are facts a resume re-derives, listed
   so the resume knows what to re-derive.
5. **The parked decision queue.** The count and the ranked list "The Parked Decision Queue" below
   requires, which also requires the questions to be presented in the same act rather than only
   recorded.
6. **What the last round did.** Issues closed and filed, pull requests landed, and the peripheral
   issues filed along the way.
7. **What not to repeat.** The section a file could never carry. It holds paths explored that led
   nowhere, approaches that failed and why, and problems discovered during execution. It also holds
   the cost of each where it is known. An entry here is a claim about a specific attempt rather than a general lesson.
8. **New learnings.** Only what is not already durable somewhere else, each with a pointer to where
   it was recorded. A lesson that belongs in governance goes to governance and appears here as one
   line and a link.
9. **The metadata block**, which `scripts/handoff.py` writes and no author types.

Sections 7 and 8 are what the chain exists for, and they are also the most likely to be padded.

A handoff issue outlives the session that wrote it, and is readable by everyone who can read the
repository. On a public one that is everyone. So it quotes no data observed in the maintainer's
environment, per `GOVERNANCE.md` "Representative Data in Agent-Authored Text". That rule binds on a
private repository exactly as it does on a public one, and the visibility only changes who the
audience is. The routine case here is an absolute home path, since a handoff naturally wants to
name a worktree. The rule for it is to name the worktree by its branch and its repository-relative
role, rather than by its path. A next session re-derives the path from `git worktree list` anyway,
which section 4 already tells it to do.

## Resuming

**Re-derive live state, and do not trust the handoff's copy of it.** Branches, worktrees, open pull
requests, and issue counts are read again from git and from GitHub at resume. The handoff's state
section is the pointer to what to read rather than the answer. `AGENTS.md` "Session Scope" already
says stale context is worse than absent. A handoff is context by construction, so this is that rule
applied to the one artifact built to outlive the session that wrote it.

Read the issue's other comments too, since `resume` prints only the body and an index of the
archived rounds. And a parking comment or a closing session's answers land in the comments between
them. Read them with the command below. It leaves out each archived round, since a long track holds
many of them in full. It keeps only visible comments from an account with write access, since anyone
can comment on a public issue. A comment counts as a round where one of its lines is a round mark:

```sh
gh issue view "<n>" --repo OWNER/NAME --json comments --jq '.comments[] | select((.authorAssociation | IN("OWNER", "MEMBER", "COLLABORATOR")) and (.isMinimized | not) and (.body | test("(^|\n)<!--[ \t]*handoff-round:[ \t]*v1[ \t][^>]*-->[ \t\r]*(\n|$)") | not)) | "\(.createdAt)\n\(.body)\n"'
```

Read the chain before re-attempting anything. `resume` prints the current body and indexes the
archived rounds behind it. `chain --grep` searches every round it walks for a regular expression.
That is how a session answers whether a path has already been tried without reading every round.
The points below bound that answer. The walk starts at one track's issue, and lists its body and
then its archived rounds newest first. It then follows the block's `previous=` to the closed issue
the track continues from. That is normally that one lane, and is in fact whatever the markers name.
So every round prints its own track, and an issue on another lane shows up rather than passing
unseen. The walk stops at `--limit` and names the round it stopped short of, so a capped search
never reads as an exhaustive one. And `--grep` takes a regular expression, so a literal string
carrying a regex character is escaped or it matches something other than what was typed. On a
track with no open handoff `resume` refuses rather than printing an empty body. `chain` falls back
to that track's newest closed issue there. So the read side of a closed track is `chain` rather
than `resume`.

Re-derive a count rather than copying one, and read it with an explicit page size. `gh issue list`
returns 30 rows unless told otherwise. A truncated count reads exactly like a repository with 30
issues. That is worse than an absent count, because it gets stated. The same holds at any page
size. So where a read returns as many rows as its limit, raise the limit and read again until it
returns fewer.

## The Attended Session

A maintainer resuming work says little, often only "resume the handoff", and that phrase stands for
the whole procedure below. Run every step without being reminded of any of them.

1. **Pick the link.** Where the maintainer names an issue, take it. Where none is named, read
   `python3 "$handoff" tracks --repo OWNER/NAME --json`. It prints each open handoff's number,
   title, track, labels, and updatedAt. It reads them from the repository's issue connection
   rather than the search index, so a link written moments ago is already read correctly.
   `$handoff` is the hub checkout path "Running the Chain" sets. A nonzero exit, including the
   refusal over a read window that filled, means the list is not whole. So say so and ask rather
   than picking from it. Take a link carrying `blocked` first, newest update first among them.
   That is because its blocker is a decision only the maintainer can make, and the maintainer is
   now present. Otherwise take the newest update among the rest. An `auto-*` link not carrying
   `blocked` is skipped unless named, since an `unattended-handoff` worker may hold it right now.
   Where one is named, confirm with the maintainer that no unattended run is live before working
   it. Where every open link is skipped,
   say so and ask the maintainer which to take. Say which link was picked in one line before
   anything else, so a wrong pick costs one reply rather than a round.
2. **Resume it** per "Resuming" above, comments included, re-deriving live state rather than
   trusting the body.
3. **Ask what it is blocked on first.** Where the link carries `blocked`, the parking comment names
   a `decision` issue. Read it first, since the maintainer may have answered it there already.
   Otherwise that question goes to the maintainer before any work starts, since the rest of the
   link waits on it. Once answered, record the answer on the decision issue, take its `decision`
   label off, and close it where it held nothing but the question. Leave `blocked` on the handoff
   while this session works the link, so a running `unattended-handoff` loop does not pick it up
   underneath the session. Settle it in step 6.
4. **Work the next steps** in a worktree of the session's own, per `repo-worktree`. Drive each pull
   request with `drive-pr` to a mergeable develop -> main promotion pull request. The phrase
   already states that target, so `drive-pr` does not ask how far. Merging that promotion pull
   request and dispatching a release stay `merge-and-release`. Each needs an explicit go-ahead,
   asked for as a prompt whose option names the action.
5. **Ask every question as a dialog.** Where the interface has a prompt mechanism, each decision is its own
   question with its answers as the options. There the recommended one comes first and is marked as
   the recommendation, and every one carries its reason, per "The Parked Decision Queue" below. The numbered list is the fallback where no prompt exists.
6. **Close the session** per "Closing a Session" below, which records lessons, writes the next round
   with `new`, presents the parked decision queue, and saves memories last. Where the issue carries
   the `blocked` label left on in step 3, take it off once the next round is written. Remove it with
   `gh issue edit "<n>" --repo OWNER/NAME --remove-label blocked`, since the round it marked is
   archived. An `auto-*` lane differs in one way, since it holds one piece of work. Where that work
   is done, end the track with `close` instead, the outcome as its comment, as `unattended-handoff`
   closes such a lane. Where work remains, the next round's next steps name the decision issue and
   the answer. In that case, taking `blocked` off hands the lane back to the unattended loop. Either way the
   parked decision queue is still presented.

## Closing a Session

Every session that writes a handoff ends in this order, except an `unattended-handoff` seat, which
closes as that skill states. Each step leaves the chain whole if the session is interrupted after
it.

1. **Record what was learned** per `GOVERNANCE.md` "Durable Knowledge and Self-Improvement", which
   says where a lesson goes. Do it before the handoff is written, so its "New learnings" section
   points at a record that exists.
2. **Write the handoff** with `new`, per "Running the Chain" below, or end the track with `close`
   where its work is done. The handoff is written before the closing questions are put. That is
   because a prompt blocks until someone answers, and an unanswered one must not cost the round its
   handoff.
3. **Present the parked decision queue** in the same act, per "The Parked Decision Queue" below.
   Record each answer given on its issue, where one was asked and answered. Where a handoff was
   written, comment those answers onto its issue, with the queue's count and list after them. That
   is because the body keeps the count it was written with, and a resume reads the issue's comments.
4. **Save memories last.** Where the host keeps a per-user memory, what goes there is the
   environment-specific nuance "Durable Knowledge and Self-Improvement" leaves to memory. A quirk of
   this machine or this account is one such nuance. What goes there is saved as the session's final
   act. Never
   the round's state, which the issue holds, and never a lesson, which step 1 already recorded.

## The Parked Decision Queue

The rules below are `GOVERNANCE.md` "Communicating with the User", carried whole so this skill works
in isolation. Some of them state the obligation: the one opening
"A question filed as an issue is parked rather than asked" and the one opening
"The session that writes a handoff presents the parked queue in the same act". Others state the
form the questions take. The one opening "Ask every question through the interface's prompt, never in prose"
also names the numbered list as the fallback where no prompt exists. The one opening
"Raise work blocked on the user as a direct interactive prompt" shapes the options. And the one
opening "Lead every choice with a recommendation and its reason" binds every question put either
way. A reader who stops after the obligation has it with no shape to put it in.

Recording the queue is not asking it. The handoff's "The parked decision queue" section is the
recording half. What the other half is depends on what the session can reach. It is a prompt where
one is available, and the numbered list where a user is present and no prompt is. It is nothing at
all where no user is present. In that case the handoff names the whole queue by issue number, and
the asking falls to the next session that has one. The rules below settle which case applies.

<!-- include: GOVERNANCE.md > Communicating with the User -->

- **Reference every pull request as a clickable link.** When you mention a PR on a surface that renders Markdown (chat, a summary, a report), render it as a Markdown link to the PR. Write it as `[#N](https://github.com/OWNER/REPO/pull/N)`, never a bare `#N`. The same applies to issues and commits. **The form follows the surface.** Some surfaces link neither a Markdown link nor a bare URL, an interactive prompt's question and option text among them. Pasting a full URL into one of those does not rescue it, since the reader gets a string to copy. That is the outcome this rule exists to prevent. There the reference is a bare `#N`. The clickable link goes in the message that comes **before** the prompt rather than merely alongside it. That is because the prompt blocks on an answer, and a message emitted after it is read once that answer is already given. That is the one moment the link is no longer any use. The test is whether the reader can click it where it is read, not whether it was written in the syntax that works elsewhere.
- **Put a command the user must run in the question's own text.** Where a question asks the user to run something, the question carries the exact command, complete and ready to paste. One example is a command the agent cannot run itself because the command prompts for a password. A message before the prompt may repeat the command, but the question never points at that message. A question pointing at it can leave the reader to find the command among a long run of earlier output. In the clickable-link bullet above, only the rendered link moves out of the prompt. A command needs no rendering, since the prompt shows it as plain text the reader can copy.
- **Ask every question through the interface's prompt, never in prose.** When you need the user to decide, answer, or approve anything, put it to them through the interface's own prompt mechanism. Do so whether or not work is blocked on it. An offer to do more work ("want me to file that?") is a question like any other. A question written into a message is lost in the report around it, however short it is. One closing a long report sits where the reader is least likely to reach it. Where no prompt mechanism is available, the questions go as a numbered list opening the message rather than closing it. The user can then reply per number. This bullet settles how a question is put once it is asked. Whether a question is asked now or recorded for later is the parking bullet's to settle. That is the one opening "A question filed as an issue is parked rather than asked".
- **Raise work blocked on the user as a direct interactive prompt.** When progress needs a decision, an authorization, or an answer only the user can give, ask for it at the point the work stops. Ask through the interface's own prompt mechanism. Never leave it as prose in a summary. A handoff buried in a paragraph is a handoff that did not happen. That is because a summary reads as a report of finished work. It is also because the one line still waiting on the user is the easiest in a summary to skim past. The blocked item is the message, not a closing remark on a message about something else. **The options offered are the actions themselves.** The one that unblocks the work names the action it authorizes ("squash and merge it"). Selecting it is then the go-ahead rather than a note to act on later. Offering only ways to wait is the same failure in interactive clothing. That is because a prompt whose every choice is inaction reports the block rather than clearing it. Where the agent may not perform the authorized action itself, the option says who does it. Where no interactive prompt is available, the numbered list the bullet above names is the fallback.
- **Lead every choice with a recommendation and its reason.** Wherever the user is asked to choose, in a prompt or in a numbered list, the option the agent recommends comes first. It is marked as the recommendation, and every option states the reason for it. A user who takes the recommendation then reads one line, and one who does not sees what the other options trade away. A question with no defensible recommendation says so rather than inventing one. Where an open question has no options and the agent does have an answer, that answer and its reason go in the question's text. They never go in as an invented option.
- **A question filed as an issue is parked rather than asked, and it stays owed.** Where the work cannot continue without the answer, the interactive-prompt bullet governs and the question is asked at the point the work stops. Wherever the question is recorded rather than asked, recording it is the right thing to do, and recording it is still not asking it. That holds whether the work can continue without the answer or the question was asked once and deferred. So the issue carries the fleet's `decision` label. When the filing session knows the choices the question is between, the issue states them. The label and the stated choices are what let a later session, one that was not there when the issue was filed, find the question. That session can then put the question to the user without inventing its answers. **The parked queue is the failure, not the parking.** An issue holding a question the user has never seen reads to every later session as tracked work rather than as a block. So each session files correctly and moves on. Presenting the accumulation is the step nobody owns.
- **The session that writes a handoff presents the parked queue in the same act.** This binds at the moment the session writes the handoff that `AGENTS.md` "Session Scope" defines, rather than at the moment the session ends. That is because a session also ends by interruption, where no agent acts at all and no rule reaches it. The session enumerates this repository's open issues carrying that label, and states how many issues the queue holds. It puts the highest-ranked of them to the user as questions, **one question per parked decision, carrying that decision's own answers as its options**. That is the interactive-prompt bullet's own shape applied per decision, rather than a single prompt whose options are topics. An issue that states no choices is put as the open question it is, never as invented options. That is because the label marks every decision waiting on the user, rather than only the ones filed under this rule. It is also because the issues that already carry the label predate the rule. Rank them longest-waited first. Every issue records how long it has waited, so any session can reproduce that order. Promote one ahead of that order where it blocks work in flight, and say in the handoff that it was promoted. That is because two sessions order the same queue differently where the key is subjective and nobody records it. Where more are parked than one round of questions can carry, the stated count still covers every one of them. The handoff names the remainder by issue number in that order, a list of numbers rather than of questions. That is what keeps the handoff inside the size rule that `AGENTS.md` "Session Scope" sets for it. **A count nobody states is a queue nobody can see**, which is how a backlog reported as healthy hides the questions inside it. Where a user is present but no prompt mechanism is available, the questions go as a numbered list. That is the fallback the interactive-prompt bullet already names, and it reaches a reader who is there to read it. Where no user is present at all, the session asks nothing and names the whole queue by issue number in the handoff. It leaves the asking to the next session that has one. An item leaves the queue when the user answers it, and also at triage where a later change has already answered or overtaken its decision. Either way it leaves by losing the label, with the answer or the reason recorded on the issue. The issue itself closes only where it held nothing but the question. That is because the queue holds issues that outlive their decision. It is also because closing such an issue just to clear the queue discards the other work that issue tracks.

`GOVERNANCE.md` "Communicating with the User" keeps the full rules. The `agent-conduct` and `session-handoff` Skills at `.agents/skills/agent-conduct/SKILL.md` and `.agents/skills/session-handoff/SKILL.md` in the hub each carry it whole as a generated include. Each surfaces it at its own decision moment. Those paths are not repo-relative links, since they are hub-local and not carried into every fleet repo.

<!-- /include -->

## Running the Chain

The mechanics are the hub's `scripts/handoff.py`, run from a hub checkout, per `GOVERNANCE.md`
"Hub-Hosted Tooling". Every subcommand takes `--repo`, with no default. That is because an issue
number resolves in every repository, and a chain read out of the wrong one is well formed.
`current`, `resume`, `chain`, `new`, and `close` each take an optional `--track` defaulting to
`default`. `tracks` takes none, since a whole-repository survey names its own scope. A session working a named
lane passes `--track` on each subcommand that accepts it. Omitting it does more than read the wrong
chain, since `new` then archives and rewrites whatever `default` holds, and `close` closes it.

- **`current` and `resume`** answer where a track stands, `resume` adding the body itself and an
  index of the archived rounds behind it.
- **`chain`** lists one track's rounds newest first: the body, then each archived round, then the
  closed issue `previous=` names and its own rounds. It follows whatever the markers name, and says
  so where that leaves the track. `--grep` filters the listing by a regular expression over the
  round texts, while the notices print regardless.
- **`tracks`** surveys every open handoff, one carrying no metadata block included. It is the one
  command that reports that state rather than refusing over it. That state is settled by a hand
  edit, adding the block to the issue's body or taking the label off it. That is because no read
  can place an issue on a track until its own block says which track that is. With `--json` it
  prints the same rows as an array of number, title, track, labels, and updatedAt. The track is
  null for an issue whose block is absent or unreadable. That output is what a picker ranks by.
- **`new`** is the one way a handoff is written. On a track with no open issue it creates one and
  stops. On a track with one it appends the outgoing body as an archived round, then replaces the
  title and body and advances the round. Either way it prints the issue number. A rerun after a run
  that stopped between the two finds the round archived and only replaces the body. A rerun after a
  run that finished finds the body current and writes nothing.
- **`close`** is the one way a track ends. It comments the outcome given in `--body-file` on the
  track's issue, then closes it, and a rerun after the comment landed only closes.

```sh
handoff="<hub-checkout>/scripts/handoff.py"   # in the hub itself, scripts/handoff.py
python3 "$handoff" current --repo OWNER/NAME --track "<slug>"
python3 "$handoff" resume  --repo OWNER/NAME --track "<slug>" --history 5
python3 "$handoff" chain   --repo OWNER/NAME --track "<slug>" --grep "an escaped regex"
python3 "$handoff" new     --repo OWNER/NAME --track "<slug>" --title "<subject>" \
  --body-file "<path>" --dry-run
python3 "$handoff" close   --repo OWNER/NAME --track "<slug>" --body-file "<path>" --dry-run
python3 "$handoff" tracks  --repo OWNER/NAME [--json]
```

Read `--dry-run` output before the first real `new` of a session, since the run replaces a body. It
is accepted by `new` and `close`, the subcommands that write, and by no other.

Exit `0` is success, and `1` is a refusal the caller can act on, a usage error included. Exit `2`
is the command not having run to an answer. So a refusal and a failure to reach one never share a
code. A fleet repository missing the `handoff` label, one the hub's `registry/repos.json` lists, is
a refusal rather than a degraded empty answer. The refusal names the command that applies the fleet
label set, except where the label read filled its window. That is the one case where the label's
absence is unproven rather than established.

A fork under the registry's owner that the registry does not list can host a chain. One kept for an
upstream contribution per `upstream-contribution-workflow` is an example. The chain keeps a session's
state on an unlisted fork under the owner without the fork taking on any fleet configuration.
Missing the label there, a read prints a warning and answers as an empty chain does, and `new`
refuses until it is given `--create-label`. With that flag, `new` creates the one `handoff` label, confirms
it, and then files the track's issue. Issues turned off stop it before any write, naming the command that turns them on.
Never apply the fleet label set to such a fork. A repository under another owner, or an
unregistered one of the owner's that is not a fork, refuses before any write. `new` and `close`
refuse both whatever the label state, since a label on such a repository opens no write there. That
refusal bounds no read.

Creating an issue, commenting on one, editing one, and closing one are each outward-facing writes.
`new` creates, or comments and edits, the label riding inside the one create call rather than being
a write of its own. `new --create-label` adds one write ahead of those, the label itself. `close`
comments and closes. Each of them is bound by `GOVERNANCE.md` "Repository Boundaries and Write Safety"
exactly as any other write is. Point them at the repository `AGENTS.md` "Session Scope" sends the
handoff to, the one holding the work the next session resumes, and at no other.

Every identifier a write targets is captured from a read in the same run. The one identifier no read
could have supplied, a new issue's own number, is parsed from the create's confirmation rather than
constructed. The body archived is the one read immediately before the archive. The body is read
again immediately before it is replaced, so an edit landing in between refuses rather than being
overwritten. No write's output is suppressed or forced to success. An edit and a close are each
confirmed by reading the issue back. That is because a write that appears to have failed may have
succeeded on the server.
