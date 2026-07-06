#!/usr/bin/env bash
# Repository configuration as code - the secrets, branch rulesets, and settings the workflows assume
# (see WORKFLOW.md section 6, guarantee D10). Idempotent: `apply` configures a repo to match the JSON in
# this directory; `check` validates an existing repo and exits non-zero on drift (the 5D audit). Run from
# anywhere; the target repo is resolved from the current `gh` context unless $REPO is set (owner/name).
#
#   ./repo-config/configure.sh check     # validate only, no writes (the 5D audit)
#   ./repo-config/configure.sh apply     # create-or-update rulesets + settings (writes)
#
# Requires gh and jq. Both modes read the rulesets and secrets endpoints, which need admin on the repo, so
# gh must be authenticated with admin for `check` as well as `apply`. `check` only reads; `apply` writes.

set -euo pipefail

DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="${REPO:-$(gh repo view --json nameWithOwner --jq .nameWithOwner)}"

# Secrets by store (names only; values are never readable via the API). The Docker Hub credentials, the
# merge-bot App credentials, and CODECOV_TOKEN must be set in BOTH stores: a Dependabot-triggered run gets the
# Dependabot secret store, not Actions secrets, and that run's push CI builds the Docker smoke (logs in to
# Docker Hub) and runs the validate job (uploads coverage to Codecov). Publishing the GitHub release uses the
# built-in GITHUB_TOKEN (no secret needed).
REQUIRED_ACTIONS_SECRETS=(DOCKER_HUB_USERNAME DOCKER_HUB_ACCESS_TOKEN CODEGEN_APP_CLIENT_ID CODEGEN_APP_PRIVATE_KEY CODECOV_TOKEN)
REQUIRED_DEPENDABOT_SECRETS=(DOCKER_HUB_USERNAME DOCKER_HUB_ACCESS_TOKEN CODEGEN_APP_CLIENT_ID CODEGEN_APP_PRIVATE_KEY CODECOV_TOKEN)
REQUIRED_CHECK="Check pull request workflow status job"

note()  { printf '  %s\n' "$*"; }
pass()  { printf '  \033[32mok\033[0m   %s\n' "$*"; }
fail()  { printf '  \033[31mFAIL\033[0m %s\n' "$*"; FAILED=1; }
FAILED=0

ruleset_id() { # name -> id (empty if absent); aborts with a visible reason on an API error
  local out
  # An absent ruleset is a successful call with no match (empty); only a real API error fails. Let gh print its
  # own error on stderr (do not suppress it); add a generic context line and return non-zero so the run stops
  # (the caller's $(...) cannot print the cause itself).
  # per_page=100 returns every ruleset in one array (a repo has only a handful); the default page size is 30.
  if ! out="$(gh api "repos/$REPO/rulesets?per_page=100")"; then
    echo "ERROR: could not list rulesets for $REPO (see gh error above)" >&2
    return 1
  fi
  # shellcheck disable=SC2016  # $n is a jq variable (--arg n), not a shell expansion
  # Select the first match inside jq (not `| head -1`): under pipefail, head closing the pipe early can
  # SIGPIPE jq and fail the function.
  jq -r --arg n "$1" '[.[] | select(.name==$n) | .id] | first // empty' <<<"$out"
}

apply_ruleset() {
  local file="$1" name id
  name="$(jq -r .name "$file")"
  id="$(ruleset_id "$name")"
  if [[ -n "$id" ]]; then
    gh api -X PUT "repos/$REPO/rulesets/$id" --input "$file" >/dev/null
    note "updated ruleset '$name' (#$id)"
  else
    gh api -X POST "repos/$REPO/rulesets" --input "$file" >/dev/null
    note "created ruleset '$name'"
  fi
}

cmd_apply() {
  echo "Applying repository configuration to $REPO"
  apply_ruleset "$DIR/ruleset-develop.json"
  apply_ruleset "$DIR/ruleset-main.json"
  gh api -X PATCH "repos/$REPO" --input "$DIR/settings.json" >/dev/null
  note "patched repository settings"
  gh api -X PUT "repos/$REPO/vulnerability-alerts" >/dev/null
  gh api -X PUT "repos/$REPO/automated-security-fixes" >/dev/null
  note "enabled Dependabot alerts + security updates"
  echo "Done. Run '$0 check' to validate."
}

# --- validation (5D) -------------------------------------------------------------------------------

# assert MESSAGE TEST... - run the test command; pass on success, fail on non-zero (proper if/else, not
# the A && B || C footgun). The test command may read stdin (e.g. a `<<<` heredoc on the assert call).
# Do not redirect the assert call's stdout - that would also swallow the pass/fail line; commands that
# print (jq) use `jq_has`, which silences only itself.
assert() {
  local msg="$1"; shift
  if "$@"; then pass "$msg"; else fail "$msg"; fi
}

# jq_has FILTER... - true iff the jq filter selects something; jq's own output is discarded, not the
# caller's. Reads JSON from stdin.
jq_has() { jq -e "$@" >/dev/null 2>&1; }

# jq_lacks FILTER... - true iff the jq filter yields no truthy value (selects nothing, or only false/null).
# `jq -e` exits 1 (last output false/null) or 4 (no output at all) for the "lacks" cases, 0 for a truthy
# match, and 2/3/5 for a real error (malformed filter or input), which is propagated so the calling assert
# fails loudly. The `|| rc=$?` keeps jq in a list (exempt from set -e) so a non-zero exit captures rc instead
# of aborting. Only stdout is discarded - jq's stderr is kept so a real error shows its diagnostic.
jq_lacks() { local rc=0; jq -e "$@" >/dev/null || rc=$?; case "$rc" in 0) return 1 ;; 1|4) return 0 ;; *) return "$rc" ;; esac; }

check_ruleset() { # name  expected-merge-method  expect-linear(true/false)
  local name="$1" method="$2" linear="$3" id rs
  id="$(ruleset_id "$name")"
  if [[ -z "$id" ]]; then fail "ruleset '$name' missing"; return; fi
  rs="$(gh api "repos/$REPO/rulesets/$id")"
  assert "ruleset '$name' active" \
    test "$(jq -r '.enforcement' <<<"$rs")" = active
  assert "'$name' merge method = $method" \
    test "$(jq -r '.rules[] | select(.type=="pull_request") | .parameters.allowed_merge_methods | join(",")' <<<"$rs")" = "$method"
  assert "'$name' requires signed commits" \
    jq_has '.rules[] | select(.type=="required_signatures")' <<<"$rs"
  assert "'$name' strict status policy off" \
    test "$(jq -r '.rules[] | select(.type=="required_status_checks") | .parameters.strict_required_status_checks_policy' <<<"$rs")" = false
  # shellcheck disable=SC2016  # $c is a jq variable (--arg c), not a shell expansion
  assert "'$name' requires '$REQUIRED_CHECK'" \
    jq_has --arg c "$REQUIRED_CHECK" '.rules[] | select(.type=="required_status_checks") | .parameters.required_status_checks[] | select(.context==$c)' <<<"$rs"
  if [[ "$linear" == "true" ]]; then
    assert "'$name' requires linear history" \
      jq_has '.rules[] | select(.type=="required_linear_history")' <<<"$rs"
  else
    # main must NOT require linear history - it would block the develop -> main merge-commit promotion.
    assert "'$name' does not require linear history" \
      jq_lacks '.rules[] | select(.type=="required_linear_history")' <<<"$rs"
  fi
}

# gh_ok ENDPOINT... - true iff the gh api call succeeds (2xx, including 204). Output and errors are
# discarded, so it is safe to pass to `assert`.
gh_ok() { gh api "$@" >/dev/null 2>&1; }

check_settings() {
  local s; s="$(gh api "repos/$REPO")"
  # Drive every assertion from settings.json, so the check covers exactly the applied desired state and
  # never drifts from the file (add a key there and it is audited here automatically).
  local key want got
  while IFS=$'\t' read -r key want; do
    # shellcheck disable=SC2016  # $k is a jq variable (--arg k), not a shell expansion
    got="$(jq -r --arg k "$key" '.[$k]' <<<"$s")"
    assert "setting $key = $want" test "$got" = "$want"
  done < <(jq -r 'to_entries[] | "\(.key)\t\(.value)"' "$DIR/settings.json")
}

check_security() {
  # apply enables both; audit that they are still on. vulnerability-alerts returns 204 when enabled and
  # 404 when disabled; automated-security-fixes returns { "enabled": true/false }.
  assert "Dependabot vulnerability alerts enabled" gh_ok "repos/$REPO/vulnerability-alerts"
  assert "Dependabot automated security updates enabled" \
    jq_has '.enabled == true' < <(gh api "repos/$REPO/automated-security-fixes")
}

check_secrets() {
  # --paginate: the secrets endpoints page at 30, so without it a repo with many secrets could miss a
  # required name and report a false failure. An API/auth error FAILs fast (the required secrets cannot be
  # verified, so reporting "matches" would be wrong) - distinct from a genuinely missing secret, which also
  # FAILs. gh prints its own error (stderr not suppressed) so the cause is actionable.
  local actions deps
  if ! actions="$(gh api --paginate "repos/$REPO/actions/secrets" --jq '.secrets[].name')"; then
    fail "could not list Actions secrets (API error - cannot verify required secrets)"; return
  fi
  if ! deps="$(gh api --paginate "repos/$REPO/dependabot/secrets" --jq '.secrets[].name')"; then
    fail "could not list Dependabot secrets (API error - cannot verify required secrets)"; return
  fi
  for s in "${REQUIRED_ACTIONS_SECRETS[@]}"; do
    assert "actions secret $s present" grep -qx "$s" <<<"$actions"
  done
  for s in "${REQUIRED_DEPENDABOT_SECRETS[@]}"; do
    assert "dependabot secret $s present" grep -qx "$s" <<<"$deps"
  done
}

check_app() {
  # Best-effort: confirm a GitHub App installation backs the merge-bot automation. A precise check
  # requires app-level auth; presence of the App secrets above is the practical proxy.
  if gh api "repos/$REPO/installation" >/dev/null 2>&1; then
    pass "a GitHub App is installed on the repo"
  else
    note "could not confirm App installation via this token (verify the merge-bot App is installed)"
  fi
}

cmd_check() {
  echo "Validating repository configuration for $REPO"
  check_ruleset develop squash true
  check_ruleset main   merge  false
  check_settings
  check_security
  check_secrets
  check_app
  # Not checkable via gh api beyond name presence: that DOCKER_HUB_ACCESS_TOKEN is valid and has push
  # access to docker.io/ptr727/plexcleaner. Verify it by hand in the Docker Hub account.
  note "verify manually: Docker Hub access token valid with push to docker.io/ptr727/plexcleaner"
  if [[ "$FAILED" -ne 0 ]]; then echo "Configuration drift detected."; exit 1; fi
  echo "Configuration matches."
}

case "${1:-check}" in
  apply) cmd_apply ;;
  check) cmd_check ;;
  *) echo "usage: $0 [apply|check]" >&2; exit 2 ;;
esac
