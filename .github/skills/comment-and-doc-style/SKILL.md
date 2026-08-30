---
name: comment-and-doc-style
description: >-
  Governs prose, comment, Markdown, character-set, line-ending, and PR-title/commit-message
  conventions for every ptr727/ProjectTemplate fleet repo. Use this whenever writing or editing a
  code comment, workflow comment, Markdown doc, commit message, or PR title, whenever choosing
  which characters to type in agent-authored text, whenever the file being edited is CRLF, and
  whenever naming a tool in prose or docs. Triggers even when the task looks purely mechanical,
  such as "just fix a typo" or "add a one-line comment", because the fleet's ASCII character-set
  tiers, no-semicolon rule, comment-growth discipline, and CRLF-preservation rule are each easy to
  violate without noticing: an em dash slipped into a sentence, a comment that grew by one more
  clause, or a text-mode edit that silently flattens a CRLF file to LF. Also triggers when
  authoring a new Markdown file (reference-style links, Table of Contents, present tense), when a
  carried instruction file (AGENTS.md, GOVERNANCE.md, CODESTYLE.md, WORKFLOW.md,
  .github/copilot-instructions.md) is being edited (no coordination references to the template or
  a sibling repo), and when writing a PR title or commit message (imperative subject, no vague
  titles, no unsolicited Co-Authored-By, no release-bump magnitude).
---

# Comment and Doc Style

## Why this exists

These are the fleet's mechanical prose rules, kept in one place instead of re-derived per repo or
per session: how to write a comment, which characters an agent may type, how a Markdown file is
structured, how a carried instruction file may reference the hub, and how a PR title or commit
message reads. None of these are matters of taste. Each is checked, by `prose_lint.py`,
`editorconfig-checker`, `markdownlint`, `cspell`, or a human reviewer, and each has been the exact
subject of a real review finding.

## Naming tools in prose

Use each tool's official casing in task labels, docs, and prose: `.NET` (not `.Net`),
`CSharpier`, `ruff`, `pyright`, `uv`. Do not invent personal variants.

## Markdown files: linting and spelling

- **Markdown lints clean, repo-wide.** Every `.md` file is error and warning free via
  `markdownlint-cli2` against the shared `.markdownlint-cli2.jsonc`. A rule it deliberately
  disables (for example `MD013` line length) stays disabled, do not "fix" it. `MD033` inline HTML
  stays enabled: HTML comments, and `details`/`summary` (no Markdown equivalent for a
  collapsible), are allowed, everything else with a native Markdown equivalent uses the Markdown.
- **Spelling is US English**, checked by CSpell against the shared `cspell.json`
  (`"language": "en-US"`, so a British spelling is flagged). Add a project term to `cspell.json`'s
  `words` list, never to a `.code-workspace`'s own `cspell.words` block.
- **CI's spelling gate covers `README.md` and `HISTORY.md` only**, deliberately not every `.md`
  file, so a new topical doc is not spell-gated in CI (the editor extension still flags it live).
  A repo may widen its own CI list, README plus HISTORY is the default. A repo shipping no
  `HISTORY.md` drops it from the CI workflow, the `Lint: Spelling` task, and the GOVERNANCE.md
  cspell line together, all three or none.
- **`HISTORY.md` mirrors the README's opening**: the same `# <Title>`, the same tagline verbatim
  (the first line after the README's H1), then its own `## Release History`. It never repeats a
  paragraph below the README's tagline.
- **"Markdown" is a proper noun in prose** (a Markdown file, a Markdown-only repo), lowercase only
  for what a machine reads: a tool or package name (`markdownlint`), a settings key, a heading
  anchor, a file extension.

## Docker lint authorization

A restricted executor treats Docker socket access, image fetching, and repository exposure as
separate permissions. Repository exposure needs explicit maintainer approval even when the mount
is read-only. Use the hub's `scripts/docker_lint.py` wrapper for the standard lint shape. It
discovers targets, pulls images in a separate phase, resolves each digest, and announces the
boundary before repository mounts begin. Each Docker command has a timeout and visible result.
Lint containers disable networking and mount the checkout read-only. Persist approval only when
the executor constrains that whole shape. Never allow an unconstrained `docker run` prefix.
PSScriptAnalyzer downloads its pinned module in a separate container that has network access and
no repository mount. `GOVERNANCE.md` "Running the Linters Locally (Known-Working Invocations)"
owns the exact invocation and full authorization model.

Agent-specific authorization stays in provider-labeled bullets so one agent's configuration does
not read as a shared requirement:

- **Codex:** rules cannot safely cover changing worktree paths and digests. Smart Approvals can
  prompt per task. No-prompt operation is supported only inside an external sandbox because it
  removes command-wide protection.

## Markdown formatting

- **Reference-style links everywhere**, except the four files read one section at a time rather
  than end to end: `AGENTS.md`, `GOVERNANCE.md`, `OPERATIONS.md`, `.github/copilot-instructions.md`.
  Those keep inline links so a target resolves where it is read. Every other Markdown file defines
  every URI at the bottom, grouped by type under an HTML-comment header, each group alphabetized
  by reference name rather than by the full definition line (a name that is a prefix of another
  sorts first, `[governance]` above `[governance-branching-model]`). A URL inside a fenced code
  block stays inline. See `references/markdown-links.md` for the full grouping and naming
  convention.
- **Table of Contents**: generated by the Markdown All in One extension on save, never
  hand-authored or hand-edited. Exclude a heading with an inline `<!-- omit from toc -->` marker.
- **One logical paragraph per line**, no hard-wrap line-length limit. For an intentional line
  break within a block (stacked badges, status lines), end the line with a trailing backslash
  rather than trailing whitespace.
- **Headings use the PR-title casing rule** below.
- **Write in the present tense.** State what *is*, never a change from a prior state ("X does Y",
  not "X now does Y" or "X no longer does Z"). This applies to docs and code/workflow comments
  alike. Before/after framing belongs in changelogs, commit messages, and PR descriptions, where
  the prior state is the point.
- **When a behavior changes, grep for prose asserting the old one.** Comments, diagram labels,
  workflow-input descriptions, and audit statements elsewhere may still describe the prior
  behavior, and each was accurate when written. No linter catches a claim that is merely untrue,
  so this sweep is the only mechanism that will.

## Sentence structure

The structural half of ASD-STE100 is the adopted house style for agent-authored prose, and the
controlled dictionary is deliberately not adopted: vocabulary stays unrestricted, structure is
restricted. Each structural rule a pattern can reach lands as a `prose_lint.py` check
incrementally, and this section names each check as it ships.

- **Short sentences: at most 25 words in one sentence**, ASD-STE100's descriptive cap, checked by
  the `sentence-length` rule in `prose_lint.py`. The check is opt-in like `sentence-split`,
  because the existing corpus predates the cap and a default gate would fail whole files nobody
  is editing. Write new prose under the cap, and scope a run to a change with
  `--check sentence-length --diff <base>`.
- **One instruction per sentence.** A procedure step states one action, and a second action is a
  second step. No pattern reaches this, so it is authoring discipline with no check.
- **Active voice, imperative mood for procedure steps.** Write "run the gate", never "the gate
  should be run". Also authoring discipline, since a reliable passive-voice pattern does not
  exist.

## Comments

Applies to code and workflow (`#`) comments alike.

- Comment only when the code does not explain itself, or the logic is genuinely complex.
  Self-evident code needs no comment.
- State only the non-obvious *why*, for the human reading *this* project's code now. No
  cross-project references, no historic or design narrative, no rule citations. Governance lives
  in the fleet's own instruction set, not echoed inline.
- **Keep it short**: one line is the default. A second line is earned only by a constraint the
  code cannot otherwise carry.
- **Structured, not prose**: one sentence per line, never wrapped across lines, never a
  multi-sentence run-on. A comment that genuinely needs several sentences is several lines, each
  one sentence.
- A comment line opening prose starts with a capital. A trailing label, or the version pin an
  action-pinning rule requires, does not.
- Mark a sub-topic with `-` after the comment marker (`# -`), only for genuine parallel sub-items
  hanging off a lead line, never a continuation of one thought.
- **No file, class, or type header summary blocks.** A type or file gets a comment only for a
  specific non-obvious point, never a block restating what it contains (a license or provenance
  header a tool or policy requires is not a summary and is unaffected).
- **Never let a comment grow across edits.** Touching code near an existing comment means the
  comment comes out the same length or shorter, never one more clause of rationale appended.

A continuation stays unindented, one sentence per line:

```text
# Change gate for the compile tests.
# An esp-idf build costs minutes, so gate on what each test covers.
# A diff that cannot be computed runs everything.
```

Sub-topics take a `-` after the comment marker, each elaborating a distinct item named in the lead:

```text
# Source lint plus change-gated compile tests.
# - compile-test builds the external component.
# - template-compile-test builds one example device per template.
```

## Character set

Agent-authored text is ASCII by default: documentation, code, comments, commit messages, and PR
descriptions. A non-ASCII character is read against three tiers, because whether one is
typography or meaning depends on where it sits. A character in no tier is a finding rather than a
silent pass.

- **Tier 1, never legitimate.** Typography carrying no meaning its ASCII form loses. Remove on
  sight:
  - em dash (U+2014) and en dash (U+2013) to a restructured sentence, two sentences or a comma,
    never a spaced hyphen
  - right arrow (U+2192) to `->`, double arrow (U+21D2) to `=>`
  - curly quotes (U+2018/U+2019/U+201C/U+201D) to straight `'` and `"`
  - ellipsis (U+2026) to `...`, bullet (U+2022) to `-`
  - no-break space (U+00A0) to a space, non-breaking hyphen (U+2011) to `-`
- **Tier 2, legitimate only next to a number.** Relational and arithmetic operators: U+2264,
  U+2265, U+2260, U+00B1, U+2212, U+00D7, U+00F7, U+00B7. Keep one when an adjacent non-space token
  is a number, a tier-3 symbol, or another tier-2 operator, so a threshold table or a measured
  range reads as the range it is. In flowing prose write the ASCII form: `<=`, `>=`, `!=`, `+/-`,
  `-`, `x`, `/`. A tier-2 operator directly before a number in a table of thresholds is the range
  it describes and stays, the same character between two words in a sentence is prose and takes
  the ASCII form.
- **Tier 3, always legitimate.** Scientific and unit symbols whose ASCII form would be a lie:
  micro (U+00B5), degree (U+00B0), ohm (U+2126), pi (U+03C0), superscript two and three (U+00B2,
  U+00B3), section (U+00A7). Keep the symbol, never approximate it away or spell it out.
- **Unicode a developer deliberately typed** stays regardless of tier, such as emoji used for
  emphasis or as callout markers. Never strip a developer's own characters, this is developer
  authored text and not a license for the agent to add its own.
- **An unrecognized non-ASCII character is reported, not allowed.** Classify it into a tier above
  before using it.
- **No semicolon in agent-authored prose.** Recast a mid-sentence semicolon as a comma or as two
  sentences. A semicolon separating items in a list that already contains commas, or a statement
  terminator in code, is unaffected.
- **No spaced hyphen joining or interrupting a sentence** (` - `, or the paired aside ` - x - `).
  Recast as a comma, two sentences, or parentheses. A hyphen inside a compound word, a leading
  list marker, a range, and the `- **Label** - explanation` bullet separator are unaffected.
- **In carried verbatim content, fix the whole class at the hub**, not one instance, since a
  downstream repo cannot edit a section byte-matched against the hub. Everywhere else, correct as
  each file is next edited, not swept.

## Line endings

This repo's default is LF (`[*] end_of_line = lf` in `.editorconfig`), with CRLF pinned only for
`*.bat` and `*.cmd`, the one type Windows itself requires it for.
**Preserve a file's existing line ending when editing it, never reflow as a side effect of a
content change.** A text-mode tool, including a naive programmatic write, can silently flip CRLF
to LF and turn a one-line change into a whole-file diff. After any programmatic edit, verify with
`git diff --stat` (it should touch only the lines you changed) and a byte scan, `file` and a naive
`git ls-files --eol` are both unreliable here. Idempotent normalize:
`b.replace(b"\r\n", b"\n").replace(b"\n", b"\r\n")`. The full policy, choosing an ending for a new
file type, operational-repo overrides, extensionless-script pins, and auditing, is in
`references/line-endings.md`.

## Carried files reference no coordination machinery

`AGENTS.md`, `GOVERNANCE.md`, `CODESTYLE.md`, `WORKFLOW.md`, `.github/copilot-instructions.md`,
the `spec/` files and the carried `AUDIT.md` never reference the template repo
(in prose or a link), and never name a sibling fleet repo as an illustrative example. State the
behavior a carried rule needs, not the coordination flow that produced it, the maintainer supplies
the destination out of band. A contextually relevant link to a related project (the image this
config feeds, a library this depends on) is not a coordination reference and is expected. The full
exceptions, a verbatim section that must name the hub to do its job, and a pointer to a
hub-hosted tool the reader runs, are in `references/carried-doc-references.md`.

## PR titles and commit messages

- **Format**: an imperative subject, 72 characters or fewer, no trailing period ("Add 24-Hour
  PM2.5 Average Sensor", not "Added X" or "Adds X"). An optional body, blank-line separated,
  explains *why* the change is being made when that is non-obvious, the diff already shows *what*.
- **Rules**: no vague titles (`update stuff`, `wip`). Dependabot's default `Bump X from Y to Z`
  titles are fine as-is. No `Co-Authored-By:` lines unless the developer explicitly asks. No
  release-bump magnitude in the title ("minor", "patch", "release v0.2.0"), Nerdbank.GitVersioning
  computes the next version from `version.json` and git history, a dependency version in a
  dependency-bump title is fine and expected. US English spelling, and title case with lowercase
  short bind words (a, an, the, and, but, or, of, in, on, at, to, by, for, from), a hyphenated
  compound capitalizes both parts unless the second is a short preposition (*Built-in*,
  *EPA-Corrected*, *24-Hour*).

```text
Add Structured Logging Extensions to Library
Pin softprops/action-gh-release to Commit SHA
Drop net8.0 Multi-Targeting from Console Project
Bump xunit.v3 from 3.2.2 to 3.3.0
Clarify devcontainer Setup Steps in README
```

## Quantitative claims

A quantitative claim in `README.md` (a count, a size, a version floor, a supported-platform list)
is verified against current code before it is written. When a doc number is derived from a code
constant, mark the dependency in a source-code comment so the next editor knows to update both.
