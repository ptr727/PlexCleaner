---
name: git-commit-conventions
description: >-
  Governs how an agent stages, commits, signs, and pushes in a ptr727/ProjectTemplate fleet repo:
  default-to-staging vs. explicit commit authorization, why "commit" means commit-and-push, the
  mandatory signed-commit and noreply-identity checks, never force-pushing, how a history rewrite
  must re-identify a commit that is not the agent's own, and the destructive-git-command ban. Use
  this whenever about to run git add/commit/push, whenever authorization to commit is ambiguous
  ("fix this" versus "commit this"), whenever about to configure or verify commit signing or
  git user.email, whenever a merge conflict or a stale branch tempts a force-push or a hard reset,
  and whenever rewriting history (filter-repo, an interactive rebase equivalent) touches a commit
  authored or committed by someone else. Triggers even when the task looks like routine
  housekeeping, such as "clean up this branch" or "just push it", because a scope-widened commit
  authorization, an unsigned commit, a fabricated identity, or a force-push are each easy to do by
  habit and each one is a hard-to-reverse mistake on a shared branch.
---

# Git Commit Conventions

## Why this exists

These are the fleet's mechanical git rules for producing a commit, kept in one place instead of
re-derived per repo or per session: whether to commit at all, what committing implies, how
signing and identity are verified rather than configured, and which commands are never run
without being asked. None of these are style preferences. Branch protection enforces several of
them at push time, and the rest guard against damage a rejected push does not undo (a
scope-widened commit, a rewritten shared history, a destructive reset).

## Staging versus committing

- **Default to staging, not committing.** Stage with `git add` and leave `git commit` to the
  developer unless the developer has explicitly authorized committing for the current ask ("commit
  this", "open a PR"). Authorization is scope-bound: it covers the commits that specific task
  needs, not a blanket license for the rest of the session.
- **Stage by explicit path, never `git add -A` or `git add .`.** A blanket add stages whatever
  else happens to be in the tree, and what it sweeps in is another task's uncommitted work,
  landing in a commit whose subject never mentions it, committed by a session that never saw it.
  That sweep has happened, which is why task isolation exists (the `repo-worktree` skill), and
  isolation makes a shared tree rare rather than impossible. Name the files this task changed,
  and let anything else stay unstaged.
- **"Commit" means commit and push.** An authorization to commit carries the push to the feature
  branch the work belongs on, because nothing reviews a local commit. The Copilot review loop, the
  required status checks, and the maintainer all read the remote, so stopping at `git commit`
  leaves the review unstarted and the branch's state private to one machine, which reads as
  progress while none of the gates have run. Push to the feature branch, never to a protected
  branch, and never with `--force`. Holding a commit locally is the narrower case: it happens when
  the developer asks for it, not by default.
- **Check `git status` before committing, and treat any change this session did not make as a
  stop.** The maintainer hand-edits files live, often `README.md`/`HISTORY.md`, sometimes with an
  editor's LF -> CRLF flip on top, and a sibling agent session sharing the tree leaves its edits
  the same way. Whoever the author is, a change this session did not make is never bundled: ask
  whether to include it, or leave it unstaged and say so, rather than committing half-finished
  work or stranding it in an unrelated commit. An unexpected change in the tree is also the
  signal to re-check isolation per the `repo-worktree` skill, since it may mean another task is
  live in this checkout.

## Signing, verified not configured

- **Every commit must be cryptographically signed (SSH or GPG).** Branch protection enforces this
  on every fleet branch, and an unsigned commit is rejected on push. Signing depends on
  environment configuration (`commit.gpgsign`, `user.signingkey`, `gpg.format`), but none of those
  values prove signing actually works: `gpg.format=ssh` can sign straight from a key file with no
  `ssh-agent` running at all (the common case on Git for Windows), just as GPG can sign
  agent-backed or straight from a keyring. **Probing agent liveness (`ssh-add -L`, a `gpg-agent`
  check) is not a valid test and must not be used.** It tests one specific delivery path, not
  whether a commit actually ends up signed, and a host that signs straight from a key file fails
  that probe while signing correctly.
- **Verify with a real scratch commit, read back with git's own verdict, not a text grep.** This
  single probe is tech-agnostic (SSH agent-backed, SSH key-file, GPG agent-backed, and GPG keyring
  all exercise the same code path) and doubles as the identity check below. Run it once before the
  first agent-authored commit of a session. Don't assume a prior session left config correct. The
  commit below is plain, deliberately no `-S`: forcing it would still succeed on a host where
  `commit.gpgsign` is unset or false, which is the exact default-config gap this probe exists to
  catch, since every real commit an agent makes is plain too:

  The probe is one physical line, not backslash-joined ones, so it copy-pastes cleanly into a
  shell:

  ```sh
  d=$(mktemp -d "${TMPDIR:-/tmp}/sign-check.XXXXXX") && ( trap 'rm -rf "$d"' 0; email=$(git config --global --get user.email) && git init -q "$d" && git -C "$d" commit --allow-empty -q -m check && out=$(git -C "$d" log -1 --format='sig=%G? author=%an <%ae> committer=%cn <%ce>') && echo "$out" && ae=$(git -C "$d" log -1 --format='%ae') && ce=$(git -C "$d" log -1 --format='%ce') && case "$out" in sig=G\ *|sig=U\ *) true ;; *) false ;; esac && case "$email" in *@users.noreply.github.com) true ;; *) false ;; esac && [ "$ae" = "$email" ] && [ "$ce" = "$email" ] )
  ```

  PowerShell equivalent:

  ```powershell
  $d = Join-Path $env:TEMP ([guid]::NewGuid())
  try {
    $email = git config --global --get user.email
    git init -q "$d" `
      && git -C "$d" commit --allow-empty -q -m check
    $out = git -C "$d" log -1 --format='sig=%G? author=%an <%ae> committer=%cn <%ce>'
    $out
    $ae = git -C "$d" log -1 --format='%ae'
    $ce = git -C "$d" log -1 --format='%ce'
    if ($out -notmatch '^sig=[GU] ' -or $email -notmatch '@users\.noreply\.github\.com$' `
        -or $ae -ne $email -or $ce -ne $email) {
      throw "signing/identity check failed: $out"
    }
  } finally {
    if (Test-Path "$d") { Remove-Item -Recurse -Force "$d" }
  }
  ```

  `sig` must read `G` (good signature) or `U` (good signature, unrecognized signer). For GPG, `U`
  is a valid signature from a key whose trust level is merely undefined, common right after
  generating a new key. For SSH, it's a valid signature from a key not found in the local
  `allowed_signers` file, which doesn't affect whether GitHub itself verifies the commit, only
  local `git verify-commit` output. `sig` is git's own verdict char. Don't grep localized
  "Good" text, since that varies by git version and locale. Anything else, or the commit failing
  outright, means **do not commit**: surface the actual error to the developer and stop at
  `git add`. Nothing else is contrary evidence: not an unreachable agent, not a config value, not a
  signature type you can't otherwise explain in past history (see below).
- **A mix of SSH- and GPG-signed commits in history is structural, not a host to track down.**
  `git log --pretty='%G? %GK'` shows two distinct shapes, not two health states: a commit committed
  by the PR's own author carries that host's own signature type, while a commit committed by
  `GitHub <noreply@github.com>` is a squash-merge: GitHub creates and signs that commit itself,
  server-side, with GitHub's own GPG key, regardless of what the PR author signed with locally.
  Every commit on `develop`/`main` past its first squash-merge shows `GitHub` as committer and a
  GPG signature. That's expected on every fleet repo, on every host, and is not evidence anything
  is misconfigured. Check `commit.committer.name` before treating a differing signature type as a
  clue worth chasing.
- **Signing must be live before the *first* commit, not retrofitted.** Turning on a
  require-signed-commits rule against a branch that already carries unsigned commits forces a
  rewrite of that entire history to re-sign it, changing every commit SHA and making whoever does
  the rewrite the committer and signer of every commit in it (a rebase preserves `author` but not
  the original signatures, and one contributor cannot sign for another). During new-repo setup,
  never create commits until signing is verified.

## Identity, verified not set

**Commit under the committing account's own GitHub `noreply` identity, never a private, personal,
or invented address.** `author` and `committer` on every agent-authored commit are the GitHub
`noreply` address of the account whose key signs the commit, in `username@users.noreply.github.com`
or `ID+username@users.noreply.github.com` form. **Verify it, do not set it**: the scratch commit
from the signing check above already proves this end-to-end. Read its `author=`/`committer=`
output rather than trusting `git config --get user.email` alone, since a global config value
doesn't prove what actually lands on a commit object, and read both rather than the author alone
since a rebase, amend, or cherry-pick can rewrite the committer while leaving the author
untouched. Match both against that address before committing, rather than
writing a repo-local override. The identity is host configuration set globally once, so a repo-local
`user.email` is redundant where the global is right and a silently-shadowing wrong identity where
it is not. A mismatch is a host fault to surface to the maintainer, not to patch per repo, because
a local override hides a broken host that then commits wrong in every other repo on that machine.
A wrong identity is not cosmetic: a private email trips GitHub's email-privacy push protection, and
an invented author pollutes history. It is also a distinct failure from signing (a wrong author
does not by itself fail the signature check), though the ad-hoc identities that produce one are
typically also unsigned, which the signing rule above then rejects independently.

## Never force push

Do not run `git push --force` or `git push --force-with-lease` under any circumstances. Force
pushing rewrites shared history and can cause data loss. This holds regardless of how confident
the rewrite looks, a rejected push is recoverable, a force-pushed one is not.

## History rewrites re-identify only what changed

**Do not rewrite a commit that does not need to change.** A history rewrite (e.g. `git filter-repo`
to strip PII) re-signs every touched commit with the rewriter's key. If that commit is still
committed by a bot (`dependabot[bot]`, `github-actions[bot]`) or GitHub's own web-flow, the
signature will not match the committer and the require-signed-commits rule rejects it. Scope the
rewrite to only the commits that must change. Set `committer` (and `author`) to the rewriter's
identity on any non-own commit that must be modified. Verify with `git log --show-signature` after
any rewrite. See `references/history-rewrite.md` for the full two-gate rule.

## Never run destructive git commands without being asked

`git reset --hard`, `git checkout .`, `git restore .`, `git clean -f`, and other commands that discard work require explicit developer instruction. Never use them as a convenience inside a larger task. One narrow cleanup exception applies to `git branch -D <exact-task-branch>` after a squash merge. It requires live proof that the pull request for that exact branch merged and a clean worktree at the verified head SHA, per `repo-worktree`. The exception never applies to `develop`, an unmerged branch, an unresolved pull request, or a branch with uncommitted work.
