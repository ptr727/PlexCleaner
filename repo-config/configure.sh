#!/usr/bin/env bash
# Configure or validate a repository against the committed fleet config in this directory, via the GitHub API.
#
#   Apply:  repo-config/configure.sh apply [owner/repo] [release|operational]   # create-or-update settings + rulesets (writes)
#   Check:  repo-config/configure.sh check [owner/repo] [release|operational]   # validate an existing repo, non-zero on drift (reads)
#
# Both modes need admin on the repo, because the rulesets endpoints require it.
# The command defaults to apply, the repo to the current gh repo, and the model to the registry lookup.
# With no registry to consult, the model is inferred from the carried develop payload.
# The model may be passed as the sole positional, as in `configure.sh check operational`.
# The command may be omitted for the apply default, so `configure.sh owner/repo` still applies.
#
# The apply mode writes three groups, in order.
# First settings.json via PATCH, plus has_discussions (public repos only) and default_branch (main, only when it exists).
# Then the Dependabot vulnerability alerts and automated security updates.
# Then the branch rulesets, main.json shared and the model-specific develop ruleset, create-or-update by name.
# The develop ruleset is develop.json where the model is PR-gated, or operational/develop.json for direct signed pushes.
# Applying the same configuration twice changes nothing, so the mode is idempotent.
#
# The check mode is the read-only inverse, where every applied ruleset, setting, and security feature must match.
# The ruleset and settings assertions are driven by the committed payloads, so they stay repo-agnostic.
# That also survives the GitHub API normalizing a stored ruleset, comparing rule presence, merge methods and required checks rather than a byte diff.
# Secrets are per-repo (see spec/secrets.json) and not checkable from a standalone carry, so they are a manual-verify note.
set -Eeuo pipefail

# ----- Command + target + model -----
cmd=apply
case "${1:-}" in apply|check) cmd="$1"; shift ;; esac
repo_arg="${1:-}"
model="${2:-}"
# Allow the model as the sole positional (`configure.sh check operational`): a model name is not a repo.
case "$repo_arg" in release|operational) model="$repo_arg"; repo_arg="" ;; esac
repo="${repo_arg:-$(gh repo view --json nameWithOwner --jq '.nameWithOwner')}"
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# ----- Resolve the workflow model (selects the develop ruleset), shared by apply and check -----
registry="$script_dir/../registry/repos.json"
name="${repo##*/}"
if [ -z "$model" ]; then
    if [ -f "$registry" ]; then
        # Fail fast on a jq or parse error (a malformed registry) rather than silently applying the release default.
        # Silently defaulting would hide a lookup that actually broke.
        # A repo simply absent from the registry is not an error.
        # The expression falls back through defaults.workflowModel to "release", so jq still exits 0 with a value.
        if ! model="$(jq -r --arg n "$name" '(.repos[] | select(.name==$n) | .workflowModel) // .defaults.workflowModel // "release"' "$registry")"; then
            echo "Failed to read workflowModel from $registry (invalid JSON?). Pass the model explicitly (release|operational)." >&2
            exit 1
        fi
    else
        # With no registry to consult (a downstream carry), infer the model from which develop payload is carried.
        # A carry holds exactly its own model's payload.
        # An ambiguous layout (both or neither, as in a partial copy) aborts rather than guesses.
        # A wrong guess would apply or check the wrong develop ruleset.
        if [ -f "$script_dir/develop.json" ] && [ ! -f "$script_dir/operational/develop.json" ]; then
            model="release"
        elif [ -f "$script_dir/operational/develop.json" ] && [ ! -f "$script_dir/develop.json" ]; then
            model="operational"
        else
            echo "Registry $registry not found and the carried develop payloads are ambiguous (expected exactly one of develop.json or operational/develop.json). Pass the model explicitly (release|operational)." >&2
            exit 1
        fi
        echo "Registry $registry not found. Inferred workflow model '$model' from the carried develop payload." >&2
    fi
fi
case "$model" in
    release) develop_ruleset="$script_dir/develop.json" ;;
    operational) develop_ruleset="$script_dir/operational/develop.json" ;;
    *) echo "Unknown workflow model '$model' (expected release or operational)." >&2; exit 1 ;;
esac
main_ruleset="$script_dir/main.json"
settings_file="$script_dir/settings.json"

# ----- Ruleset id lookup (shared by apply and check) -----
# Map a ruleset name to the id of the first match, leaving it empty when nothing matches.
# It warns on duplicates, and aborts on an API error or at the per_page cap, where a single-fetch lookup is unreliable.
ruleset_id() {
    local out ids count
    # Requesting per_page=100 returns every ruleset in one array, since a repo carries only a handful.
    # The response is then a single JSON document, where a paginated fetch would concatenate arrays and break the jq below.
    # Let gh print its own error on stderr.
    # Add a context line and return non-zero, so the caller stops rather than treat an API failure as "not found".
    if ! out="$(gh api "repos/$repo/rulesets?per_page=100")"; then
        echo "Failed to list rulesets for $repo (check auth and repo access)." >&2
        return 1
    fi
    # Fail loud rather than silently narrow, because a full page means the single-fetch assumption no longer holds.
    # A missed lookup would make apply create a duplicate ruleset by name.
    # Abort so the caller stops, since it treats a non-zero return as "stop" and never as "not found".
    if [ "$(jq 'length' <<<"$out")" -eq 100 ]; then
        echo "Failed for $repo: 100 rulesets returned (the per_page cap), so the single-fetch lookup is unreliable. Reduce rulesets or add pagination before applying." >&2
        return 1
    fi
    # shellcheck disable=SC2016  # $n is a jq --arg variable, not a shell expansion
    ids="$(jq -r --arg n "$1" '.[] | select(.name==$n) | .id' <<<"$out")"
    if [ -z "$ids" ]; then return 0; fi
    # Pre-existing drift can leave more than one ruleset with the same name.
    # Use the first and warn, so the duplicates get resolved rather than silently operating on the wrong one.
    # Both grep -c and sed read all their input with no early pipe close, so neither SIGPIPEs jq under pipefail.
    count="$(printf '%s\n' "$ids" | grep -c .)"
    if [ "$count" -gt 1 ]; then
        echo "Warning: $count rulesets named '$1' on $repo. Using the first (resolve the duplicates)." >&2
    fi
    printf '%s\n' "$ids" | sed -n '1p'
}

# =============================== apply ===============================
apply_ruleset() { # payload-file - create-or-update the ruleset by name
    local file="$1" rname id
    if [ ! -e "$file" ]; then
        echo "Ruleset payload $file not found. Aborting to avoid a partially-applied configuration." >&2
        exit 1
    fi
    rname="$(jq -r '.name // empty' "$file")"
    if [ -z "$rname" ]; then
        echo "Ruleset payload $file has no name. Aborting to avoid a partially-applied configuration." >&2
        exit 1
    fi
    id="$(ruleset_id "$rname")"
    if [ -n "$id" ]; then
        echo "Updating ruleset '$rname' (id $id) on $repo"
        gh api --method PUT "repos/$repo/rulesets/$id" --input "$file" >/dev/null
    else
        echo "Creating ruleset '$rname' on $repo"
        gh api --method POST "repos/$repo/rulesets" --input "$file" >/dev/null
    fi
}

cmd_apply() {
    local f private disc payload
    # Pre-flight every required payload before any write, so a partial carry aborts before it half-applies.
    for f in "$settings_file" "$develop_ruleset" "$main_ruleset"; do
        if [ ! -e "$f" ]; then
            echo "Required payload $f not found. Aborting to avoid a partially-applied configuration." >&2
            exit 1
        fi
    done
    echo "Applying configuration to $repo (model: $model)"
    # The writes below silence stdout only, because the success-response JSON is noise.
    # They still fail loud, since gh errors go to stderr and a failed write aborts the script.
    # These writes run unguarded under `set -e`.
    # ----- General repository settings -----
    # Discussions are enabled on public repos only by fleet policy, and never on a private one.
    private="$(gh api "repos/$repo" --jq '.private')"
    disc=false; [ "$private" = "false" ] && disc=true
    # The default branch is main, but only point it there once main exists.
    # Never set the default to a missing branch, as on a repo still living on a rework branch.
    if gh api "repos/$repo/branches/main" --jq '.name' >/dev/null 2>&1; then
        payload="$(jq --argjson d "$disc" '. + {has_discussions: $d, default_branch: "main"}' "$settings_file")"
    else
        payload="$(jq --argjson d "$disc" '. + {has_discussions: $d}' "$settings_file")"
        echo "Warning: $repo has no 'main' branch. Leaving default_branch unchanged." >&2
    fi
    echo "Applying general settings (has_discussions=$disc)"
    printf '%s' "$payload" | gh api --method PATCH "repos/$repo" --input - >/dev/null
    # ----- Dependabot alerts + automated security updates -----
    gh api --method PUT "repos/$repo/vulnerability-alerts" >/dev/null
    gh api --method PUT "repos/$repo/automated-security-fixes" >/dev/null
    echo "Enabled Dependabot vulnerability alerts + automated security updates"
    # ----- Branch rulesets (main shared, develop selected by workflow model) -----
    apply_ruleset "$develop_ruleset"
    apply_ruleset "$main_ruleset"
    echo "Configuration applied to $repo. Run '$0 check${repo_arg:+ $repo}' to validate."
}

# =============================== check ===============================
FAILED=0
note() { printf '  %s\n' "$*"; }
pass() { printf '  ok   %s\n' "$*"; }
fail() { printf '  FAIL %s\n' "$*"; FAILED=1; }

# Run a test command with `assert MESSAGE TEST...`, passing on success and failing on non-zero.
# It is a proper if/else rather than the `A && B || C` footgun.
# Do not redirect the assert call's own stdout, which would swallow the pass or fail line.
# A command that prints, such as jq, goes through jq_has, which silences only itself.
assert() { local msg="$1"; shift; if "$@"; then pass "$msg"; else fail "$msg"; fi; }

# Test with `jq_has FILTER...`, which is true only when the filter selects a truthy value.
# The jq output is discarded, not the caller's.
# It reads its JSON from stdin.
jq_has() { jq -e "$@" >/dev/null 2>&1; }

# Test with `gh_ok ENDPOINT...`, which is true only when the gh api call succeeds, a 204 included.
# Output and errors are both discarded, so it is safe to pass to assert.
# The vulnerability-alerts endpoint is the example, returning 204 when enabled and 404 when disabled.
gh_ok() { gh api "$@" >/dev/null 2>&1; }

check_ruleset() { # payload-file - the live ruleset must match the committed policy, driven by the payload
    local file="$1" rname id live t want got wantc gotc want_enf
    if [ ! -e "$file" ]; then fail "ruleset payload $file missing"; return; fi
    rname="$(jq -r '.name // empty' "$file")"
    if [ -z "$rname" ]; then fail "ruleset payload $file has no name"; return; fi
    if ! id="$(ruleset_id "$rname")"; then fail "ruleset '$rname' - could not resolve id"; return; fi
    if [ -z "$id" ]; then fail "ruleset '$rname' missing"; return; fi
    if ! live="$(gh api "repos/$repo/rulesets/$id")"; then fail "ruleset '$rname' - could not read live state"; return; fi
    want_enf="$(jq -r '.enforcement' "$file")"
    assert "ruleset '$rname' enforcement = $want_enf" test "$(jq -r '.enforcement' <<<"$live")" = "$want_enf"
    # Every rule type the committed payload declares must be present live (payload-driven, so repo-agnostic).
    while IFS= read -r t; do
        # shellcheck disable=SC2016  # $t is a jq --arg variable, not a shell expansion
        assert "'$rname' enforces rule '$t'" jq_has --arg t "$t" '.rules[] | select(.type==$t)' <<<"$live"
    done < <(jq -r '.rules[].type' "$file")
    # For pull_request, the live merge methods must match the payload, which is the develop=squash and main=merge policy.
    if jq_has '.rules[] | select(.type=="pull_request")' "$file"; then
        want="$(jq -c '[.rules[]|select(.type=="pull_request").parameters.allowed_merge_methods[]]|sort' "$file")"
        got="$(jq -c '[.rules[]|select(.type=="pull_request").parameters.allowed_merge_methods[]]|sort' <<<"$live")"
        assert "'$rname' merge methods = $want" test "$got" = "$want"
    fi
    # For required_status_checks, the live required contexts must match the payload.
    if jq_has '.rules[] | select(.type=="required_status_checks")' "$file"; then
        wantc="$(jq -c '[.rules[]|select(.type=="required_status_checks").parameters.required_status_checks[].context]|sort' "$file")"
        gotc="$(jq -c '[.rules[]|select(.type=="required_status_checks").parameters.required_status_checks[].context]|sort' <<<"$live")"
        assert "'$rname' required checks = $wantc" test "$gotc" = "$wantc"
    fi
}

check_settings() {
    local live key want got private wantdisc
    if [ ! -e "$settings_file" ]; then fail "settings payload $settings_file missing"; return; fi
    if ! live="$(gh api "repos/$repo")"; then fail "could not read repository settings"; return; fi
    # Static settings are driven from settings.json, so the check never drifts from the file.
    # Add a key there and it is audited here automatically.
    while IFS=$'\t' read -r key want; do
        # shellcheck disable=SC2016  # $k is a jq --arg variable, not a shell expansion
        got="$(jq -r --arg k "$key" '.[$k]' <<<"$live")"
        assert "setting $key = $want" test "$got" = "$want"
    done < <(jq -r 'to_entries[] | "\(.key)\t\(.value)"' "$settings_file")
    # Dynamic settings apply sets: has_discussions (public repos only), default_branch (main, if it exists).
    private="$(jq -r '.private' <<<"$live")"
    wantdisc=true; [ "$private" = "true" ] && wantdisc=false
    assert "has_discussions = $wantdisc" test "$(jq -r '.has_discussions' <<<"$live")" = "$wantdisc"
    if gh api "repos/$repo/branches/main" --jq '.name' >/dev/null 2>&1; then
        assert "default_branch = main" test "$(jq -r '.default_branch' <<<"$live")" = main
    fi
}

check_security() {
    local sec
    # The vulnerability-alerts endpoint returns 204 when enabled and 404 when disabled, so probe it with gh_ok.
    # The automated-security-fixes endpoint returns a JSON body of { enabled, paused }.
    # That one is captured under an explicit failure guard, so a read error is a clean FAIL rather than a set -e abort.
    assert "Dependabot vulnerability alerts enabled" gh_ok "repos/$repo/vulnerability-alerts"
    if sec="$(gh api "repos/$repo/automated-security-fixes" 2>/dev/null)"; then
        assert "Dependabot automated security updates enabled" jq_has '.enabled == true' <<<"$sec"
    else
        fail "Dependabot automated security updates - could not read the setting"
    fi
}

cmd_check() {
    echo "Validating configuration for $repo (model: $model)"
    check_ruleset "$develop_ruleset"
    check_ruleset "$main_ruleset"
    check_settings
    check_security
    # Secrets are per-repo (spec/secrets.json) and not readable by value.
    # A standalone carry has no registry to derive the required set from, so they are verified by hand rather than asserted here.
    note "verify manually: the repo's required secrets (see spec/secrets.json) are present with valid values"
    if [ "$FAILED" -ne 0 ]; then echo "Configuration drift detected on $repo."; exit 1; fi
    echo "Configuration matches on $repo."
}

case "$cmd" in
    apply) cmd_apply ;;
    check) cmd_check ;;
esac
