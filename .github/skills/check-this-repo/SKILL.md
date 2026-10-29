---
name: check-this-repo
description: >-
  Checks, from inside a downstream repo's own session, whether this repo and this machine are
  current against the ptr727/ProjectTemplate hub, and safely self-applies what it can. Use this
  whenever asked to check if this repo is up to date with the hub, whenever a fleet rule or Skill
  seems to not be applying and the cause is unclear, or whenever about to work in a fleet repo and
  wanting to confirm the ground under that work is current before trusting it. Needs no standing
  hub checkout of its own and no named target repo, only the repo the session is already in,
  though the check itself fetches a hub checkout to reach scripts/skills_install.py, since
  scripts/ is hub-hosted rather than carried. This is the counterpart to resync-a-repo, which
  needs both a hub checkout already in hand and a named external target to drive change from the
  hub side instead. Also triggers on "why do I have to keep restating this rule every session,"
  since a stale or missing Skills install is the most common cause and the cheapest one to rule
  out first.
---

# Check This Repo

## Why this exists

A downstream repo today only finds out it has drifted when someone runs an audit or a resync
against it. Nothing notices from the inside on its own. This skill is that inside check,
run with no hub-side operator watching. It lets a stale Skills install or an out-of-date
`AGENTS.md` pointer get noticed and fixed. That happens without waiting for a fleet-wide sweep to
reach this particular repo.

## What it checks

1. **Is the Skills install current on this machine.** `scripts/` is hub-hosted and reached rather
   than carried, per GOVERNANCE.md "Hub-Hosted Tooling". So fetch a hub checkout
   (`github.com/ptr727/ProjectTemplate`, `main` branch, fetched fresh) and run
   `python3 scripts/skills_install.py --report` from it. A snapshot not current, or no stamp, is very often
   the direct answer to "why isn't a fleet rule applying". That is because the harness never loaded the current
   content in the first place, and no amount of re-reading `GOVERNANCE.md` fixes that. For a
   Claude Code session, read `live` as well, since that channel loads the registered directory in
   place rather than the copy. A registered directory that is missing is an answer there whatever
   the exit code says. So is a `live.plugin` reading `installed: false` or `enabled: false`. That reading
   describes the user-scope install the installer makes, as Claude Code reports it for the
   directory the report runs in. A project-level install does not count. A null there means the plugin listing could not be read.
   - Where `live` carries `vcs: archive`, the channel serves the tree the hub's
     `host-setup/bootstrap.sh` or `bootstrap.ps1` keeps. That tree has no git, so `branch: null` there
     is not a detached checkout. A `live.commit` behind the hub's `main` is the answer. So is
     a null one, which means the bootstrap could not resolve the commit it fetched. Re-running
     `bootstrap.sh --skills` or `bootstrap.ps1 -Skills` is the fix. Where an earlier run kept the tree somewhere other than the
     default, pass the re-run `--dir` or `-Dir` naming the parent of `live.checkout`. That is because re-running the installer from any other directory leaves an existing
     registration where it is.
   - Where `live` carries no `vcs` and a non-null `commit`, it names a git checkout. One that
     is detached, `branch: null`, or on an old branch is an answer too. Moving that checkout
     is the fix rather than re-installing.
   - Any other shape names no registration, or one this report could not read, and the `reason`
     it carries, where it carries one, says which.
2. **Does this repo's own carried content still match the hub.** Compare `AGENTS.md`'s
   "Where the Rules Live" pointer text, and any other verbatim `AGENTS.md`/`GOVERNANCE.md` section
   this repo carries, against the same hub checkout's current wording. Do it by reading the text rather
   than by feel.

## What it is safe to fix on its own

- **Re-run the installer**, `python3 scripts/skills_install.py --snapshot-only`, from that same
  `main` checkout, when `--report` exits non-zero. `--snapshot-only` refreshes the copy and leaves
  the Claude Code registration alone, since a fetched checkout is not one that registration should
  load from.
  This is a per-machine, local-only change, nothing in it touches this repo's git history or
  needs a review.

Nothing else. This skill never re-vendors a carried file, never deletes one, and never applies a
setting or ruleset. Converging that drift is a resync, a separate change on its own branch. It is run
per the hub's `RESYNC.md` by this repo's own session or by `resync-a-repo` from a hub checkout.

## Refresh cadence

This skill refreshes only when `--report` exits non-zero, and only with the `--snapshot-only` run
above, from its own freshly fetched `main` checkout. A copy taken from `develop` reads not current
by design, since the snapshot is judged against the promoted revision. Session entry runs no
automatic check, by design. The trigger is suspicion, and the restated-rule symptom this skill
triggers on is the loudest form of it. The refresh from the maintainer's own long-lived hub
checkout is a different run, and `docs/host-setup.md` "Fleet Skills Install" in the hub states its
cadence.

## What it escalates instead of touching

- **A carried section that differs from the hub in a way that reads as a genuine local addition**
  rather than plain staleness. That is the exact case `carried-instruction-file-guard` exists to protect.
  Report precisely what differs and stop there, naming a resync as the next step, where
  `carried-instruction-file-guard` decides the merge.
- **Anything the installer alone cannot resolve**, a broken `claude` CLI marketplace
  registration, a settings or ruleset drift, a workflow interface mismatch. Name it and hand it to
  the maintainer or a resync per the hub's `RESYNC.md` rather than patching around it locally.

## Answering "why isn't a fleet rule applying"

Check the install stamp first, before assuming a Skill's description is worded wrong or that the
rule was never carried to this repo at all. It is the most common cause, and it is the cheapest
one to confirm.
