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
  titles, no unsolicited Co-Authored-By, no release-bump magnitude). Also triggers when text
  states how many items a list or a table holds, since that count goes stale.
---

# Comment and Doc Style

## Why this exists

These are the fleet's mechanical prose rules, kept in one place instead of re-derived per repo or
per session. They cover how to write a comment and which characters an agent may type. They also
cover how a Markdown file is structured and how a carried instruction file may reference the hub.
They also cover how a PR title or commit message reads. None of these are matters of taste. Each is checked, by `prose_lint.py`,
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
  collapsible), are allowed. Everything else with a native Markdown equivalent uses the Markdown.
- **A repo-local exclusion goes in a nested config, never in the root one.** The shared
  `.markdownlint-cli2.jsonc` at the repo root is fleet-fixed, and its `ignores` list covers only
  what every repo has, third-party Markdown under `node_modules`. A repo excluding a subtree of
  its own that it does not treat as authored prose puts a `.markdownlint-cli2.jsonc` beside that
  content. That config carries its own `ignores`. That subtree is a committed data archive, a
  vendored theme, or a hand-maintained record. A config inside a
  tree that is re-imported or re-vendored wholesale is deleted by the next refresh, so it is
  re-added with the import. Excluding through the CI workflow's negated glob input instead is a
  CI-only fix. Such an exclusion leaves those same files flagged for anyone who runs the linter
  locally.
- **What decides whether a nested config works.** It applies to the directory it sits in and to
  every subdirectory below it. It filters those files even when a run names them explicitly
  as arguments. So a bare local run and the CI step honor it alike. Its `ignores` patterns
  resolve against that directory rather than against the repo root. So an entry written
  repo-root-relative matches nothing and reports no error saying so. Its settings
  merge with those above it rather than replacing them, so the fleet rule block still governs
  the files it does not exclude. And the exclusion has to be expressed as `ignores`. That is
  because the `globs` and `gitignore` keys are read only from the config in the directory the
  linter is run from. So a nested copy of either is inert.
- **Spelling is US English**, checked by CSpell against the shared `cspell.json`
  (`"language": "en-US"`, so a British spelling is flagged). Add a project term to `cspell.json`'s
  `words` list, never to a `.code-workspace`'s own `cspell.words` block.
- **CI's spelling gate covers `README.md` and `HISTORY.md` only**, deliberately not every `.md`
  file. So a new topical doc is not spell-gated in CI (the editor extension still flags it live).
  A repo may widen its own CI list, README plus HISTORY is the default. A repo shipping no
  `HISTORY.md` drops it from the CI workflow, the `Lint: Spelling` task, and the GOVERNANCE.md
  cspell line together, all of them or none.
- **`HISTORY.md` mirrors the README's opening**: the same `# <Title>`, the same tagline verbatim
  (the first line after the README's H1), then its own `## Release History`. It never repeats a
  paragraph below the README's tagline.
- **"Markdown" is a proper noun in prose** (a Markdown file, a Markdown-only repo). The word is
  lowercase only for what a machine reads: a tool or package name (`markdownlint`), a settings
  key, a heading anchor, a file extension.

## Docker lint authorization

A restricted executor treats Docker socket access, image fetching, and repository exposure as
separate permissions. Repository exposure needs explicit maintainer approval even when the mount
is read-only. Use the hub's `scripts/docker_lint.py` wrapper for the standard lint shape. It
discovers targets, pulls images in a separate phase, resolves each digest, and announces the
boundary before repository mounts begin. Each Docker command has a timeout and visible result.
Lint containers disable networking and mount a read-only snapshot rather than the live checkout.
The snapshot holds only the tracked and unignored files, so a git-ignored file stays on the host.
Persist approval only when the executor constrains that whole shape. Never allow an unconstrained
`docker run` prefix.
PSScriptAnalyzer downloads its pinned module in a separate container that has network access and
no repository mount. `GOVERNANCE.md`'s hub-only "Running the Linters Locally (Known-Working
Invocations)" section owns the exact invocation and full authorization model.

Agent-specific authorization stays in provider-labeled bullets so one agent's configuration does
not read as a shared requirement:

- **Codex:** execution rules match exact argument prefixes, so they cannot safely cover changing
  worktree paths and digests. Smart Approvals can prompt per task. No-prompt operation is
  supported only inside an external sandbox because it removes command-wide protection.

## Markdown formatting

- **Reference-style links everywhere**, except the files read one section at a time rather
  than end to end: `AGENTS.md`, `GOVERNANCE.md`, `OPERATIONS.md`, `.github/copilot-instructions.md`.
  Those keep inline links so a target resolves where it is read. Every other Markdown file defines
  every URI at the bottom, grouped by type under an HTML-comment header. Each group is
  alphabetized by reference name rather than by the full definition line. A name that is a prefix
  of another sorts first, `[governance]` above `[governance-branching-model]`. A URL inside a fenced code
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

The structural half of ASD-STE100 is the adopted house style for agent-authored prose. The
controlled dictionary is deliberately not adopted: vocabulary stays unrestricted, structure is
restricted. Each structural rule a pattern can reach lands as a `prose_lint.py` check
incrementally, and this section names each check as it ships.

- **Short sentences: at most 25 words in one sentence**, ASD-STE100's descriptive cap, checked by
  the `sentence-length` rule in `prose_lint.py`. The rule reads Markdown prose, code comments, and
  docstrings. The check is opt-in like `sentence-split`, because the existing corpus predates the
  cap and a default gate would fail whole files nobody is editing. Write new prose under the cap,
  and scope a run to a change with `--check sentence-length --diff <base>`.
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
  cross-project references, no historic or design narrative, and no rule restated inline.
  Governance lives in the fleet's own instruction set. A comment may name the instruction-set
  section holding its why, such as `D5.2`, and never echoes or paraphrases that section's text.
  An id unique across that set, such as a `WORKFLOW.md` D-id, may stand alone, and any other
  citation names its file.
  A hub-only file may cite a hub `docs/` section by repository path.
  A carried file cites only instruction-set sections a carrier holds.
- **Keep it short**: one line is the default. A second line is earned only by a constraint the
  code cannot otherwise carry.
- **Structured, not prose**: one sentence per line, never wrapped across lines, never a
  multi-sentence run-on. A comment that genuinely needs several sentences is several lines, each
  one sentence.
- A comment line opening prose starts with a capital. A trailing label, or the version pin an
  action-pinning rule requires, does not.
- Mark a sub-topic with `-` after the comment marker (`# -`). Do this only for genuine parallel
  sub-items hanging off a lead line, never a continuation of one thought.
- **No file, class, or type header summary blocks.** A type or file gets a comment only for a
  specific non-obvious point, never a block restating what it contains. A license or provenance
  header a tool or policy requires is not a summary and is unaffected.
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

A change that adds a comment line in code or config fails the `comment-added` rule in the prose
gate. It reports a prose comment that opens its own line, in the diff's scope. A diff counts a
modified line as an added one, so rewording one and re-indenting one each report it. That is the
rule's cost and the label is its answer, since a comment worth keeping takes the same label as a
comment worth writing. A trailing comment is out of scope. That is because which mid-line marker
opens a comment differs by language in ways a gate cannot settle from the marker alone. A docstring is not
a comment line, an instruction to a tool is not a comment the rule reads, and Markdown is out of
scope.

Deleting the comment is the ordinary answer, since the bullets above already say what one has to
earn. Where a comment is genuinely owed, and a rule requiring one is the clearest case of that, the
pull request carries the `comments` label. With that label the gate stands down for that change.
Locally a `PROSE_ALLOW_COMMENTS` does the same, for as long as it is set to anything but a false
spelling. And `--allow-comments` does it for one run by hand. The label and the variable are separate deliberately, so a variable
left exported reaches the commit and never the merge gate.

The facts below about reaching those escapes read as a broken gate until they are known. On a public
repository the lint job reads the label when it runs, and its log says "Read the pull request's
labels live". A label added before then applies, and a rerun picks up one added since. In a private
repository, or where the log lacks that line, the event's snapshot decides. It misses a label
applied after the event, `gh pr create --label` included, so
there the label applies to the next push. The label reaches a repository only when the fleet label
set is applied to it. So a repository that has not had that applied since the label was declared
cannot carry it. In such a repository the finding names a remedy that is not yet available. And the local
escape reaches a commit before any of that. A commit is where a repository meets this rule first,
since a hook runs on every commit while the label decides a pull request.

## Issue, pull request, and commit references

No comment, no docstring, and no instruction document names an issue, a pull request, or a commit.
The surfaces are code and workflow comments, a docstring, a documentation comment, the Skills trees,
and the fleet's own rule documents. Those rule documents are `AGENTS.md`, `AUDIT.md`, `CLAUDE.md`, `CODESTYLE.md`,
`GOVERNANCE.md`, `OPERATIONS.md`, `RESYNC.md`, `STANDUP.md`, `WORKFLOW.md`, and
`.github/copilot-instructions.md`. A tracker, a history, a plan, and a README outside those trees
are the repository's own narrative and keep their references. So do a commit message and a pull
request body, which are the surfaces a reference belongs on.

The carve-outs below are each stated as a single case. Whatever none of them affirmatively permits is
banned by the paragraph above. That ban is the whole of the test and is why no list of banned
cases follows. The first concerns a code or workflow comment. **A URL there naming an issue or a
pull request on a public repository other than this one is a permitted source citation.** It does
the same job as the datasheet link, vendor wiki link, and SDK doc link the rule already leaves alone
on the adjacent line. The second is the revision record in the paragraph below.

The second carve-out: a record whose subject is the revision itself keeps it. A disproved-claims
entry in `.github/copilot-instructions.md` names the revision its proof was read against. That is
because a proof is true of one tree at one revision. It is also because an entry whose subject has
moved is deleted rather than edited to look current. The revision there is the record's own load-bearing field rather than a
citation beside a claim, which is the distinction this rule turns on.

Separately, `AGENTS.md`, `GOVERNANCE.md`, `CODESTYLE.md`, and `WORKFLOW.md` carry no three-part
version and no commit SHA, full or abbreviated. That holds whether the version or SHA is a pin's value, an example,
a minimum version, or a fixed constant. The reason is that a pin's copy goes stale at the next
Dependabot bump, and every other kind reads exactly like one. No carve-out above lifts this ban. Item 3 of this skill's
carried-doc-references reference says what to write instead of each and carries the audit that flags
a literal.

The reasons follow, and the first decides it.

- **A reference is a second lookup, and the reader is already holding the file.** A comment earns
  its place by explaining the line under it to whoever reads that line now. A number they have to
  go and resolve somewhere else is the opposite of that.
- **The lookup can be impossible.** A repository may be private, so a reference in content carried
  into a public one names something its reader cannot open at all.
- **It pollutes the content.** A rationale block that takes one more citation per round is how a
  file comes to teach a house style the rules forbid. That is what happened here.

The first carve-out is where all of the reasons fail at once, which is what makes it one case rather than a
taxonomy. Another repository's status is not a fact this file can hold. It changes without anyone
touching this file. The constraint the citation stands in for is therefore unwritable. The
lookup is not impossible, that repository being public. Nor is a URL line pollution on a surface
where the datasheet, the forum thread, and the component docs already sit on the adjacent lines.
On such a surface the hostname is the only thing that separates a URL line from them. A workaround whose justification is an open
report on the project it works around is the routine case. A reader revisiting such a workaround
needs to know whether the cause still stands.

Move one of that carve-out's conditions and a reason comes back. An instruction document states its
constraint rather than citing a tracker for it, so the first reason holds there whatever the tracker
names. A private repository's tracker cannot be opened by a reader of the content carried into a
public repository, which is the second reason exactly. This repository's own tracker holds status
this file can state, so the first and the third hold on every surface. And a bare reference carries
no destination a reader can open at all. The hash-and-number form resolves against whichever
repository the reader happens to be in, which is why that carve-out is written as a URL.

Write the constraint the reference was standing in for, or drop the clause where the reference was
the whole of its value. "A prior version re-scanned from every unmatched open, which was O(N^2)"
carries what the reader needs, and the number of the round that found it does not. Inside the
carve-out there is nothing to rewrite, the referenced thing being live status somewhere else. The
carve-out is why that case needs no remedy rather than a remedy an author is expected to find.

The `issue-ref` rule in the prose gate reads the pattern-detectable half of this. It reads a bare
reference in a comment, in a Python docstring, and in instruction text. In instruction text it
reads a URL naming an issue or a pull request as well. It reads that URL written as an inline link
destination, as a reference definition, or bare in the prose. It reads one forge's URL paths, so a URL naming a tracker it does
not know is banned there and goes unreported. Whatever a gate does not reach is unlicensed all the
same, since the rule binds a reader rather than a scan. On instruction text, the one surface it
reads both, reading the URL is what stops the gate reporting the bare spelling and passing the URL.
The reason is that an author met by the bare form's finding is otherwise pointed at respelling the
reference rather than at removing it. On every other surface the gate reads the bare form alone, so a banned
URL is banned there and goes unreported.

A bare commit reference is not a shape the prose gate can read. The reason is that a short SHA
carries the same shape as a blob id, a version fragment, and a fixture hash. The audit's
version-literal scan reads it
in `AGENTS.md`, `GOVERNANCE.md`, `CODESTYLE.md`, and `WORKFLOW.md`, bare or inside a URL.
Elsewhere the text above is the whole of what covers a commit. A reference in a
string literal is not read either. That is because a test builds the numbers it asserts against.
It is also because reading those would report a fixture rather than a claim about this repository.

## Character set

Agent-authored text is ASCII by default: documentation, code, comments, commit messages, and PR
descriptions. A non-ASCII character is read against the tiers below, because whether one is
typography or meaning depends on where it sits. A character in no tier is a finding rather than a
silent pass, a letter of a recorded name excepted per the bullet below.

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
  is a number, a tier-3 symbol, or another tier-2 operator. So a threshold table or a measured
  range reads as the range it is. In flowing prose write the ASCII form: `<=`, `>=`, `!=`, `+/-`,
  `-`, `x`, `/`. A tier-2 operator directly before a number in a table of thresholds is the range
  it describes and stays. The same character between two words in a sentence is prose and takes
  the ASCII form.
- **Tier 3, always legitimate.** Scientific and unit symbols whose ASCII form would be a lie.
  They are micro (U+00B5), degree (U+00B0), ohm (U+2126), pi (U+03C0), superscript two and three
  (U+00B2, U+00B3), section (U+00A7). Keep the symbol, never approximate it away or spell it out.
- **Tier 4, emoji callout markers.** Each is admitted alone or followed by VARIATION SELECTOR-16
  (U+FE0F). The markers are warning (U+26A0), information (U+2139), check mark (U+2705), cross mark
  (U+274C), memo as the note marker (U+1F4DD), and light bulb (U+1F4A1). The gate keeps reporting every other
  emoji, and a U+FE0F not directly after a listed marker. A downstream repository needs no markup
  to carry one, and an agent may write one only where it is mirroring a callout a developer typed.
- **Unicode a developer deliberately typed** stays regardless of tier, such as emoji used for
  emphasis. Never strip a developer's own characters, this is developer authored text and not a
  license for the agent to add its own. Among emoji the gate admits only the tier-4 markers, so
  another developer-typed emoji is classified here before it passes.
- **A letter in a recorded name keeps its actual spelling.** A person's or a place's name that a
  document records as data is written with its own letters and the marks they carry.
  The reason is that folding such a name to ASCII records a different name. Anywhere else a letter the agent writes that no
  tier covers takes its ASCII form.
- **An unrecognized non-ASCII character is reported, not allowed.** Classify it into a tier above
  before using it. A letter of a recorded name is the one exception, per the bullet above.
- **No semicolon in agent-authored prose.** Recast a mid-sentence semicolon as a comma or as two
  sentences. A semicolon separating items in a list that already contains commas, or a statement
  terminator in code, is unaffected. A single semicolon separates such a list only between two
  labeled items, as in `Inputs: a, b; outputs: c`, and a list item's bold opening label is not one.
- **No spaced hyphen joining or interrupting a sentence** (` - `, or the paired aside ` - x - `).
  Recast as a comma, two sentences, or parentheses. A hyphen inside a compound word, a leading
  list marker, a range, and the `- **Label** - explanation` bullet separator are unaffected.
- **In carried verbatim content, fix the whole class at the hub**, not one instance. The reason is
  that a downstream repo cannot edit a section byte-matched against the hub. Everywhere else, correct as
  each file is next edited, not swept.

## Line endings

This repo's default is LF (`[*] end_of_line = lf` in `.editorconfig`), with CRLF pinned only for
`*.bat` and `*.cmd`, the one type Windows itself requires it for.
**Preserve a file's existing line ending when editing it, never reflow as a side effect of a
content change.** A text-mode tool, including a naive programmatic write, can silently flip CRLF
to LF and turn a one-line change into a whole-file diff. After any programmatic edit, verify with
`git diff --stat` (it should touch only the lines you changed) and a byte scan. `file` and a naive
`git ls-files --eol` are both unreliable here. Idempotent normalize:
`b.replace(b"\r\n", b"\n").replace(b"\n", b"\r\n")`. The full policy, choosing an ending for a new
file type, operational-repo overrides, extensionless-script pins, and auditing, is in
`references/line-endings.md`.

## Carried files reference no coordination machinery

`AGENTS.md`, `GOVERNANCE.md`, `CODESTYLE.md`, `WORKFLOW.md`, `.github/copilot-instructions.md`,
the `spec/` files and the carried `AUDIT.md` never reference the template repo
(in prose or a link). They also never name a sibling fleet repo as an illustrative example. State the
behavior a carried rule needs, not the coordination flow that produced it, the maintainer supplies
the destination out of band. A contextually relevant link to a related project (the image this
config feeds, a library this depends on) is not a coordination reference. It is expected. The full
exceptions are in `references/carried-doc-references.md`. They are a verbatim section that must
name the hub to do its job, and a pointer to a hub-hosted tool the reader runs.

## PR titles and commit messages

- **Format**: an imperative subject, 72 characters or fewer, no trailing period ("Add 24-Hour
  PM2.5 Average Sensor", not "Added X" or "Adds X"). An optional body, blank-line separated,
  explains *why* the change is being made when that is non-obvious, the diff already shows *what*.
- **Rules**: no vague titles (`update stuff`, `wip`). Dependabot's default `Bump X from Y to Z`
  titles are fine as-is. No `Co-Authored-By:` lines unless the developer explicitly asks. No
  release-bump magnitude in the title ("minor", "patch", "release v0.2.0"), Nerdbank.GitVersioning
  computes the next version from `version.json` and git history. A dependency version in a
  dependency-bump title is fine and expected. US English spelling, and title case with lowercase
  short bind words (a, an, the, and, but, or, of, in, on, at, to, by, for, from). A hyphenated
  compound capitalizes both parts unless the second is a short preposition (*Built-in*,
  *EPA-Corrected*, *24-Hour*).

```text
Add Structured Logging Extensions to Library
Pin softprops/action-gh-release to Commit SHA
Drop net8.0 Multi-Targeting from Console Project
Bump xunit.v3 from 3.2.2 to 3.3.0
Clarify Devcontainer Setup Steps in README
```

## Quantitative claims

<!-- include: GOVERNANCE.md > Incidental Counts -->

Text refers to an enumeration rather than stating its size, so it writes "the following checks", "each case", or "the rows below". This holds in every fleet repository, for docs, code and workflow comments, and docstrings alike. A stated count is a second copy of what the list or table already shows, and nothing keeps the two copies in sync. Every later edit to the enumeration then owes an edit to the sentence, and a missed one surfaces as a review finding. A word such as "both" or "all three" is a count too, where the list it counts can grow. A count is written only where the number is what the reader needs, such as a limit, a threshold, or a measured result. Such a count names its source, such as the constant or the measurement, so a reader can re-derive it. A wrong incidental count is fixed by removing it rather than by correcting it, since a corrected number goes stale the same way.

<!-- /include -->

A `README.md` quantitative claim (a count, a size, a version floor, a supported-platform list) is
verified against current code before it is written. When a doc number
is derived from a code constant, mark the dependency in a source-code comment so the next editor
knows to update both.
