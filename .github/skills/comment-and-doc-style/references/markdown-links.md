# Reference-Style Links

Full detail for the "Markdown formatting" reference-style-links rule in `SKILL.md`. Load this
when actually authoring or reorganizing a Markdown file's link definitions, not for a small
in-place prose edit.

## Where the rule applies

Every Markdown file in the repo uses reference-style links only, except the four files that are
read one section at a time rather than end to end: `AGENTS.md`, `GOVERNANCE.md`, `OPERATIONS.md`,
and `.github/copilot-instructions.md`. Those keep inline `[text](uri)` links, since a reader
jumping straight to one section needs the target to resolve where it is, while a definition parked
at the bottom of the file is never reached. The exception is that closed list of four files, never
a category to argue from case by case. Every other Markdown file follows the rule regardless of
its audience.

## The definition block

Every URI, an internal path, an anchor, an external URL, or a shield image, is defined at the
bottom of the file, split into groups by type under an HTML-comment header, for example:

```markdown
<!-- Shields -->

[license-shield]: https://img.shields.io/...

<!-- Repo -->

[governance]: ./GOVERNANCE.md
[governance-branching-model]: ./GOVERNANCE.md#branching-model

<!-- External -->

[markdownlint-cli2]: https://github.com/DavidAnson/markdownlint-cli2
```

Within a group, definitions are alphabetized by **reference name alone**, the text inside the
brackets, never by the whole definition line. Where one name is a prefix of another, the shorter
one sorts first: `[governance]` above `[governance-branching-model]`, `[repo-config]` above
`[repo-config-settings]`. Sorting the full line instead inverts every such pair, because `-`
precedes `]` in byte order, so the two readings disagree on exactly the names a reader looks up
together, and a plain `sort -c` over the block passes on the inverted order regardless.

## Naming a reference

Reference names are contextual and encode both the target and its group:

- `foo-shield` for a shield image
- `foo-link` for an external URL
- a bare `foo` for a local path or anchor

For example `[license-shield]`, `[releases-link]`, `[repo-config]`. Never a numeric name (`[1]`)
and never an opaque one.

## Mechanics

- No inline `[text](uri)` targets in prose, in any file outside the four-file exception above.
- **A URL inside a fenced code block stays inline.** Reference links do not resolve inside a code
  block, so do not extract it there, and exclude fenced code from any link-integrity check
  (bracket literals like `["a", "b"]` otherwise read as undefined references).
- **Removing a link also removes its reference definition.** An orphaned definition fails the
  no-unused-defs rule.
- The one exception to "no inline links" is the Table of Contents, whose entries stay inline
  anchor links, since the ToC extension generates them that way and they are never hand-edited.
