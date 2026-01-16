# PlexCleaner

Utility to optimize media files for Direct Play in Plex, Emby, Jellyfin, etc.

## Build and Distribution

- **Source Code**: [GitHub][github-link] - Full source code, issues, and CI/CD pipelines.
- **Binary Releases**: [GitHub Releases][releases-link] - Pre-compiled executables for Windows, Linux, and macOS.
- **Docker Images**: [Docker Hub][docker-link] - Container images with all tools pre-installed.

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

## Prerequisites

**For Docker users** (recommended):

- Docker or compatible container runtime installed and running.
- Basic familiarity with Docker volume mapping.
- Media files in common formats (MKV, MP4, AVI, etc.).

**For native binary users**:

- .NET 10.0 Runtime (or SDK for building from source).
- Supported media processing tools (FFmpeg, HandBrake, MkvToolNix, MediaInfo).
- Windows, Linux, or macOS operating system.

**For all users**:

- Backup your media files before processing (PlexCleaner modifies files in place).
- Read access to media files for analysis.
- Write access to create sidecar files and modify media files.

## Quick Start

Get started with PlexCleaner in three easy steps using Docker (recommended):

> **⚠️ Important**: PlexCleaner modifies media files in place. Always maintain backups of your media library before processing. Consider testing with the `--testsnippets` option on sample files first.
>
> **Note**: Replace `/data/media` with your actual media directory path. All examples map your host directory to `/media` inside the container.

```shell
# 1. Create a default settings file
docker run --rm --volume /data/media:/media:rw docker.io/ptr727/plexcleaner \
  /PlexCleaner/PlexCleaner defaultsettings --settingsfile /media/PlexCleaner/PlexCleaner.json

# 2. Edit /data/media/PlexCleaner/PlexCleaner.json to suit your needs

# 3. Process your media files
docker run --rm --volume /data/media:/media:rw docker.io/ptr727/plexcleaner \
  /PlexCleaner/PlexCleaner --logfile /media/PlexCleaner/PlexCleaner.log \
  process --settingsfile /media/PlexCleaner/PlexCleaner.json \
  --mediafiles /media/Movies --mediafiles /media/Series
```

See [Installation](#installation) for detailed setup instructions and other platforms.

## Table of Contents

- [PlexCleaner](#plexcleaner)
  - [Build and Distribution](#build-and-distribution)
  - [Status](#status)
  - [Releases](#releases)
  - [Prerequisites](#prerequisites)
  - [Quick Start](#quick-start)
  - [Table of Contents](#table-of-contents)
  - [Release Notes](#release-notes)
  - [Questions or Issues](#questions-or-issues)
  - [Use Cases](#use-cases)
  - [Performance Considerations](#performance-considerations)
  - [Installation](#installation)
    - [Docker](#docker)
      - [Docker Compose (Recommended for Monitor Mode)](#docker-compose-recommended-for-monitor-mode)
      - [Docker Run Examples](#docker-run-examples)
    - [Windows](#windows)
    - [Linux](#linux)
    - [macOS](#macos)
    - [AOT](#aot)
  - [Configuration](#configuration)
    - [Common Configuration Examples](#common-configuration-examples)
    - [Custom FFmpeg and HandBrake CLI Parameters](#custom-ffmpeg-and-handbrake-cli-parameters)
      - [FFmpeg Options](#ffmpeg-options)
      - [HandBrake Options](#handbrake-options)
  - [Usage](#usage)
    - [Common Commands Quick Reference](#common-commands-quick-reference)
    - [Global Options](#global-options)
    - [Process Command](#process-command)
    - [Monitor Command](#monitor-command)
    - [Other Commands](#other-commands)
  - [IETF Language Matching](#ietf-language-matching)
  - [EIA-608 and CTA-708 Closed Captions](#eia-608-and-cta-708-closed-captions)
  - [Troubleshooting](#troubleshooting)
    - [Processing Failures](#processing-failures)
    - [Docker Issues](#docker-issues)
    - [Sidecar File Issues](#sidecar-file-issues)
    - [Tool Version Issues](#tool-version-issues)
    - [Getting Help](#getting-help)
  - [Testing](#testing)
    - [Unit Testing](#unit-testing)
    - [Docker Testing](#docker-testing)
    - [Regression Testing](#regression-testing)
  - [Development Tooling](#development-tooling)
    - [Install](#install)
    - [Update](#update)
  - [Frequently Asked Questions](#frequently-asked-questions)
  - [Feature Ideas](#feature-ideas)
  - [3rd Party Tools](#3rd-party-tools)
  - [Sample Media Files](#sample-media-files)
  - [License](#license)

## Release Notes

**Current Version: 3.15** - Code refactoring with .NET 10, Native AOT support, and Ubuntu-only Docker images.

> **⚠️ What's New in 3.15 - Breaking Changes:**
>
> **Docker Users:**
>
> - Only `ubuntu:rolling` images are published (Alpine and Debian discontinued).
> - Only `linux/amd64` and `linux/arm64` architectures supported (`linux/arm/v7` discontinued).
> - Update your compose files: Use `docker.io/ptr727/plexcleaner:latest` (Ubuntu only).
>
> **All Users:**
>
> - SidecarFile schema changed from v4 to v5 (MediaInfo XML → JSON).
> - Existing `.PlexCleaner` sidecar files will be automatically migrated on first run.
> - Files may be re-analyzed during first processing after upgrade.

**Key Highlights:**

- Updated from .NET 9 to .NET 10.
- Added Nullable types and Native AOT support.
- Changed MediaInfo output from XML to JSON for better AOT compatibility.
- Improved performance and reduced binary size with Native AOT.

See [Release History](./HISTORY.md) for complete release notes and older versions.

## Questions or Issues

**For General Questions:**

- Use the [Discussions][discussions-link] forum for general questions, feature requests, and sharing configurations.

**For Bug Reports:**

- Check the [Issues][issues-link] tracker for known problems first.
- When reporting a new bug, please include:
  - PlexCleaner version (`PlexCleaner --version`)
  - Operating system and architecture (Windows/Linux/Docker, x64/arm64)
  - Media tool versions (`PlexCleaner gettoolinfo`)
  - Complete command line and relevant configuration settings
  - Full log output with `--debug` flag enabled
  - Sample media file information (`PlexCleaner getmediainfo --mediafiles <file>`)
  - Steps to reproduce the issue

**For Feature Requests:**

- Search [Discussions][discussions-link] and [Issues][issues-link] to see if already proposed.
- Describe the use case and expected behavior clearly.

## Use Cases

The objective of PlexCleaner is to modify media content such that it will always Direct Play in [Plex](https://support.plex.tv/articles/200250387-streaming-media-direct-play-and-direct-stream/), [Emby](https://support.emby.media/support/solutions/articles/44001920144-direct-play-vs-direct-streaming-vs-transcoding), [Jellyfin](https://jellyfin.org/docs/plugin-api/MediaBrowser.Model.Session.PlayMethod.html), etc.

Common issues resolved by the `process` command:

**Container & Codec Issues:**

- Non-MKV containers → Re-multiplex to MKV.
- MPEG-2 video → Re-encode to H.264 (licensing prevents hardware decoding).
- MPEG-4 or VC-1 video → Re-encode to H.264 (playback issues).
- H.264 `Constrained Baseline@30` → Re-encode to H.264 `High@40` (playback issues).
- Vorbis or WMAPro audio → Re-encode to AC3 (platform compatibility).

**Track Management:**

- Missing language tags → Set language for unknown tracks (enables automatic selection).
- Duplicate audio/subtitle tracks → Remove duplicates, keep best quality.
- VOBsub subtitles without `MuxingMode` → Re-multiplex to set correct attribute (prevents hangs).

**Video Quality:**

- Interlaced video → Deinterlace using HandBrake `--comb-detect --decomb`.
- Embedded closed captions (EIA-608/CTA-708) → Remove from video streams (can't be managed by player).
- Dolby Vision profile 5 → Warn when not profile 7 (DV/HDR10 compatibility).

**Performance & Integrity:**

- Corrupt media streams → Verify integrity and attempt automatic repair.
- High bitrate content → Warn when exceeding network capacity (WiFi/100Mbps Ethernet).

See the [`process` command](#process-command) for detailed workflow and the [Common Configuration Examples](#common-configuration-examples) for quick setup examples.

## Performance Considerations

PlexCleaner is optimized for processing large media libraries efficiently. Key performance features and tips:

> **⚡ Performance Tips:**
>
> - **Large libraries**: Use `--parallel` to process multiple files concurrently.
> - **Testing**: Combine `--testsnippets` and `--quickscan` for faster test iterations.
> - **Network storage**: Process files locally when possible to avoid network bottlenecks.
> - **Interruptions**: Use `Ctrl-Break` to stop; sidecar files allow resuming without re-processing verified files.
> - **Docker logging**: Configure [log rotation](https://docs.docker.com/config/containers/logging/configure/) to prevent large log files.
> - **Thread count**: Default is half of CPU cores (min 4); adjust with `--threadcount` if needed.

**Sidecar Files:**

- To improve processing performance of large media collections, the media file attributes and processing state is cached in sidecar files. (`filename.mkv` -> `filename.PlexCleaner`)
- Sidecar files allow re-processing of the same files to be very fast as the state will be read from the sidecar vs. re-computed from the media file.
- The sidecar maintains a hash of small parts of the media file (timestamps are unreliable), and the media file will be reprocessed when a change in the media file is detected.

**Processing Operations:**

- Re-multiplexing is an IO intensive operation and re-encoding is a CPU intensive operation.
- Parallel processing, using the `--parallel` option, is useful when a single instance of FFmpeg or HandBrake does not saturate all the available CPU resources.
- Processing can be interrupted using `Ctrl-Break`, if using sidecar files restarting will skip previously verified files.

**Docker Considerations:**

- Processing very large media collections on docker may result in a very large docker log file, set appropriate [docker logging](https://docs.docker.com/config/containers/logging/configure/) options.

## Installation

Choose an installation method based on your platform and requirements:

- **[Docker](#docker)** (Recommended): Easiest and most up-to-date option.
  - ✅ All tools pre-installed and automatically updated.
  - ✅ Consistent experience across platforms.
  - ✅ Supports `linux/amd64` and `linux/arm64` architectures.
  - Best for: Linux, NAS devices, servers, cross-platform deployments.

- **[Windows](#windows)**: Native installation with automatic tool updates.
  - ✅ Automatic tool downloads and updates via `checkfornewtools` command.
  - ✅ Or use `winget` for system-wide tool installation.
  - Best for: Windows desktops and servers.

- **[Linux](#linux)**: Manual installation.
  - ⚠️ Requires manual tool installation via package manager.
  - ⚠️ No automatic tool updates.
  - Best for: Users who prefer native binaries over Docker.

- **[macOS](#macos)**: Limited support.
  - ⚠️ Binaries built but not tested in CI.
  - ⚠️ Manual tool installation required.

### Docker

- Builds are published on [Docker Hub][plexcleaner-hub-link].
- See the [Docker README][docker-link] for distribution details and current media tool versions.
- `ptr727/plexcleaner:latest` is based on [Ubuntu][ubuntu-hub-link] (`ubuntu:rolling`) built from the `main` branch.
- `ptr727/plexcleaner:develop` is based on [Ubuntu][ubuntu-hub-link] (`ubuntu:rolling`) built from the `develop` branch.
- Images are updated weekly with the latest upstream updates.
- The container has all the prerequisite 3rd party tools pre-installed.
- Map your host volumes, and make sure the user has permission to access and modify media files.
- The container is intended to be used in interactive mode, for long running operations run in a `screen` session.

**Path Mapping Convention**: All examples use `/data/media` as the host path mapped to `/media` inside the container. Replace `/data/media` with your actual media location.

#### Docker Compose (Recommended for Monitor Mode)

For continuous monitoring of media folders, use Docker Compose.

```yaml
services:

  plexcleaner:
    image: docker.io/ptr727/plexcleaner:latest  # Use :develop for pre-release builds
    container_name: PlexCleaner
    restart: unless-stopped
    user: nonroot:users  # Change to match your user:group (e.g., 1000:1000)
    command:
      - /PlexCleaner/PlexCleaner
      - monitor
      - --settingsfile=/media/PlexCleaner/PlexCleaner.json  # Path inside container
      - --logfile=/media/PlexCleaner/PlexCleaner.log        # Path inside container
      - --preprocess  # Process all existing files on startup
      - --mediafiles=/media/Series  # Add multiple --mediafiles for each folder to monitor
      - --mediafiles=/media/Movies  # Paths inside container (mapped from host volumes)
    environment:
      - TZ=America/Los_Angeles  # Set your timezone
    volumes:
      - /data/media:/media  # Map host path /data/media to container /media (read/write)
```

#### Docker Run Examples

For a simple one-time process operation, see the [Quick Start](#quick-start) example.

**Setup Permissions** (if running as non-root user):

```shell
# Create nonroot user and set media directory permissions
sudo adduser --no-create-home --shell /bin/false --disabled-password --system --group users nonroot
sudo chown -R nonroot:users /data/media
sudo chmod -R ug=rwx,o=rx /data/media
```

**Interactive Shell Access:**

```shell
# Replace /data/media with your actual media directory
docker run \
  -it --rm --pull always \
  --name PlexCleaner \
  --user nonroot:users \
  --volume /data/media:/media:rw \
  docker.io/ptr727/plexcleaner \
  /bin/bash

# Inside the container, run PlexCleaner commands (see Quick Start)
exit
```

**Monitor Command in Screen Session:**

```shell
# Start or attach to screen session
screen
# Or: screen -rd

# Run monitor (adjust paths as needed)
docker run -it --rm --pull always \
  --log-driver json-file --log-opt max-size=10m \
  --name PlexCleaner --env TZ=America/Los_Angeles \
  --volume /data/media:/media:rw \
  docker.io/ptr727/plexcleaner \
  /PlexCleaner/PlexCleaner --logfile /media/PlexCleaner/PlexCleaner.log --logwarning \
  monitor --settingsfile /media/PlexCleaner/PlexCleaner.json --parallel \
  --mediafiles /media/Movies --mediafiles /media/Series
```

**Process Command:**

For one-time processing, see the [Quick Start](#quick-start) example or use similar syntax as above, replacing `monitor` with `process`.

### Windows

**Prerequisites:**

- For pre-compiled binaries: Install [.NET Runtime](https://docs.microsoft.com/en-us/dotnet/core/install/windows) (smaller, runtime only).
- For compiling from source: Install [.NET SDK](https://dotnet.microsoft.com/download) (includes build tools).

**Installation Steps:**

1. Download [PlexCleaner](https://github.com/ptr727/PlexCleaner/releases/latest) and extract the pre-compiled binaries.
   - Or compile from [code](https://github.com/ptr727/PlexCleaner.git) using [Visual Studio](https://visualstudio.microsoft.com/downloads/) or [VSCode](https://code.visualstudio.com/download) with the .NET SDK.

2. Create a default JSON settings file using the `defaultsettings` command:
   - `PlexCleaner defaultsettings --settingsfile PlexCleaner.json`
   - Modify the settings to suit your needs.

3. Install the required 3rd party tools (choose one method):

   **Option A: Automatic download (Recommended)**
   - `PlexCleaner checkfornewtools --settingsfile PlexCleaner.json`
   - The default `Tools` folder will be created in the same folder as the `PlexCleaner` binary file.
   - The tool version information will be stored in `Tools\Tools.json`.
   - Keep the 3rd party tools updated by periodically running the `checkfornewtools` command, or update tools on every run by setting `ToolsOptions:AutoUpdate` to `true`.

   **Option B: System-wide installation via winget**
   - Note: Run from an elevated shell e.g. using [`gsudo`](https://github.com/gerardog/gsudo), else [symlinks will not be created](https://github.com/microsoft/winget-cli/issues/3437).
   - `winget install --id=Gyan.FFmpeg --exact`
   - `winget install --id=MediaArea.MediaInfo --exact`
   - `winget install --id=HandBrake.HandBrake.CLI --exact`
   - `winget install --id=MoritzBunkus.MKVToolNix --exact --installer-type portable`
   - Set `ToolsOptions:UseSystem` to `true` and `ToolsOptions:AutoUpdate` to `false`.

   **Option C: Manual download**
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

### AOT

AOT single binary builds are platform specific, and can be built for the [target platform](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot) using [`dotnet publish`](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-publish).

```shell
# Install .NET SDK and native code compiler
apt install -y dotnet-sdk-10.0 clang zlib1g-dev

# Publish standalone executable
dotnet publish ./PlexCleaner/PlexCleaner.csproj \
    --runtime linux-x64 \
    -property:PublishAot=true
```

## Configuration

Create a default JSON configuration file by running:

```shell
PlexCleaner defaultsettings --settingsfile PlexCleaner.json
```

> **⚠️ Important**: The default settings file must be edited to match your requirements before processing media files.
> **Required Changes**:
>
> - Review and adjust `ProcessOptions:KeepLanguages` for your preferred languages
> - Review codec and processing options in `ConvertOptions`
> - Adjust tool paths if not using default locations

Refer to the commented default JSON [settings file](./PlexCleaner.defaults.json) for detailed configuration options and explanations.

### Common Configuration Examples

Quick configuration examples for common use cases. Edit your `PlexCleaner.json` file:

**Keep Only English Audio and Subtitles:**

```json
"ProcessOptions": {
  "KeepLanguages": ["en"],
  "RemoveUnwantedLanguageTracks": true
}
```

**Keep English and Spanish:**

```json
"ProcessOptions": {
  "KeepLanguages": ["en", "es"],
  "RemoveUnwantedLanguageTracks": true
}
```

**Re-encode MPEG-2 Video to H.264:**

```json
"ProcessOptions": {
  "ReEncodeVideo": true
},
"ConvertOptions": {
  "ReEncodeVideoFormats": ["MPEG Video"],
  "FfMpegOptions": {
    "Video": "libx264 -crf 22 -preset medium"
  }
}
```

**Re-encode Vorbis and WMAPro Audio to AC3:**

```json
"ProcessOptions": {
  "ReEncode": true
},
"ConvertOptions": {
  "ReEncodeAudioFormats": ["Vorbis", "WMA"],
  "FfMpegOptions": {
    "Audio": "ac3"
  }
}
```

**Remove Duplicate Tracks (Keep Best Quality):**

```json
"ProcessOptions": {
  "RemoveDuplicateTracks": true,
  "PreferredAudioFormats": ["TrueHD", "DTS-HD MA", "DTS", "AC-3", "AAC"]
}
```

**Verify Media Integrity and Auto-Repair:**

```json
"ProcessOptions": {
  "Verify": true,
  "AutoRepair": true,
  "DeleteInvalidFiles": false,
  "RegisterInvalidFiles": true
}
```

See the [Custom FFmpeg and HandBrake CLI Parameters](#custom-ffmpeg-and-handbrake-cli-parameters) section for advanced encoding options.

### Custom FFmpeg and HandBrake CLI Parameters

> **ℹ️ Note**: The default encoding settings work well for most users and provide good compatibility with Plex/Emby/Jellyfin. Only customize these settings if you have specific requirements (e.g., hardware encoding, different quality targets, or specific codec preferences).

The `ConvertOptions:FfMpegOptions` and `ConvertOptions:HandBrakeOptions` settings allow custom CLI parameters for media processing. This is useful for:

- Hardware-accelerated encoding (GPU encoding via NVENC, QuickSync, etc.).
- Custom quality/speed tradeoffs (CRF values, presets).
- Alternative codecs (AV1, VP9, etc.).

Note that hardware encoding options are operating system, hardware, and tool version specific.\
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

### Common Commands Quick Reference

| Command | Purpose | When to Use |
| ------- | ------- | ----------- |
| `defaultsettings` | Create default configuration file | First time setup |
| `process` | Batch process media files | One-time processing of media library |
| `monitor` | Watch folders and auto-process changes | Continuous monitoring of active media folders |
| `verify` | Verify media integrity without processing | Test media files or check for corruption |
| `remux` | Re-multiplex to MKV without re-encoding | Fix container issues, faster than full processing |
| `reencode` | Re-encode video/audio tracks | Fix codec compatibility issues |
| `deinterlace` | Remove interlacing artifacts | Fix interlaced video playback issues |
| `removeclosedcaptions` | Remove embedded closed captions | Remove unwanted CC from video streams |
| `checkfornewtools` | Download/update media tools (Windows only) | Keep tools up-to-date |

See detailed command documentation below for all options and usage examples.

---

Use the `PlexCleaner --help` commandline option to get a list of commands and options.\
To get help for a specific command run `PlexCleaner <command> --help`.

```text
> PlexCleaner --help
Description:
  Utility to optimize media files for Direct Play in Plex, Emby, Jellyfin, etc.

Usage:
  PlexCleaner [command] [options]

Options:
  -?, -h, --help          Show help and usage information
  --version               Show version information
  --logfile <filepath>    Path to log file
  --logappend <boolean>   Append to existing log file
  --logwarning <boolean>  Log warnings and errors only
  --debug <boolean>       Wait for debugger to attach

Commands:
  defaultsettings       Create default JSON settings file
  checkfornewtools      Check for and download new tool versions
  process               Process media files
  monitor               Monitor file changes and process changed files
  verify                Verify media container and stream integrity
  remux                 Conditionally re-multiplex media files
  reencode              Conditionally re-encode media files
  deinterlace           Conditionally de-interlace media files
  removesubtitles       Remove all subtitle tracks
  removeclosedcaptions  Remove all closed caption tracks
  createsidecar         Create new sidecar files
  updatesidecar         Create or update sidecar files
  getsidecarinfo        Print media sidecar information
  getmediainfo          Print media file information
  gettoolinfo           Print media tool information
  gettagmap             Print media tool attribute mappings
  testmediainfo         Test parsing media tool information
  getversioninfo        Print application and media tool version information
  createschema          Create JSON settings schema file
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
  --settingsfile <filepath> (REQUIRED)  Path to settings file
  --mediafiles <filepath> (REQUIRED)    Path to media file or folder
  --parallel <boolean>                  Enable parallel file processing
  --threadcount <integer>               Number of threads for parallel file processing
  --quickscan <boolean>                 Scan only part of the file
  --resultsfile <filepath>              Path to results file
  --testsnippets <boolean>              Create short media file clips
  -?, -h, --help                        Show help and usage information
  --logfile <filepath>                  Path to log file
  --logappend <boolean>                 Append to existing log file
  --logwarning <boolean>                Log warnings and errors only
  --debug <boolean>                     Wait for debugger to attach
```

The `process` command will process the media content using options as defined in the settings file and the optional commandline arguments.

Refer to [PlexCleaner.defaults.json](PlexCleaner.defaults.json) for complete configuration details, or see [Common Configuration Examples](#common-configuration-examples) for quick setup examples.

**Processing Workflow (in order):**

**1. File Management:**

- Delete unwanted files based on patterns.
  - `FileIgnoreMasks`, `ReMuxExtensions`, `DeleteUnwantedExtensions`

**2. Container Operations:**

- Re-multiplex non-MKV containers to MKV format.
  - `ReMuxExtensions`, `ReMux`
- Remove all tags, titles, thumbnails, cover art, and attachments.
  - `RemoveTags`

**3. Track Language and Metadata:**

- Set IETF language tags and Matroska special track flags if missing.
  - `SetIetfLanguageTags`
- Set Matroska special track flags based on track titles.
  - `SetTrackFlags`
- Set the default language for any track with an undefined language.
  - `SetUnknownLanguage`, `DefaultLanguage`

**4. Track Selection:**

- Remove tracks with unwanted languages.
  - `KeepLanguages`, `KeepOriginalLanguage`, `RemoveUnwantedLanguageTracks`
- Remove duplicate tracks (same type and language, keep best quality).
  - `RemoveDuplicateTracks`, `PreferredAudioFormats`

**5. Video Processing:**

- De-interlace the video stream if interlaced.
  - `DeInterlace`
- Remove EIA-608 and CTA-708 closed captions from video stream.
  - `RemoveClosedCaptions`
- Re-encode video based on specified codecs and formats.
  - `ReEncodeVideo`, `ReEncodeVideoFormats`, `ConvertOptions`

**6. Audio Processing:**

- Re-encode audio based on specified formats.
  - `ReEncode`, `ReEncodeAudioFormats`, `ConvertOptions`

**7. Integrity and Verification:**

- Re-multiplex the media file if required to fix inconsistencies.
  - `ReMux`
- Verify the media container and stream integrity.
  - `MaximumBitrate`, `Verify`
- If verification fails, attempt automatic repair.
  - `AutoRepair`
- If repair fails, delete or mark file to be ignored.
  - `DeleteInvalidFiles`, `RegisterInvalidFiles`

**8. Finalization:**

- Restore modified timestamp of modified files to original timestamp.
  - `RestoreFileTimestamp`
- Delete empty folders after processing.
  - `DeleteEmptyFolders`

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
  Monitor file changes and process changed files

Usage:
  PlexCleaner monitor [options]

Options:
  --settingsfile <filepath> (REQUIRED)  Path to settings file
  --mediafiles <filepath> (REQUIRED)    Path to media file or folder
  --parallel <boolean>                  Enable parallel file processing
  --threadcount <integer>               Number of threads for parallel file processing
  --quickscan <boolean>                 Scan only part of the file
  --preprocess <boolean>                Pre-process all monitored folders
  -?, -h, --help                        Show help and usage information
  --logfile <filepath>                  Path to log file
  --logappend <boolean>                 Append to existing log file
  --logwarning <boolean>                Log warnings and errors only
  --debug <boolean>                     Wait for debugger to attach
```

The `monitor` command will watch the specified folders for file changes, and periodically run the `process` command on the changed folders:

- All the referenced directories will be watched for changes, and any changes will be added to a queue to be periodically processed.
- Note that the [FileSystemWatcher](https://docs.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher) used to monitor for changes may not always work as expected when changes are made via virtual or network filesystem, e.g. NFS or SMB backed volumes may not detect changes made directly to the underlying ZFS filesystem, while running directly on ZFS will work fine.
- See [Troubleshooting - File changes not detected](#docker-issues) for more details on monitor mode limitations.

Options:

- Most of the `process` command options apply.
- `--preprocess`:
  - On startup process all existing media files while watching for new changes.
- Advanced options are configured in [`MonitorOptions`](PlexCleaner.defaults.json).

### Other Commands

Additional commands for specific tasks, organized by category:

**Configuration:**

- `defaultsettings`:
  - Create JSON configuration file using default settings.
- `createschema`:
  - Write JSON settings schema to file for validation.

**Tool Management:**

- `checkfornewtools`:
  - Check for new tool versions and download if newer.
  - Only supported on Windows.
- `gettoolinfo`:
  - Print media tool information and versions.

**File Operations:**

- `remux`:
  - Conditionally re-multiplex media files.
  - Re-multiplex non-MKV containers in the `ReMuxExtensions` list to MKV container format.
  - Same logic as used in the `process` command.
- `reencode`:
  - Conditionally re-encode media files.
  - Re-encode video and audio if format matches `ReEncodeVideo` or `ReEncodeAudioFormats` to formats set in [`ConvertOptions`](#custom-ffmpeg-and-handbrake-cli-parameters).
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

**Information and Debugging:**

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
  - Print media file information and track details.

## IETF Language Matching

Language tag matching supports [IETF / RFC 5646 / BCP 47](https://en.wikipedia.org/wiki/IETF_language_tag) tag formats as implemented by [MkvMerge](https://gitlab.com/mbunkus/mkvtoolnix/-/wikis/Languages-in-Matroska-and-MKVToolNix).

**Quick Start - Most Common Use Cases:**

- **Keep only English**: Set `ProcessOptions:KeepLanguages` to `["en"]`.
- **Keep English and Spanish**: Set `ProcessOptions:KeepLanguages` to `["en", "es"]`.
- **Keep all Portuguese variants**: Use `"pt"` to match `pt`, `pt-BR` (Brazilian), `pt-PT` (European), etc.
- **Keep only Brazilian Portuguese**: Use `"pt-BR"` to match specifically Brazilian Portuguese.

**Understanding Language Matching:**

Tags are in the form of `language-extlang-script-region-variant-extension-privateuse`, and matching happens left to right (most specific to least specific).

Examples:

- `pt` matches: `pt` Portuguese, `pt-BR` Brazilian Portuguese, `pt-PT` European Portuguese.
- `pt-BR` matches: only `pt-BR` Brazilian Portuguese.
- `zh` matches: `zh` Chinese, `zh-Hans` simplified Chinese, `zh-Hant` traditional Chinese, and other variants.
- `zh-Hans` matches: only `zh-Hans` simplified Chinese.

**Technical details:**

During processing the absence of IETF language tags will be treated as a track warning, and an RFC 5646 IETF language will be temporarily assigned based on the ISO639-2B tag.\
If `ProcessOptions.SetIetfLanguageTags` is enabled MkvMerge will be used to remux the file using the `--normalize-language-ietf extlang` option, see the [MkvMerge docs](https://mkvtoolnix.download/doc/mkvpropedit.html) for more details.

Normalized tags will be expanded for matching.\
E.g. `cmn-Hant` will be expanded to `zh-cmn-Hant` allowing matching with `zh`.

See the [W3C Language tags in HTML and XML](https://www.w3.org/International/articles/language-tags/) and [BCP47 language subtag lookup](https://r12a.github.io/app-subtags/) for more details.

## EIA-608 and CTA-708 Closed Captions

> **TL;DR**: Closed captions (CC) are subtitles embedded in the video stream (not separate tracks). They can cause issues with some players that always display them or cannot disable them. PlexCleaner can detect and remove them using the `RemoveClosedCaptions` option, but detection requires scanning the entire file (use `--quickscan` for faster testing with reduced accuracy).

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

## Troubleshooting

### Processing Failures

**Verification fails:**

- Check the log file for detailed error messages from FFmpeg.
- Files with bitrate exceeding `MaximumBitrate` will fail verification.
- Stream integrity errors indicate corrupted or malformed media files.
- If `AutoRepair` is enabled, PlexCleaner will attempt automatic repair using FFmpeg.
- If repair fails and `DeleteInvalidFiles` is enabled, invalid files will be deleted.
- Alternatively, if `RegisterInvalidFiles` is enabled, files will be marked as failed in the sidecar file to prevent re-processing.

**Re-encoding fails:**

- FFmpeg is the primary encoder; HandBrake is used as fallback for deinterlacing or if FFmpeg fails.
- Check encoder options in [`ConvertOptions`](#custom-ffmpeg-and-handbrake-cli-parameters) match your hardware capabilities.
- Hardware encoding may fail on unsupported GPUs or missing drivers.
- Try software encoding first by using default settings.

### Docker Issues

**Permission denied errors:**

- Ensure the container user has read/write permissions on mapped volumes.
- Check ownership: `chown -R <user>:<group> /data/media`.
- Check permissions: `chmod -R ug=rwx,o=rx /data/media`.
- The container runs as `nonroot:users` by default; adjust `user:` in compose file to match your system.

**File changes not detected in monitor mode:**

- `FileSystemWatcher` may not work correctly with network filesystems (NFS, SMB) when changes occur directly on the underlying storage.
- Test by running monitor directly on the storage system instead of through network mounts.
- Consider using periodic `process` command execution instead of continuous monitoring.
- See the [Monitor Command](#monitor-command) documentation for limitations.

### Sidecar File Issues

**Files being re-processed unnecessarily:**

- Delete `.PlexCleaner` sidecar files to force re-analysis.
- Sidecar files contain processing state and file hash (first/last 64KB).
- File modifications invalidate the sidecar; timestamp changes alone do not.
- Use `createsidecar` command to rebuild all sidecar files.

**Sidecar schema version mismatch:**

- Newer PlexCleaner versions may use updated sidecar schemas.
- Old sidecar files are automatically migrated or recreated.
- Check log for schema version warnings.

### Tool Version Issues

**Tool version outdated or incompatible:**

- Windows: Run `checkfornewtools` or enable `ToolsOptions:AutoUpdate`.
- Linux/Docker: Update the container image to get latest tool versions.
- Tool versions are cached in `Tools/Tools.json` (Windows) or embedded in container.
- Mismatched tool versions between Windows and Docker may produce different results.

**Tools not found:**

- Windows: Ensure tools are installed via `checkfornewtools`, winget, or manual download.
- Set `ToolsOptions:UseSystem` to `true` for system-installed tools, `false` for local `Tools` folder.
- Linux: Install tools via package manager (apt, yum, etc.).
- Check log file for tool execution errors and paths.

### Getting Help

If you're still experiencing issues:

1. **Check existing resources:**
   - Review [Release Notes](#release-notes) and [HISTORY.md](HISTORY.md) for known issues and changes.
   - Search [GitHub Discussions][discussions-link] for similar problems and solutions.
   - Check [GitHub Issues][issues-link] for reported bugs.

2. **Gather diagnostic information:**
   - Log file (specify location with `--logfile` option).
   - PlexCleaner version: Run `PlexCleaner getversioninfo`.
   - Tool versions: Run `PlexCleaner gettoolinfo --settingsfile <path>`.
   - Configuration file (redact sensitive paths if needed).
   - Media file information: Run `PlexCleaner getmediainfo --mediafiles <path>`.
   - Sidecar file (if relevant): Run `PlexCleaner getsidecarinfo --mediafiles <path>`.

3. **Report the issue:**
   - For questions or general help: Start a [Discussion][discussions-link].
   - For confirmed bugs: Open an [Issue][issues-link] with:
     - Clear description of the problem.
     - Steps to reproduce.
     - Expected vs actual behavior.
     - All diagnostic information from step 2.
     - Sample media files (if possible) or MediaInfo output.

## Testing

PlexCleaner includes multiple testing approaches for different scenarios:

- **Unit Tests**: Fast automated tests for code logic (no media files required).
- **Docker Tests**: Validate container functionality with sample media files.
- **Regression Tests**: Compare processing results across versions using real media files.

The majority of development and debugging time is spent figuring out how to deal with media file and media processing tool specifics affecting playback.\
For repetitive test tasks pre-configured on-demand tests are included in VSCode [`tasks.json`](./.vscode/tasks.json) and [`launch.json`](./.vscode/launch.json), and VisualStudio [`launchSettings.json`](./PlexCleaner/Properties/launchSettings.json).\
Several of the tests reference system local paths containing media files, so you may need to make path changes to match your environment.

### Unit Testing

Unit tests validate core functionality without requiring media files. Run locally or in CI/CD pipelines.

```console
dotnet build
dotnet format --verify-no-changes --severity=info --verbosity=detailed
dotnet test
```

### Docker Testing

The [`Test.sh`](./Docker/Test.sh) script validates basic container functionality. It downloads sample files if no external media path is provided.

The [`Test.sh`](./Docker/Test.sh) test script is included in the docker build and can be used to test basic functionality from inside the container.\
If an external media path is not specified the test will download and use the [Matroska test files](https://github.com/ietf-wg-cellar/matroska-test-files/archive/refs/heads/master.zip).

```console
docker run \
  -it --rm \
  --name PlexCleaner-Test \
  docker.io/ptr727/plexcleaner:latest \
  /Test/Test.sh
```

### Regression Testing

Regression testing ensures consistent behavior across versions by comparing processing results on the same media files.

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

# Test containers
RunContainer docker.io/ptr727/plexcleaner latest
RunContainer docker.io/ptr727/plexcleaner develop
```

## Development Tooling

**Prerequisites**: .NET SDK 10, Visual Studio Code, and Git.

### Install

Install development tools using winget (Windows):

```shell
# Core development tools
winget install Microsoft.DotNet.SDK.10      # .NET 10 SDK for building
winget install Microsoft.VisualStudioCode  # IDE
winget install nektos.act                   # Local GitHub Actions testing
```

Install .NET development tools:

```shell
# Initialize tool manifest
dotnet new tool-manifest

# Code formatting and quality tools
dotnet tool install csharpier           # Code formatter
dotnet tool install husky               # Git hooks for pre-commit checks
dotnet tool install dotnet-outdated-tool # Dependency update checker

# Setup git hooks
dotnet husky install
dotnet husky add pre-commit -c "dotnet husky run"
```

### Update

Keep tools up-to-date:

```shell
# Update winget-installed tools
winget upgrade Microsoft.DotNet.SDK.10
winget upgrade Microsoft.VisualStudioCode
winget upgrade nektos.act
```

```shell
# Update .NET tools and dependencies
dotnet tool restore                  # Restore tools from manifest
dotnet tool update --all             # Update all .NET tools
dotnet husky install                 # Reinstall git hooks
dotnet outdated --upgrade:prompt     # Interactive dependency updates
```

## Frequently Asked Questions

**Q: What is Direct Play and why does it matter?**

A: Direct Play means your media server (Plex/Emby/Jellyfin) sends the file directly to your player without transcoding. This saves server CPU, reduces power consumption, preserves quality, and enables playback on low-power devices. PlexCleaner ensures your media files are in formats that all your devices can Direct Play.

**Q: How long does processing take?**

A: Processing time varies significantly based on operations:

- **Re-multiplexing** (container changes): Very fast, typically 1-5% of playback time.
- **Track removal/language tags**: Very fast, typically <1 minute per file.
- **Verification**: Fast, typically 10-30% of playback time (faster with `--quickscan`).
- **De-interlacing**: Slow, typically 0.5-2x real-time (depends on CPU/GPU).
- **Re-encoding**: Very slow, typically 0.1-1x real-time (heavily depends on CPU/GPU and quality settings).
- **First run**: Slower due to analysis; subsequent runs are much faster thanks to sidecar files.

Use `--parallel` for large libraries to process multiple files concurrently.

**Q: When should I re-encode vs just re-multiplex?**

A: Re-multiplex (container changes only) when:

- File is in MP4, AVI, or other non-MKV container.
- Tracks need removal/reordering.
- Metadata needs updating.

Re-encode (computationally expensive) only when:

- Codec is incompatible (MPEG-2, VC-1, Vorbis, WMAPro).
- Video is interlaced and causing playback issues.
- Embedded closed captions need removal.
- Specific quality/size requirements.

**Q: Will PlexCleaner delete my files?**

A: PlexCleaner modifies files in place and creates `.PlexCleaner` sidecar files. It only deletes files if:

- `DeleteUnwantedExtensions` is enabled and file matches unwanted patterns.
- `DeleteInvalidFiles` is enabled and file fails verification/repair.
- `DeleteEmptyFolders` is enabled after processing.

Always maintain backups before processing.

**Q: Can I run PlexCleaner while Plex/Emby/Jellyfin is running?**

A: Yes, but be cautious:

- Media servers may have files open/locked during playback.
- Modified files may need library refresh to reflect changes.
- Consider using `monitor` mode with `--parallel` for continuous processing.
- Test with a small subset first.

**Q: Why are my files being re-processed every time?**

A: Check:

- Sidecar files (`.PlexCleaner`) are being preserved (not deleted/excluded).
- File content hasn't changed (sidecar uses content hash, not timestamp).
- Configuration changes may invalidate sidecar state.
- Tool version updates may trigger re-analysis.

Delete sidecar files with `createsidecar` to force fresh analysis.

**Q: Does PlexCleaner support 4K/HDR/Dolby Vision?**

A: Yes. PlexCleaner preserves video quality and HDR metadata when re-multiplexing. When re-encoding:

- HDR10 metadata is preserved with proper encoder settings.
- Dolby Vision: Profile 7 (cross-compatible) is supported; Profile 5 generates warnings.
- Use hardware encoding carefully - not all GPUs preserve HDR metadata correctly.
- Test with sample files before processing entire library.

## Feature Ideas

Have a feature request or idea? Please check [GitHub Discussions][discussions-link] and [Issues][issues-link] first to see if it's already been proposed. If not, feel free to start a new discussion!

Some ideas being considered:

- Cleanup chapters (e.g. chapter markers that exceed media play time).
- Cleanup NFO files (e.g. verify schema, verify image URLs).
- Cleanup text-based subtitle files (e.g. convert file encoding to UTF8).
- Process external subtitle files (e.g. merge or extract).

## 3rd Party Tools

- [7-Zip](https://www.7-zip.org/)
- [AwesomeAssertions](https://awesomeassertions.org/)
- [Bring Your Own Badge](https://github.com/marketplace/actions/bring-your-own-badge)
- [CliWrap][cliwrap-link]
- [Docker Hub Description](https://github.com/marketplace/actions/docker-hub-description)
- [Docker Run Action](https://github.com/marketplace/actions/docker-run-action)
- [dotnet-outdated](https://github.com/dotnet-outdated/dotnet-outdated)
- [FFmpeg](https://www.ffmpeg.org/)
- [Git Auto Commit](https://github.com/marketplace/actions/git-auto-commit)
- [GitHub Actions](https://github.com/actions)
- [GitHub Dependabot](https://github.com/dependabot)
- [HandBrake](https://handbrake.fr/)
- [Husky.Net](https://alirezanet.github.io/Husky.Net/)
- [ISO 639-2 language tags](https://www.loc.gov/standards/iso639-2/langhome.html)
- [ISO 639-3 language tags](https://iso639-3.sil.org/)
- [JSON2CSharp][json2csharp-link]
- [MediaInfo](https://mediaarea.net/en-us/MediaInfo/)
- [MKVToolNix](https://mkvtoolnix.download/)
- [Nerdbank.GitVersioning](https://github.com/marketplace/actions/nerdbank-gitversioning)
- [regex101.com](https://regex101.com/)
- [RFC 5646 language tags](https://www.rfc-editor.org/rfc/rfc5646.html)
- [Serilog](https://serilog.net/)
- [Utf8JsonAsyncStreamReader][utf8jsonasync-link]
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
[cliwrap-link]: https://github.com/Tyrrrz/CliWrap
[commit-link]: https://github.com/ptr727/PlexCleaner/commits/main
[discussions-link]: https://github.com/ptr727/PlexCleaner/discussions
[docker-develop-version-shield]: https://img.shields.io/docker/v/ptr727/plexcleaner/develop?label=Docker%20Develop&logo=docker&color=orange
[docker-latest-version-shield]: https://img.shields.io/docker/v/ptr727/plexcleaner/latest?label=Docker%20Latest&logo=docker
[docker-link]: https://hub.docker.com/r/ptr727/plexcleaner
[docker-status-shield]: https://img.shields.io/github/actions/workflow/status/ptr727/PlexCleaner/BuildDockerPush.yml?logo=github&label=Docker%20Build
[github-link]: https://github.com/ptr727/PlexCleaner
[plexcleaner-hub-link]: https://hub.docker.com/r/ptr727/plexcleaner
[issues-link]: https://github.com/ptr727/PlexCleaner/issues
[json2csharp-link]: https://json2csharp.com
[last-build-shield]: https://byob.yarr.is/ptr727/PlexCleaner/lastbuild
[last-commit-shield]: https://img.shields.io/github/last-commit/ptr727/PlexCleaner?logo=github&label=Last%20Commit
[license-link]: ./LICENSE
[license-shield]: https://img.shields.io/github/license/ptr727/PlexCleaner?label=License
[pre-release-version-shield]: https://img.shields.io/github/v/release/ptr727/PlexCleaner?include_prereleases&label=GitHub%20Pre-Release&logo=github
[release-status-shield]: https://img.shields.io/github/actions/workflow/status/ptr727/PlexCleaner/BuildGitHubRelease.yml?logo=github&label=Releases%20Build
[release-version-shield]: https://img.shields.io/github/v/release/ptr727/PlexCleaner?logo=github&label=GitHub%20Release
[releases-link]: https://github.com/ptr727/PlexCleaner/releases
[ubuntu-hub-link]: https://hub.docker.com/_/ubuntu
[utf8jsonasync-link]: https://github.com/gragra33/Utf8JsonAsyncStreamReader
