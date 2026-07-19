# PlexCleaner

Utility to optimize media files for Direct Play in Plex, Emby, Jellyfin, etc.

## Release History

- Version 3.22:
  - Added always-on runtime metrics published via `System.Diagnostics.Metrics` under the `PlexCleaner.Process` meter, readable with `dotnet-counters` with no extra infrastructure.
    - Overall progress is operation-weighted: each heavy full-file operation (the closed-caption and interlace scans, bitrate analysis, re-encode, deinterlace, and verify) counts the file's size as work to do when it starts and as work done when it finishes, so a run mixing tiny and huge files reports the actual work completed, and the total grows as the non-deterministic per-file path is discovered.
    - Instruments include the run file total and input byte total, in-flight and active-thread counts, the `work.total`/`work.completed` byte counters and the `progress.ratio` and `eta.seconds` derived from them, cumulative per-outcome counters (completed, modified, errors, verify-failed, and a per-`State`-flag tally), and the `file.duration` and per-tool `tool.duration` histograms. Metrics are aggregate only, with bounded `state` and `tool` tags and no filename tags.
    - The run-scoped gauges reset at the start of every processing pass, so monitor mode and back-to-back commands each report their own run, while the counters stay cumulative for rate display.
    - Instruments are inert until a listener observes them, so the feature is always on with no configuration flag and negligible idle overhead.
    - The meter is OpenTelemetry and `dotnet-monitor` compatible, and the Docker image ships a `counters` wrapper, so reading the metrics is `docker exec <container> counters`.
- Version 3.21:
  - Repair non-monotonic DTS muxer warnings losslessly instead of failing repair permanently.
    - `ffmpeg -f null` can exit `0` yet emit `Application provided invalid, non monotonically increasing dts to muxer` for files that may decode and play correctly.
    - The previous "any stderr means failure" rule promoted this muxer-interleaving artifact to a hard `VerifyFailed`/`RepairFailed`, and a re-encode could not fix it because Matroska stores no DTS and ffmpeg re-derives a non-monotonic timeline on read.
    - Verify now classifies the decode diagnostics deterministically as clean, a timestamp-only failure, or a decode error; a timestamp-only failure is a repairable failure and everything else fails (fail-closed, so an unrecognized diagnostic fails as a decode error).
    - The classification streams the output line by line, so memory stays bounded even when a file emits a warning per packet ([#827][issue-827-link]).
  - Added a lossless timestamp repair as the first repair tier, escalating to remux and re-encode when it cannot apply.
    - When verification detects a demux-visible non-monotonic DTS on an audio stream, the audio packet timestamps are rewritten to be strictly monotonic using the `setts` bitstream filter with a stream copy (no re-encode), then re-verified.
    - A regression gate compares the per-stream coded payload hash and the per-stream start and duration before and after, discarding the result unless every stream is byte-identical and no stream shifted beyond the A/V-sync tolerance, so the lossless repair can neither alter the media nor drift the audio out of sync.
    - A non-monotonic DTS the `setts` repair cannot fix - a video stream (where a `setts` would reorder B-frames), or a break visible only after decode - falls through to a remux and then a full re-encode that rebuilds the timestamps, matching the general detect -> surgical -> remux -> re-encode -> fail escalation, instead of stopping at `RepairFailed`. The re-encode tier also repairs genuine decode corruption.
  - Consolidated the bitrate and DTS packet analyses into a single `ffprobe -show_packets` pass, computing the per-second bitrate and the per-stream DTS monotonicity together instead of reading packets twice.
  - Switched closed caption detection to `ffprobe -analyze_frames -show_entries stream=closed_captions`, replacing the `movie=...[out0+subcc]` lavfi filter and its QuickScan snippet-remux workaround; QuickScan now bounds the scan with `-read_intervals`.
  - Added the `DtsTimestampRepair` example plugin.
    - It revisits files that a previous version marked `RepairFailed`, re-verifies them, clears the flag when the only problem was timestamps, and losslessly repairs the timestamps when the DTS is demux-visible. Not available in AOT builds.
  - Restricted `--testsnippets` to slow re-encode and deinterlace operations. Fast remux, stream-copy, and the lossless timestamp repair now always produce full output, so a repair or remux is validated on the whole file rather than an unrepresentative leading clip; a snippet had caused the timestamp-repair byte-identical gate to fail during testing.
  - Hardened interlace detection against interleaved `idet` output. On a source with non-monotonic DTS, `ffmpeg -fflags +genpts` emits muxer warnings between the `idet` stat lines; the parser now matches each stat line independently instead of requiring a contiguous block, so a full-file scan no longer fails to parse and abort the file. The raw output is logged on a parse failure.
  - Improved tool execution logging for troubleshooting.
    - A tool failure now logs the tool's error output on a single line with the exit code, the operation, and the file name, instead of a bare exit code with the error text discarded or split across lines.
    - The error text is read from the stream the tool writes to (stderr for the ffmpeg family, HandBrake, and 7-Zip; stdout for the mkvtoolnix tools), falling back to the other captured stream so output on an unexpected stream, such as MediaInfo's stdout, is never lost. The previously non-buffered mkvpropedit and 7-Zip executions now buffer their output, so their failures log the error text instead of nothing.
    - The operation (the calling method, via `[CallerMemberName]`) is included in the execution, cancellation, and failure lines, so a command can be tied to its purpose in a parallel log without correlating separate lines. Redundant per-operation debug lines already covered by the command execution log were removed.
    - Added per-file elapsed processing time to the `ProcessFiles` result line, formatted consistently with the run total.
- Version 3.20:
  - Switched tool downloads and the application version check to the resilient HTTP client in `ptr727.Utilities` (retry with backoff and a circuit breaker via `Microsoft.Extensions.Http.Resilience`), replacing the plain `HttpClient`.
  - Enabled closed caption removal for H.265/HEVC video: the SEI NAL unit lookup keyed on `h265` never matched FFprobe's `hevc` codec name, so HEVC files were incorrectly reported as an "Unsupported video format for Closed Captions removal". HEVC video (excluding HDR10 and HDR10+ content, which remains guarded) is now cleaned using the `filter_units=remove_types=39` bitstream filter, same as H.264 and MPEG-2.
  - Reworked the logging configuration and command line:
    - Added `--loglevel` (`Verbose`, `Debug`, `Information` (default), `Warning`, `Error`, `Fatal`) as the single log level control. `Debug` and `Verbose` output can now be selected; previously `Information` was a hard floor and could not be lowered.
    - Added `--logelevate` to opt into raising a file's log level back to `Information` after it logs a warning or error, so the full context of a problem file is shown even at a higher `--loglevel` (previously this per-file elevation was implicit in `--logwarning`). Elevation only reveals `Information` events and never hides a lower configured level.
    - The log file now appends by default and rolls to a numbered file (`<name>_001.<ext>`) when it reaches a 1 GiB size limit, retaining the most recent 31 files; use `--logclear` to clear it before writing.
    - Deprecated `--logwarning` (use `--loglevel Warning`, add `--logelevate` for the previous per-file elevation) and `--logappend` (appending is now the default, use `--logclear` to clear). Both still work but emit a one-time deprecation warning.
    - Moved logger construction into a static `LoggerFactory`, and re-leveled log events to a consistent taxonomy (documented in `AGENTS.md`): the decision to modify a file is a `Warning` (the elevation trigger), the action doing its job is `Information`, the underlying tool invocation and read/probe mechanics are `Debug`, and failures are `Error`. This moves low-level tool and per-track chatter to `Debug` so the default output focuses on per-file actions and results. A Serilog `MinimumLevel.Override` escape hatch keeps the end-of-run summary and banner lines visible at any level.
    - Propagated the configured logger to the library dependencies (`ptr727.Utilities` and `ptr727.LanguageTags`) so their output shares the same sinks and level, using a `Microsoft.Extensions.Logging` factory bridged from the Serilog logger (`Serilog.Extensions.Logging`).
  - Log a warning when a repair or cleanup condition is detected (redundant `Default` flags, invalid language tags, interlaced video, tracks needing re-encode, etc.) so `--loglevel Warning` surfaces every file that is modified; enable `--logelevate` to also see the subsequent cleanup steps for those files.
  - Always log the end-of-run summary regardless of the configured log level, so the modified, error, and verify-failed counts are recorded for every processing run.
  - Fixed a defect in `idet` interlace detection, beyond the logging changes: ffmpeg can emit its idet statistics more than once (an early empty pass before the final cumulative counts), which could cause the counts to parse incorrectly and the interlace detection operation to fail; the parser now matches every emitted statistics block and uses the final cumulative counts.
  - Improved interlace detection reporting: `FindInterlacedTracks` is now a pure predicate that surfaces how interlacing was detected (idet scan versus container metadata flag), the interlaced verdict and its human-readable justification are encapsulated in a self-describing reason string, and dead idet reporting code that never fed the decision was removed ([#809][issue-809-link], [#810][issue-810-link]).
  - Handle the `SIGINT`, `SIGTERM`, and `SIGQUIT` termination signals (`docker stop`, `Ctrl+C`) so processing is interrupted gracefully and the summary and exit code are logged before exit. The custom `Ctrl+Q`/`Ctrl+Z` exit keys are removed in favor of the standard signals.
  - Normalize `Default` track flags instead of only warning about them: clear the flag on a lone track of a type, keep the preferred audio track as the single default when multiple are flagged, and clear all default flags on subtitle tracks.
  - Added a `custom` command that loads a user-provided plugin assembly implementing `IProcessPlugin` and runs it over the media files, reusing the file iteration and processing API for bespoke re-processing or repair. Includes the `MatroskaHeaderCleanup` example plugin. Not available in AOT builds.
  - Added a regression test suite and reduced-corpus tooling under `RegressionTests/`: a ZFS-clone harness and Python catalog / reduce / locate / audit utilities that verify processing decisions stay consistent across versions. No application changes.
- Version 3.19:
  - Reworked the CI/CD pipeline to a branch-scoped self-publishing model: a weekly scheduled run (and manual dispatch) publishes both `main` (stable, Docker `latest`) and `develop` (prerelease, Docker `develop`) - native executables, the multi-arch Docker image, and the GitHub release - while merges accumulate until the next run. No application changes.
  - Added `WORKFLOW.md` (the canonical CI/CD specification) and `repo-config/` (rulesets and repository settings as code).
- Version 3.18:
  - Fixed an infinite remux loop in `monitor` mode on files with unmappable IETF / BCP-47 language tags ([#747][issue-747-link]).
    - Invalid IETF language tags (e.g. `language_ietf` set to a value that cannot be resolved to ISO 639) are now set in place to a valid tag (the ISO 639 equivalent if known, else `und`) so the repair converges instead of remuxing the same file every cycle.
    - Added a non-convergence guard: if a repair (invalid language tags, metadata remux, or the Matroska structure check) does not resolve the detected errors, the file is marked `VerifyFailed` and is no longer re-processed, breaking the loop for any unfixable condition.
  - Added a deterministic Direct Play verification check ([#746][issue-746-link]).
    - Some Matroska files parse cleanly with FfProbe / MkvMerge / MediaInfo yet fail Direct Play in Jellyfin / Emby / Shield because the player cannot use the file's seek index (e.g. the Cues are positioned before the Tracks, which a forward-only reader cannot reach).
    - `Verify` now validates the Matroska seek index (the SeekHead and Cues a player needs for keyframe seeking) using the NEbml library, and remuxes only files whose index is missing or unusable, leaving valid files untouched.
  - Quickscan accuracy: under `--quickscan` the limited sample is not representative, so interlace detection now skips the unreliable `idet` frame analysis (relying on container interlace flags only) and bitrate verification is skipped, avoiding false-positive deinterlacing of progressive content and unreliable bitrate-exceeded reports ([#749][issue-749-link]).
  - Interlace detection is more conservative to avoid deinterlacing progressive content: it uses idet's more reliable MultiFrame pass and the dominant field order, and only treats content as interlaced when interlaced frames outnumber progressive frames ([#749][issue-749-link]).
- Version 3.16:
  - Structural changes only, no functional changes.
  - Consolidated project structure, build configuration, CI/CD workflows, and Docker configuration across projects.
  - Project changes: Added `Directory.Build.props` and `Directory.Packages.props`, refactored `.csproj` files, added `CODESTYLE.md` and `AGENTS.md`.
  - Build changes: Enabled `TreatWarningsAsErrors`, `AnalysisLevel=latest-all`, centralized NuGet package versions.
  - CI/CD changes: Renamed workflows to kebab-case, restructured into reusable task workflows, consolidated Docker test workflows.
  - Docker changes: Merged build and test scripts, renamed Dockerfile.
  - Editor changes: Updated `.editorconfig`, `.dockerignore`, VS Code tasks and workspace settings.
- Version 3.15:
  - This is primarily a code refactoring release.
  - Updated from .NET 9 to .NET 10.
  - Added [Nullable types][nullable-value-types-link] support.
  - Added [Native AOT][native-aot-link] support.
    - Replaced `JsonSchemaBuilder.FromType<T>()` with `GetJsonSchemaAsNode()` as `FromType<T>()` is [not AOT compatible][json-everything-issue-link].
    - Replaced `JsonSerializer.Deserialize<T>()` with `JsonSerializer.Deserialize(JsonSerializerContext)` for generating [AOT compatible][jsonserializercontext-link] JSON serialization code.
    - Replaced `MethodBase.GetCurrentMethod()?.Name` with `[System.Runtime.CompilerServices.CallerMemberName]` to generate the caller function name during compilation.
    - AOT cross compilation is [not supported][native-aot-cross-compile-link] by the CI/CD pipeline and single file native AOT binaries can be [manually built](./README.md#aot) if needed.
  - Changed MediaInfo output from `--Output=XML` using XML to `--Output=JSON` using JSON.
    - Attempts to use `Microsoft.XmlSerializer.Generator` and generate AOT compatible XML parsing was [unsuccessful][stackoverflow-xmlserializer-link], while JSON `JsonSerializerContext` is AOT compatible.
    - Parsing the existing XML schema is done with custom AOT compatible XML parser created for the MediaInfo XML content.
    - SidecarFile schema changed from v4 to v5 to account for XML to JSON content change.
    - Schema will automatically be upgraded and convert XML to JSON equivalent on reading.
  - Using [`ArrayPool<byte>.Shared.Rent()`][arraypool-link] vs. `new byte[]` to improve memory pressure during sidecar hash calculations.
  - Removed `MonitorOptions` from the config file schema, default values do not need to be changed.
  - ⚠️ Standardized on only using the Ubuntu [rolling][ubuntu-releases-link] docker base image.
    - No longer publishing Debian or Alpine based docker images, or images supporting `linux/arm/v7`.
    - The media tool versions published with the rolling release are typically current, and matches the versions available on Windows, offering a consistent experience, and requires less testing due to changes in behavior between versions.
- Version 3.14:
  - Switch to using [CliWrap][cliwrap-link] for commandline tool process execution.
  - Remove dependency on [deprecated][command-line-api-issue-2576-link] `System.CommandLine.NamingConventionBinder` by directly using commandline options binding.
  - Converted media tool commandline creation to using fluent builder pattern.
  - Converted FFprobe JSON packet parsing to using streaming per-packet processing using [Utf8JsonAsyncStreamReader][utf8jsonasync-link] vs. read everything into memory and then process.
  - Switched editorconfig `charset` from `utf-8-bom` to `utf-8` as some tools and PR merge in GitHub always write files without the BOM.
  - Improved closed caption detection in MediaInfo, e.g. discrete detection of separate `SCTE 128` tracks vs. `A/53` embedded video tracks.
  - Improved media tool parsing resiliency when parsing non-Matroska containers, i.e. added `testmediainfo` command to attempt parsing media files.
  - Add [Husky.Net][husky-link] for pre-commit hook code style validation.
  - General refactoring.
- Version 3.13:
  - Escape additional filename characters for use with `ffprobe movie=filename[out0+subcc]` command. Fixes [#524][issue-524-link].
- Version 3:12:
  - Update to .NET 9.0.
    - ⚠️ Dropping Ubuntu docker `arm/v7` support as .NET for ARM32 is no longer published in the Ubuntu repository.
    - Switching Debian docker builds to install .NET using install script as the Microsoft repository now only supports x64 builds. (Ubuntu and Alpine still installing .NET using the distribution repository.)
    - Updated code style [`.editorconfig`](./.editorconfig) to closely follow the Visual Studio and .NET Runtime defaults.
    - Set [CSharpier][csharpier-link] as default C# code formatter.
  - ⚠️ Removed docker [`UbuntuDevel.Dockerfile`](./Docker/Ubuntu.Devel.Dockerfile), [`AlpineEdge.Dockerfile`](./Docker/Alpine.Edge.Dockerfile), and [`DebianTesting.Dockerfile`](./Docker/Debian.Testing.Dockerfile) builds from CI as theses OS pre-release / Beta builds were prone to intermittent build failures. If "bleeding edge" media tools are required local builds can be done using the Dockerfile.
  - Updated 7-Zip version number parsing to account for newly [observed](./PlexCleanerTests/VersionParsingTests.cs) variants.
  - EIA-608 and CTA-708 closed caption detection was reworked due to FFmpeg [removing][ffmpeg-commit-link] easy detection using FFprobe.
    - See the [EIA-608 and CTA-708 Closed Captions](./README.md#eia-608-and-cta-708-closed-captions) section for details.
    - Refactored the logic used to determine if a video stream should be considered to contain closed captions.
    - Detection may have been broken since the release of FFmpeg v7, it is possible that media files may be in the `Verified` state with closed captions being undetected, run the `removeclosedcaptions` command to re-detect and remove closed captions.
  - Interlace and Telecine detection is complicated and this implementation using track flags and `idet` is naive and may not be reliable, changed `DeInterlace` to default to `false`.
  - Re-added `parallel` and `threadcount` option to `monitor` command, fixes [#498][issue-498-link].
  - Added conditional checks for `ReMux` to warn when disabled and media must be modified for processing logic to work as intended, e.g. removing extra video streams, removing cover art, etc.
  - Added `quickscan` option to limit the scan duration and improve performance, at the potential cost of accuracy.
  - When `parallel` is enabled and `threadcount` is not specified, cap the default of 1/2 CPU cores to max 4, and cap set value to CPU count, prevents CPU starvation.
  - Removed the `reverify` option, it was only partially resetting process state, to reset state and start fresh use the `createsidecar` command.
  - Removed the `testnomodify` option, some modifying code paths missed and conditional logic became too convoluted to maintain, use `testsnippets` and `quickscan` options with sample media files to test instead.
  - Modified logic for `reencode`, `remux`, `verify`, `removesubtitles`, and `removeclosedcaptions` commands to use the same logic as used by the `process` command.
  - Capturing all media tool console output, printing any errors only when encountered.
  - Added additional unit tests.
  - General refactoring.
- Version 3.11:
  - Add `resultsfile` option to `process` command, useful for regression testing in new versions.
- Version 3:10:
  - Removed [Rob Savoury's][savoury-link] Ubuntu Jammy 22.04 LTS builds with backported media tools.
    - The builds would periodically break due to incompatible or missing libraries.
    - The `ubuntu` docker tag (alias for `latest`) uses `ubuntu:rolling` as upstream and does include the latest released media tools.
    - If "bleeding edge" media tools are required consider using `ubuntu-devel` (based on `ubuntu:devel`), `alpine-edge` (based on `alpine:edge`) or `debian-testing` (based on `debian:testing-slim`) tags.
    - If you are currently using the `ptr727/plexcleaner:savoury` docker tag, please switch to `ptr727/plexcleaner:ubuntu`.
- Version 3.9:
  - Re-enabling Alpine Stable builds now that Alpine 3.20 has been [released][alpine-release-link].
  - No longer pre-installing VS Debug Tools in docker builds, replaced with [`DebugTools.sh`](./Docker//DebugTools.sh) script that can be used to install [VS Debug Tools][remote-debugging-link] and [.NET Diagnostic Tools][dotnet-diagnostics-link] if required.
- Version 3.8:
  - Added Alpine Stable and Edge, Debian Stable and Testing, and Ubuntu Rolling and Devel docker builds.
  - ⚠️ Removed ArchLinux docker build, only supported x64 and media tool versions were often lagging.
  - No longer using MCR base images with .NET pre-installed, support for new linux distribution versions were often lagging.
  - Alpine Stable builds are still [disabled][issue-344-link], waiting for Alpine 3.20 to be released, ETA 1 June 2024.
  - Rob Savoury [announced][savoury-link] that due to a lack of funding Ubuntu Noble 24.04 LTS will not get PPA support.
    - Pinning `savoury` docker builds to Jammy 22.04 LTS.
    - Switching `latest` docker tag from `savoury` to an alias for `ubuntu` builds, i.e. the latest released version of Ubuntu, currently Noble 24.04 LTS.
  - Updated `savoury` docker builds to FfMpeg v7, currently the only docker build supporting FfMpeg v7.
- Version 3.7:
  - Added `ProcessOptions:FileIgnoreMasks` to support skipping (not deleting) sample files per [discussions request][discussion-341-link].
    - Wildcard characters `*` and `?` are supported, e.g. `*.sample` or `*.sample.*`.
    - Wildcard support now also allows excluding temporary UnRaid FuseFS files, e.g. `*.fuse_hidden*`.
  - Settings JSON schema changed from v3 to v4.
    - `ProcessOptions:KeepExtensions` has been deprecated, existing values will be converted to `ProcessOptions:FileIgnoreMasks`.
      - E.g. `ProcessOptions:KeepExtensions` : `.nfo` will be converted to `ProcessOptions:FileIgnoreMasks` : `*.nfo`.
    - `ConvertOptions:FfMpegOptions:Output` has been deprecated, no need for user configurable values.
    - `ConvertOptions:FfMpegOptions:Global` no longer requires defaults values and will only be used during encoding, only add custom values for e.g. hardware acceleration, existing values will be converted.
      - E.g. `-analyzeduration 2147483647 -probesize 2147483647 -hwaccel cuda -hwaccel_output_format cuda` will be converted to `-hwaccel cuda -hwaccel_output_format cuda`.
  - Changed JSON serialization from `Newtonsoft.Json` [to][migrate-from-newtonsoft-link] .NET native `Text.Json`.
  - Changed JSON schema generation from `Newtonsoft.Json.Schema` [to][jsonschema-link] `JsonSchema.Net.Generation`.
  - Fixed issue with old settings schemas not upgrading as expected, and updated associated unit tests to help catch this next time.
  - ⚠️ Disabling Alpine Edge builds, Handbrake is [failing][alpine-issue-15979-link] to install, again.
    - Will re-enable Alpine builds if Alpine 3.20 and Handbrake is stable.
- Version 3.6:
  - Disabling Alpine 3.19 release builds and switching to Alpine Edge.
    - Handbrake is only available on Edge, and mixing released and Edge versions cause too many [issues][alpine-issue-15949-link].
    - Alpine stable release builds will no longer be built, or not until Handbrake is supported on stable releases (v3.20 May 2024).
    - Alpine Edge builds will be tagged as `alpine-edge`.
- Version 3.5:
  - Download 7-Zip builds from [GitHub][sevenzip-releases-link], fixes [#324][issue-324-link].
  - Update Alpine Docker image to 3.19.
- Version 3.4:
  - Updated to [.NET 8.0][dotnet-8-link].
  - Updated Debian Docker image to Bookworm.
  - Warn when a newer [GitHub Release][releases-latest-link] version is available.
    - Only tests for new release availability if `ToolsOptions:AutoUpdate` is enabled.
    - Updating the tool itself is still a manual process.
    - Alternatively subscribe to GitHub [Release Notifications][github-release-notification].
  - Added `verify` command option to verify media streams in files.
    - Only media stream validation is performed, track-, bitrate-, and HDR verification is only performed as part of the `process` command.
    - The `verify` command is useful when testing or selecting from multiple available media sources.
- Version 3.3:
  - Download Windows FfMpeg builds from [GyanD FfMpeg GitHub mirror][codexffmpeg-link], may help with [#214][issue-214-link].
  - Install Alpine media tools from `latest-stable` to match the v3.18 base image version, resolves [MediaInfo segfault][issue-208-link].
  - Add "legacy" `osx.13-arm64` build.
  - Make Rider 2023.2.1 happy with current C# linter rules.
- Version 3.2:
  - Added `Ctrl-Q` and `Ctrl-Z` as additional break commands, `Ctrl+C` may terminate the shell command vs. cleanly exiting the process.
- Version 3.1:
  - Added `--preprocess` option to the `monitor` command, that will pre-process all monitored folders.
- Version 3.0:
  - Docker builds expanded to include support for `linux/amd64`, `linux/arm64`, and `linux/arm/v7`, on Ubuntu, Debian, Alpine, and Arch.
    - See the Docker [README][docker-link] for image and tag usage details.
    - The Ubuntu x64 build now utilizes [Rob Savoury's private PPA][savoury-link] for up to date FFmpeg and HandBrake builds.
  - Switched from .NET 6 to .NET 7.
    - Utilizing some new capabilities, e.g. `GeneratedRegex` and `LibraryImport`.
  - Added additional architectures to the published releases, including `win-x64`, `linux-x64`, `linux-musl-x64`, `linux-arm`, `linux-arm64`, and `osx-x64`.
  - Added support for custom FFmpeg and HandBrake command line arguments.
    - See the [Custom FFmpeg and HandBrake CLI Parameters](./README.md#custom-ffmpeg-and-handbrake-cli-parameters) section for usage details.
    - Custom options allows for e.g. AV1 video codec, Intel QuickSync encoding, NVidia NVENC encoding, custom profiles, etc.
    - Removed the `ConvertOptions:EnableH265Encoder`, `ConvertOptions:VideoEncodeQuality` and `ConvertOptions:AudioEncodeCodec` options.
    - Replaced with `ConvertOptions:FfMpegOptions` and `ConvertOptions:HandBrakeOptions` options.
    - On v3 schema upgrade old `ConvertOptions` settings will be upgrade to equivalent settings.
  - Added support for [IETF / RFC 5646 / BCP 47][ietf-language-tag-link] language tag formats.
    - See the [Language Matching](./README.md#language-matching) section usage for details.
    - IETF language tags allows for greater flexibility in Matroska player [language matching][mkvtoolnix-languages-link].
      - E.g. `pt-BR` for Brazilian Portuguese vs. `por` for Portuguese.
      - E.g. `zh-Hans` for simplified Chinese vs. `chi` for Chinese.
    - Update `ProcessOptions:DefaultLanguage` and `ProcessOptions:KeepLanguages` from ISO 639-2B to RFC 5646 format, e.g. `eng` to `en`.
      - On v3 schema upgrade old ISO 639-2B 3 letter tags will be replaced with generic RFC 5646 tags.
    - Added `ProcessOptions.SetIetfLanguageTags` to conditionally remux files using MkvMerge to apply IETF language tags when not set.
      - When enabled all files without IETF tags will be remuxed in order to set IETF language tags, this could be time consuming on large collections of older media that lack the now common IETF tags.
    - [FFmpeg][issue-148-link] and [HandBrake][issue-149-link] removes IETF language tags.
      - Files are remuxed using MkvMerge, and IETF tags are restored using MkvPropEdit, after any FFmpeg or HandBrake operation.
      - If you care and can, please do communicate the need for IETF language support to the FFmpeg and HandBrake development teams.
    - Added warnings and attempt to repair when the Language and LanguageIetf are set and are invalid or do not match.
    - `MkvMerge --identify` added the `--normalize-language-ietf extlang` option to report e.g. `zh-cmn-Hant` vs. `cmn-Hant`.
      - Existing sidecar metadata can be updated using the `updatesidecar` command.
  - Added `ProcessOptions:KeepOriginalLanguage` to keep tracks marked as [original language][matroska-original-flag-link].
  - Added `ProcessOptions:RemoveClosedCaptions` to conditionally vs. always remove closed captions.
  - Added `ProcessOptions:SetTrackFlags` to set track flags based on track title keywords, e.g. `SDH` -> `HearingImpaired`.
  - Added `createschema` command to create the settings JSON schema file, no longer need to use `Sandbox` project to create the schema file.
  - Added warnings when multiple tracks of the same kind have a Default flag set.
  - Added `--logwarning` commandline option to filter log file output to warnings and errors, console still gets all output.
  - Added `updatesidecar` commandline option to update sidecar files using current media tool information.
  - Added `getversioninfo` commandline option to print app, runtime, and media tool versions.
  - Added settings file correctness verification to detect missing but required values.
  - Fixed bitrate calculation packet filter logic to exclude negative timestamps leading to out of bounds exceptions, see FFmpeg `avoid_negative_ts`.
  - Fixed sidecar media file hash calculation logic to open media file read only and share read, avoiding file access or sharing violations.
  - Updated cover art detection and removal logic to not be dependent on `RemoveTags` setting.
  - Updated `DeleteInvalidFiles` logic to delete any file that fails processing, not just files that fail verification.
  - Updated `RemoveDuplicateLanguages` logic to use MkvMerge IETF language tags.
  - Updated `RemoveDuplicateTracks` logic to account for Matroska [track flags][matroska-track-flags-link].
  - Refactored JSON schema versioning logic to use `record` instead of `class` allowing for derived classes to inherited attributes vs. needing to duplicate all attributes.
  - Refactored track selection logic to simplify containment and use with lambda filters.
  - Refactored verify and repair logic, became too complicated.
  - Removed forced file flush and waiting for IO to flush logic, unnecessarily slows down processing and is ineffective.
  - Removed `VerifyOptions:VerifyDuration`, `VerifyOptions:IdetDuration`, `VerifyOptions:MinimumDuration`, and `VerifyOptions:MinimumFileAge` configuration options.
  - Removed docker image publishing to GHCR, `broken pipe` errors too frequently break the build.
  - Changed the process exit code to return `1` vs. `-1` in case of error, more conformant with standard exit codes, `0` remains success.
  - Settings JSON schema updated from v2 to v3 to account for new and modified settings.
    - Older settings schemas will automatically be upgraded with compatible settings to v3 on first run.
  - ⚠️ Removed the `reprocess` commandline option, logic was very complex with limited value, use `reverify` instead.
  - ⚠️ Refactored commandline arguments to only add relevant options to commands that use them vs. adding global options to all commands.
    - Maintaining commandline backwards compatibility was [complicated][command-line-api-issue-2023-link], and the change is unfortunately a breaking change.
    - The following global options have been removed and added to their respective commands:
      - `--settingsfile` used by several commands.
      - `--parallel` used by the `process` command.
      - `--threadcount` used by the `process` command.
    - Move the option from the global options to follow the specific command, e.g.:
      - From: `PlexCleaner --settingsfile PlexCleaner.json defaultsettings ...`
      - To: `PlexCleaner defaultsettings --settingsfile PlexCleaner.json ...`
      - From: `PlexCleaner --settingsfile PlexCleaner.json --parallel --threadcount 2 process ...`
      - To: `PlexCleaner process --settingsfile PlexCleaner.json --parallel --threadcount 2 ...`
- Version 2.10:
  - Added the `--reverify` option, to allow verification and repair of media that previously failed to verify or failed to repair.
    - When enabled the `VerifyFailed` and `RepairFailed` states will be removed before processing starts, allowing media to be re-processed.
    - The alternative was to use `--reprocess=2`, but that would re-process all media, while this option only re-processes media in a failed state.
    - As with the `--reprocess` option, this option is useful when the tooling changed, and may now be better equipped to verify or repair broken media.
- Version 2.9:
  - Added remote docker container debug support.
    - `develop` tagged docker builds use the `Debug` build target, and will now install the .NET SDK and the [VsDbg][vsdbg-link] .NET Debugger.
    - Added a `--debug` command line option that will wait for a debugger to be attached on launch.
    - Remote debugging in docker over SSH can be done using [VSCode][omnisharp-remote-link] or [Visual Studio][vs-docker-debug-link].
  - Updated Dockerfile with latest Linux install steps for MediaInfo and MKVToolNix.
  - Updated System.CommandLine usage to accommodate Beta 4 breaking changes.
- Version 2.8:
  - Added parallel file processing support:
    - Greatly improves throughput on high core count systems, where a single instance of FFmpeg or HandBrake can't utilize all available processing power.
    - Enable parallel processing by using the `--parallel` command line option.
    - The default thread count is equal to half the number of system cores.
    - Override the default thread count by using the `--threadcount` option, e.g. `PlexCleaner --parallel --threadcount 2`.
    - The executing ThreadId is logged to output, this helps with correlating between sequential and logical operations.
    - Interactive console output from tools are disabled when parallel processing is enabled, this avoids console overwrites.
  - General refactoring, bug fixes, and upstream package updates.
- Version 2.7:
  - Log names of all processed files that are in `VerifyFailed` state at the end of the `process` command.
  - Prevent duplicate entries in `ProcessOptions:FileIgnoreList` setting when `VerifyOptions:RegisterInvalidFiles` is set, could happen when using `--reprocess 2`.
  - Added a JSON schema for the configuration file, useful when authoring in tools that honors schemas.
  - Added a "Sandbox" project to simplify code experimentation, e.g. creating a JSON schema from code.
  - Fixed verify and repair logic when `VerifyOptions:AutoRepair` is enabled and file is in `VerifyFailed` state but not `RepairFailed`, could happen when processing is interrupted.
  - Silenced the noisy `tool version mismatch` warnings when `ProcessOptions:SidecarUpdateOnToolChange` is disabled.
  - Replaced `FileEx.IsFileReadWriteable()` with `!FileInfo.IsReadOnly` to optimize for speed over accuracy, testing for attributes vs. opening for write access.
  - Pinned docker base image to `ubuntu:focal` vs. `ubuntu:latest` until Handbrake PPA ads support for Jammy, tracked as [#98][issue-98-link].
- Version 2.6:
  - Fixed `SidecarFile.Update()` bug that would not update the sidecar when only the `State` changed, and kept re-verifying the same verified files.
  - Added a `--reprocess` option to the `process` command, `process --reprocess [0 (default), 1, 2]`
    - The `--reprocess` option can be used to override conditional sidecar state optimizations, e.g. don't verify if already verified.
    - 0: Default behavior, do not do any reprocessing.
    - 1: Re-process low cost operations, e.g. tag detection, closed caption detection, etc.
    - 2: Re-process all operations including expensive operations, e.g. deinterlace detection, bitrate calculation, stream verification, etc.
    - Whenever processing logic is updated or improved (e.g. this release), it is recommended to run with `--reprocess 1` at least once.
  - Added workaround for HandBrake that [force converts][handbrake-issue-link] closed captions and subtitle tracks to `ASS` format.
    - After HandBrake deinterlacing, the original subtitles are added to the output file, bypassing HandBrake subtle logic.
    - Subtitle track formats and attributes are preserved, and closed captions embedded are not converted to subtitle tracks.
    - The HandBrake issue tracked as [#95][issue-95-link].
  - Added the removal of [EIA-608][eia-608-link] Closed Captions from video streams.
    - Closed Caption subtitles in video streams are undesired as they cannot be managed, all subtitles should be in discrete tracks.
    - FFprobe [fails][ffmpeg-devel-link] to set the `closed_captions` JSON attribute in JSON output mode, but does detect and print `Closed Captions` in normal output mode.
    - FFprobe issue tracked as [#94][issue-94-link].
  - Added the ability to bootstrap 7-Zip downloads on Windows, manually downloading `7za.exe` is no longer required.
    - Getting started is now easier, just run:
      - `PlexCleaner.exe --settingsfile PlexCleaner.json defaultsettings`
      - `PlexCleaner.exe --settingsfile PlexCleaner.json checkfornewtools`
  - The `--mediafiles` option no longer supports multiple entries per option, use multiple `--mediafiles` options instead.
    - Deprecation warning initially issued with v2.3.5.
    - Old style: `--mediafiles path1 path2`
    - New style: `--mediafiles path1 --mediafiles path2`
  - Improved the metadata, tag, and attachment detection and cleanup logic.
    - FFprobe container and track tags are now evaluated for unwanted metadata.
    - Attachments are now deleted before processing, eliminating problems with cover art being detected as video tracks, or FFMpeg converting covert art into video tracks.
    - Run with `process --reprocess 1` at least once to re-evaluate conditions.
  - Removed the `upgradesidecar` command.
    - Sidecar schemas are automatically upgraded since v2.5.
  - Removed the `verify` command.
    - Use `process --reprocess 2` instead.
  - Removed the `getbitrateinfo` command.
    - Use `process --reprocess 2` instead.
  - Minor code cleanup and improvements.
- Version 2.5:
  - Changed the config file JSON schema to simplify authoring of multi-value settings, resolves [#85][issue-85-link]
    - Older file schemas will automatically be upgraded without requiring user input.
    - Comma separated lists in string format converted to array of strings.
      - Old: `"ReMuxExtensions": ".avi,.m2ts,.ts,.vob,.mp4,.m4v,.asf,.wmv,.dv",`
      - New: `"ReMuxExtensions": [ ".avi", ".m2ts", ".ts", ".vob", ".mp4", ".m4v", ".asf", ".wmv", ".dv" ]`
    - Multiple VideoFormat comma separated lists in strings converted to array of objects.
      - Old:
        - `"ReEncodeVideoFormats": "mpeg2video,mpeg4,msmpeg4v3,msmpeg4v2,vc1,h264,wmv3,msrle,rawvideo,indeo5"`
        - `"ReEncodeVideoCodecs": "*,dx50,div3,mp42,*,*,*,*,*,*"`
        - `"ReEncodeVideoProfiles": "*,*,*,*,*,Constrained Baseline@30,*,*,*,*"`
      - New: `"ReEncodeVideo": [ { "Format": "mpeg2video" }, { "Format": "mpeg4", "Codec": "dx50" }, ... ]`
  - Replaced [GitVersion][gitversion-link] with [Nerdbank.GitVersioning][nerdbank-gitversioning-link] as versioning tool, resolves [#16][issue-16-link].
    - Main branch will now build using `Release` configuration, other branches will continue building with `Debug` configuration.
    - Prerelease builds are now posted to GitHub releases tagged as `pre-release`, Docker builds continue to be tagged as `develop`.
  - Docker builds are now also pushed to [GitHub Container Registry][ghcr-link].
    - Builds will continue to push to Docker Hub while it remains free to use.
  - Added a xUnit unit test project.
    - Currently the only tests are for config and sidecar JSON schema backwards compatibility.
  - Code cleanup and refactoring to make current versions of Visual Studio and Rider happy.
- Version 2.4.5
  - Update FfMpeg in Linux instructions and in Docker builds to version 5.0.
- Version 2.4.3
  - Added more robust error and control logic for handling specific AVI files.
    - Detect and ignore cover art and thumbnail video tracks.
    - Perform conditional interlace detection using FfMpeg idet filter.
    - Verify media tool track identification matches.
    - Modify sidecar file hashing to support small files.
  - Use C# 10 file scoped namespaces.
- Version 2.4.1
  - Added `ProcessOptions:RestoreFileTimestamp` JSON option to restore the media file modified time to match the original value.
  - Fixed media tool logic to account for WMV files with cover art, and added `wmv3` and `wmav2` codecs to be converted.
- Version 2.3.5
  - Deprecation warning for `--mediafiles` option taking multiple paths, instead use multiple invocations.
    - Old style: `--mediafiles path1 path2`
    - New style: `--mediafiles path1 --mediafiles path2`
  - Added `removesubtitles` command to remove all subtitles, useful when the media contains annoying forced subtitles with ads.
- Version 2.3.2
  - Warn when the HDR profile is `Dolby Vision` (profile 5) vs. `Dolby Vision / SMPTE ST 2086` (profile 7).
    - Unless using DV capable hardware, profile 5 may play but will result in funky colors on HDR10 hardware.
    - The warning is only logged during the verify step, repair is not possible.
    - To re-verify existing 4K files use the `verify` command, or reset the state using the `createsidecar` and `process` commands.
  - Renamed `getsidecar` command to `getsidecarinfo` for consistency with other `getxxxinfo` commands.
  - Added `gettoolinfo` command to print media info reported by tools.
  - Refactored duplicate file iteration logic to use lambdas.
- Version 2.3:
  - Migrated from .NET 5 to .NET 6.
- Version 2.1:
  - Added backwards compatibility for some older JSON schemas.
  - Added the `upgradesidecar` command to migrate sidecar files to the current JSON schema version.
  - Sidecar JSON schema changes:
    - Replaced the unreliable file modified timestamp state tracking with a SHA256 hash of parts of the MKV file.
    - Replaced the `Verified` boolean with `State` flags to track more granular file state and modification changes.
    - Run the `upgradesidecar` command to migrate sidecar files to the current schema version.
  - Repairing metadata inconsistencies, e.g. MuxingMode not specified for S_VOBSUB subtitle codecs, by remuxing the MKV file.
  - Added a `ToolsOptions:AutoUpdate` configuration option to automatically update the tools before each run.
- Version 2.0:
  - Linux and Docker are now supported platforms.
    - Automatic downloading of tools on Linux is not currently supported, tools need to be manually installed on the system.
    - The Docker build includes all the prerequisite tools, and is easier to use vs. installing all the tools on Linux.
  - Support for H.265 encoding added.
  - All file metadata, titles, tags, and track names are now deleted during media file cleanup.
  - Windows systems will be kept awake during processing.
  - ⚠️ Schema version numbers were added to JSON config files, breaking backwards compatibility.
    - Sidecar JSON will be invalid and recreated, including re-verifying that can be very time consuming.
    - Tools JSON will be invalid and `checkfortools` should be used to update tools.
  - Tool version numbers are now using the short version number, allowing for Sidecar compatibility between Windows and Linux.
  - Processing of the same media can be mixed between Windows, Linux, and Docker, but the paths in the `FileIgnoreList` setting are platform specific.
  - New options were added to the JSON config file.
    - `ConvertOptions:EnableH265Encoder`: Enable H.265 encoding vs. H.264.
    - `ToolsOptions:UseSystem`: Use tools from the system path vs. from the Tools folder, this is the default on Linux.
    - `VerifyOptions:RegisterInvalidFiles`: Add files that fail verify and repair to the `ProcessOptions:FileIgnoreList`.
    - `ProcessOptions:ReEncodeAudioFormats` : `opus` codec added to default list.
  - File logging and console output is now done using structured Serilog logging.
    - Basic console and file logging options are used, configuration from JSON is not currently supported.

<!-- Internal -->
[discussion-341-link]: https://github.com/ptr727/PlexCleaner/discussions/341
[docker-link]: https://hub.docker.com/r/ptr727/plexcleaner
[ghcr-link]: https://github.com/ptr727/PlexCleaner/pkgs/container/plexcleaner
[issue-148-link]: https://github.com/ptr727/PlexCleaner/issues/148
[issue-149-link]: https://github.com/ptr727/PlexCleaner/issues/149
[issue-16-link]: https://github.com/ptr727/PlexCleaner/issues/16
[issue-208-link]: https://github.com/ptr727/PlexCleaner/issues/208
[issue-214-link]: https://github.com/ptr727/PlexCleaner/issues/214
[issue-324-link]: https://github.com/ptr727/PlexCleaner/issues/324
[issue-344-link]: https://github.com/ptr727/PlexCleaner/issues/344
[issue-498-link]: https://github.com/ptr727/PlexCleaner/issues/498
[issue-524-link]: https://github.com/ptr727/PlexCleaner/issues/524
[issue-746-link]: https://github.com/ptr727/PlexCleaner/issues/746
[issue-747-link]: https://github.com/ptr727/PlexCleaner/issues/747
[issue-749-link]: https://github.com/ptr727/PlexCleaner/issues/749
[issue-809-link]: https://github.com/ptr727/PlexCleaner/issues/809
[issue-810-link]: https://github.com/ptr727/PlexCleaner/issues/810
[issue-827-link]: https://github.com/ptr727/PlexCleaner/issues/827
[issue-85-link]: https://github.com/ptr727/PlexCleaner/issues/85
[issue-94-link]: https://github.com/ptr727/PlexCleaner/issues/94
[issue-95-link]: https://github.com/ptr727/PlexCleaner/issues/95
[issue-98-link]: https://github.com/ptr727/PlexCleaner/issues/98
[releases-latest-link]: https://github.com/ptr727/PlexCleaner/releases/latest

<!-- External -->
[alpine-issue-15949-link]: https://gitlab.alpinelinux.org/alpine/aports/-/issues/15949
[alpine-issue-15979-link]: https://gitlab.alpinelinux.org/alpine/aports/-/issues/15979
[alpine-release-link]: https://alpinelinux.org/posts/Alpine-3.20.0-released.html
[arraypool-link]: https://learn.microsoft.com/en-us/dotnet/api/system.buffers.arraypool-1
[cliwrap-link]: https://github.com/Tyrrrz/CliWrap
[codexffmpeg-link]: https://github.com/GyanD/codexffmpeg
[command-line-api-issue-2023-link]: https://github.com/dotnet/command-line-api/issues/2023
[command-line-api-issue-2576-link]: https://github.com/dotnet/command-line-api/issues/2576
[csharpier-link]: https://csharpier.com/
[dotnet-8-link]: https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-8
[dotnet-diagnostics-link]: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/tools-overview
[eia-608-link]: https://en.wikipedia.org/wiki/EIA-608
[ffmpeg-commit-link]: https://code.ffmpeg.org/FFmpeg/FFmpeg/commit/19c95ecbff84eebca254d200c941ce07868ee707
[ffmpeg-devel-link]: https://www.mail-archive.com/ffmpeg-devel@ffmpeg.org/msg126211.html
[github-release-notification]: https://docs.github.com/en/account-and-profile/managing-subscriptions-and-notifications-on-github/managing-subscriptions-for-activity-on-github/viewing-your-subscriptions
[gitversion-link]: https://github.com/GitTools/GitVersion
[handbrake-issue-link]: https://github.com/HandBrake/HandBrake/issues/160
[husky-link]: https://alirezanet.github.io/Husky.Net
[ietf-language-tag-link]: https://en.wikipedia.org/wiki/IETF_language_tag
[json-everything-issue-link]: https://github.com/json-everything/json-everything/issues/975
[jsonschema-link]: https://json-everything.net/json-schema/
[jsonserializercontext-link]: https://learn.microsoft.com/en-us/dotnet/api/system.text.json.serialization.jsonserializercontext
[matroska-original-flag-link]: https://www.ietf.org/archive/id/draft-ietf-cellar-matroska-15.html#name-original-flag
[matroska-track-flags-link]: https://www.ietf.org/archive/id/draft-ietf-cellar-matroska-15.html#name-track-flags
[migrate-from-newtonsoft-link]: https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/migrate-from-newtonsoft
[mkvtoolnix-languages-link]: https://codeberg.org/mbunkus/mkvtoolnix/wiki/Languages-in-Matroska-and-MKVToolNix
[native-aot-cross-compile-link]: https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/cross-compile
[native-aot-link]: https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot
[nerdbank-gitversioning-link]: https://github.com/dotnet/Nerdbank.GitVersioning
[nullable-value-types-link]: https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/nullable-value-types
[omnisharp-remote-link]: https://github.com/OmniSharp/omnisharp-vscode/wiki/Attaching-to-remote-processes
[remote-debugging-link]: https://learn.microsoft.com/en-us/visualstudio/debugger/remote-debugging-dotnet-core-linux-with-ssh
[savoury-link]: https://launchpad.net/~savoury1
[sevenzip-releases-link]: https://github.com/ip7z/7zip/releases
[stackoverflow-xmlserializer-link]: https://stackoverflow.com/questions/79858800/statically-generated-xml-parsing-code-using-microsoft-xmlserializer-generator
[ubuntu-releases-link]: https://releases.ubuntu.com/
[utf8jsonasync-link]: https://github.com/gragra33/Utf8JsonAsyncStreamReader
[vs-docker-debug-link]: https://docs.microsoft.com/en-us/visualstudio/debugger/attach-to-process-running-in-docker-container?view=vs-2022
[vsdbg-link]: https://aka.ms/getvsdbgsh
