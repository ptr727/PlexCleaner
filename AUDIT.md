# AUDIT.md

How this repository audits itself against its committed baseline and reports drift. This is the repo-scoped adaptation of the fleet-wide AUDIT.md kept at the fleet hub (carried per the [repo-config downstream carry][repo-config-readme]); the hub's fleet-wide audit remains authoritative. The ground truth here is the committed [`repo-config/`][repo-config] payloads and [`spec/secrets.json`][secrets]; the prose authorities are [`AGENTS.md`][agents], [`CODESTYLE.md`][codestyle], and [`WORKFLOW.md`][workflow].

The audit is read-only: it diffs live state against the committed baseline and reports findings; it never applies changes. The verdict vocabulary is [`WORKFLOW.md`][workflow]'s: **operational / not operational**, **N/A**, **defect**, and the applicable/absent rule.

## Scope

This is a release-model repo: the self-audit covers the `main` and `develop` rulesets, general repository settings, and secret names. Code-project conformance (analyzers, tests, coverage, publish workflows) is CI's job and the fleet hub's fleet-wide audit's, not this self-audit's - see [AGENTS.md "Branching Model"][agents-branching-model] for the model this baseline encodes.

## General Settings

Diff the live repository settings against [`repo-config/settings.json`][repo-config-settings]. The two state-dependent settings are not in the file: `has_discussions` follows visibility (public on / private off) and `default_branch` is `main`.

```sh
repo="$(gh repo view --json nameWithOwner --jq '.nameWithOwner')"
live=$(gh api "repos/$repo" --jq '{has_wiki,has_projects,allow_merge_commit,allow_squash_merge,allow_rebase_merge,allow_auto_merge,allow_update_branch,delete_branch_on_merge}')
diff <(jq -S . repo-config/settings.json) <(jq -S . <<<"$live") \
  && echo "settings: in sync" || echo "settings: DRIFT"
```

## Rulesets

Diff each live ruleset against the committed expected payload with a normalized comparison (sort the order-insensitive `rules[]` and `bypass_actors[]` before diffing so a reordered but equivalent ruleset does not read as drift). This release carry keeps its `develop` payload at [`repo-config/develop.json`][repo-config-develop].

```sh
repo="$(gh repo view --json nameWithOwner --jq '.nameWithOwner')"
norm='{name,target,enforcement,bypass_actors,conditions,rules} | .rules|=sort_by(.type) | .bypass_actors|=sort_by(.actor_id)'
for b in develop main; do
  file="repo-config/$b.json"
  id=$(gh api "repos/$repo/rulesets" --jq ".[]|select(.name==\"$b\").id")
  diff <(jq -S "$norm" "$file") \
       <(gh api "repos/$repo/rulesets/$id" --jq '{name,target,enforcement,bypass_actors,conditions,rules}' | jq -S "$norm") \
    && echo "$b: in sync" || echo "$b: DRIFT"
done
```

The result must be exactly two rulesets named `develop` and `main` - a missing ruleset or a divergent payload is a **defect**; a duplicate or stray ruleset is a **drift finding**.

## Secrets

Confirm each name [`spec/secrets.json`][secrets] requires exists in the stores its mechanism claims, and no forbidden name is present (names only; values are not readable). The baseline App pair and the Docker Hub pair live in both the Actions and Dependabot stores; `CODECOV_TOKEN` is claimed in the Actions store.

```sh
repo="$(gh repo view --json nameWithOwner --jq '.nameWithOwner')"
for store in actions dependabot; do
  names=$(gh api "repos/$repo/$store/secrets" --jq '.secrets[].name')
  want="CODEGEN_APP_CLIENT_ID CODEGEN_APP_PRIVATE_KEY DOCKER_HUB_USERNAME DOCKER_HUB_ACCESS_TOKEN"
  [ "$store" = "actions" ] && want="$want CODECOV_TOKEN"
  for s in $want; do
    grep -qx "$s" <<<"$names" && echo "$store/$s: present" || echo "$store/$s: MISSING (defect)"
  done
  for s in CODEGEN_APP_ID; do
    grep -qx "$s" <<<"$names" && echo "$store/$s: forbidden name present (defect)" || true
  done
done
```

## Verdict and Follow-Up

A missing required item or a divergent payload is a **defect** (not operational); an equivalent outcome in a non-standard form is a **drift finding**. N/A items are excluded, never counted as failures. Surface findings as repository issues; fixes land as a pull request to `develop` per [AGENTS.md "Branching Model"][agents-branching-model]. To re-apply the whole baseline, run `repo-config/configure.sh` (see [repo-config/README.md][repo-config-readme]).

<!-- Repo -->

[agents]: ./AGENTS.md
[agents-branching-model]: ./AGENTS.md#branching-model
[codestyle]: ./CODESTYLE.md
[repo-config]: ./repo-config/
[repo-config-develop]: ./repo-config/develop.json
[repo-config-readme]: ./repo-config/README.md
[repo-config-settings]: ./repo-config/settings.json
[secrets]: ./spec/secrets.json
[workflow]: ./WORKFLOW.md
