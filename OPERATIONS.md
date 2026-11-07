# Operations

How this repository is run. PlexCleaner is a code repository, so its operations are the verification a change owes before it merges, the publish that ships a release, and the media-dependent testing that no runner can perform. The publish contract itself is [WORKFLOW.md](./WORKFLOW.md), and this file is the runbook side of it.

## Local Verification

What verifying a change here requires, including the part CI cannot perform.

The clean-compile is the `.NET Format` VS Code task, chaining `CSharpier Format` -> `.NET Build` -> `dotnet format style --verify-no-changes --severity=info`, and it must pass before a commit, per [CODESTYLE.md](./CODESTYLE.md). CI runs the same checks plus the rest of the hub validator's gate, including unit tests with coverage, markdownlint, cspell, actionlint, editorconfig-checker, shellcheck, and shfmt. It also runs ruff, mypy, and the `unittest` suite over `RegressionTests/`, plus the prose and repo gates. `Lint: All (CI parity)` in [.vscode/tasks.json](./.vscode/tasks.json) runs the lint half locally except shellcheck, shfmt, and the prose and repo gates, and `Python Test` runs the `RegressionTests/` suite. A change to a shell script or to prose therefore owes those checks by hand, per the `shell-codestyle` and `comment-and-doc-style` Skills. A green clean-compile does not predict a green CI, so run the parity task before pushing a change that touches anything beyond `.cs` files.

The part CI structurally cannot exercise is media processing. The unit tests need no media files and run everywhere, but the behavior of the tool depends on the media it processes and on the external tools (FFmpeg, HandBrake, MkvToolNix, MediaInfo, 7-Zip) it drives, and a pull request runner has neither a media collection nor a reason to trust one. So a change to processing, sidecar, media-tool, or language-tag code is verified locally against real media, by the Docker test script and by the regression harness described under Runbooks, before it is called done. Read [ARCHITECTURE.md](./ARCHITECTURE.md) before changing that code.

Running PlexCleaner against real media tools needs none of them installed on the host. So a missing or outdated host tool is never a reason to skip a real-tool check. The published image carries every tool at a version that passes the startup version gate. So the check runs in the image, per the runbooks below. A host tool older than `MediaTool.MinimumVersion` stops PlexCleaner at that gate. An older tool can also behave differently from the one the image ships, so a host-tool result proves nothing about shipped behavior. A change needing a tool version that no published tag carries yet still tests in a container, per the runbooks below.

## Runbooks

### Ship a change

A change moves from a feature branch into `develop`, then from `develop` to `main` in a promotion. Regression testing gates each step in proportion to the change's risk. The branch rules themselves are in [GOVERNANCE.md](./GOVERNANCE.md) "Branching Model".

1. **Feature to `develop`.** Branch from `develop`, and squash merge a pull request back into it. A change to processing, sidecar, media-tool, or language-tag code, or to the image, is regression tested from local images first. Bring the branch up to `develop` head first, so the pair differs only by the change. Build an image of the committed branch and one of `develop` head as a pair, per [RegressionTests/README.md](./RegressionTests/README.md) "Running the harness". Pass each image its own commit as `LABEL_VERSION`, which names its run's results directory. Tag each image by that commit too, so one build does not replace the other. A pair built together carries the same tools, so `check_results.py --baseline` can compare their runs. Rebuild both images whenever either changes, since a later build can pull newer tools. A comparison refused on tool versions means rebuilding the pair. A change that updates a tool on purpose is refused by design, so compare its runs' `Results_process.json` files by hand. Iterate until the comparison shows only the differences the change intends. A `quick` run over the `reduced` corpus is the usual first pass. A rerun of one image over one corpus overwrites its results directory in either mode, so copy an earlier run aside first.
2. **Dispatch a `develop` build.** Once `develop` holds the changes for a release, dispatch `publish-release.yml` from `develop` per "Publish a release". That publishes a prerelease and the `develop` image tag.
3. **Regression test the `develop` image against the last release.** Run the harness on the `develop` tag over the `full` corpus. Use `full` mode when the changes touch remux, repair, or re-encode, since a `quick` run stops at the test snippet. Then compare the run with `check_results.py --baseline` against a run of the last release. The comparison refuses two runs whose recorded inputs differ, other than the image and the plugins, per [RegressionTests/README.md](./RegressionTests/README.md) "Comparing two runs". It only warns about an input one run does not record, so treat that warning as a mismatch. It also refuses two runs whose logged tool versions differ, and `latest` can carry other tools than a fresh `develop` build. So build a local image of `main` head as in step 1 right after the dispatch, and run it as the baseline. A `latest` run serves instead only where its recorded inputs and tool versions match the `develop` run's. A release that updates a tool on purpose is compared by hand, as in step 1. Judge every difference as intended, a regression, or noise before promoting.
4. **Promote `develop` to `main`.** Open a `develop -> main` pull request, merged with a merge commit. A pull request into `develop` closes no issue, since GitHub closes issues from the default branch only. So the promotion pull request's body carries a closing keyword for every issue its changes fix. `develop` takes no back-merge from `main`, so content only `main` holds reaches `develop` first, in a feature pull request of its own. A promotion that still conflicts, because `develop` holds a superset of each conflicted file, is resolved on a branch cut from `main` that merges `develop`. Take `develop`'s side only after confirming that superset for each file, and open that branch as the promotion's head.
5. **Fix and re-test.** A fix found during promotion goes into `develop` through step 1. Where a conflict-resolution branch heads the promotion, merge `develop` into that branch again to carry the fix. A fix of a kind step 1 tests repeats steps 2 and 3, and a documentation or CI-only fix needs no new run. The same holds for any change that reaches `develop` after the tested dispatch.
6. **Merge and release.** The maintainer merges the promotion and dispatches `publish-release.yml` from `main`, which publishes the stable release and the `latest` image.

Each step keeps the version and the release notes correct:

- Only the maintainer changes the `major.minor` version in [version.json](./version.json), and only for a functional change. Nerdbank.GitVersioning sets the patch number from git height, so a release needs no version edit.
- A version bump changes the README "Release Notes" version in the same commit. It replaces the previous version's summary there, and adds a new top entry to [HISTORY.md](./HISTORY.md). Notes for changes not yet released move from the previous entry into the new one.
- A pull request with a notable change adds it to the current version's `HISTORY.md` entry. It also updates the README summary when the change is among that version's highlights.
- Before promoting, confirm the README "Release Notes" version, the top `HISTORY.md` entry, and `version.json` name the same version.

### Publish a release

Merges never publish. The publisher (`publish-release.yml`) runs on a weekly schedule and on manual dispatch, and each run builds the branch it starts from. The schedule rebuilds `main` only, as a stable release and the `latest` image on a refreshed `ubuntu:rolling` base. A dispatch from `develop` publishes the changes accumulated there as a prerelease and the `develop` image. A dispatch from `main` after a promotion publishes them as a stable release and the `latest` image. A dispatch from any other branch fails at the `plan` job rather than passing as a no-op. A re-run whose version is unchanged creates no duplicate release, and the Docker push still refreshes the image. Absent publish rights, nothing here can be run, and the verification falls back to the static and trace audits in [WORKFLOW.md](./WORKFLOW.md) section 5.

### Test the Docker image

[Docker/test.sh](./Docker/test.sh) validates basic container functionality against sample media, downloading the Matroska test files when no external media path is given. It is included in the image, so a published tag is tested from inside the container. The image has no `USER` directive and defaults to root, so the command below passes the host's `nonroot` user and `users` group with `--user`. The test then cannot write to the root-owned `/Test`, so it writes its output under `/tmp/PlexCleaner-Test`:

```sh
# Replace with your own IDs, from `id -u nonroot` and `getent group users | cut -d: -f3`
USER_NONROOT_ID=1001
USERS_GROUP_ID=100
docker run -it --rm --user "${USER_NONROOT_ID:?}:${USERS_GROUP_ID:?}" --name PlexCleaner-Test docker.io/ptr727/plexcleaner:latest /Test/test.sh
```

### Test an unreleased build in the image

The image is the reference environment, so a branch build is tested inside it before an image carries it. Publish the build framework-dependent and run it with the image's own .NET runtime:

```sh
dotnet publish PlexCleaner -c Release --no-self-contained -o ./publish
docker run --rm --user "$(id -u):$(id -g)" --volume "$PWD:/test" --entrypoint dotnet \
  docker.io/ptr727/plexcleaner:latest /test/publish/PlexCleaner.dll process \
  --settingsfile /test/PlexCleaner.json --mediafiles /test/media
```

The `develop` tag works in place of `latest`. It is the image last published from `develop`, so it carries that branch's Dockerfile changes as of that dispatch. The weekly schedule refreshes only `latest`, so `latest` can carry newer tools. Where no tag holds what the change needs, build a local image per the next runbook.

A self-contained publish crashes in the image with `No usable version of libssl`. The image's Ubuntu ships an OpenSSL that only Ubuntu's own .NET runtime supports.

### Test a tool version the image does not carry

The published tags install their tools from the `ubuntu:rolling` archive, so a release not yet in that archive is in no published tag. Test against it in a container rather than on the host.

To test a different FFmpeg version, mount a static FFmpeg build into the container and put its directory first on `PATH` with `--env PATH=...`. Startup logs the FFmpeg version in use, and the minimum tool version check applies to it. A distribution image with FFmpeg from apt is a quick way to check a single command line against an older FFmpeg release.

For any other tool, or an image change, build a local image from [Docker/Dockerfile](./Docker/Dockerfile). Replace the tool's `apt install` entry with a source that carries the needed version. Such a source is the tool's own apt repository, a PPA, or a downloaded static build. On Linux PlexCleaner runs each tool by name from `PATH`, so a static build goes in a `PATH` directory. The builder stage compiles PlexCleaner from the working tree, so the local image carries the branch build too. Build with `--pull` and `--no-cache` so the base image and its package lists are current rather than cached. Leaving out `--platform` builds for the host's own architecture:

```sh
docker buildx build --load --pull --no-cache --tag plexcleaner:local --file ./Docker/Dockerfile .
docker run --rm --user "$(id -u):$(id -g)" --volume "$PWD:/test" \
  plexcleaner:local /PlexCleaner/PlexCleaner process \
  --settingsfile /test/PlexCleaner.json --mediafiles /test/media
```

`/PlexCleaner/PlexCleaner getversioninfo --settingsfile /test/PlexCleaner.json` in the local image prints the version of every tool it carries.

### Run the regression tests

The regression harness under [RegressionTests/](./RegressionTests/) processes a curated collection of troublesome media through a given image on a ZFS clone. It compares the results across versions down to the per-file processing decision. The image is a published tag, or a local image built from a branch. The collection and its media-specific reduction rules live with the media on a server and are never committed. So this runs on the host that holds them. [RegressionTests/README.md](./RegressionTests/README.md) is the procedure.

### Measure code coverage

[Codecov](https://app.codecov.io/gh/ptr727/PlexCleaner) merges the coverage of several runs. CI's validator uploads the .NET unit tests with no flag, and the `RegressionTests/` Python tests under a `python-<version>` flag. [codecov.yml](./codecov.yml) declares a flag for each further run. The `integration` flag is the CI integration job, which runs the image's test commands over a synthetic corpus and checks each file's result. [RegressionTests/README.md](./RegressionTests/README.md) "CI integration run" describes it. The `regression` and `windows` flags are host-specific runs. It carries each of these flags forward, since none runs on every commit. Those runs need the media collection and a Windows workstation, so their wrappers and uploads live in the private HomeAutomation-Config repository rather than here. The regression harness itself collects the `regression` reports when `REGRESSION_COVERAGE_TOOL` is set, as [RegressionTests/README.md](./RegressionTests/README.md) "Running the harness" describes.

Measure the unit tests the way CI does. The reports land under `.artifacts/coverage`, which git ignores. Each run adds a report, so clear the directory first to keep an older commit's report out of the merge:

```sh
dotnet tool restore
rm -rf ./.artifacts/coverage
dotnet test --project PlexCleanerTests/PlexCleanerTests.csproj --coverage --coverage-output-format cobertura --results-directory ./.artifacts/coverage
```

Measure the real-tool paths in the image with `dotnet-coverage`, pinned in [.config/dotnet-tools.json](./.config/dotnet-tools.json). The image has no SDK, so mount the restored tool and run it on the image's runtime. [Docker/PlexCleaner.coverage.json](./Docker/PlexCleaner.coverage.json) is the default settings with every optional processing step turned on, `RegisterInvalidFiles` rather than `DeleteInvalidFiles` since the two cannot both be on. `test.sh` runs the Debug build, whose PDB sits beside it. The settings are copied first, since `RegisterInvalidFiles` writes to the file it reads:

```sh
version="$(jq -r '.tools["dotnet-coverage"].version' .config/dotnet-tools.json)"
tool="$(dirname "$(find "${NUGET_PACKAGES:-$HOME/.nuget/packages}/dotnet-coverage/$version" -name dotnet-coverage.dll)")"
cp ./Docker/PlexCleaner.coverage.json ./.artifacts/coverage/PlexCleaner.json
docker run --rm --user "$(id -u):$(id -g)" --env HOME=/tmp --env DOTNET_COVERAGE_TELEMETRY_OPTOUT=1 \
  --volume "$tool:/dotnet-coverage:ro" --volume "$PWD/.artifacts/coverage:/coverage" \
  --volume "$PWD/.artifacts/coverage/PlexCleaner.json:/Test/PlexCleaner.json" \
  --entrypoint dotnet docker.io/ptr727/plexcleaner:develop /dotnet-coverage/dotnet-coverage.dll \
  collect --output-format cobertura --output /coverage/image.xml /Test/test.sh
```

[RegressionTests/integration-test.sh](./RegressionTests/integration-test.sh) runs the same `dotnet-coverage collect` over the integration job's media and checks the results. Its report, `WORKDIR/out/image.xml`, takes the place of `./.artifacts/coverage/image.xml` in the rewrite below. To measure a branch, use a local image built per [Test a tool version the image does not carry](#test-a-tool-version-the-image-does-not-carry) in place of `develop`. The image report names each source file under `/Builder`, the builder stage's source root. Rewrite that prefix to the checkout into a report the merge reads, then merge the reports with the pinned ReportGenerator:

```sh
sed "s#filename=\"/Builder/#filename=\"$PWD/#" ./.artifacts/coverage/image.xml > ./.artifacts/coverage/image.cobertura.xml
dotnet reportgenerator "-reports:./.artifacts/coverage/*.cobertura.xml" "-targetdir:./.artifacts/coverage/report" \
  "-reporttypes:Html;TextSummary" "-filefilters:+**/PlexCleaner/*;+**/Plugins/*;-**/.artifacts/*"
```

The file filter keeps the project sources and drops the source-generated files under `.artifacts/obj`, which Codecov also drops. Codecov's total also holds the Python coverage of `RegressionTests/`, so compare against its .NET files alone. The merged rate still reads higher than Codecov's .NET rate. ReportGenerator counts a line with partly covered branches as covered, while Codecov counts it as a partial, which lowers its rate. A comparison holds only when every report comes from the same commit. A published image's `--version` ends in its commit's short SHA, and a local image is the checkout it was built from.

A carried-forward flag holds no data for a line changed or added since its upload. Codecov moves the flag's unchanged lines through the diff and drops the changed ones. So a commit's total measures changed code with that commit's own uploads alone. Compare it with another total only where both commits uploaded every carried-forward flag.

On Windows, run the `win-x64` folder of the release archive under `dotnet-coverage collect`. It is framework-dependent with its PDB beside it, not a single-file executable, so it instruments as shipped. A `develop` prerelease carries the Debug build, a `main` release carries the Release build, and `PlexCleaner.exe --version` names the commit.

## Backup and Recovery

The repository is the record, and GitHub holds it. Nothing here keeps state outside git, and a published release or image is re-creatable from its tag by dispatching the publisher on that commit's branch. The regression media collection is the one asset the repository does not hold, and it is backed up with the media it sits beside rather than by anything here.

## Logs and Debugging

Workflow runs are the log for CI and publishing. `gh run list --branch [branch]` and `gh run view [id] --log-failed` reach them, and a local gate reproduces a CI lint failure exactly, so reproduce locally before reading workflow logs.

Application logging is Serilog, to the console and, with `--logfile`, to a file, and `--loglevel` sets the minimum level (`Debug` logs every media-tool invocation with its arguments, so a processing decision is traceable to the tool call that drove it). The sidecar file beside each media file (`filename.PlexCleaner`) caches the media attributes and the processing state, so re-processing a file reads it rather than the media, and a stale or suspect result is re-derived by deleting the sidecar. The regression harness keeps per-version results and logs for diffing.

## Tool Usage

The application orchestrates FFmpeg, HandBrake, MkvToolNix, MediaInfo, and 7-Zip through CLI wrappers, and the Docker image bundles tested versions of all of them, so the image is the reference environment for reproducing a media-processing result. On Windows `checkfornewtools` downloads newer tool versions and `gettoolinfo` prints the installed ones, and the [README](./README.md) covers each platform.

The doc linters run through the hub-hosted wrapper, whose invocations and rationale are
[the hub's GOVERNANCE.md "Running the Linters Locally"](https://github.com/ptr727/ProjectTemplate/blob/main/GOVERNANCE.md#running-the-linters-locally-known-working-invocations),
not restated here.

The `.NET Tool Update` and `.NET Outdated Upgrade` tasks refresh the local .NET tools and prompt through dependency updates, and `dotnet husky install` reinstalls the commit hook after a fresh clone.

## Configuration Layout

- [PlexCleaner.defaults.json](./PlexCleaner.defaults.json) and [PlexCleaner.schema.json](./PlexCleaner.schema.json): the default settings file and its JSON schema, regenerated by the `defaultsettings` and `createschema` commands.
- [Samples/](./Samples/): the versioned sample settings files and sidecar fixtures the tests read.
- The branch rulesets, repository settings, and required secret names are hub-hosted rather than carried here, and [AUDIT.md](./AUDIT.md) states how the self-audit reaches them.
- [host-tools.json](./host-tools.json): the tools a host needs beyond the fleet's own declaration.
- [.github/workflows/](./.github/workflows/): the CI and publish entry points, under the [WORKFLOW.md](./WORKFLOW.md) contract. The validate, build, publish, plan, merge-bot and Docker-Hub-overview tasks are hub-hosted and reached by a SHA pin.
- [global.json](./global.json): the Microsoft.Testing.Platform opt-in `dotnet test` needs on the .NET 10 SDK.
