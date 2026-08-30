# Line Ending Policy

Full detail for the "Line endings" rule in `SKILL.md`. Load this when choosing an ending for a
new file type, working in an operational (config) repo, pinning an extensionless executable, or
auditing a repo's endings, not for an ordinary content edit to an existing file (the SKILL.md
summary, preserve the existing ending and verify with a byte scan, covers that case).

## The defaults

- **`.editorconfig` sets the line ending.** `[*] end_of_line = lf` is the default, every file
  type is LF unless pinned otherwise, with CRLF pinned for the one exception Windows requires:
  `*.bat` and `*.cmd` (cmd.exe's line handling is unreliable on LF). Only the CRLF exception is
  declared, the redundant per-type LF rules are intentionally omitted, since the default already
  gives shell scripts, Dockerfiles, workflow YAML, `uv.lock`, and every shebang-executed `.py`
  the ending they need without a path-specific pin.
- **`.gitattributes` mirrors the repository-wide defaults**: `* text=auto eol=lf` normalizes every
  detected text file to LF while leaving binary files byte-preserved. `*.bat` and `*.cmd` override
  that default to CRLF. Do not add per-language or per-file LF pins where the global LF default
  already applies. The CRLF-native exception for POSIX-executed paths is defined below.
- **Both files are required together.** `.editorconfig` governs the editor, `.gitattributes`
  governs git (checkout, commit, `--renormalize`). A repo missing either file, or whose
  `.editorconfig` sets no global `end_of_line` default (for example declares it only under
  `[*.md]`), accumulates files mixed between LF and CRLF, the exact failure these two files
  prevent together. Carry both files whole. An inert `[*.cs]` block costs nothing in a non-.NET
  repo.

## Choosing an ending for a new file type

LF is the default, since it is what every tool, CI runner, and Dependabot bump produces, and
Windows GUI editors (VS Code, Visual Studio, Notepad, WordPad) all read and write it cleanly. Pin
CRLF only for a type Windows itself requires it for: `*.bat` and `*.cmd`. Everything else,
including YAML (workflow and non-workflow alike, no distinction needed now that both are LF),
`.gitignore`, `.dockerignore`, and a tool-owned format with a native LF ending (KiCad), takes the
`[*]` default with no override.

## Operational (config) repos

The global default follows the consuming application's native platform, not the fleet LF default.
A config repo (registry `workflowModel: operational`) is a view into an application's
configuration directory, often the exact tree mounted into that app's container, so its files use
the ending the app itself reads and writes, and forcing the fleet LF default would fight an app
that needs CRLF. Set the `[*] end_of_line` default to the app's native ending and record it in the
registry `lineEndings` field (`lf` or `crlf`): the field is required for every operational repo
because a config repo's ending is a load-bearing decision tied to its consuming app. A
Linux-native app or container config uses the `lf` fleet default. A Windows-native app that uses
CRLF requires `[*] end_of_line = crlf` and
`* text=auto eol=crlf`. `release` repos keep the LF fleet defaults above. Do not re-normalize an
operational repo to
the fleet default, that is exactly the over-normalization these per-repo endings exist to
prevent, whichever direction the fleet default currently points.

**Mixed-consumer config: prefer to split by platform into single-platform repos, not one mixed
repo.** When a config repo would be consumed on two platforms (a Linux app plus a Windows-edited
subtree), the clean answer is a repo per consumer, each single-platform with its own
`lineEndings`. For example a controller config edited by a Windows-native editor lives in its own
CRLF repo, not as a subtree inside a Linux `lf` config repo. Fallback only if a subtree genuinely
cannot be split out: keep the global default at the primary consumer and pin the odd subtree with
an `.editorconfig` path override (for example `[<subtree>/**] end_of_line = crlf`) matching its
consumer. Pair the same path override in `.gitattributes`, since both layers must resolve the
path to the same ending.

## Scripts and extensionless executables

Must be LF. A CRLF shebang (`#!/usr/bin/env bash\r`) breaks execution. The paired global LF
defaults cover extensionless executables, shell scripts, and directly executed Python without
path-specific pins. A CRLF-native operational repo adds narrow matching LF overrides in both
files only for scripts it executes on POSIX.

For a type that genuinely needs an ending the `[*]` default no longer supplies (a Windows-native
tool-owned format outside `.bat`/`.cmd`, or a byte-preserve data directory whose exact bytes the
consumer may depend on), still pair a `.gitattributes` pin with a matching `.editorconfig`
override, since the git pin alone is not enough there, `.gitattributes` governs git while the
editor follows `.editorconfig`. For a byte-preserve directory, disable all editor normalization,
not just EOL: `[<dir>/**]` with `charset = unset`, `end_of_line = unset`, `insert_final_newline =
false`, `trim_trailing_whitespace = false` (`unset` is EditorConfig's spec-defined special value
that removes an inherited property, and `**` is needed rather than `*` so a nested file under the
directory is covered too, since `*` excludes `/` and only matches one path component).

## Editing discipline

- **New files**: create with the `.editorconfig`-mandated ending.
- **Editing an existing file**: preserve its current line endings, do not reflow them as a side
  effect of a content change, even if the file is already non-compliant. A tool that rewrites a
  file in text mode (a script, a bulk find/replace) can silently flip CRLF to LF and turn a
  one-line change into a whole-file diff. After any programmatic edit, verify before staging:
  `git diff --stat` should touch only the lines you changed, and a byte check should confirm the
  expected ending. If a diff balloons to the whole file, the endings flipped, restore them and
  re-stage.
- **Fixing a non-compliant file**: bring it to its `.editorconfig` ending as a deliberate change,
  and prefer to isolate it in its own EOL-only commit so the churn is reviewable. When a broader
  maintenance change has to normalize endings alongside content edits, call it out explicitly in
  the commit or PR description and verify the content separately with
  `git diff --ignore-cr-at-eol`.

## Auditing

Don't trust `file` or a naive `git ls-files --eol`. The authoritative check is a byte scan that
classifies by which endings are present: CRLF-only (every `\n` preceded by `\r`), LF-only (no
`\r`), or mixed (both forms present). Flag mixed explicitly rather than lumping it in with CRLF,
and skip binaries via a NUL-byte check. `file` mislabels some types (it reports a CRLF `.json` or
`.code-workspace` as plain "JSON text data" with no CRLF note), and `git ls-files --eol`'s `attr/`
column holds multiple tokens that shift naive field-splitting into false positives. Scope a
repo-wide audit to `git ls-files` plus `git ls-files --others --exclude-standard`, never a raw
`find`, which sweeps self-ignoring caches (`.mypy_cache`, `.artifacts`).

Idempotent normalize: `b.replace(b"\r\n", b"\n").replace(b"\n", b"\r\n")`. A single within-line
string replace is EOL-safe, but a tool that inserts multiple lines or writes a new file into a
CRLF file must emit `\r\n`, since a naive `\n` insert creates mixed endings. `.code-workspace` is
JSONC (it has `//` comments), so strip them before JSON-parsing it.

Editing CRLF files programmatically with a regex has a sharper trap: `.` matches `\r`, so a
captured line keeps its carriage return and rejoining with `\r\n` yields `CRCRLF`. A text-mode
rewrite has the mirror failure, silently flattening CRLF to LF. Prefer line-based edits
(`splitlines(keepends=True)`) or literal replacement over regex reassembly. In Python the
text-mode failure is the default: `Path.read_text()` decodes through universal newlines and
`write_text()` writes `\n` back, so a read-edit-write round trip flattens the whole file while the
edit itself looks correct. Pass `newline=''` to both, or work in bytes.
