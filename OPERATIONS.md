# Operations

How PlexCleaner is verified, built, released, and operated.

## Local Verification

Run the relevant clean-compile task after code changes. For the C# application, run `CSharpier`, `dotnet build`, and `dotnet format style --verify-no-changes --severity=info --verbosity=detailed`. For the RegressionTests Python tools, run `uvx ruff check RegressionTests` and `uvx mypy RegressionTests`. Before pushing, also run the repository-wide Markdown, spelling, workflow, and EditorConfig checks documented in [GOVERNANCE.md](./GOVERNANCE.md#running-the-linters-locally).

The regression harness requires an external media corpus and a ZFS clone. The corpus is intentionally not committed; follow [RegressionTests/README.md](./RegressionTests/README.md) for corpus setup and reduction verification.

## Runbooks

### Release

The release workflow is manual or scheduled. It builds the trigger branch, publishes the GitHub release and Docker image, and never publishes from pull-request CI. Dispatch `publish-release.yml` from `main` for a stable release or from `develop` for a prerelease.

### Pull Request Verification

Pull requests are gated by `Check pull request workflow status job`. CI runs validation and smoke builds without publishing or uploading release artifacts. Workflow changes are checked by `actionlint` and the next workflow execution that uses them.

## Backup and Recovery

PlexCleaner modifies media files in place. Users must maintain backups of their media libraries and test changes against representative files before processing a collection. The repository contains no media corpus or production configuration.

## Logs and Debugging

Native runs write logs according to the `--logfile` option. Docker runs should map the media directory to `/media` and persist the PlexCleaner log directory. Use `--debug` when collecting a failure report, and include `gettoolinfo` output for external media-tool versions.

## Tool Usage

PlexCleaner invokes FFmpeg, HandBrake, MkvToolNix, MediaInfo, and 7-Zip through its CLI wrapper layer. The Docker image bundles the required tools. Native users install the tools separately or use the application's tool-download commands where supported.

## Configuration Layout

- `PlexCleaner/` contains the CLI application and its processing configuration.
- `PlexCleanerTests/` contains the xUnit test suite.
- `Docker/` contains the multi-architecture image definition and Docker Hub overview.
- `RegressionTests/` contains the external-corpus regression harness and its standalone Python utilities.
- `version.json` controls the major.minor version floor; NBGV supplies the build height.
