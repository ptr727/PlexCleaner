---
name: fleet-conformance-check
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

# Fleet Conformance Check

## Why this exists

A downstream repo today only finds out it has drifted when someone runs a hub-driven resync
against it by name. Nothing notices from the inside on its own. This skill is that inside check,
run with no hub-side operator watching, so a stale Skills install or an out-of-date `AGENTS.md`
pointer gets noticed and fixed without waiting for a fleet-wide sweep to reach this particular
repo.

## What it checks

1. **Is the Skills install current on this machine.** `scripts/` is hub-hosted and reached rather
   than carried, per GOVERNANCE.md "Hub-Hosted Tooling", so fetch a hub checkout
   (`github.com/ptr727/ProjectTemplate`, `main` branch, fetched fresh) and run
   `python3 scripts/skills_install.py --report` from it. A stale or missing stamp is very often
   the direct answer to "why isn't a fleet rule applying": the harness never loaded the current
   content in the first place, and no amount of re-reading `GOVERNANCE.md` fixes that.
2. **Does this repo's own carried content still match the hub.** Compare `AGENTS.md`'s
   "Where the Rules Live" pointer text, and any other verbatim `AGENTS.md`/`GOVERNANCE.md` section
   this repo carries, against the same hub checkout's current wording, by reading the text rather
   than by feel.

## What it is safe to fix on its own

- **Re-run the installer**, `python3 scripts/skills_install.py`, when the stamp reports stale.
  This is a per-machine, local-only change, nothing in it touches this repo's git history or
  needs a review.

Nothing else. This skill never re-vendors a carried file, never deletes one, and never applies a
setting or ruleset. Those are `resync-a-repo`'s job, driven from the hub with a named target,
never a downstream repo acting on itself.

## Refresh cadence

Re-run the installer when `--report` exits non-zero, and after any hub merge that touches
`.agents/skills/`. Session entry runs no automatic check, by design: the trigger is suspicion,
and the restated-rule symptom below is the loudest form of it. `docs/host-setup.md`
"Fleet Skills Install" in the hub states the same cadence for the host side, and an automated
refresh stays out of scope until the fleet has evidence the manual cadence fails.

## What it escalates instead of touching

- **A carried section that differs from the hub in a way that reads as a genuine local addition**
  rather than plain staleness, the exact case `carried-instruction-file-guard` exists to protect.
  Report precisely what differs and stop there. Per AUDIT.md, a downstream repo does not write its
  own audit report or resync itself against the hub, it names what it found and points at
  `resync-a-repo`, run from a hub checkout, as the next step.
- **Anything the installer alone cannot resolve**, a broken `claude` CLI marketplace
  registration, a settings or ruleset drift, a workflow interface mismatch. Name it and hand it to
  the maintainer or a hub-driven resync rather than patching around it locally.

## Answering "why isn't a fleet rule applying"

Check the install stamp first, before assuming a Skill's description is worded wrong or that the
rule was never carried to this repo at all. It is the most common cause, and it is the cheapest
one to confirm.
