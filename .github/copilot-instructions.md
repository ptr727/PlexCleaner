# Copilot Instructions

Repository conventions for GitHub Copilot (and any other AI agent reading this file).

The **canonical guide is [AGENTS.md](../AGENTS.md)** at the repo root - read it first, including the [PR Review Etiquette](../AGENTS.md#pr-review-etiquette) review-loop contract this file's runbook implements. This file is intentionally narrow: commit/PR-title conventions (summarized inline so VS Code's commit-message and PR-title generators have them) plus the GitHub Copilot Review Runbook.

For code-style rules, see [`CODESTYLE.md`](../CODESTYLE.md) at the repo root.

For PlexCleaner's architecture, processing pipeline, and design patterns, see [../ARCHITECTURE.md](../ARCHITECTURE.md).

Do not duplicate language-specific rules here. **Project-specific conventions and API/behavioral contracts also belong in [AGENTS.md](../AGENTS.md), not here** - this file is intentionally limited to the inline commit/PR-title summary and the GitHub Copilot Review Runbook. Non-Copilot agents (Claude Code, Codex, Cursor, ...) are not directed to this file and don't read it by default, so any rule a reviewer must honor has to live in `AGENTS.md` to be provider-independent.

## Commit Messages and Pull Request Titles

Summarized for VS Code's generators; the full rules, rationale, and examples are in [AGENTS.md "Pull Request Title and Commit Message Conventions"](../AGENTS.md#pull-request-title-and-commit-message-conventions).

- Imperative subject, <= 72 characters, no trailing period; optional blank-line-separated body for the non-obvious *why*.
- US English, title case with lowercase short bind words; no vague titles, no `Co-Authored-By:` unless asked, no release-bump magnitude (NBGV handles versioning). Dependabot's `Bump X from Y to Z` titles are fine.
- develop PRs squash-merge (`gh pr merge --squash`), main PRs merge-commit (`--merge`); a mismatched flag is rejected by branch protection.

## GitHub Copilot Review Runbook

> This runbook implements the [AGENTS.md "PR Review Etiquette"](../AGENTS.md#pr-review-etiquette) review-loop contract for GitHub Copilot. Without it in-repo, an agent has no pointer to the reliable Copilot mechanics and falls back to known-broken paths (the no-op `POST /requested_reviewers`, the wrong bot-login filter). In the API snippets below, `<N>` is the PR number.

Use this section for provider-specific mechanics. The expected review loop *contract* (request review on every push, verify head-SHA coverage, triage findings, reply + resolve, escalate when stuck) is defined in [AGENTS.md -> PR Review Etiquette](../AGENTS.md#pr-review-etiquette). This section only describes how to make GitHub Copilot reliably execute it.

### Triggering and Polling

Auto-review on push is configured (via the branch ruleset's `copilot_code_review` rule with `review_on_push: true`) but fires inconsistently in practice - treat it as best-effort, not guaranteed. After every push, **re-request a review programmatically** via the GraphQL `requestReviews` mutation, passing the Copilot reviewer's bot node id in `botIds`. This drives the loop end-to-end without a UI hand-off.

**A review with no inline comments is still a completed review - not a failure, and not a reason to ask the maintainer to re-trigger.** Copilot very often posts a single formal review (GraphQL `state: COMMENTED`) whose body ends with "...reviewed N of N changed files ... and generated no comments" and adds **zero** inline threads. That review carries the head `commit.oid` and fully satisfies the loop - it is the clean-pass success case. Never read "no inline comments" as "the review didn't run," and never re-request or escalate to the maintainer because comments are absent.

**Round 1 is normally auto-seeded - poll for it before trying to self-trigger.** Auto-review-on-open supplies the first review with no `botIds` call needed, but it can lag one to three minutes. After opening a PR (or the first push), **poll** for a Copilot review on the head SHA (see [Verify Review Covered Current Head](#verify-review-covered-current-head)) before concluding none ran. The `requestReviews` mutation below is for **re-requesting on later pushes** (a new head SHA); by then a prior review exists, so its bot node id is readable. A missing bot node id on round 1 therefore means "the auto-review has not landed yet - wait and poll," **not** "ask the maintainer to kick it off."

> **The reviewer login differs by API.** In **GraphQL** (`gh api graphql` and `gh pr view --json reviews`, which is GraphQL-backed) the `Bot.login` is `copilot-pull-request-reviewer` - **no `[bot]` suffix**. In the **REST** API (`gh api repos/.../issues|pulls/...`) the same account's `user.login` is `copilot-pull-request-reviewer[bot]` - **with** the suffix. Each query below uses the correct form for its API; match the API, not a single spelling, when adapting them.

```sh
# 1. PR node id + the Copilot reviewer's bot node id (read from any existing
#    Copilot review; the reviewer login is `copilot-pull-request-reviewer`).
PR_NODE=$(gh pr view <N> --json id --jq '.id')
BOT_ID=$(gh api graphql -f query='
{
  repository(owner: "ptr727", name: "PlexCleaner") {
    pullRequest(number: <N>) {
      reviews(first: 50) { nodes { author { __typename login ... on Bot { id } } } }
    }
  }
}' --jq '[.data.repository.pullRequest.reviews.nodes[]
          | select(.author.login == "copilot-pull-request-reviewer")
          | .author.id] | first')

# 2. Re-request a Copilot review on the current head.
gh api graphql -f query='
mutation($pr: ID!, $bot: ID!) {
  requestReviews(input: { pullRequestId: $pr, botIds: [$bot], union: true }) {
    pullRequest { id }
  }
}' -F pr="$PR_NODE" -F bot="$BOT_ID"
```

The bot node id is read from an existing Copilot **formal** review (`pullRequest.reviews`), so step 1 needs at least one prior formal review on the PR - the auto-review-on-open normally supplies the first one (it may have **no inline comments**; that still counts, and its bot node id is still readable). Poll for it (give auto-review-on-open a few minutes) before deciding it is missing. The Copilot reviewer bot's global node id is `BOT_kgDOCnlnWA` (login `copilot-pull-request-reviewer`) if you need to skip discovery. If Copilot posted **only an issue comment** and no formal review, the head is covered but `reviews` yields no bot node id - read the id from the Copilot issue comment's author by querying the PR's issue comments in GraphQL (`pullRequest.comments` -> author `... on Bot { id }`), or request `Copilot` once through the GitHub PR UI to produce a formal review. Manual UI seeding is the fallback specifically when no formal review exists to read the id from; then use the mutation for every subsequent re-request.

**Do NOT post `@Copilot review` as a PR comment.** That comment triggers the Copilot *coding agent* (`copilot-swe-agent[bot]`), which makes code changes rather than posting a review.

Known non-working request paths (don't rely on them - use the `requestReviews` mutation above instead):

- `POST /requested_reviewers` with `reviewers=[Copilot]` can return 200 but no-op.
- `copilot-pull-request-reviewer` as a requested reviewer slug returns 422.

### Verify Review Covered Current Head

Before merging, confirm Copilot reviewed the current PR head SHA. Copilot may respond as either a formal review (carries an exact commit SHA) or an issue comment (no SHA - use the most recent Copilot comment for manual confirmation). Check both.

```sh
PR_HEAD=$(gh pr view <N> --json headRefOid --jq '.headRefOid')

# 1. Formal review - exact SHA match.
gh pr view <N> --json reviews --jq \
  '.reviews[] | select(.author.login=="copilot-pull-request-reviewer") | .commit.oid' \
  | grep -q "$PR_HEAD" && echo "covered via formal review"

# 2. Issue comment - show the most recent Copilot comment for manual
#    confirmation. This is the REST API, so the login carries the `[bot]` suffix.
gh api repos/ptr727/PlexCleaner/issues/<N>/comments --jq \
  '[.[] | select(.user.login=="copilot-pull-request-reviewer[bot]")] | last | {created_at, body: .body[:200]}'
```

Coverage is confirmed when (1) exits 0 - **a formal review with no inline comments still satisfies path (1)**, because coverage is about the head SHA, not the comment count. For issue comments (path 2), body content is the only reliable signal - `created_at` is not: `git log -1 --format=%cI` is the **commit** timestamp, not the push timestamp, so amended or rebased commits can have an earlier timestamp and an older Copilot comment could satisfy a time check even though Copilot never saw the current head. Treat path (2) as confirmed only when the comment body explicitly refers to the current changes.

### Bounded Retry Workflow

This path is only for a **genuinely missing** review - no Copilot review (formal *or* issue comment) covers the current head SHA after polling. A review that covered the head but produced no comments is a clean pass, not a missing review; do not enter this retry path for it.

If a review did not run on the current head, retry:

1. Wait briefly and check head-SHA coverage (see above).
1. Re-request the review via the `requestReviews` mutation (see "Triggering and Polling"); fall back to the GitHub PR UI only if the mutation no-ops.
1. Retry up to two more times (three total).
1. If still missing, mark review as blocked and escalate to the user/maintainer with what was attempted.

### Reply and Thread Resolution Workflow

List unresolved threads. Use `first: 100` with cursor-based pagination; if `hasNextPage` is true, re-run with `after: "<endCursor>"` to retrieve the next page:

```sh
gh api graphql -f query='
{
  repository(owner: "ptr727", name: "PlexCleaner") {
    pullRequest(number: <N>) {
      reviewThreads(first: 100) {
        nodes {
          id isResolved path
          comments(first: 1) { nodes { author { login } body } }
        }
        pageInfo { hasNextPage endCursor }
      }
    }
  }
}' | jq '
  .data.repository.pullRequest.reviewThreads |
  (.pageInfo | "hasNextPage=\(.hasNextPage) endCursor=\(.endCursor)"),
  (.nodes[] | select(.isResolved == false))
'
```

Reply on a thread, then resolve it:

```sh
gh api graphql -f query='
mutation($threadId: ID!, $body: String!) {
  addPullRequestReviewThreadReply(input: { pullRequestReviewThreadId: $threadId, body: $body }) {
    comment { id }
  }
}' -F threadId="PRRT_..." -F body="Fixed in <SHA>: <one-line summary>."

gh api graphql -f query='
mutation($threadId: ID!) {
  resolveReviewThread(input: { threadId: $threadId }) { thread { id isResolved } }
}' -F threadId="PRRT_..."
```

Issue-level Copilot comments (those in `issues/<N>/comments`) have no resolution action - GitHub provides no API or UI to resolve them. Reply if the finding warrants it; no resolution step is needed or possible.

Reply-body conventions:

- Accepted bug/style fix: include fixing commit SHA and a one-line summary.
- Declined style comment: cite the rule (AGENTS.md or the CODESTYLE.md language section) and the existing-tree precedent.
- Declined architecture proposal: one-sentence rationale.

After the final push, sweep-resolve stale older threads for removed code paths.

## When in Doubt

Read [AGENTS.md](../AGENTS.md) for this repo's conventions and [../ARCHITECTURE.md](../ARCHITECTURE.md) for PlexCleaner's architecture, processing pipeline, and design patterns. For code-style rules, [`CODESTYLE.md`](../CODESTYLE.md) is authoritative. Don't restate any of these files' rules in commit bodies or PR descriptions - keep those focused on the change itself.
