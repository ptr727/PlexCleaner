# AUDIT.md

How this repository audits itself against its committed baseline and reports drift. General settings, rulesets, and secret names are hub-hosted ground truth in `repo-config/` and `spec/secrets.json`. They are checked from a hub checkout rather than carried here, per [GOVERNANCE.md "Hub-Hosted Tooling"][governance-hub-hosted-tooling]. The prose authorities are [`GOVERNANCE.md`][governance], [`CODESTYLE.md`][codestyle], and [`WORKFLOW.md`][workflow].

The audit is read-only. It diffs live state against the committed baseline and reports findings, and it never applies changes. The verdict vocabulary is [`WORKFLOW.md`][workflow]'s: **operational / not operational**, **N/A**, **defect**, and the applicable/absent rule.

## Scope

This is a release-model repository. The self-audit covers the `main` and `develop` rulesets, general repository settings, and secret names. Code-project conformance is the job of CI, not of this self-audit. See [GOVERNANCE.md "Branching Model"][governance-branching-model] for the model this baseline encodes.

## General Settings and Rulesets

Fetch the hub and check out `main`. Check out `develop` instead while this repository's registry entry has not yet reached `main`. Run `repo-config/configure.sh check ptr727/PlexCleaner release` from that checkout. The command checks what `repo-config/configure.sh apply` writes, and the deployment environments the registry declares. It reports each ruleset's `bypass_actors` without asserting them, since who may bypass a ruleset is a human decision no payload declares. The token needs project access as well as admin on the repository, since the command reads the fleet project link through GraphQL.

The result must be exactly two rulesets named `develop` and `main`. A missing ruleset or a divergent payload is a **defect**, and a duplicate or stray ruleset is a **drift finding**.

## Secrets

From the same hub checkout, run `uv run --no-project --script spec/audit.py PlexCleaner` and read its secret findings. It resolves the required set from the hub's `spec/secrets.json` plus this repository's registry entry. It confirms each required name exists in the stores its mechanism claims, by name only and never by value. `repo-config/configure.sh check ptr727/PlexCleaner release` asserts the same names per store. `repo-config/configure.sh apply ptr727/PlexCleaner release` writes each one from the host's `~/.secrets/` directory.

For PlexCleaner the required set is the baseline App pair, the Docker Hub pair, and `CODECOV_TOKEN`, each in both the Actions and Dependabot stores. A workflow run that a Dependabot pull request triggers reads the Dependabot store, so without it the coverage upload is silently skipped.

## Verdict and Follow-Up

A missing required item or a divergent payload is a **defect** (not operational). An equivalent outcome in a non-standard form is a **drift finding**. N/A items are excluded, never counted as failures. Surface findings as repository issues, each naming the hub commit its run read.

<!-- Repo -->

[codestyle]: ./CODESTYLE.md
[governance]: ./GOVERNANCE.md
[governance-branching-model]: ./GOVERNANCE.md#branching-model
[governance-hub-hosted-tooling]: ./GOVERNANCE.md#hub-hosted-tooling
[workflow]: ./WORKFLOW.md
