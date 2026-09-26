# Carried Files Carry No Coordination References

Full detail for the "Carried files reference no coordination machinery" rule in `SKILL.md`. Load
this when editing one of the carried files themselves, not when writing an ordinary repo-owned
doc.

## Which files this governs

`AGENTS.md`, `GOVERNANCE.md`, `CODESTYLE.md`, `WORKFLOW.md`, `.github/copilot-instructions.md`,
the `spec/` files and the carried `AUDIT.md`, the files the fleet carries
verbatim or at `intent` fidelity from the hub into every repo. This rule governs carried template
content only. A repo's own `README.md` and topical docs are its own content, never carried
verbatim, and this rule does not reach them.

## What is banned

Three things, in the files above:

1. **Any reference to the template repo**, in prose or in a link. The coordination flow that
   produced a carried file is machinery a consumer of that repo should never have to see, and
   naming where a file came from is exactly the derived-from framing the present-tense rule (in
   `SKILL.md`'s "Markdown formatting" section) independently forbids. Where a carried file must
   express a template-level behavior ("report a rule discrepancy upstream"), state the behavior
   rather than the destination. The maintainer supplies the destination out of band.
2. **A sibling fleet repo named as an illustrative example** ("repo X does it this way", "see repo
   Y's adoption"), which couples the repos and rots as they diverge. To point at a current good
   example, name it in the onboarding or conformance issue, never in a carried doc.
3. **A three-part version or a commit SHA**, full or abbreviated, of any kind, in `AGENTS.md`,
   `GOVERNANCE.md`, `CODESTYLE.md`, or `WORKFLOW.md`. The other files above are outside this item. A
   pin's value ("SHA-pinned at hub release 2.0.x", with a real number in place of the x) is stale at
   the next Dependabot bump, since the pin lives in the workflow or manifest that uses it, and it
   sends the next agent to edit governance for a change that needed none. An illustrative example, a
   minimum version, and a fixed constant read exactly like a copied pin, so no check can tell them
   apart, and the rule covers them too. Write each as its mechanism instead: a pin as "SHA-pinned to
   a hub release, with the release in a trailing comment", an example with a placeholder (`1.0.N`
   publishing as `1.0.(N+1)`, or a dependency bump "from X to Y"), a minimum version by naming the
   manifest or skill that holds it, and a fixed constant by what it is ("the all-zero placeholder
   version"). A two-part language or runtime version, such as a minimum Python minor, stays. The
   audit flags a three-part version, a full SHA, or an abbreviated one in those four files, outside
   their verbatim sections downstream and across the whole file in the hub.
   `.github/copilot-instructions.md` is outside it, since its disproved-claims records name a
   revision by design, which the revision carve-out in `GOVERNANCE.md` "References" permits.

## The two exceptions

**The first exception is a verbatim section**, and `AGENTS.md` "Fleet Bootstrap" is why it exists.
That section's whole function is to name where the canonical rules live, for an agent in a
repository whose carried copies are stale, partial, or absent, which is exactly when no other file
present can say it. Its bytes are fixed fleet-wide, so a repository cannot edit the reference out
without failing the verbatim check instead, and a rule banning it would be unsatisfiable rather
than merely strict. The exception is scoped to the verbatim region and never leaks past it: the
same document's own prose, outside that region, is governed normally. A reference that reaches a
verbatim section is a defect in the canonical, fixed once at the source rather than reported
against every repository carrying it.

**The second exception is a hub-hosted tool the reader is told to run**, which is a different kind
of reference. A rule naming a gate, a script, or a reference snippet the reader executes or copies
states an instruction rather than a provenance, and an instruction with no destination is
unfollowable, which is precisely how a pointer in carried text comes to read as decorative. The
test is whether the reference is something the reader *does* or something that *happened to this
file*: where the content came from stays out, what the reader runs stays in. Such a pointer names
the hub's canonical rather than this repository's provenance, so it is the hub's to keep resolving
and never a repository's to edit out or re-point at a local path. What is reached rather than
carried, and how, is `GOVERNANCE.md` "Hub-Hosted Tooling". In `AGENTS.md` and `GOVERNANCE.md` this
belongs in verbatim rule text, the same region the first exception already covers, so the whole
fleet reads one wording and no repository is asked to answer for a reference it did not write.

## What is not a coordination reference

**A contextually relevant link to a related project is expected, not banned.** Where another repo
is part of this repo's subject matter (the image that consumes this config, the builder that
generates this hardware, a library this depends on), link it normally. The test is whether the
link serves a reader of *this* repo's content, not whether the target happens to be in the fleet.

This pairs with the present-tense rule: state the current shape, not a history of which repo it
came from.
