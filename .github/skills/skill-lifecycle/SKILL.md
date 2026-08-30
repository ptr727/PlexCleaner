---
name: skill-lifecycle
description: >-
  Governs the lifecycle of the fleet's own skills in ptr727/ProjectTemplate: creating, changing, splitting, and retiring a skill under .agents/skills/, the source-versus-generated split with .github/skills/ and .claude-plugin/, the regenerate and --check semantics of scripts/build_dist.py, the install and stamp semantics of scripts/skills_install.py, the doc-packaging pattern that keeps a law doc and its skill in agreement, and the trigger-description conventions that make a skill fire. Use this whenever about to create, edit, move, or delete anything under .agents/skills/, .github/skills/, or .claude-plugin/, whenever packaging a doc or a doc section as a skill, and whenever deciding whether a topic deserves a skill at all. Triggers even when the edit looks trivial, such as fixing a typo in one SKILL.md, because the generated distributions desync the moment the source changes without a build_dist.py run, and CI fails the pull request on exactly that. Hub-context only, since .agents/skills/ exists only in the hub.
---

# Skill Lifecycle

## Why This Exists

The agent most likely to get a skill wrong is the one editing a skill, and before this skill existed nothing watched that moment: the regenerate and install semantics lived in `scripts/` docstrings and scattered prose, so the procedure was rediscovered per session. The two standing hazards are mechanical and silent. A hand-edit to the generated `.claude-plugin/` tree is overwritten by the next regenerate, and a source edit without a regenerate ships a plugin that no longer matches its source, which the CI `--check` gate fails rather than anyone noticing in review.

## The Pipeline

- **`.agents/skills/<name>/SKILL.md` is the only hand-authored source**, with optional `references/` and `scripts/` directories beside it. Codex and opencode read this tree directly, project-local, and also read the global `~/.agents/skills/` copy the installer materializes.
- **Generated distributions serve GitHub Copilot and Claude Code.** `scripts/build_dist.py` generates `.github/skills/` for GitHub Copilot and a Claude-plugin-compatible copy at `.claude-plugin/fleet-skills/`, published through `.claude-plugin/marketplace.json`. Neither generated tree is hand-edited, and `build_dist.py --check` exits non-zero when either tree differs from `.agents/skills/`.
- **The skill set is implicit.** Every `.agents/skills/<name>/` directory carrying a `SKILL.md` is a skill, and the generated `plugin.json` derives its list from those directories, so adding or retiring a skill edits no manifest by hand. `marketplace.json` names the plugin, not the skills, and is untouched by ordinary lifecycle work.
- **`scripts/skills_install.py`, run from a hub checkout, installs both forms per machine**: an overlay copy into `~/.agents/skills/` for Codex and opencode, marked per skill so a retired skill is removed on the next run and a foreign skill is never touched, and a user-scope plugin install for Claude Code via the `claude` CLI. Each run stamps the hub commit into `~/.agents/skills-install-stamp.json`, and `--report` reads that stamp against the checkout and exits non-zero when the machine is behind. The install is global per user, and per-repo pinning is a settled non-goal (`docs/fleet-map.md` "Skills Install Model").

## Deciding a Topic Deserves a Skill

A skill surfaces at a trigger moment. A rule that binds every action all the time, or a short reference section a task reads once, gains nothing from being one: the always-on layer is the carried instruction set (`AGENTS.md` and the sections it maps), and packaging it as a skill duplicates it and spends the tokens the delegation rules exist to save. The `AGENTS.md` "Where the Rules Live" map records the disposition either way, a skill annotation on the row or the deliberate absence of one, so a topic with no skill reads as a decision rather than an oversight.

## Creating a Skill

1. **Name the directory in kebab-case** and set the frontmatter `name:` to the same string.
2. **Write the `description:` to carry the trigger**, since it is the only part an agent reads before deciding to load the skill: state what the skill governs, then the concrete moments it applies ("Use this whenever..."), then the routine phrasings that precede the failure it guards against ("Triggers even when..."), naming a real incident where one exists. Disambiguate against sibling skills by name, the way `standup-a-repo`, `resync-a-repo`, and `fleet-conformance-check` each state which of the three a session is in.
3. **Author the body per the `comment-and-doc-style` skill**: LF (the repo default), present tense, ASCII tiers, no semicolon in prose. Name hub paths as plain code spans rather than repo-relative links, because an installed copy resolves no repo path, and say "from a hub checkout" for anything the reader must run.
4. **Split bulk into `references/`** when the source doc is large: the SKILL.md carries the summary and the binding rules, and each `references/*.md` carries one topic read on demand, the shape `comment-and-doc-style` uses.
5. **Apply the doc-packaging pattern below in the same change** when the skill packages a law doc or one of its sections.
6. **Regenerate and commit all trees together**: `python3 scripts/build_dist.py`, then, once authorized, commit the source and both generated trees in one commit, per `git-commit-conventions`. CI runs `--check` on every pull request and fails a desynced distribution. `python3 scripts/tests/test_build_dist.py` covers the generator itself.
7. **Record the surfacing**: annotate the `AGENTS.md` "Where the Rules Live" row when the skill packages a GOVERNANCE section, or its closing paragraph when the skill is new content, so the map stays the one place coverage is read from.
8. **Refresh the machines after merge**: re-run `python3 scripts/skills_install.py` per machine, the cadence `docs/host-setup.md` "Fleet Skills Install" states. Until then every machine serves the previous skill set, which `--report` says.

## Changing or Retiring a Skill

- **Edit only the source tree.** Any skill-content change under `.github/skills/` or `.claude-plugin/` that did not come from a `build_dist.py` run is a defect, whatever it fixes.
- **Retiring is deleting the source directory and regenerating.** The derived `plugin.json` list shrinks with it, and the installer's per-skill markers remove the retired skill from `~/.agents/skills/` on each machine's next run.
- **A deletion sweeps the prose that references the skill**, in the same change rather than as follow-up: the `AGENTS.md` map row or paragraph naming it, any law-doc packaging pointer to it, and any sibling skill that disambiguates against it. A law-doc section that had moved its full rules into the skill takes them back, or is retired with it, so no rule is silently lost with the skill that carried it.
- **Renaming is a retire plus a create** as far as the installer's markers and the plugin list are concerned, so sweep references the same way.

## The Doc-Packaging Pattern

Packaging keeps one topic in one authoritative place while the skill makes it surface automatically. It has two shapes, and each pairing states which it uses:

- **Moved content.** The law-doc section keeps a summary and the skill holds the full rules (`git-commit-conventions`, `comment-and-doc-style`, `pr-review-conduct`). The section ends with the standard pointer sentence: packaged as the named skill at `.agents/skills/<name>/SKILL.md` in the hub, not a repo-relative link since that path is hub-local and not carried into every fleet repo, read the skill for the full rules.
- **Kept authority.** The source doc keeps the full rules and the skill is the summary that routes to them (`audit-a-repo` over `AUDIT.md`, `workflow-ci-contract` over `WORKFLOW.md`, `agent-conduct` over its GOVERNANCE sections). The skill states per topic which doc section owns it.

In both shapes the doc wins on any disagreement, and the skill is what needs fixing. A rule stated fully in both places is the drift this pattern exists to prevent, so an edit to a packaged rule lands in its owning place and the other side's summary is checked against it in the same change.
