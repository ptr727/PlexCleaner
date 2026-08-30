# History Rewrites: Re-identification Rules

**A history rewrite includes only the commits that must change, and re-identifies any commit it
rewrites that is not the agent's own.** Filtering history (`git filter-repo` or an equivalent, for
example to strip PII) re-signs every commit it touches with the rewriter's own key, while the
tooling preserves each commit's original `author`/`committer` unless told otherwise. GitHub
verifies a signature against the commit's `committer` identity, so a signature from the rewriter's
key over a commit still committed by a bot (`dependabot[bot]`, `github-actions[bot]`) or GitHub's
own web-flow does not match its committer and lands `unknown_key`/unverified, which a
require-signed-commits rule then rejects.

Two gates keep committer and signature aligned:

1. **Scope the rewrite to only the commits that must be modified.** By default those are the
   rewriter's own, whose committer already matches, so a commit that needs no change stays out of
   the rewrite entirely and its identity and signature are never touched.
2. **If a commit that must change is not the rewriter's own, set its `committer` to the rewriter's
   own signing identity before re-signing** (and its `author` too, since a rewrite that alters
   content should not keep attributing it to the bot). The original bot attribution is deliberately
   given up as the cost of having to rewrite it.

Never leave a signature over a commit committed by another identity. Verify after any rewrite that
every rewritten commit is signed and committed under the correct identity
(`git log --show-signature`).
