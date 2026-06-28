# repo-config

Repository configuration as code - the parts of "operational" that live in GitHub settings rather than
in workflow YAML: branch rulesets, repository settings, and the secrets the workflows read. This is the
concrete form of [`WORKFLOW.md`](../WORKFLOW.md) section 6 and guarantee **D10**, and the implementation
of its **5D configuration audit**.

This directory is intentionally **not** under `.github/` - that path is GitHub's own (workflows, issue
templates); repository administration config-as-code is the maintainer's, so it lives here.

## Files

- [`configure.sh`](./configure.sh) - idempotent `gh api` script with two modes:
  - `./repo-config/configure.sh check` - validate only, no writes; exits non-zero on drift (the 5D
    audit). Read-only, but it reads the rulesets and secrets endpoints, so it still needs a `gh` token
    with admin on the repo.
  - `./repo-config/configure.sh apply` - create-or-update the rulesets and settings to match this
    directory (needs admin; writes).
- [`ruleset-develop.json`](./ruleset-develop.json) - the `develop` branch ruleset (squash-only, linear
  history, signed commits, the required status check, strict-status **off**).
- [`ruleset-main.json`](./ruleset-main.json) - the `main` branch ruleset (merge-commit-only, signed
  commits, the same required check, strict **off**; no linear-history rule).
- [`settings.json`](./settings.json) - repository settings (auto-merge on; squash **and** merge-commit
  allowed; rebase off; auto-delete-on-merge **off**). Auto-delete is **off** so a `develop -> main` promotion
  does not delete `develop` (GitHub's auto-delete would remove the merged head branch); the trade-off is that
  merged bot and feature branches are not auto-removed - clean them up manually (the merge UI's delete button
  or `gh pr merge --delete-branch`).

## What it does not store

Secret **values** are never readable through the API, so the script only asserts the required secret
**names** exist (`DOCKER_HUB_USERNAME` / `DOCKER_HUB_ACCESS_TOKEN` for the image, and the App credentials
`CODEGEN_APP_CLIENT_ID` / `CODEGEN_APP_PRIVATE_KEY` for the merge-bot) and that a GitHub App is installed.
The Docker Hub and App credentials must be set in **both** the Actions and Dependabot secret stores, since a
Dependabot-triggered run gets the Dependabot store. Set the values in the repository (or organization) secret store directly. There is no
NuGet publishing here; the GitHub release uses the built-in `GITHUB_TOKEN`. The Docker Hub access token's
validity and push scope are verified by hand, not by this script.

## Applying, and the required-check rename lockstep

The live ruleset's required status check is matched by **name** to the aggregator job in
[`test-pull-request.yml`](../.github/workflows/test-pull-request.yml) (`Check pull request workflow
status job`). GitHub binds the check by that exact string, so the ruleset JSON here, the live ruleset, and
the aggregator job name must move **in lockstep** ([`WORKFLOW.md`](../WORKFLOW.md) D6.2). If they drift, a
pull request runs CI but its required check never resolves and the PR cannot merge.

So whenever the ruleset JSON or that job name changes, run `apply` against the live repo in the same
change that ships the workflow edit, then `check`:

```sh
REPO=ptr727/PlexCleaner ./repo-config/configure.sh apply   # sync live rulesets + settings + security
REPO=ptr727/PlexCleaner ./repo-config/configure.sh check   # confirm no drift
```

First-time adoption is the same step: the live ruleset predates the renamed aggregator, so the first
`apply` is what lets a pull request against the new workflows go green. Both modes need a `gh` login
with admin on the repo (the rulesets and secrets endpoints require it). `apply` writes, `check` only
reads.

## Why both a script and JSON

The JSON files are the unambiguous source of truth for the configuration; the script applies and audits
them idempotently. An agent can also derive the same checks on the fly from `WORKFLOW.md` section 6, but
the committed script and JSON codify the exact intended state so the configuration is reproducible and
diffable rather than tribal knowledge.
