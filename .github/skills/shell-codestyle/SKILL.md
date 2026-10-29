---
name: shell-codestyle
description: >-
  Governs Bash/shell script style for ptr727/ProjectTemplate fleet repos: when a bootstrap or
  host-tool script may be shell instead of Python, the mandatory set -Eeuo pipefail header, the
  pipefail-versus-early-reader pitfall, self-recursive command shims, self-locating scripts, the
  shellcheck-plus-shfmt clean-compile, the --help and unknown-argument rule, and the why-not-what
  comment rule. Use this whenever writing, reviewing, or editing a shell script (a `.sh` file, or
  an extensionless bash/sh shebang script), whenever deciding whether a new script should be Bash
  or Python, or whenever a pipeline built from `curl`/`grep`/`jq`-style commands looks like it
  silently swallowed a failure, or whenever a script or probe writes a wrapper or shim named after
  a command it also calls. Triggers even when the task looks like a one-line tweak to an existing
  script, because a missing `-e`/`pipefail`, or a reader piped straight from a producer that
  closes the pipe early, are each invisible until the exact failure mode they guard against
  actually happens. Fleet-wide: a shell script can appear in any repo (a bootstrap that installs
  the interpreter, a host tool that must run before a toolchain exists), not only a repo whose
  primary language is shell.
---

# Shell Codestyle

## Why this exists

This is the shell-specific half of the fleet's code style guide, kept in one place instead of
re-derived per repo or per session. Shell is the fleet's exception language, reached for only
where Python cannot run yet. So its rules exist to keep that narrow surface safe rather than to
cover general scripting style.

## When shell, not Python

Bash, and only where a program cannot be Python: a bootstrap that installs the interpreter cannot
be written in it. Nor can a host tool that must run before a development toolchain exists depend on
Python. Everything else is Python, with a test under its own scripts tree's
`tests/` directory.

## Rules

- **`shellcheck` is the linter and `shfmt` the formatter.** The clean-compile is `shellcheck`
  clean at default severity plus `shfmt -d`, both reporting nothing before a commit. CI enforces
  both, per `GOVERNANCE.md`'s hub-only "Running the Linters Locally (Known-Working Invocations)"
  section, and the hub's `scripts/docker_lint.py` runs the same pair headless. Neither is scoped
  to the `*.sh` glob alone. A tracked, extension-less script whose shebang names bash or sh joins
  the target list too. That is the shape a script meant to run as a bare command takes.
- **`set -Eeuo pipefail`, before the first command the script runs.** A header comment sits above
  it, as the hub's own `repo-config/configure.sh` and `host-setup/` scripts do. That works because
  what matters is that nothing executes unguarded rather than which line number it lands on.
  Without `-e` a failed command in the middle of a sequence lets the rest run against a state
  nobody checked. Without `pipefail` a pipeline reports the exit of its last stage. So a fetch that
  failed reads as an answer when a parser downstream succeeds on an empty input. `-E` carries an
  `ERR` trap into functions and command substitutions. So a script that later adds one is not
  surprised by where it does not fire.
- **A reader that stops early needs its producer read first.** Under `pipefail`, a producer
  writing to a closed pipe exits non-zero. So `curl ... | grep -q` reports a successful fetch as a
  failure whenever the match is found early enough. Capture the output, then search it.
- **A shim named for a command resolves that command by absolute path.** A wrapper named `jq`,
  placed ahead of the real one on `PATH`, that calls `command jq` or a bare `jq` calls itself. That
  is because `command` skips functions and aliases but not the `PATH` search. Every call starts
  another copy of the wrapper, so the chain grows until something stops it. Run `hash -r`, then
  capture the real path with `type -P` before changing `PATH`. Stop unless it is an absolute path
  that is not the shim itself. Compare the two as files with `[ "$real" -ef "$shim" ]` rather than
  as spellings. Run that test again once the shim is written, removing the shim where it fails.
  Call the real path from inside the shim. An inherited `PATH` can already hold the shim,
  under a symlinked spelling too, and a `.` entry yields a relative path. `type -P` prints a hashed
  path without checking that the file is still there, and `-ef` is false while the shim is not yet
  written. So a shim deleted and rebuilt at an already-hashed path passes a test made only before
  the write while the hash is left in place. `command -v` is not a substitute, since it prints the
  bare name for a shell function of that name.
- **Self-locating, never dependent on the caller's directory.** A script resolves its own
  directory from `BASH_SOURCE` and references its payloads through it. That is because the working
  directory at invocation is not a property of the script.
- **`shellcheck` clean, and a deliberate exception carries its reason inline.** A
  `# shellcheck disable=SCxxxx` names why the rule does not apply here, so the next reader can
  tell a considered exception from an unread warning. The hub's own `repo-config/configure.sh` is
  the worked example. Its `SC2016` disables sit only where shellcheck flags a single-quoted `jq`
  program or GraphQL query that must stay unexpanded. Each carries its reason on the same line.
- **Comments say why, never what.** The code states what it does. A comment restating it goes
  stale silently, where a comment carrying a reason fails visibly when the reason stops being
  true.

## Parsing Structured Formats

<!-- include: CODESTYLE.md > Parsing Structured Formats -->

- **A program reading a structured format uses a maintained parser rather than a hand-written one, where a field-tested parser exists for the runtime in use.** That covers YAML, Markdown, TOML, JSON, a programming language's comments or strings, and a shell command line. The standard library's own parsers count, such as Python's `json`, `tomllib`, `ast`, `tokenize`, and `html.parser`. A hand-written reader converges on the format's full grammar one review finding at a time, and every script that grows one pays that cost again.

<!-- /include -->

## Command-Line Arguments

<!-- include: CODESTYLE.md > Command-Line Arguments -->

A command-line script is one a person or an agent runs by name. A hook its host runs, taking its input from that host rather than from a command line, is not one.

- **Every command-line script answers `--help` and `-h`** with the same text on stdout, then exits 0, changing nothing. That text opens on a usage line naming the script. An agent learns a script's interface by asking it, so a script that reads `--help` as an input acts in a way nobody predicted.
- **An argument the script does not recognize is refused**, with a non-zero exit before any read or write. The refusal names it as an unknown or unrecognized option, argument, flag, or parameter, and PowerShell's own refusal of a parameter it cannot bind counts. An unrecognized first argument is never taken as a default mode or as a target.

<!-- /include -->
