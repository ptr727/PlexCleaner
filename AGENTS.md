# Instructions for AI Coding Agents

**ProjectTemplate** exists for agent enablement across a fleet of related projects, so an agent works over all of them autonomously and productively, at repeatable quality, inside guardrails that keep the autonomy from doing harm. It holds the portable rules those projects follow, a machine-readable ground-truth spec ([`spec/`](./spec/)), a registry of the projects ([`registry/repos.json`](./registry/repos.json)), and an audit-agent instruction set ([`AUDIT.md`](./AUDIT.md)). It ships no sample application code.

This file is the entry point every coding agent reads first, and it holds only three things: the bootstrap that says where the canonical rules live and which procedure to follow for the state this repository is actually in, the rules for managing context and delegation, which apply to every task, and a map of where every other rule lives. The rule text itself is in [`GOVERNANCE.md`](./GOVERNANCE.md), one section per topic. Code style lives in [`CODESTYLE.md`](./CODESTYLE.md) (a General section plus per-language sections for .NET and Python), and the CI/CD workflow contract in [`WORKFLOW.md`](./WORKFLOW.md).

Treat this file and `GOVERNANCE.md` as authoritative for cross-cutting rules, and do not restate their rules elsewhere. A project's **project-specific conventions and public-API/behavioral contracts** (e.g. a "Library API Conventions" section) live in that project's own `AGENTS.md`, **not** in [`.github/copilot-instructions.md`](./.github/copilot-instructions.md), because that file targets GitHub Copilot / VS Code specifically, while this file and `GOVERNANCE.md` are the agent-agnostic ones every coding agent is directed to read, so any rule a reviewer must honor has to live in one of those two files to be provider-independent.

**PlexCleaner** is a .NET 10 CLI utility that optimizes media files for Direct Play in Plex, Emby, and Jellyfin. It orchestrates FFmpeg, HandBrake, MkvToolNix, MediaInfo, and 7-Zip, and ships standalone executables plus a multi-architecture Docker image. The repository also contains the xUnit test suite and the external-corpus regression tooling. Read [`ARCHITECTURE.md`](./ARCHITECTURE.md) before changing processing, sidecar, media-tool, or language-tag code. Read [`OPERATIONS.md`](./OPERATIONS.md) for local verification, release runbooks, and regression-corpus requirements.

## Fleet Bootstrap

This repository is governed by a shared template, and the canonical rules, machine-readable spec, and procedures live in `github.com/ptr727/ProjectTemplate`, the repository these rules call the hub. Fetch that repository before acting on anything about conformance, carried content, repository settings, or standing a repository up, because a carried copy here can be stale or absent and the hub is the only authority on what this repository is supposed to hold. This section is byte-locked across every repository in the fleet, so it reads identically wherever it is found, and it is the entry point whenever nothing else present says where the rules are.

Route by what this repository currently holds rather than by what it is expected to hold, since the two differ exactly when this section matters most.

- **No repository yet, or a local tree with no remote.** Follow the hub's `STANDUP.md` from section 0. That file is hub-only and deliberately not carried, because a repository needing it cannot be relied on to hold a current copy. Note that nothing in it creates the GitHub repository, which is an outward-facing write requiring explicit permission, so section 0A is the list handed to the maintainer before anything else starts.
- **A repository with no carried instruction set, or a partial one.** Carry the baseline per the hub's `STANDUP.md` sections 1A and 2, which resolve what this repository is owed from its declared types and workflow model. Absent files are not drift to re-vendor, they are a baseline that never arrived, and the two are fixed differently.
- **A repository with the instruction set, current or stale.** Follow the hub's `RESYNC.md`, which runs `AUDIT.md` end to end for the findings and then applies each one in an order that matters, since the rules govern what comes after them, a deletion must precede the re-vendor that would otherwise refresh the file, and only some findings are mechanically detectable at all. An audit that reports drift and stops is half the procedure.
- **A repository that believes it is conformant.** Run the audit anyway and commit the report, because conformance asserted without a report is conformance nobody can check. This is the same procedure as the case above and is listed separately only because it is the one most often skipped.

Three rules bound every path above. **Read the hub's `main` branch as ground truth**, since that is the promoted and gated state, and read `develop` only to detect divergence. **Reach the hub as a checkout of your own and fetch it immediately before reading it**, because a clone is whatever it last fetched rather than the branch it names, and work only in that checkout rather than in one that another task is using, per [`GOVERNANCE.md`](./GOVERNANCE.md) "Repository Boundaries and Write Safety" and "Hub-Hosted Tooling". And **the audit is read-only**: it produces a report and never edits the repository it measures, so a fix is a separate, reviewable change.

## Context and Delegation Discipline

An agent session is billed on the context it carries, not the work it does. Every request re-reads the whole accumulated context, so a token added early is paid for again on every request that follows, and a long session bills its last task for every earlier one. These are cost rules. None of them licenses doing less work, skipping verification, or shipping something unreviewed.

### Session Scope

- **One deliverable, one session.** A session covers one branch and one deliverable, and ends when that work merges. A multi-step task is one deliverable and stays in one session. Two unrelated tasks are two sessions even when they run back to back.
- **End a session at any of these, without being asked:** the branch changes, the pull request merges, or the next task is unrelated to the last. A review round is none of them. A loop still producing findings is the deliverable in progress, and a round count is not a reason to leave one open.
- **Hand off in a file, never in context.** Close a session by writing at most 2 KB to a scratch file: branch, pull request link, what is done, the next command. A summary held in context is re-billed until the session ends, and a summary on disk is read once by whoever needs it.
- **Re-derive state, do not carry it.** "This session already has the context" is the signal to split, not to continue. Context that has gone stale is worse than absent, because a file read hundreds of requests ago no longer describes the file.
- **Compaction is a fallback, not the strategy.** It restarts context from a floor and climbs again, where a fresh session starts from zero.

### Reading

- **Map a large file, then read one range.** For anything over about 200 lines, list the headings with `grep -n '^## '` first and read only the range the task needs. Read the section, not the file that contains it.
- **Prefer an in-place edit to a whole-file rewrite.** Rewriting a file bills its full content again on top of what the read already cost.

### Commands

- **Bound output at the source.** Write every command so its output is the answer, not the haystack: a `--jq` projection on an API call, a count or files-only flag on a search, a summary flag on a diff, an explicit cap on anything unbounded. A command whose output you then skim is a command that should have been narrower.
- **Keep a long query in a file, not in the command.** A heredoc re-typed on every call costs its own length in context each time, often more than the answer it retrieves.

### Delegation

- **Delegate exploration, keep judgment.** A subagent starts from an empty context and returns only its conclusion, so a wide search, a multi-file audit, or a "which of these is affected" question costs a fraction of the same work inline. Delegate when the finding compresses to a short answer, and stay inline when the intermediate detail drives the next edit.
- **Match the model tier to the judgment, not to the diff size.** Mechanical work (a known-shape edit repeated across files, an extraction, a status check, a lint fix) runs on the cheapest model that does it correctly, at the lowest reasoning effort that holds. State the tier in the delegation itself rather than accepting the default. A change to a gate, a ruleset, a release condition, or a carried governance section is a design change however small it looks.
- **Never tier down the seat holding the judgment.** Governance wording, spec logic, rulesets, repository visibility, and the decision to decline a review finding are fleet-wide and durable when wrong. Tier the subagents, not the main thread.
- **Brief a subagent so it never needs a governance file.** A subagent inherits no context, so anything it must honor has to be in its prompt. Reading `GOVERNANCE.md` to find out costs it the same tokens the main thread would have paid. Brief on this shape:

```text
Task: <the one question or edit, stated so the answer compresses>
Paths: <exact files or globs - never "find the relevant files">
Rules that bind this task: <the specific rules, quoted, not a pointer to a doc>
Return: <the shape of the answer - a list, a diff, a yes/no with evidence>
Bounds: <what not to touch, and what to do when a rule looks incomplete>
If a rule you were given does not cover what you find, stop and report it. Do not guess, and do not read a governance file to resolve it.
```

- **Wait in a background process, not in a poll loop.** A review or CI wait is a sequence of near-identical requests, each billed for whatever context it happens to carry. Run the wait as one backgrounded command that returns when the condition is met.
- **A wait separates three outcomes, and says which one it reached.** The condition was met, it has not been met yet, and the wait cannot reach it at all are three different results, and a backgrounded wait that emits nothing renders all three identically. Run the command once in the foreground and read its output before backgrounding it, because a wait is only as good as the command inside it, and an unsupported flag on the installed tool version exits non-zero with an empty stdout that every naive test reads as "nothing yet". Never let a fallback stand in for a failed command, since `|| echo '[]'`, `|| true`, and `2>/dev/null` convert an error into that same reading, which is the suppression the write-safety rules already forbid on a mutation. Make the wait emit on failure as loudly as on success, so silence means "still running" and nothing else, and bound it, so a condition that is never coming ends in a report rather than in another wait.

## Where the Rules Live

Every rule below is a level-two section of [`GOVERNANCE.md`](./GOVERNANCE.md). Read the section the task needs.

| Working on | Section |
| --- | --- |
| Why the rules are shaped this way | `Foundational Principles` |
| Recording a durable lesson or updating governance | `Durable Knowledge and Self-Improvement` |
| Any push, API mutation, comment, label, or merge, or which checkout the work happens in | `Repository Boundaries and Write Safety` |
| Quoting data into a comment, commit, test, or doc | `Representative Data in Agent-Authored Text` |
| Committing, signing, rebasing, force-pushing | `Git and Commit Rules` |
| Branch choice, promotion, keeping branches in sync | `Branching Model` |
| Releasing, version bumps, publishing | `Release Model` |
| A live config repo rather than a code repo | `Operational Repositories` |
| Onboarding a repo or running a conformance sweep | `Repository Onboarding and Conformance` (hub only, not carried) |
| Running a fleet gate, the review digest, or the config script | `Hub-Hosted Tooling` |
| Writing a commit message or pull request title | `Pull Request Title and Commit Message Conventions` |
| Any prose, comment, doc, or line-ending change | `Documentation Style Conventions` |
| Proving work actually happened | `Verification Discipline` |
| Requesting, answering, or closing a review | `PR Review Etiquette` |
| Reporting progress or asking the user something | `Communicating with the User` |
| Editing a workflow YAML file | `Workflow YAML Conventions` |
| Choosing an OS, runtime, or toolchain target | `Supported Development Platforms` |
| The devcontainer | `Devcontainer` |
| Editor settings and tasks | `Editor and Tasks` |
| The About panel, description, or repo toggles | `Repository Details` |
| Where a file belongs in the tree | `Repository Layout` |
