# Qodo Global Configuration Probe

This file exists only to measure whether Qodo honors a global `pr-agent-settings` repository under
a personal account rather than an organization. Qodo's documentation says the repository must live
in an organization, and this account is a personal one, so the behavior is undocumented and has to
be observed.

The measurement reads two independent signals from the review this pull request draws.

- <https://github.com/ptr727/pr-agent-settings> sets `use_images_and_animations = false`. Qodo
  applies that itself rather than asking the model to comply, so the divider and severity images
  disappearing from the review is the structural signal.
- The same repository's `best_practices.md` reserves the `qodo-global-probe` file name prefix, so a
  reported rule violation naming that rule is the second signal. This file carries that prefix.

The two signals test different halves of the same claim and can disagree, which is why both are
read.

This pull request is closed unmerged once the review lands. Nothing here is intended to be kept,
and the measurement is recorded on <https://github.com/ptr727/ProjectTemplate/issues/1321>.
