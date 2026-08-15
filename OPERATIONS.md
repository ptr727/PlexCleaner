# Operations

How this repository is run. PlexCleaner is a code repository, so its operations are the verification a change owes before it merges, the publish that ships a release, and the media-dependent testing that no runner can perform. The publish contract itself is [WORKFLOW.md](./WORKFLOW.md), and this file is the runbook side of it.

## Local Verification

What verifying a change here requires, including the part CI cannot perform.

The clean-compile is the `.NET Format` VS Code task, chaining `CSharpier Format` -> `.NET Build` -> `dotnet format style --verify-no-changes --severity=info`, and it must pass before a commit, per [CODESTYLE.md](./CODESTYLE.md). CI runs the same checks plus the rest of its lint gate (unit tests with coverage, markdownlint, cspell, actionlint, editorconfig-checker, and ruff plus mypy over `RegressionTests/`), and `Lint: All (CI parity)` in [.vscode/tasks.json](./.vscode/tasks.json) runs that whole gate locally. A green clean-compile does not predict a green CI, so run the parity task before pushing a change that touches anything beyond `.cs` files.

The part CI structurally cannot exercise is media processing. The unit tests need no media files and run everywhere, but the behavior of the tool depends on the media it processes and on the external tools (FFmpeg, HandBrake, MkvToolNix, MediaInfo, 7-Zip) it drives, and a pull request runner has neither a media collection nor a reason to trust one. So a change to processing, sidecar, media-tool, or language-tag code is verified locally against real media, by the Docker test script and by the regression harness described under Runbooks, before it is called done. Read [ARCHITECTURE.md](./ARCHITECTURE.md) before changing that code.

## Runbooks

### Publish a release

Merges never publish. The publisher (`publish-release.yml`) runs on a weekly schedule and on manual dispatch, and each run builds the one branch it is started from: the schedule rebuilds `main` only (a stable release, the `latest` image, and a refreshed `ubuntu:rolling` base), and a dispatch publishes its own branch (`main` -> stable / `latest`, `develop` -> prerelease / `develop`). To ship the changes accumulated on `develop`, dispatch the workflow from `develop`. A re-run whose version is unchanged creates no duplicate release, and the Docker push still refreshes the image. Absent publish rights, nothing here can be run, and the verification falls back to the static and trace audits in [WORKFLOW.md](./WORKFLOW.md) section 5.

### Test the Docker image

[Docker/Test.sh](./Docker/Test.sh) validates basic container functionality against sample media, downloading the Matroska test files when no external media path is given. It is included in the image, so a published tag is tested from inside the container:

```sh
docker run -it --rm --name PlexCleaner-Test docker.io/ptr727/plexcleaner:latest /Test/Test.sh
```

### Run the regression tests

The regression harness under [RegressionTests/](./RegressionTests/) processes a curated collection of troublesome media through a given image tag on a ZFS clone and compares the results across versions down to the per-file processing decision. The collection and its media-specific reduction rules live with the media on a server and are never committed, so this runs on the host that holds them. [RegressionTests/README.md](./RegressionTests/README.md) is the procedure.

## Backup and Recovery

The repository is the record, and GitHub holds it. Nothing here keeps state outside git, and a published release or image is re-creatable from its tag by dispatching the publisher on that commit's branch. The regression media collection is the one asset the repository does not hold, and it is backed up with the media it sits beside rather than by anything here.

## Logs and Debugging

Workflow runs are the log for CI and publishing. `gh run list --branch [branch]` and `gh run view [id] --log-failed` reach them, and a local gate reproduces a CI lint failure exactly, so reproduce locally before reading workflow logs.

Application logging is Serilog, to the console and, with `--logfile`, to a file, and `--loglevel` sets the minimum level (`Debug` logs every media-tool invocation with its arguments, so a processing decision is traceable to the tool call that drove it). The sidecar file beside each media file (`filename.PlexCleaner`) caches the media attributes and the processing state, so re-processing a file reads it rather than the media, and a stale or suspect result is re-derived by deleting the sidecar. The regression harness keeps per-version results and logs for diffing.

## Tool Usage

The application orchestrates FFmpeg, HandBrake, MkvToolNix, MediaInfo, and 7-Zip through CLI wrappers, and the Docker image bundles tested versions of all of them, so the image is the reference environment for reproducing a media-processing result. On Windows `checkfornewtools` downloads newer tool versions and `gettoolinfo` prints the installed ones, and the [README](./README.md) covers each platform.

The doc linters run as pinned Docker images so a local run matches CI:

```sh
docker run --rm --pull=always -v "$PWD":/workdir --workdir /workdir davidanson/markdownlint-cli2:latest "**/*.md"
docker run --rm --pull=always -v "$PWD":/workdir --workdir /workdir ghcr.io/streetsidesoftware/cspell:latest --no-progress README.md HISTORY.md
```

The `.NET Tool Update` and `.NET Outdated Upgrade` tasks refresh the local .NET tools and prompt through dependency updates, and `dotnet husky install` reinstalls the commit hook after a fresh clone.

## Configuration Layout

- [PlexCleaner.defaults.json](./PlexCleaner.defaults.json) and [PlexCleaner.schema.json](./PlexCleaner.schema.json): the default settings file and its JSON schema, regenerated by the `defaultsettings` and `createschema` commands.
- [Samples/](./Samples/): the versioned sample settings files and sidecar fixtures the tests read.
- [repo-config/](./repo-config/): the branch rulesets and repository settings this repository is configured by, applied by the hub-hosted script and self-audited by [AUDIT.md](./AUDIT.md).
- [spec/secrets.json](./spec/secrets.json): the secret names the self-audit cross-checks.
- [host-tools.json](./host-tools.json): the tools a host needs beyond the fleet's own declaration.
- [.github/workflows/](./.github/workflows/): the CI, build, and publish workflows, under the [WORKFLOW.md](./WORKFLOW.md) contract.
