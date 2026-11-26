# History Rewrites: Re-identification Rules

**Every commit a history rewrite reaches needs a valid signature again. The pass that re-signs it re-identifies the committer of each commit that another identity committed.** The facts below about the tooling drive every rule after them.

- **A rewrite cascades to every descendant.** A commit's hash covers its parents' hashes. So changing one commit changes the hash of every commit below it, on every branch that holds it. Rewriting a commit near the root rewrites nearly the whole branch, however few commits the edit itself changes.
- **`git filter-repo` strips signatures and signs nothing.** Its documentation says a rewritten commit's signature "cannot continue to work" and is removed. A run strips the signature from every commit it processes, including one whose content it leaves alone. By default it processes every ref. So every commit it emits lands unsigned, which a require-signed-commits rule rejects.
- **GitHub verifies a signature against the commit's `committer`, not its `author`.** The REST API's `verification.reason` values `no_user`, `unverified_email`, and `bad_email` are each defined by the committer email. So a rewriter's valid signature over a commit still committed by a bot (`dependabot[bot]`, `github-actions[bot]`) or GitHub's own web-flow lands unverified.

These gates keep committer and signature aligned:

1. **State the reach before rewriting anything.** Rewrite only when a commit must change, since no scoping stops the cascade to every descendant of the earliest commit that changes. A default `git filter-repo` run reaches every commit on every ref. Also list what is anchored to the old hashes, from "Collateral" below. Record which tags are signed, since a rewrite can strip a tag's signature:

   ```sh
   git for-each-ref --format='%(if)%(contents:signature)%(then)%(refname:short)%(end)' refs/tags
   ```

2. **Re-sign every rewritten commit in a signing pass that also sets the committer.** A tool that creates each commit through `git commit-tree -S` signs it. The command below re-signs every commit on every local branch and moves each tag with it, which fits a default `git filter-repo` run. It keeps merge commits, authors, and both dates. Before running it, record each branch's `git rev-list --count "<branch>"` and `git log --format=%T "<branch>"` output. Replace the placeholders with the rewriter's own signing identity:

   ```sh
   git filter-branch --tag-name-filter cat --commit-filter 'GIT_COMMITTER_NAME="<name>" GIT_COMMITTER_EMAIL="<email>" git commit-tree -S "$@"' -- --branches --tags
   ```

3. **Re-identify the committer, and keep the author.** The command above sets the committer on every commit, the rewriter's own included. Leave `author` as the rewrite left it, an identity correction included, since verification reads only the committer.
4. **Verify against GitHub, not the local checkout.** `git log --show-signature` checks a signature against the local keyring or allowed-signers file. It knows nothing of the account GitHub matches the committer to, so a commit can read good locally and still land unverified. Before the push, confirm the signing pass changed metadata only. Each branch's commit count and its `git log --format=%T` tree hashes match the values recorded before the signing pass. Publishing the rewrite is a force push, so an agent stops here and hands the push to the maintainer. After the maintainer's push, read every commit on each rewritten branch through the REST API and require `verified: true` with `reason: valid` on all of them. A single output line ending `true valid` is a pass, and any other line names a failing reason:

   ```sh
   out=$(gh api --paginate "repos/<owner>/<repo>/commits?sha=<branch>&per_page=100" --jq '.[].commit.verification | "\(.verified) \(.reason)"') && printf '%s\n' "$out" | sort | uniq -c
   ```

Never leave a signature over a commit committed by another identity, and never leave a rewritten commit unsigned.

## Collateral

What is anchored to an old hash goes stale when the rewrite lands, including the following:

- **Tags** move to the re-signed commits through `--tag-name-filter cat` above. A signed tag's signature no longer verifies after the rewrite, so re-create each tag gate 1 recorded as signed with `git tag -f -s "<tag>" '<tag>^{commit}'`.
- **Remote tags and pull request refs** keep the old commits readable on GitHub. Each moved tag is pushed again, and GitHub keeps every pull request's commits until GitHub Support purges them.
- **Other clones** diverge from the rewritten branch, so each one is re-cloned rather than pulled.
- **Published artifacts** that record a commit hash keep naming a hash the branch no longer holds, and no rewrite reaches them.
