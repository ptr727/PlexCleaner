# PlexCleaner

Utility to optimize media files for Direct Play in Plex, Emby, Jellyfin, etc.

## Build

Code and Pipeline is on [GitHub][github-link].\
Binary releases are published on [GitHub Releases][releases-link].\
Docker images are published on [Docker Hub][docker-link].

## Status

[![Release Status][release-status-shield]][actions-link]\
[![Docker Status][docker-status-shield]][actions-link]\
[![Last Commit][last-commit-shield]][commit-link]\
[![Last Build][last-build-shield]][actions-link]

## Releases

[![GitHub Release][release-version-shield]][releases-link]\
[![GitHub Pre-Release][pre-release-version-shield]][releases-link]\
[![Docker Latest][docker-latest-version-shield]][docker-link]\
[![Docker Develop][docker-develop-version-shield]][docker-link]

## Release Notes

- Version 3:14:
  - Switch to using [CliWrap](https://github.com/Tyrrrz/CliWrap) for commandline tool process execution.
  - Replaced dependency on [deprecated](https://github.com/dotnet/command-line-api/issues/2576) `System.CommandLine.NamingConventionBinder` with direct commandline options binding.
  - Converted media tool commandline creation to using fluent builder pattern.
  - Converted FFprobe JSON packet parsing to using streaming per-packet processing vs. read everything into memory and then process.
  - Switched editorconfig `charset` from `utf-8-bom` to `utf-8` as some tools and PR merge in GitHub always write files without the BOM.
  - Improved closed caption detection in MediaInfo, e.g. discrete detection of separate `SCTE 128` tracks vs. `A/53` embedded video tracks.
  - Improved media tool parsing resiliency when parsing non-Matroska containers, i.e. added `testmediainfo` command to attempt parsing media files.
  - Add [Husky.Net](https://alirezanet.github.io/Husky.Net) for pre-commit hook and formatting.
  - General refactoring.
- Version 3.13:
  - Escape additional filename characters for use with `ffprobe movie=filename[out0+subcc]` command. Fixes #524.
- Version 3:12:
  - Update to .NET 9.0.
    - Dropping Ubuntu docker `arm/v7` support as .NET for ARM32 is no longer published in the Ubuntu repository.
    - Switching Debian docker builds to install .NET using install script as the Microsoft repository now only supports x64 builds. (Ubuntu and Alpine still installing .NET using the distribution repository.)
    - Updated code style [`.editorconfig`](./.editorconfig) to closely follow the Visual Studio and .NET Runtime defaults.
    - Set [CSharpier](https://csharpier.com/) as default C# code formatter.
  - Removed docker [`UbuntuDevel.Dockerfile`](./Docker/Ubuntu.Devel.Dockerfile), [`AlpineEdge.Dockerfile`](./Docker/Alpine.Edge.Dockerfile), and [`DebianTesting.Dockerfile`](./Docker/Debian.Testing.Dockerfile) builds from CI as theses OS pre-release / Beta builds were prone to intermittent build failures. If "bleeding edge" media tools are required local builds can be done using the Dockerfile.
  - Updated 7-Zip version number parsing to account for newly [observed](./PlexCleanerTests/VersionParsingTests.cs) variants.
  - EIA-608 and CTA-708 closed caption detection was reworked due to FFmpeg [removing](https://code.ffmpeg.org/FFmpeg/FFmpeg/commit/19c95ecbff84eebca254d200c941ce07868ee707) easy detection using FFprobe.
    - See the [EIA-608 and CTA-708 Closed Captions](#eia-608-and-cta-708-closed-captions) section for details.
    - Refactored the logic used to determine if a video stream should be considered to contain closed captions.
    - Note that detection may have been broken since the release of FFmpeg v7, it is possible that media files may be in the `Verified` state with closed captions being undetected, run the `removeclosedcaptions` command to re-detect and remove closed captions.
  - Interlace and Telecine detection is complicated and this implementation using track flags and `idet` is naive and may not be reliable, changed `DeInterlace` to default to `false`.
  - Re-added `parallel` and `threadcount` option to `monitor` command, fixes [#498](https://github.com/ptr727/PlexCleaner/issues/498).
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
  - Re-enabling Alpine Stable builds now that Alpine 3.20 has been [released](https://alpinelinux.org/posts/Alpine-3.20.0-released.html).
  - No longer pre-installing VS Debug Tools in docker builds, replaced with [`DebugTools.sh`](./Docker//DebugTools.sh) script that can be used to install [VS Debug Tools](https://learn.microsoft.com/en-us/visualstudio/debugger/remote-debugging-dotnet-core-linux-with-ssh) and [.NET Diagnostic Tools](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/tools-overview) if required.
- Version 3.8:
  - Added Alpine Stable and Edge, Debian Stable and Testing, and Ubuntu Rolling and Devel docker builds.
  - Removed ArchLinux docker build, only supported x64 and media tool versions were often lagging.
  - No longer using MCR base images with .NET pre-installed, support for new linux distribution versions were often lagging.
  - Alpine Stable builds are still [disabled](https://github.com/ptr727/PlexCleaner/issues/344), waiting for Alpine 3.20 to be released, ETA 1 June 2024.
  - Rob Savoury [announced][savoury-link] that due to a lack of funding Ubuntu Noble 24.04 LTS will not get PPA support.
    - Pinning `savoury` docker builds to Jammy 22.04 LTS.
    - Switching `latest` docker tag from `savoury` to an alias for `ubuntu` builds, i.e. the latest released version of Ubuntu, currently Noble 24.04 LTS.
  - Updated `savoury` docker builds to FfMpeg v7, currently the only docker build supporting FfMpeg v7.
- Version 3.7:
  - Added `ProcessOptions:FileIgnoreMasks` to support skipping (not deleting) sample files per [discussions request](https://github.com/ptr727/PlexCleaner/discussions/341).
    - Wildcard characters `*` and `?` are supported, e.g. `*.sample` or `*.sample.*`.
    - Wildcard support now also allows excluding temporary UnRaid FuseFS files, e.g. `*.fuse_hidden*`.
  - Settings JSON schema changed from v3 to v4.
    - `ProcessOptions:KeepExtensions` has been deprecated, existing values will be converted to `ProcessOptions:FileIgnoreMasks`.
      - E.g. `ProcessOptions:KeepExtensions` : `.nfo` will be converted to `ProcessOptions:FileIgnoreMasks` : `*.nfo`.
    - `ConvertOptions:FfMpegOptions:Output` has been deprecated, no need for user configurable values.
    - `ConvertOptions:FfMpegOptions:Global` no longer requires defaults values and will only be used during encoding, only add custom values for e.g. hardware acceleration, existing values will be converted.
      - E.g. `-analyzeduration 2147483647 -probesize 2147483647 -hwaccel cuda -hwaccel_output_format cuda` will be converted to `-hwaccel cuda -hwaccel_output_format cuda`.
  - Changed JSON serialization from `Newtonsoft.Json` [to](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/migrate-from-newtonsoft) .NET native `Text.Json`.
  - Changed JSON schema generation from `Newtonsoft.Json.Schema` [to][jsonschema-link] `JsonSchema.Net.Generation`.
  - Fixed issue with old settings schemas not upgrading as expected, and updated associated unit tests to help catch this next time.
  - Disabling Alpine Edge builds, Handbrake is [failing](https://gitlab.alpinelinux.org/alpine/aports/-/issues/15979) to install, again.
    - Will re-enable Alpine builds if Alpine 3.20 and Handbrake is stable.
- See [Release History](./HISTORY.md) for older Release Notes.

## Questions or Issues

- Use the [Discussions][discussions-link] forum for general questions.
- Refer to the [Issues][issues-link] tracker for known problems.
- Report bugs in the [Issues][issues-link] tracker.

## Use Cases

The objective of PlexCleaner is to modify media content such that it will always Direct Play in [Plex](https://support.plex.tv/articles/200250387-streaming-media-direct-play-and-direct-stream/), [Emby](https://support.emby.media/support/solutions/articles/44001920144-direct-play-vs-direct-streaming-vs-transcoding), [Jellyfin](https://jellyfin.org/docs/plugin-api/MediaBrowser.Model.Session.PlayMethod.html), etc.

Below are examples of issues that can be resolved using the primary `process` command:

- Container file formats other than MKV are not supported by all platforms, re-multiplex to MKV.
- Licensing for some codecs like MPEG-2 prevents hardware decoding, re-encode to H.264.
- Some video codecs like MPEG-4 or VC-1 cause playback issues, re-encode to H.264.
- Some H.264 video profiles like `Constrained Baseline@30` cause playback issues, re-encode to H.264 `High@40`.
- On some displays interlaced video cause playback issues, deinterlace using HandBrake and the `--comb-detect --decomb` options.
- Some audio codecs like Vorbis or WMAPro are not supported by the client platform, re-encode to AC3.
- Some subtitle tracks like VOBsub cause hangs when the `MuxingMode` attribute is not set, re-multiplex to set the correct `MuxingMode`.
- Automatic audio and subtitle track selection requires the track language to be set, set the language for unknown tracks.
- Duplicate audio or subtitle tracks of the same language cause issues with player track selection, delete duplicate tracks, and keep the best quality audio tracks.
- Corrupt media streams cause playback issues, verify stream integrity, and try to automatically repair by re-encoding.
- Some WiFi or 100Mbps Ethernet connected devices with small read buffers hang when playing high bitrate content, warn when media bitrate exceeds the network bitrate.
- Dolby Vision is only supported on DV capable displays, warn when the HDR profile is `Dolby Vision` (profile 5) vs. `Dolby Vision / SMPTE ST 2086` (profile 7) that supports DV and HDR10/HDR10+ displays.
- EIA-608 and CTA-708 closed captions (CC) embedded in video streams can't be disabled or managed from the player, remove embedded closed captions from video streams.
- See the [`process` command](#process-command) for more details.

## Performance Considerations

- To improve processing performance of large media collections, the media file attributes and processing state is cached in sidecar files. (`filename.mkv` -> `filename.PlexCleaner`)
- Sidecar files allow re-processing of the same files to be very fast as the state will be read from the sidecar vs. re-computed from the media file.
- The sidecar maintains a hash of small parts of the media file (timestamps are unreliable), and the media file will be reprocessed when a change in the media file is detected.
- Re-multiplexing is an IO intensive operation and re-encoding is a CPU intensive operation.
- Parallel processing, using the `--parallel` option, is useful when a single instance of FFmpeg or HandBrake does not saturate all the available CPU resources.
- Processing can be interrupted using `Ctrl-Break`, if using sidecar files restarting will skip previously verified files.
- Processing very large media collections on docker may result in a very large docker log file, set appropriate [docker logging](https://docs.docker.com/config/containers/logging/configure/) options.

## Installation

[Docker](#docker) builds are the easiest and most up to date way to run, and can be used on any platform that supports `linux/amd64`, `linux/arm64`, or `linux/arm/v7` architectures.
Alternatively, install directly on [Windows](#windows), [Linux](#linux), or [MacOS](#macos) following the provided instructions.

### Docker

- Builds are published on [Docker Hub][plexcleaner-hub-link].
- See the [Docker README][docker-link] for full distribution details and current media tool versions.
  - `ptr727/plexcleaner:latest` is an alias for the `ubuntu` tag.
  - `ptr727/plexcleaner:ubuntu` is based on [Ubuntu][ubuntu-hub-link] (`ubuntu:rolling`).
  - `ptr727/plexcleaner:alpine` is based on [Alpine][alpine-docker-link] (`alpine:latest`).
  - `ptr727/plexcleaner:debian` is based on [Debian][debian-hub-link] (`debian:stable-slim`).
- Images are updated weekly with the latest upstream updates.
- The container has all the prerequisite 3rd party tools pre-installed.
- Map your host volumes, and make sure the user has permission to access and modify media files.
- The container is intended to be used in interactive mode, for long running operations run in a `screen` session.
- See examples below for instructions on getting started.

Example, run in an interactive shell:

```shell
# The host "/data/media" directory is mapped to the container "/media" directory
# Replace the volume mappings to suit your needs

# If running docker as a non-root user make sure the media file permissions allow writing for the executing user
# adduser --no-create-home --shell /bin/false --disabled-password --system --group users nonroot
# sudo chown -R nonroot:users /data/media
# sudo chmod -R ug=rwx,o=rx /data/media
# docker run --user nonroot:users

# Run the bash shell in an interactive session
docker run \
  -it \
  --rm \
  --pull always \
  --name PlexCleaner \
  --volume /data/media:/media:rw \
  docker.io/ptr727/plexcleaner \
  /bin/bash

# Create default settings file
# Edit the settings file to suit your needs
/PlexCleaner/PlexCleaner \
  defaultsettings \
  --settingsfile /media/PlexCleaner/PlexCleaner.json

# Process media files
/PlexCleaner/PlexCleaner \
  --logfile /media/PlexCleaner/PlexCleaner.log \
  process \
  --settingsfile /media/PlexCleaner/PlexCleaner.json \
  --mediafiles /media/Movies \
  --mediafiles /media/Series

# Exit the interactive session
exit
```

Example, run `monitor` command in a screen session:

```shell
# Start a new screen session
screen
# Or attach to the existing screen session
# screen -rd

# Run the monitor command in an interactive session
docker run \
  -it \
  --rm \
  --log-driver json-file --log-opt max-size=10m \
  --pull always \
  --name PlexCleaner \
  --env TZ=America/Los_Angeles \
  --volume /data/media:/media:rw \
  docker.io/ptr727/plexcleaner \
  /PlexCleaner/PlexCleaner \
    --logfile /media/PlexCleaner/PlexCleaner.log \
    --logwarning \
    monitor \
    --settingsfile /media/PlexCleaner/PlexCleaner.json \
    --parallel \
    --mediafiles /media/Movies \
    --mediafiles /media/Series
```

Example, run `process` command:

```shell
# Run the process command
docker run \
  --rm \
  --pull always \
  --name PlexCleaner \
  --env TZ=America/Los_Angeles \
  --volume /data/media:/media:rw \
  docker.io/ptr727/plexcleaner \
  /PlexCleaner/PlexCleaner \
    --logfile /media/PlexCleaner/PlexCleaner.log \
    --logwarning \
    process \
    --settingsfile /media/PlexCleaner/PlexCleaner.json \
    --mediafiles /media/Movies \
    --mediafiles /media/Series
```

Example, run `monitor` command as a docker compose stack:

```yaml
services:

  plexcleaner:
    image: docker.io/ptr727/plexcleaner:latest
    container_name: PlexCleaner
    restart: unless-stopped
    user: nonroot:users
    command:
      - /PlexCleaner/PlexCleaner
      - monitor
      - --settingsfile=/media/PlexCleaner/PlexCleaner.json
      - --logfile=/media/PlexCleaner/PlexCleaner.log
      - --preprocess
      - --mediafiles=/media/Series
      - --mediafiles=/media/Movies
    environment:
      - TZ=America/Los_Angeles
    volumes:
      - /data/media:/media
```

### Windows

- Install the [.NET Runtime](https://docs.microsoft.com/en-us/dotnet/core/install/windows).
- Download [PlexCleaner](https://github.com/ptr727/PlexCleaner/releases/latest) and extract the pre-compiled binaries.
- Or compile from [code](https://github.com/ptr727/PlexCleaner.git) using [Visual Studio](https://visualstudio.microsoft.com/downloads/) or [VSCode](https://code.visualstudio.com/download) or the [.NET SDK](https://dotnet.microsoft.com/download).
- Create a default JSON settings file using the `defaultsettings` command:
  - `PlexCleaner defaultsettings --settingsfile PlexCleaner.json`
  - Modify the settings to suit your needs.
- Install the required 3rd party tools:
  - Using the `checkfornewtools` to install tools locally:
    - `PlexCleaner checkfornewtools --settingsfile PlexCleaner.json`
    - The default `Tools` folder will be created in the same folder as the `PlexCleaner` binary file.
    - The tool version information will be stored in `Tools\Tools.json`.
    - Keep the 3rd party tools updated by periodically running the `checkfornewtools` command, or update tools on every run by setting `ToolsOptions:AutoUpdate` to `true`.
  - Using `winget` to install tools system wide:
    - Note, run from an elevated shell e.g. using [`gsudo`](https://github.com/gerardog/gsudo), else [symlinks will not be created](https://github.com/microsoft/winget-cli/issues/3437).
    - `winget install --id=Gyan.FFmpeg --exact`.
    - `winget install --id=MediaArea.MediaInfo --exact`.
    - `winget install --id=HandBrake.HandBrake.CLI --exact`.
    - `winget install --id=MoritzBunkus.MKVToolNix --exact --installer-type portable`.
    - Set `ToolsOptions:UseSystem` to `true` and `ToolsOptions:AutoUpdate` to `false`.
  - Manually downloaded and extracted locally:
    - [FfMpeg Full](https://github.com/GyanD/codexffmpeg/releases), e.g. `ffmpeg-6.0-full.7z`: `\Tools\FfMpeg`
    - [HandBrake CLI x64](https://github.com/HandBrake/HandBrake/releases), e.g. `HandBrakeCLI-1.6.1-win-x86_64.zip`: `\Tools\HandBrake`
    - [MediaInfo CLI x64](https://mediaarea.net/en/MediaInfo/Download/Windows), e.g. `MediaInfo_CLI_23.07_Windows_x64.zip`: `\Tools\MediaInfo`
    - [MkvToolNix Portable x64](https://mkvtoolnix.download/downloads.html#windows), e.g. `mkvtoolnix-64-bit-79.0.7z`: `\Tools\MkvToolNix`
    - [7-Zip Extra](https://www.7-zip.org/download.html), e.g. `7z2301-extra.7z`: `\Tools\SevenZip`
    - Set `ToolsOptions:UseSystem` to `false` and `ToolsOptions:AutoUpdate` to `false`.

### Linux

- Automatic downloading of Linux 3rd party tools are not supported, consider using the [Docker](#docker) build instead.
- Manually install the 3rd party tools, e.g. following steps similar to the [Docker](./Docker) file commands.
- Download [PlexCleaner](https://github.com/ptr727/PlexCleaner/releases/latest) and extract the pre-compiled binaries matching your platform.
- Or compile from [code](https://github.com/ptr727/PlexCleaner.git) using the [.NET SDK](https://docs.microsoft.com/en-us/dotnet/core/install/linux).
- Create a default JSON settings file using the `defaultsettings` command:
  - `./PlexCleaner defaultsettings --settingsfile PlexCleaner.json`
  - Modify the settings to suit your needs.

### macOS

- macOS x64 and Arm64 binaries are built as part of [Releases](https://github.com/ptr727/PlexCleaner/releases/latest), but are not tested during CI.

## Configuration

Create a default JSON configuration file by running:
`PlexCleaner defaultsettings --settingsfile PlexCleaner.json`

Refer to the commented default JSON [settings file](./PlexCleaner.defaults.json) for usage.

### Custom FFmpeg and HandBrake CLI Parameters

The `ConvertOptions:FfMpegOptions` and `ConvertOptions:HandBrakeOptions` settings allows for custom CLI parameters to be used during processing.

Note that hardware assisted encoding options are operating system, hardware, and tool version specific.\
Refer to the Jellyfin hardware acceleration [docs](https://jellyfin.org/docs/general/administration/hardware-acceleration/) for hints on usage.
The example configurations are from documentation and minimal testing with Intel QuickSync on Windows only, please discuss and post working configurations in [Discussions][discussions-link].

#### FFmpeg Options

See the [FFmpeg documentation](https://ffmpeg.org/ffmpeg.html) for complete commandline option details.\
The typical FFmpeg commandline is `ffmpeg [global_options] {[input_file_options] -i input_url} ... {[output_file_options] output_url}`.
E.g. `ffmpeg "-analyzeduration 2147483647 -probesize 2147483647 -i "/media/foo.mkv" -max_muxing_queue_size 1024 -abort_on empty_output -hide_banner -nostats -map 0 -c:v libx265 -crf 26 -preset medium -c:a ac3 -c:s copy -f matroska "/media/bar.mkv"`

Settings allows for custom configuration of:

- `FfMpegOptions:Global`: Custom hardware global options, e.g. `-hwaccel cuda -hwaccel_output_format cuda`
- `FfMpegOptions:Video`: Video encoder options following the `-c:v` parameter, e.g. `libx264 -crf 22 -preset medium`
- `FfMpegOptions:Audio`: Audio encoder options following the `-c:a` parameter, e.g. `ac3`

Get encoder options:

- List hardware acceleration methods: `ffmpeg -hwaccels`
- List supported encoders: `ffmpeg -encoders`
- List options supported by an encoder: `ffmpeg -h encoder=libsvtav1`

Example video encoder options:

- [H.264](https://trac.ffmpeg.org/wiki/Encode/H.264): `libx264 -crf 22 -preset medium`
- [H.265](https://trac.ffmpeg.org/wiki/Encode/H.265): `libx265 -crf 26 -preset medium`
- [AV1](https://trac.ffmpeg.org/wiki/Encode/AV1): `libsvtav1 -crf 30 -preset 5`

Example hardware assisted video encoding options:

- NVidia NVENC:
  - See [FFmpeg NVENC](https://trac.ffmpeg.org/wiki/HWAccelIntro#CUDANVENCNVDEC) documentation.
  - View NVENC encoder options: `ffmpeg -h encoder=h264_nvenc`
  - `FfMpegOptions:Global`: `-hwaccel cuda -hwaccel_output_format cuda`
  - `FfMpegOptions:Video`: `h264_nvenc -preset medium`
- Intel QuickSync:
  - See [FFmpeg QuickSync](https://trac.ffmpeg.org/wiki/Hardware/QuickSync) documentation.
  - View QuickSync encoder options: `ffmpeg -h encoder=h264_qsv`
  - `FfMpegOptions:Global`: `-hwaccel qsv -hwaccel_output_format qsv`
  - `FfMpegOptions:Video`: `h264_qsv -preset medium`

#### HandBrake Options

See the [HandBrake documentation](https://handbrake.fr/docs/en/latest/cli/command-line-reference.html) for complete commandline option details.
The typical HandBrake commandline is `HandBrakeCLI [options] -i <source> -o <destination>`.
E.g. `HandBrakeCLI --input "/media/foo.mkv" --output "/media/bar.mkv" --format av_mkv --encoder x265 --quality 26 --encoder-preset medium --comb-detect --decomb --all-audio --aencoder copy --audio-fallback ac3`

Settings allows for custom configuration of:

- `HandBrakeOptions:Video`: Video encoder options following the `--encode` parameter, e.g. `x264 --quality 22 --encoder-preset medium`
- `HandBrakeOptions:Audio`: Audio encoder options following the `--aencode` parameter, e.g. `copy --audio-fallback ac3`

Get encoder options:

- List all supported encoders: `HandBrakeCLI --help`
- List presets supported by an encoder: `HandBrakeCLI --encoder-preset-list svt_av1`

Example video encoder options:

- H.264: `x264 --quality 22 --encoder-preset medium`
- H.265: `x265 --quality 26 --encoder-preset medium`
- AV1: `svt_av1 --quality 30 --encoder-preset 5`

Example hardware assisted video encoding options:

- NVidia NVENC:
  - See [HandBrake NVENC](https://handbrake.fr/docs/en/latest/technical/video-nvenc.html) documentation.
  - `HandBrakeOptions:Video`: `nvenc_h264 --encoder-preset medium`
- Intel QuickSync:
  - See [HandBrake QuickSync](https://handbrake.fr/docs/en/latest/technical/video-qsv.html) documentation.
  - `HandBrakeOptions:Video`: `qsv_h264 --encoder-preset balanced`

Note that HandBrake is primarily used for video deinterlacing, and only as backup encoder when FFmpeg fails.\
The default `HandBrakeOptions:Audio` configuration is set to `copy --audio-fallback ac3` that will copy all supported audio tracks as is, and only encode to `ac3` if the audio codec is not natively supported.

## Usage

Use the `PlexCleaner --help` commandline option to get a list of commands and options.\
To get help for a specific command run `PlexCleaner <command> --help`.

```text
> PlexCleaner --help
Description:
  Utility to optimize media files for Direct Play in Plex, Emby, Jellyfin, etc.

Usage:
  PlexCleaner [command] [options]

Options:
  --logfile <logfile>  Path to log file
  --logappend          Append to existing log file
  --logwarning         Log warnings and errors only
  --debug              Wait for debugger to attach
  --version            Show version information
  -?, -h, --help       Show help and usage information

Commands:
  defaultsettings       Write default values to settings file
  checkfornewtools      Check for new tool versions and download if newer
  process               Process media files
  monitor               Monitor for file changes and process changed media files
  remux                 Re-Multiplex media files
  reencode              Re-Encode media files
  deinterlace           De-Interlace media files
  removesubtitles       Remove subtitles from media files
  removeclosedcaptions  Remove closed captions from media files
  verify                Verify media files
  createsidecar         Create new sidecar files
  updatesidecar         Update existing sidecar files
  getversioninfo        Print application and tools version information
  getsidecarinfo        Print sidecar file information
  gettagmap             Print media information tag-map
  getmediainfo          Print media information using sidecar files
  gettoolinfo           Print media information using media tools
  createschema          Write settings schema to file
```

### Global Options

Global options apply to all commands:

- `--logfile`:
  - Path to the log file.
- `--logappend`:
  - Append to the existing log file, default will overwrite the log file on startup.
- `--logwarning`:
  - Only log warnings and errors, default will log information, warnings, and errors.
- `--debug`:
  - Launch and wait for a debugger to attach.

### Process Command

```text
> PlexCleaner process --help
Description:
  Process media files

Usage:
  PlexCleaner process [options]

Options:
  --settingsfile <settingsfile> (REQUIRED)  Path to settings file
  --mediafiles <mediafiles> (REQUIRED)      Path to media file or folder
  --parallel                                Enable parallel file processing
  --threadcount <threadcount>               Number of threads for parallel file processing
  --quickscan                               Scan only part of the file
  --resultsfile <resultsfile>               Path to results file
  --testsnippets                            Create short media file clips
  --logfile <logfile>                       Path to log file
  --logappend                               Append to existing log file
  --logwarning                              Log warnings and errors only
  --debug                                   Wait for debugger to attach
  -?, -h, --help                            Show help and usage information
```

The `process` command will process the media content using options as defined in the settings file and the optional commandline arguments:

- Refer to [PlexCleaner.defaults.json](PlexCleaner.defaults.json) for configuration details.
- Delete unwanted files.
  - `FileIgnoreMasks`, `ReMuxExtensions`, `DeleteUnwantedExtensions`.
- Re-multiplex non-MKV containers to MKV format.
  - `ReMuxExtensions`, `ReMux`.
- Remove all tags, titles, thumbnails, cover art, and attachments from the media file.
  - `RemoveTags`.
- Set IETF language tags and Matroska special track flags if missing.
  - `SetIetfLanguageTags`.
- Set Matroska special track flags based on track titles.
  - `SetTrackFlags`.
- Set the default language for any track with an undefined language.
  - `SetUnknownLanguage`, `DefaultLanguage`.
- Remove tracks with unwanted languages.
  - `KeepLanguages`, `KeepOriginalLanguage`, `RemoveUnwantedLanguageTracks`
- Remove duplicate tracks, where duplicates are tracks of the same type and language.
  - `RemoveDuplicateTracks`, `PreferredAudioFormats`.
- Re-multiplex the media file if required to fix inconsistencies.
  - `ReMux`.
- De-interlace the video stream if interlaced.
  - `DeInterlace`.
- Remove EIA-608 and CTA-708 closed captions from video stream if present.
  - `RemoveClosedCaptions`.
- Re-encode video and audio based on specified codecs and formats.
  - `ReEncodeVideo`, `ReEncodeAudioFormats`, `ConvertOptions`, `ReEncode`.
- Verify the media container and stream integrity.
  - `MaximumBitrate`, `Verify`.
- If verification fails attempt repair.
  - `AutoRepair`.
- If verification after repair fails delete or mark file to be ignored.
  - `DeleteInvalidFiles`, `RegisterInvalidFiles`.
- Restore modified timestamp of modified files to original timestamp.
  - See `RestoreFileTimestamp`.
- Delete empty folders.
  - `DeleteEmptyFolders`.

Options:

- `--settingsfile`: (required)
  - Path to the JSON settings file.
- `--mediafiles`: (required)
  - Path to file or folder containing files to process.
  - Paths with spaces should be double quoted.
  - Repeat the option to include multiple files or directories, e.g. `--mediafiles path1 --mediafiles "path with space" --mediafiles file1 --mediafiles file2`.
- `--testsnippets`:
  - Create shortened output snippets (30s) for any files created during processing.
  - Useful when testing to speed up processing times.
  - Can be combined with `--quickscan` to limit file scanning operations.
  - Use this option only when testing and on test files.
- `--parallel`:
  - Process multiple files concurrently.
  - Useful when the system has more processing power than being utilized with serial file processing.
- `--threadcount`:
  - Concurrent file processing thread count when the `--parallel` option is enabled.
  - The default thread count is the largest of 1/2 number of logical processors or 4.
  - Note that media tools internally use multiple threads.
- `--quickscan`:
  - Limits the time duration (3min) when scanning media files, applies to:
    - Stream verification.
    - Interlaced frame detection.
    - Closed caption detection.
    - Bitrate calculation.
  - Improves processing times, but there is some risk of missing information present in later parts of the stream.
- `--resultsfile`:
  - Write processing results to a JSON file.
  - Useful when comparing results with previous processing runs.

Example:

```text
./PlexCleaner \
  --logfile PlexCleaner.log \
  process \
  --settingsfile PlexCleaner.json \
  --mediafiles "C:\Foo With Space\Test.mkv" \
  --mediafiles D:\Media
```

### Monitor Command

```text
> PlexCleaner monitor --help
Description:
  Monitor for file changes and process changed media files

Usage:
  PlexCleaner monitor [options]

Options:
  --settingsfile <settingsfile> (REQUIRED)  Path to settings file
  --mediafiles <mediafiles> (REQUIRED)      Path to media file or folder
  --parallel                                Enable parallel file processing
  --threadcount <threadcount>               Number of threads for parallel file processing
  --quickscan                               Scan only part of the file
  --preprocess                              Pre-process all monitored folders
  --logfile <logfile>                       Path to log file
  --logappend                               Append to existing log file
  --logwarning                              Log warnings and errors only
  --debug                                   Wait for debugger to attach
  -?, -h, --help                            Show help and usage information
```

The `monitor` command will watch the specified folders for file changes, and periodically run the `process` command on the changed folders:

- All the referenced directories will be watched for changes, and any changes will be added to a queue to be periodically processed.
- Note that the [FileSystemWatcher](https://docs.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher) used to monitor for changes may not always work as expected when changes are made via virtual or network filesystem, e.g. NFS or SMB backed volumes may not detect changes made directly to the underlying ZFS filesystem, while running directly on ZFS will work fine.

Options:

- Most of the `process` command options apply.
- `--preprocess`:
  - On startup process all existing media files while watching for new changes.
- Advanced options are configured in [`MonitorOptions`](PlexCleaner.defaults.json).

### Other Commands

- `defaultsettings`:
  - Create JSON configuration file using default settings.
- `checkfornewtools`:
  - Check for new tool versions and download if newer.
  - Only supported on Windows.
- `remux`:
  - Conditionally re-multiplex media files.
  - Re-multiplex non-MKV containers in the `ReMuxExtensions` list to MKV container format.
  - Same logic as used in the `process` command.
- `reencode`:
  - Conditionally re-encode media files.
  - Re-encode video and audio if format matches `ReEncodeVideo` or `ReEncodeAudioFormats` to formats set in `ConvertOptions`.
  - Same logic as used in the `process` command.
- `deinterlace`:
  - De-interlace the video stream if interlaced.
  - Same logic as used in the `process` command.
- `removesubtitles`:
  - Remove all subtitle tracks.
  - Useful when media players cannot disable subtitle output, or content is undesirable.
- `removeclosedcaptions`:
  - Remove closed captions from video stream.
  - Useful when media players cannot disable EIA-608 and CTA-708 embedded in the video stream, or content is undesirable.
  - Same logic as used in the `process` command.
- `verify`:
  - Verify media container and stream integrity.
  - Same logic as used in the `process` command.
- `createsidecar`:
  - Create new sidecar files.
  - Useful to start fresh and update tool info and remove old processing state.
- `updatesidecar`:
  - Create or update sidecar files.
- `getversioninfo`:
  - Print application and tools version information.
- `getsidecarinfo`:
  - Print sidecar file information.
- `gettagmap`:
  - Print media file attribute mappings.
  - Useful to show how different media tools interprets the same attributes.
- `getmediainfo`:
  - Print media file information.
- `gettoolinfo`:
  - Print media tool information.
- `createschema`:
  - Write JSON settings schema to file.

## IETF Language Matching

Language tag matching supports [IETF / RFC 5646 / BCP 47](https://en.wikipedia.org/wiki/IETF_language_tag) tag formats as implemented by [MkvMerge](https://gitlab.com/mbunkus/mkvtoolnix/-/wikis/Languages-in-Matroska-and-MKVToolNix).\
During processing the absence of IETF language tags will treated as a track warning, and an RFC 5646 IETF language will be temporarily assigned based on the ISO639-2B tag.\
If `ProcessOptions.SetIetfLanguageTags` is enabled MkvMerge will be used to remux the file using the `--normalize-language-ietf extlang` option, see the [MkvMerge docs](https://mkvtoolnix.download/doc/mkvpropedit.html) for more details.

Tags are in the form of `language-extlang-script-region-variant-extension-privateuse`, and matching happens left to right.\
E.g. `pt` will match `pt` Portuguese, or `pt-BR` Brazilian Portuguese, or `pt-PT` European Portuguese.\
E.g. `pt-BR` will only match only `pt-BR` Brazilian Portuguese.\
E.g. `zh` will match `zh` Chinese, or `zh-Hans` simplified Chinese, or `zh-Hant` for traditional Chinese, and other variants.\
E.g. `zh-Hans` will only match `zh-Hans` simplified Chinese.

Normalized tags will be expanded for matching.\
E.g. `cmn-Hant` will be expanded to `zh-cmn-Hant` allowing matching with `zh`.

See the [W3C Language tags in HTML and XML](https://www.w3.org/International/articles/language-tags/) and [BCP47 language subtag lookup](https://r12a.github.io/app-subtags/) for more details.

## EIA-608 and CTA-708 Closed Captions

[EIA-608](https://en.wikipedia.org/wiki/EIA-608) and [CTA-708](https://en.wikipedia.org/wiki/CTA-708) subtitles, commonly referred to as Closed Captions (CC), are typically used for broadcast television.\
Media containers typically contain separate discrete subtitle tracks, but closed captions can be encoded into the primary video stream.

Removal of closed captions may be desirable for various reasons, including undesirable content, or players that always burn in closed captions during playback.\
Unlike normal subtitle tracks, detection and removal of closed captions are non-trivial.\
Note I have no expertise in video engineering, and the following information was gathered by research and experimentation.

FFprobe [never supported](https://github.com/ptr727/PlexCleaner/issues/94) closed caption reporting when using `-print_format json`, and recently [removed reporting](https://github.com/ptr727/PlexCleaner/issues/497) of closed caption presence completely, prompting research into alternatives.\
E.g.

```text
Stream #0:0(eng): Video: h264 (High), yuv420p(tv, bt709, progressive), 1920x1080, Closed Captions, SAR 1:1 DAR 16:9, 29.97 fps, 29.97 tbr, 1k tbn (default)
```

MediaInfo supports closed caption detection, but only for [some container types](https://github.com/MediaArea/MediaInfoLib/issues/2264) (e.g. TS and DV), and [only scans](https://github.com/MediaArea/MediaInfoLib/issues/1881) the first 30s of the video looking for video frames containing closed captions.\
E.g. `mediainfo --Output=JSON filename`\
MediaInfo does [not support](https://github.com/MediaArea/MediaInfoLib/issues/1881#issuecomment-2816754336) general input piping (e.g. MKV -> FFmpeg -> TS -> MediaInfo), and requires a temporary TS file to be created on disk and used as standard input.\
In my testing I found that remuxing 30s of video from MKV to TS did produce reliable results.\
E.g.

```json
{
    "@type": "Text",
    "ID": "256-1",
    "Format": "EIA-708",
    "MuxingMode": "A/53 / DTVCC Transport",
},
```

[CCExtractor](https://ccextractor.org/) supports closed caption detection using `-out=report`.\
E.g. `ccextractor -12 -out=report filename`\
In my testing I found using MKV containers directly as input produced unreliable results, either no output generated or false negatives.\
CCExtractor does support input piping, but I found it to be unreliable with broken pipes, and requires a temporary TS file to be created on disk and used as standard input.\
Even in TS format on disk, it is very sensitive to stream anomalies, e.g. `Error: Broken AVC stream - forbidden_zero_bit not zero ...`, making it unreliable.\
E.g.

```text
EIA-608: Yes
CEA-708: Yes
```

FFmpeg [`readeia608` filter](https://ffmpeg.org/ffmpeg-filters.html#readeia608) can be used in FFprobe to report EIA-608 frame information.\
E.g. `ffprobe -loglevel error -f lavfi -i "movie=filename,readeia608" -show_entries frame=best_effort_timestamp_time,duration_time:frame_tags=lavfi.readeia608.0.line,lavfi.readeia608.0.cc,lavfi.readeia608.1.line,lavfi.readeia608.1.cc -print_format json`\
Note the `movie=filename[out0+subcc]` convention requires [special escaping](https://superuser.com/questions/1893137/how-to-quote-a-file-name-containing-single-quotes-in-ffmpeg-ffprobe-movie-filena) of the filename to not interfere with commandline or filter graph parsing.\
In my testing I found only one [IMX sample](https://archive.org/details/vitc_eia608_sample) that produced the expected results, making it unreliable.\
E.g.

```json
{
    "best_effort_timestamp_time": "0.000000",
    "duration_time": "0.033367",
    "tags": {
        "lavfi.readeia608.1.cc": "0x8504",
        "lavfi.readeia608.0.cc": "0x8080",
        "lavfi.readeia608.0.line": "28",
        "lavfi.readeia608.1.line": "29"
    },
}
```

FFmpeg [`subcc` filter](https://www.ffmpeg.org/ffmpeg-devices.html#Options-10) can be used to create subtitle streams from the closed captions in video streams.\
E.g. `ffprobe -loglevel error -select_streams s:0 -f lavfi -i "movie=filename[out0+subcc]" -show_packets -print_format json`\
E.g. `ffmpeg -abort_on empty_output -y -f lavfi -i "movie=filename[out0+subcc]" -map 0:s -c:s srt outfilename`\
Note that `ffmpeg -t` and `ffprobe -read_intervals` options limiting scan time does [not work](https://superuser.com/questions/1893673/how-to-time-limit-the-input-stream-duration-when-using-movie-filenameout0subcc) on the input stream when using the `subcc` filter, and scanning the entire file can take a very long time.\
In my testing I found the results to be reliable.\
E.g.

```json
{
    "codec_type": "subtitle",
    "stream_index": 1,
    "pts_time": "0.000000",
    "dts_time": "0.000000",
    "size": "60",
    "pos": "5690",
    "flags": "K__"
},
```

```text
9
00:00:35,568 --> 00:00:38,004
<font face="Monospace">{\an7}No going back now.</font>
```

FFprobe [recently added](https://github.com/FFmpeg/FFmpeg/commit/90af8e07b02e690a9fe60aab02a8bccd2cbf3f01) the `analyze_frames` [option](https://ffmpeg.org/ffprobe.html#toc-Main-options) that reports on the presence of closed captions in video streams.\
As of writing this functionality has not yet been released, but is only in nightly builds.\
E.g. `ffprobe -loglevel error -show_streams -analyze_frames -read_intervals %180 filename -print_format json`

```json
{
    "index": 0,
    "codec_name": "h264",
    "codec_long_name": "H.264 / AVC / MPEG-4 AVC / MPEG-4 part 10",
    "coded_width": 1920,
    "coded_height": 1088,
    "closed_captions": 1,
    "film_grain": 0,
}
```

The currently implemented method of closed caption detection uses FFprobe and the `subcc` filter to detect closed caption frames, but requires scanning of the entire file as there are no options to limit the scan duration when using the `subcc` filter.\
If the `quickscan` options is enabled a small file snippet is first created, and the snippet is used for analysis reducing processing times.

FFmpeg [`filter_units` filter](https://ffmpeg.org/ffmpeg-bitstream-filters.html#filter_005funits) can be used to [remove closed captions](https://stackoverflow.com/questions/48177694/removing-eia-608-closed-captions-from-h-264-without-reencode) from video streams.\
E.g. `ffmpeg -loglevel error -i \"{fileInfo.FullName}\" -c copy -map 0 -bsf:v filter_units=remove_types=6 \"{outInfo.FullName}\"`\
Closed captions SEI unit for H264 is `6`, `39` for H265, and `178` for MPEG2.\
[Note](https://trac.ffmpeg.org/wiki/HowToExtractAndRemoveClosedCaptions) and [note](https://trac.ffmpeg.org/ticket/5283) that as of writing HDR10+ metadata may be lost when removing closed captions from H265 content.

## Testing

The majority of development and debugging time is spent figuring out how to deal with media file and media processing tool specifics affecting playback.\
For repetitive test tasks pre-configured on-demand tests are included in VSCode [`tasks.json`](./.vscode/tasks.json) and [`launch.json`](./.vscode/launch.json), and VisualStudio [`launchSettings.json`](./PlexCleaner/Properties/launchSettings.json).\
Several of the tests reference system local paths containing media files, so you may need to make path changes to match your environment.

### Unit Testing

Unit tests are included for static tests that do not require the use of media files.

```console
dotnet build
dotnet format --verify-no-changes --severity=info --verbosity=detailed
dotnet test
```

### Docker Testing

the [`Test.sh`](./Docker/Test.sh) test script is included in the docker build and can be used to test basic functionality from inside the container.\
If an external media path is not specified the test will download and use the [Matroska test files](https://github.com/ietf-wg-cellar/matroska-test-files/archive/refs/heads/master.zip).

```console
docker run \
  -it --rm \
  --name PlexCleaner-Test \
  docker.io/ptr727/plexcleaner:latest \
  /Test/Test.sh
```

### Regression Testing

The behavior of the tool is very dependent on the media files being tested, and the following process can facilitate regressions testing, assuring that the process results between versions remain consistent.

- Maintain a collection of troublesome media files that resulted in functional changes.
- Create a ZFS snapshot of the media files to test.
- Process the files, using a known good version, and save the results in JSON format using the `--resultsfile` option.
- Restore the ZFS snapshot allowing repetitive testing using the original files.
- Process the files again using the under test version.
- Compare the JSON results file from the known good version with the version under test.
- Investigate any file comparison discrepancies.

E.g.

```shell
# Copy troublesome files
rsync -av --delete --progress /data/media/Troublesome/. /data/media/test
chown -R nobody:users /data/media/test
chmod -R ug=rwx,o=rx /data/media/test

# Take snapshot
zfs destroy hddpool/media/test@backup
zfs snapshot hddpool/media/test@backup
```

```shell
# Config
PlexCleanerApp=/PlexCleaner/Debug/PlexCleaner
MediaPath=/Test/Media
ConfigPath=/Test/Config

# Test function
RunContainer () {
  local Image=$1
  local Tag=$2

  # Rollback to snapshot
  sudo zfs rollback hddpool/media/test@backup

  # Process files
  docker run \
    -it \
    --rm \
    --pull always \
    --name PlexCleaner-Test \
    --user nobody:users \
    --env TZ=America/Los_Angeles \
    --volume /data/media/test:$MediaPath:rw \
    --volume /data/media/PlexCleaner:$ConfigPath:rw \
    $Image:$Tag \
    $PlexCleanerApp process \
      --settingsfile=$ConfigPath/PlexCleaner.json \
      --logfile=$ConfigPath/PlexCleaner-$Tag.log \
      --mediafiles=$MediaPath \
      --testsnippets \
      --quickscan \
      --resultsfile=$ConfigPath/Results-$Tag.json
}

# Test release containers
RunContainer docker.io/ptr727/plexcleaner ubuntu
RunContainer docker.io/ptr727/plexcleaner debian
RunContainer docker.io/ptr727/plexcleaner alpine

# Test pre-release containers
RunContainer docker.io/ptr727/plexcleaner ubuntu-develop
RunContainer docker.io/ptr727/plexcleaner debian-develop
RunContainer docker.io/ptr727/plexcleaner alpine-develop
```

## TODO

- Cleanup chapters, e.g. chapter markers that exceed the media play time.
- Cleanup NFO files, e.g. verify schema, verify image URL's.
- Cleanup text based subtitle files, e.g. convert file encoding to UTF8.
- Process external subtitle files, e.g. merge or extract.

## 3rd Party Tools

- [7-Zip](https://www.7-zip.org/)
- [AwesomeAssertions](https://awesomeassertions.org/)
- [Bring Your Own Badge](https://github.com/marketplace/actions/bring-your-own-badge)
- [CliWrap](https://github.com/Tyrrrz/CliWrap)
- [Docker Hub Description](https://github.com/marketplace/actions/docker-hub-description)
- [Docker Run Action](https://github.com/marketplace/actions/docker-run-action)
- [FFmpeg](https://www.ffmpeg.org/)
- [Git Auto Commit](https://github.com/marketplace/actions/git-auto-commit)
- [GitHub Actions](https://github.com/actions)
- [GitHub Dependabot](https://github.com/dependabot)
- [HandBrake](https://handbrake.fr/)
- [Husky.Net](https://alirezanet.github.io/Husky.Net/)
- [ISO 639-2 language tags](https://www.loc.gov/standards/iso639-2/langhome.html)
- [ISO 639-3 language tags](https://iso639-3.sil.org/)
- [JsonSchema.Net.Generation][jsonschema-link]
- [MediaInfo](https://mediaarea.net/en-us/MediaInfo/)
- [MKVToolNix](https://mkvtoolnix.download/)
- [Nerdbank.GitVersioning](https://github.com/marketplace/actions/nerdbank-gitversioning)
- [quicktype](https://quicktype.io/)
- [regex101.com](https://regex101.com/)
- [RFC 5646 language tags](https://www.rfc-editor.org/rfc/rfc5646.html)
- [Serilog](https://serilog.net/)
- [Utf8JsonAsyncStreamReader](https://github.com/gragra33/Utf8JsonAsyncStreamReader)
- [Xml2CSharp](http://xmltocsharp.azurewebsites.net/)
- [xUnit.Net](https://xunit.net/)

## Sample Media Files

- [DemoWorld](https://www.demo-world.eu/2d-demo-trailers-hd/)
- [JellyFish](http://jell.yfish.us/)
- [Kodi](https://kodi.wiki/view/Samples)
- [Matroska](https://github.com/ietf-wg-cellar/matroska-test-files)
- [MPlayer](https://samples.mplayerhq.hu/)

## License

Licensed under the [MIT License][license-link]\
![GitHub License][license-shield]

[actions-link]: https://github.com/ptr727/PlexCleaner/actions
[alpine-docker-link]: https://hub.docker.com/_/alpine
[commit-link]: https://github.com/ptr727/PlexCleaner/commits/main
[debian-hub-link]: https://hub.docker.com/_/debian
[discussions-link]: https://github.com/ptr727/PlexCleaner/discussions
[docker-develop-version-shield]: https://img.shields.io/docker/v/ptr727/plexcleaner/develop?label=Docker%20Develop&logo=docker&color=orange
[docker-latest-version-shield]: https://img.shields.io/docker/v/ptr727/plexcleaner/latest?label=Docker%20Latest&logo=docker
[docker-link]: https://hub.docker.com/r/ptr727/plexcleaner
[docker-status-shield]: https://img.shields.io/github/actions/workflow/status/ptr727/PlexCleaner/BuildDockerPush.yml?logo=github&label=Docker%20Build
[github-link]: https://github.com/ptr727/PlexCleaner
[plexcleaner-hub-link]: https://hub.docker.com/r/ptr727/plexcleaner
[issues-link]: https://github.com/ptr727/PlexCleaner/issues
[jsonschema-link]: https://json-everything.net/json-schema/
[last-build-shield]: https://byob.yarr.is/ptr727/PlexCleaner/lastbuild
[last-commit-shield]: https://img.shields.io/github/last-commit/ptr727/PlexCleaner?logo=github&label=Last%20Commit
[license-link]: ./LICENSE
[license-shield]: https://img.shields.io/github/license/ptr727/PlexCleaner?label=License
[pre-release-version-shield]: https://img.shields.io/github/v/release/ptr727/PlexCleaner?include_prereleases&label=GitHub%20Pre-Release&logo=github
[release-status-shield]: https://img.shields.io/github/actions/workflow/status/ptr727/PlexCleaner/BuildGitHubRelease.yml?logo=github&label=Releases%20Build
[release-version-shield]: https://img.shields.io/github/v/release/ptr727/PlexCleaner?logo=github&label=GitHub%20Release
[releases-link]: https://github.com/ptr727/PlexCleaner/releases
[savoury-link]: https://launchpad.net/~savoury1
[ubuntu-hub-link]: https://hub.docker.com/_/ubuntu
