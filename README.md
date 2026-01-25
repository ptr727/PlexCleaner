# PlexCleaner

Utility to optimize media files for Direct Play in Plex, Emby, Jellyfin, etc.

## Build and Distribution

- **Source Code**: [GitHub][github-link] - Full source code, issues, and CI/CD pipelines.
- **Binary Releases**: [GitHub Releases][releases-link] - Pre-compiled executables for Windows, Linux, and macOS.
- **Docker Images**: [Docker Hub][docker-link] - Container images with all tools pre-installed.

### Build Status

[![Release Status][release-status-shield]][actions-link]\
[![Docker Status][docker-status-shield]][actions-link]\
[![Last Commit][last-commit-shield]][commit-link]\
[![Last Build][last-build-shield]][actions-link]

### Releases

[![GitHub Release][release-version-shield]][releases-link]\
[![GitHub Pre-Release][pre-release-version-shield]][releases-link]\
[![Docker Latest][docker-latest-version-shield]][docker-link]\
[![Docker Develop][docker-develop-version-shield]][docker-link]

### Release Notes

**Version: 3.15**:

**Summary:**

- Updated from .NET 9 to .NET 10.
- Refactored code to support Nullable types and Native AOT.
- Changed MediaInfo output from XML to JSON for AOT compatibility.
- Documentation structure update.

> **⚠️ Docker Breaking Changes:**
>
> - Only `ubuntu:rolling` images are published (Alpine and Debian discontinued).
> - Only `linux/amd64` and `linux/arm64` architectures supported (`linux/arm/v7` discontinued).
> - Update compose files: Use `docker.io/ptr727/plexcleaner:latest` (Based on `ubuntu:rolling`).

See [Release History](./HISTORY.md) for complete release notes and older versions.

## Getting Started

Get started with PlexCleaner in three easy steps using Docker (recommended):

> **⚠️ Important**: PlexCleaner modifies media files in place. Always maintain backups of your media library before processing. Consider testing on sample files first.
>
> **ℹ️ Note**: Replace `/data/media` with your actual host media directory path. All examples map the host directory to `/media` inside the container.

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

- [Build and Distribution](#build-and-distribution)
  - [Build Status](#build-status)
  - [Releases](#releases)
  - [Release Notes](#release-notes)
- [Getting Started](#getting-started)
- [Table of Contents](#table-of-contents)
- [Questions or Issues](#questions-or-issues)
- [Use Cases](#use-cases)
- [Performance Considerations](#performance-considerations)
- [Installation](#installation)
  - [Docker](#docker)
  - [Windows](#windows)
  - [Linux](#linux)
  - [macOS](#macos)
  - [AOT](#aot)
- [Configuration](#configuration)
  - [Default Settings](#default-settings)
  - [Common Configuration Examples](#common-configuration-examples)
  - [IETF Language Matching](#ietf-language-matching)
  - [EIA-608 and CTA-708 Closed Captions](#eia-608-and-cta-708-closed-captions)
  - [Custom FFmpeg and Handbrake Encoding Settings](#custom-ffmpeg-and-handbrake-encoding-settings)
- [Usage](#usage)
  - [Common Commands Quick Reference](#common-commands-quick-reference)
  - [Global Options](#global-options)
  - [Process Command](#process-command)
  - [Monitor Command](#monitor-command)
  - [Other Commands](#other-commands)
- [Testing](#testing)
  - [Unit Testing](#unit-testing)
  - [Docker Testing](#docker-testing)
  - [Regression Testing](#regression-testing)
- [Development Tooling](#development-tooling)
- [Feature Ideas](#feature-ideas)
- [3rd Party Tools](#3rd-party-tools)
- [Sample Media Files](#sample-media-files)
- [License](#license)

## Questions or Issues

**For General Questions:**

- Use the [Discussions][discussions-link] forum for general questions, feature requests, and sharing working configurations.

**For Bug Reports:**

- Ask in the [Discussions][discussions-link] forum if you are not sure if it is a bug.
- Check the [Issues][issues-link] tracker for known problems first.
- When reporting a new bug, please include:
  - PlexCleaner version (`PlexCleaner --version`).
  - Operating system and architecture (Windows/Linux/Docker, x64/arm64).
  - Media tool versions (`PlexCleaner gettoolinfo`).
  - Complete command line and relevant configuration settings.
  - Full log output with `--debug` flag enabled.
  - Sample media file information (`PlexCleaner getmediainfo --mediafiles <file>`).
  - Steps to reproduce the issue.

## Use Cases

> **ℹ️ TL;DR**: *Direct Play* means your media server (Plex/Emby/Jellyfin) sends the file directly to your player without transcoding on the server or the client. This saves server CPU, reduces power consumption, preserves quality, and enables playback on low-power devices. The **objective of PlexCleaner** is to *modify media content* such that it will always Direct Play in [Plex](https://support.plex.tv/articles/200250387-streaming-media-direct-play-and-direct-stream/), [Emby](https://support.emby.media/support/solutions/articles/44001920144-direct-play-vs-direct-streaming-vs-transcoding), [Jellyfin](https://jellyfin.org/docs/plugin-api/MediaBrowser.Model.Session.PlayMethod.html), etc.

Common examples of issues resolved by the `process` command:

**Container & Codec Issues:**

- Non-MKV containers → Re-multiplex to MKV (player compatibility).
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
> - **Docker logging**: Configure [log rotation](https://docs.docker.com/config/containers/logging/configure/) to prevent large log files.
> - **Thread count**: Default is half of CPU cores (max 4); adjust with `--threadcount` if needed.

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
- See the [Docker README][docker-link] for current distribution and media tool versions.
- `ptr727/plexcleaner:latest` is based on [Ubuntu][ubuntu-hub-link] (`ubuntu:rolling`) built from the `main` branch.
- `ptr727/plexcleaner:develop` is based on [Ubuntu][ubuntu-hub-link] (`ubuntu:rolling`) built from the `develop` branch.
- Images are updated weekly with the latest upstream updates.
- The container has all the prerequisite 3rd party tools pre-installed.

**Path Mapping Convention**: All examples use `/data/media` as the host path mapped to `/media` inside the container. Replace `/data/media` with your actual host media location.

#### Docker Compose (Recommended for Monitor Mode)

For continuous monitoring of media folders, use Docker Compose.

```yaml
services:

  plexcleaner:
    image: docker.io/ptr727/plexcleaner:latest  # Use :develop for pre-release builds
    container_name: PlexCleaner
    restart: unless-stopped
    user: 1000:100  # Change to match your nonroot:users
    command:
      - /PlexCleaner/PlexCleaner
      - monitor  # Monitor command
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

For a simple one-time process operation, see the [Getting Started](#getting-started) example.

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
# Replace the 1001:100 with your nonroot:users
docker run \
  -it --rm --pull always \
  --name PlexCleaner \
  --user 1001:100 \
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

For one-time processing, see the [Getting Started](#getting-started) example or use similar syntax as above, replacing `monitor` with `process`.

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
   - Run from an elevated shell e.g. using [`gsudo`](https://github.com/gerardog/gsudo), else [symlinks will not be created](https://github.com/microsoft/winget-cli/issues/3437).
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

Ahead-of-time compiled self-contained binaries do not require any .NET runtime components to be installed.\
AOT builds are [platform specific](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot), require a platform native compiler, and are created using [`dotnet publish`](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-publish).

> **ℹ️ Note**: AOT binaries are not published in CI/CD due to being platform specific, and cross compilation of AOT binaries are not supported.

```shell
# Install .NET SDK and native code compiler
apt install dotnet-sdk-10.0 clang zlib1g-dev

# Clone repository (main or develop branch)
git clone -b develop https://github.com/ptr727/PlexCleaner.git ./PlexCleanerAOT
cd ./PlexCleanerAOT

# Publish standalone release build executable
dotnet publish ./PlexCleaner/PlexCleaner.csproj \
    --output ./PublishAOT \
    --configuration release \
    -property:PublishAot=true
```

## Configuration

### Default Settings

Create a default JSON configuration file by running:

```shell
PlexCleaner defaultsettings --settingsfile PlexCleaner.json
```

> **⚠️ Important**: The default settings file must be edited to match your requirements before processing media files.
> **Required Changes**:
>
> - Verify language settings:
>   - `ProcessOptions.DefaultLanguage`
>   - `ProcessOptions:KeepLanguages`
>   - `ProcessOptions.SetUnknownLanguage`
>   - `ProcessOptions.RemoveUnwantedLanguageTracks`
> - Verify codec settings:
>   - `ProcessOptions.ReEncodeVideo`
>   - `ProcessOptions.ReEncodeAudioFormats`
> - Verify encoding settings:
>   - `ConvertOptions.FfMpegOptions`
>   - `ConvertOptions.HandBrakeOptions`
> - Verify processing settings:
>   - `ProcessOptions.Verify`
>   - `ProcessOptions.ReMux`
>   - `ProcessOptions.ReEncode`
>   - `ProcessOptions.DeInterlace`
>   - `ProcessOptions.DeleteUnwantedExtensions`
>   - `ProcessOptions.RemoveTags`
>   - `ProcessOptions.RemoveDuplicateTracks`
>   - `ProcessOptions.RemoveClosedCaptions`
> - Verify verification settings:
>   - `VerifyOptions.AutoRepair`
>   - `VerifyOptions.DeleteInvalidFiles`
>   - `VerifyOptions.RegisterInvalidFiles`

Refer to the commented default JSON [settings file](./PlexCleaner.defaults.json) for detailed configuration options and explanations.

### Common Configuration Examples

Quick configuration examples for common use cases. Edit your `PlexCleaner.json` file:

**Keep Only English and Spanish Audio and Subtitles:**

```json
"ProcessOptions": {
  "KeepLanguages": ["en", "es"],
  "RemoveUnwantedLanguageTracks": true
}
```

**Re-encode MPEG-2 and VC1 Video to H.264:**

```json
"ProcessOptions": {
  "ReEncode": true,
  "ReEncodeVideo": [
    {
      "Format": "mpeg2video"
    },
    {
      "Format": "vc1"
    }
  ]
},
"ConvertOptions": {
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
  "ReEncodeAudioFormats": ["vorbis", "wmapro"],
  "FfMpegOptions": {
    "Audio": "ac3"
  }
}
```

**Remove Duplicate Audio Tracks and Keep the Best Quality:**

```json
"ProcessOptions": {
  "RemoveDuplicateTracks": true,
  "PreferredAudioFormats": [
    "ac-3",
    "dts-hd high resolution audio",
    "dts-hd master audio",
    "dts",
    "e-ac-3",
    "truehd atmos",
    "truehd"
  ]
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

### IETF Language Matching

> **ℹ️ TL;DR**: Language tag matching supports [IETF / RFC 5646 / BCP 47](https://en.wikipedia.org/wiki/IETF_language_tag) tag formats as implemented by [MkvMerge](https://codeberg.org/mbunkus/mkvtoolnix/wiki/Languages-in-Matroska-and-MKVToolNix).

**Common Use Cases:**

- **Keep only English**: Set `ProcessOptions:KeepLanguages` to `["en"]`.
- **Keep English and Spanish**: Set `ProcessOptions:KeepLanguages` to `["en", "es"]`.
- **Keep all Portuguese variants**: Use `"pt"` to match `pt`, `pt-BR` (Brazilian), `pt-PT` (European), etc.
- **Keep only Brazilian Portuguese**: Use `"pt-BR"` to match specifically Brazilian Portuguese.
- **Set IETF Language Tags if not present**: Set `ProcessOptions.SetIetfLanguageTags` to `true`.

Refer to [Docs/LanguageMatching.md](./Docs/LanguageMatching.md) for technical details on language tag matching, including examples, normalization, and configuration options.

### EIA-608 and CTA-708 Closed Captions

> **ℹ️ TL;DR**: Closed captions (CC) are subtitles embedded in the video stream (not separate tracks). They can cause issues with some players that always display them or cannot disable them. PlexCleaner can detect and remove them using the `RemoveClosedCaptions` option.

Refer to [Docs/ClosedCaptions.md](./Docs/ClosedCaptions.md) for technical details on detection and removal methods.

### Custom FFmpeg and Handbrake Encoding Settings

> **ℹ️ Note**: The default encoding settings work well for most users and provide good compatibility with Plex/Emby/Jellyfin. Only customize these settings if you have specific requirements (e.g., hardware encoding, different quality targets, or specific codec preferences).

Refer to [Docs/CustomOptions.md](./Docs/CustomOptions.md) for hardware acceleration setup, encoder options, and real-world examples.

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
  - Media tools internally use multiple threads.
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
- The [FileSystemWatcher](https://docs.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher) used to monitor for changes may not always work as expected when changes are made via virtual or network filesystem, e.g. NFS or SMB backed volumes may not detect changes made directly to the underlying ZFS filesystem, while running directly on ZFS will work fine.

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

## Testing

PlexCleaner includes multiple testing approaches for different scenarios:

- **Unit Tests**: Fast automated tests for code logic (no media files required).
- **Docker Tests**: Validate container functionality with sample media files.
- **Regression Tests**: Compare processing results across versions using real media files.

The majority of development and debugging time is spent figuring out how to deal with media file and media processing tool specifics affecting playback.

For repetitive test tasks pre-configured on-demand tests are included in VSCode [`tasks.json`](./.vscode/tasks.json) and [`launch.json`](./.vscode/launch.json), and VisualStudio [`launchSettings.json`](./PlexCleaner/Properties/launchSettings.json).\
Several of the tests reference system local paths containing media files, so you may need to make path changes to match your environment.

### Unit Testing

Unit tests validate core functionality without requiring media files. Run locally or in CI/CD pipelines.

```console
dotnet test
```

### Docker Testing

The [`Test.sh`](./Docker/Test.sh) script validates basic container functionality. It downloads sample files if no external media path is provided.

The [`Test.sh`](./Docker/Test.sh) test script is included in the docker build and can be used to test basic functionality from inside the container.

If an external media path is not specified the test will download and use the [Matroska test files](https://github.com/ietf-wg-cellar/matroska-test-files/archive/refs/heads/master.zip).

```shell
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

Install development tools using winget (Windows):

```shell
winget install Microsoft.DotNet.SDK.10           # .NET 10 SDK
winget install Microsoft.VisualStudioCode        # Visual Studio Code
winget install Microsoft.VisualStudio.Community  # Visual Studio
```

Update .NET development tools:

```shell
dotnet tool restore               # Restore tools from manifest
dotnet tool update --all          # Update all .NET tools
dotnet husky install              # Reinstall git hooks
dotnet outdated --upgrade:prompt  # Interactive dependency updates
```

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
