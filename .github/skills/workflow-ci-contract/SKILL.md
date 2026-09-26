---
name: workflow-ci-contract
description: >-
  Governs the WORKFLOW.md CI/CD behavioral contract for every ptr727/ProjectTemplate fleet repo:
  the D1-D9 guarantees, the output seam by destination (a GitHub release file, a package-registry
  push, an image-registry push, or a filesystem on a host the project owns), the artifact
  lifecycle, NBGV versioning and classification, validate-at-entry, and the 5A/5B/5C test
  methodology. Use this whenever writing or editing anything under .github/workflows/ or a
  composite action under .github/actions/, editing version.json, adding or dropping a release
  target, auditing a repo's workflows, or tracing which job, input, or condition made a publish
  run or skip. This is the YAML half of the pipeline, and `branching-and-release-model` keeps the
  git half, which branch a change targets and which events may publish at all. Triggers even when
  the edit looks mechanical, such as bumping an action, renaming a job, or adding one upload step,
  because SHA pinning, the ruleset-bound aggregator name, smoke gating on uploads, and
  retention-days are each easy to break in a one-line diff that no build fails on. WORKFLOW.md
  keeps authority, and GOVERNANCE.md's Workflow YAML Conventions and Release Model sections win
  where those two overlap.
---

# Workflow CI Contract

## Why This Exists

`WORKFLOW.md` is the fleet's CI/CD behavioral contract. This skill is that contract's surface, so an agent editing workflow YAML has the contract in view. It carries the summary, and `WORKFLOW.md` sections 3, 4, and 5 are each carried whole in `references/` as a generated include. `WORKFLOW.md`'s own canonical-scope note says which of it and `GOVERNANCE.md` is authoritative where the two overlap.

## How the Contract Is Read

- **Outcomes, not bytes.** A workflow is judged against `WORKFLOW.md` section 4's expected inputs and outputs, never against a snippet byte for byte, per `GOVERNANCE.md` "Foundational Principles".
- **Applicability.** A guarantee, or a 5B scenario from `WORKFLOW.md` section 5, governing a construct the repo does not contain is N/A: recorded, excluded from the verdict, never a defect. A source-only pipeline is mostly N/A and that is fine.
- **Operational is binary.** Every applicable guarantee holds, or the workflow is not operational. A single applicable input-output mismatch is a defect regardless of how clean the YAML looks.
- **Reached, not carried.** A standard workflow whose job graph is identical across repos of a type is reached as a hub-hosted `workflow_call` task, per `GOVERNANCE.md` "Hub-Hosted Tooling". The repo's own surface is the caller stub, pinned to a hub release commit, and a composite-action hook at `.github/actions/<hook>` for what is its own. A hub task reaches its own actions and sibling tasks through `$/`, which resolves at that pinned commit. The merge-bot is the first, and `docs/reusable-workflows.md` in the hub carries the model, the hook contract, and the stage each workflow migrates in. Until a workflow's stage ships, its copy is graded against the same contract.
- **Two layers.** The pipeline splits into an orchestrator layer and a build-leaf layer, defined in `WORKFLOW.md` section 3's `Two Layers: Orchestration vs Build` and carried in `references/architecture.md`, while `WORKFLOW.md` section 1's `Two layers when auditing` maps which layer declares which input. Assert an input a guarantee names in the layer that declares it.

## Style Rules

`GOVERNANCE.md` "Workflow YAML Conventions" keeps the style rules, and the `comment-and-doc-style` Skill keeps the line-ending policy, reached from `GOVERNANCE.md` "Documentation Style Conventions" under "Line Endings". Read both before editing a workflow or a composite action.

## The Contract Text

`references/architecture.md`, `references/d-guarantees.md`, and `references/test-methodology.md` carry `WORKFLOW.md` sections 3, 4, and 5 whole, each as a generated include, so the pipeline's architecture, a guarantee's exact wording, and the audit-trace-probe procedure are each one read away rather than restated in full here. A defect in an include region is fixed in `WORKFLOW.md` and regenerated, never edited in this skill, per the `skill-lifecycle` Skill. `WORKFLOW.md` keeps sections 1, 2, and 6 itself, the applicability rule, the style-rule pointer, and the per-project-type walkthroughs, which say which constructs each type adds, map each construct to the scenarios it reaches, and carry three rules for reading a row, one of which is about a repository declaring more than one type, so read those there.

## After Any Workflow Edit

A workflow-only change is not smoke-built, and actionlint still runs on it in CI. `GOVERNANCE.md` "Verification Discipline" requires the repository's whole lint gate before every push, rather than actionlint alone. A workflow change is still only fully exercised by CI, per the same "Verification Discipline" section.

**actionlint discovers `.github/workflows/` recursively, both extensions, and opens no action file on its own.** It reaches a local action's `action.yml` only through a workflow's `uses: ./<path>`, wherever in the tree that path leads, and reports everything it finds there against the calling workflow: the caller's `with:` block against the action's declared inputs, the caller's `steps.<id>.outputs.<name>` against its declared outputs, and the action's own `name`, top-level `description` and `runs.using`. A missing per-input `description` and an unexpected top-level key are the metadata it does not reach that way. Pointing it at an action file directly makes it parse the file as a workflow and report several syntax-check errors, so widening its file list is not available. **Where no workflow in the repository names the action, actionlint reaches it not at all**, which is the ordinary shape for a repository whose hooks are invoked from a hub reusable workflow rather than from a workflow of its own.

**A schema check covers the action file itself**, `check-jsonschema`'s `vendor.github-actions` builtin, run as `uvx check-jsonschema@latest --builtin-schema vendor.github-actions -- <files>`. That one reads every tracked `action.yml` and `action.yaml` under `.github/actions/`, whether or not a workflow references it, and reaches the structure and the keys rather than the caller's contract. The schema and actionlint do not agree on every key: the schema accepts `runs.using: node16`, which actionlint rejects as an invalid runner, so a green schema run is not a statement about what GitHub currently accepts. An action file outside `.github/actions/` keeps actionlint's caller check and gets no schema check at all.

**Neither check reads a composite action's `run:` bodies or its `if:` expressions.** Measured on a referenced action carrying both a malformed `if:` and an unterminated shell `if` in a `run:` block: actionlint and the schema check each pass it. A broken expression or shell body in a composite action therefore surfaces when a run executes it.
