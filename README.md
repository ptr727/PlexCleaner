# PlexCleaner

Utility to optimize media files for Direct Play in Plex, Emby, Jellyfin.

## License

Licensed under the [MIT License][license-link]\
![GitHub License][license-shield]

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

- version 3:12:
  - Update to .NET 9.0.
    - Dropping Ubuntu docker `arm/v7` support as .NET for ARM32 is no longer published in the Ubuntu repository.
    - Switching Debian docker builds to install .NET using Msft install script as the Microsoft repository now only supports x64 builds.
  - Updated code style [`.editorconfig`](./.editorconfig) to closely follow the Visual Studio and .NET Runtime defaults.
  - Removed docker [`UbuntuDevel.Dockerfile`](./Docker/Ubuntu.Devel.Dockerfile), [`AlpineEdge.Dockerfile`](./Docker/Alpine.Edge.Dockerfile), and [`DebianTesting.Dockerfile`](./Docker/Debian.Testing.Dockerfile) builds from CI as theses OS pre-release / Beta builds were prone to intermittent build failures. If "bleeding edge" media tools are required local builds can be done using the Dockerfiles.
  - Updated 7-Zip version number parsing to account for newly observed variants.
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

The objective of PlexCleaner is to modify media content such that it will always Direct Play in [Plex](https://support.plex.tv/articles/200250387-streaming-media-direct-play-and-direct-stream/), [Emby](https://support.emby.media/support/solutions/articles/44001920144-direct-play-vs-direct-streaming-vs-transcoding), [Jellyfin](https://jellyfin.org/docs/plugin-api/MediaBrowser.Model.Session.PlayMethod.html).

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
- EIA-608 Closed Captions embedded in video streams can't be disabled or managed from the player, remove embedded closed captions from video streams.
- See the `process` [command](#process-command) for more details.

## Performance Considerations

- To improve processing performance of large media collections, the media file attributes and processing state is cached in sidecar files. (`filename.mkv` -> `filename.PlexCleaner`)
- Sidecar files allow re-processing of the same files to be very fast as the state will be read from the sidecar vs. re-computed from the media file.
- The sidecar maintains a hash of small parts of the media file (timestamps are unreliable), and the media file will be reprocessed when a change in the media file is detected.
- Re-multiplexing is an IO intensive operation and re-encoding is a CPU intensive operation.
- Parallel processing, using the `--parallel` option, is useful when a single instance of FFmpeg or HandBrake does not saturate all the available CPU resources.
- When parallel processing is enabled, the default thread count is half the number of system cores, and can be changed using the `--threadcount` option.
- Processing can be interrupted using `Ctrl-C`, if using sidecar files restarting will skip previously verified files.
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

# Make sure the media file permissions allow writing for the executing user
# adduser --no-create-home --shell /bin/false --disabled-password --system --group users nonroot
# Replace the user account to suit your needs
sudo chown -R nonroot:users /data/media
sudo chmod -R ugo=rwx /data/media

# Run the bash shell in an interactive session
docker run \
  -it \
  --rm \
  --pull always \
  --name PlexCleaner \
  --user nonroot:users \
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
  --user nonroot:users \
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
  --user nonroot:users \
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
- Download the required 3rd party tools using the `checkfornewtools` command:
  - `PlexCleaner checkfornewtools --settingsfile PlexCleaner.json`
  - The default `Tools` folder will be created in the same folder as the `PlexCleaner` binary file.
  - The tool version information will be stored in `Tools\Tools.json`.
  - Keep the 3rd party tools updated by periodically running the `checkfornewtools` command, or update tools on every run by setting `ToolsOptions:AutoUpdate` to `true`.
- If required, e.g. no internet connectivity, the tools can be manually downloaded and extracted:
  - [FfMpeg Full](https://github.com/GyanD/codexffmpeg/releases), e.g. `ffmpeg-6.0-full.7z`: `\Tools\FfMpeg`
  - [HandBrake CLI x64](https://github.com/HandBrake/HandBrake/releases), e.g. `HandBrakeCLI-1.6.1-win-x86_64.zip`: `\Tools\HandBrake`
  - [MediaInfo CLI x64](https://mediaarea.net/en/MediaInfo/Download/Windows), e.g. `MediaInfo_CLI_23.07_Windows_x64.zip`: `\Tools\MediaInfo`
  - [MkvToolNix Portable x64](https://mkvtoolnix.download/downloads.html#windows), e.g. `mkvtoolnix-64-bit-79.0.7z`: `\Tools\MkvToolNix`
  - [7-Zip Extra](https://www.7-zip.org/download.html), e.g. `7z2301-extra.7z`: `\Tools\SevenZip`
  - Disable automatic tool updates by setting `ToolsOptions:AutoUpdate` to `false`.

### Linux

- Automatic downloading of Linux 3rd party tools are not supported, consider using the [Docker](#docker) build instead.
- Manually install the 3rd party tools, e.g. following steps similar to the [Docker](./Docker) file commands.
- Download [PlexCleaner](https://github.com/ptr727/PlexCleaner/releases/latest) and extract the pre-compiled binaries matching your platform.
- Or compile from [code](https://github.com/ptr727/PlexCleaner.git) using the [.NET SDK](https://docs.microsoft.com/en-us/dotnet/core/install/linux).
- Create a default JSON settings file using the `defaultsettings` command:
  - `./PlexCleaner defaultsettings --settingsfile PlexCleaner.json`
  - Modify the settings to suit your needs.

### macOS

- macOS x64 and Arm64 binaries are built as part of [Releases](https://github.com/ptr727/PlexCleaner/releases/latest), but are untested.

## Configuration

Create a default JSON configuration file by running:
`PlexCleaner defaultsettings --settingsfile PlexCleaner.json`

Refer to the commented default JSON [settings file](./PlexCleaner.defaults.json) for usage.

## Custom FFmpeg and HandBrake CLI Parameters

The `ConvertOptions:FfMpegOptions` and `ConvertOptions:HandBrakeOptions` settings allows for custom CLI parameters to be used during processing.

Note that hardware assisted encoding options are operating system, hardware, and tool version specific.\
Refer to the Jellyfin hardware acceleration [docs](https://jellyfin.org/docs/general/administration/hardware-acceleration/) for hints on usage.
The example configurations are from documentation and minimal testing with Intel QuickSync on Windows only, please discuss and post working configurations in [Discussions][discussions-link].

### FFmpeg Options

See the [FFmpeg documentation](https://ffmpeg.org/ffmpeg.html) for complete commandline option details.\
The typical FFmpeg commandline is `ffmpeg [global_options] {[input_file_options] -i input_url} ... {[output_file_options] output_url}`.
E.g. `ffmpeg "-analyzeduration 2147483647 -probesize 2147483647 -i "/media/foo.mkv" -max_muxing_queue_size 1024 -abort_on empty_output -hide_banner -nostats -map 0 -c:v libx265 -crf 26 -preset medium -c:a ac3 -c:s copy -f matroska "/media/bar.mkv"`

Settings allows for custom configuration of:

- `FfMpegOptions:Global`: Custom hardware global options, e.g. `-hwaccel cuda -hwaccel_output_format cuda`
- `FfMpegOptions:Video`: Video encoder options following the `-c:v` parameter, e.g. `libx264 -crf 22 -preset medium`
- `FfMpegOptions:Audio`: Audio encoder options following the `-c:a` parameter, e.g. `ac3`

Get encoder options:

- List all supported encoders: `ffmpeg -encoders`
- List options supported by an encoder: `ffmpeg -h encoder=libsvtav1`

Example video encoder options:

- [H.264](https://trac.ffmpeg.org/wiki/Encode/H.264): `libx264 -crf 22 -preset medium`
- [H.265](https://trac.ffmpeg.org/wiki/Encode/H.265): `libx265 -crf 26 -preset medium`
- [AV1](https://trac.ffmpeg.org/wiki/Encode/AV1): `libsvtav1 -crf 30 -preset 5`

Example hardware assisted video encoding options:

- NVidia NVENC:
  - See [NVidia](https://developer.nvidia.com/blog/nvidia-ffmpeg-transcoding-guide/) and [FFmpeg](https://trac.ffmpeg.org/wiki/HWAccelIntro#CUDANVENCNVDEC) documentation.
  - View NVENC encoder options: `ffmpeg -h encoder=h264_nvenc`
  - `FfMpegOptions:Global`: `-hwaccel cuda -hwaccel_output_format cuda`
  - `FfMpegOptions:Video`: `h264_nvenc -crf 22 -preset medium`
- Intel QuickSync:
  - See [FFmpeg](https://trac.ffmpeg.org/wiki/Hardware/QuickSync) documentation.
  - View QuickSync encoder options: `ffmpeg -h encoder=h264_qsv`
  - `FfMpegOptions:Global`: `-hwaccel qsv -hwaccel_output_format qsv`
  - `FfMpegOptions:Video`: `h264_qsv -crf 22 -preset medium`

### HandBrake Options

See the [HandBrake documentation](https://handbrake.fr/docs/en/latest/cli/command-line-reference.html) for complete commandline option details.
The typical HandBrake commandline is `HandBrakeCLI [options] -i <source> -o <destination>`.
E.g. `HandBrakeCLI --input "/media/foo.mkv" --output "/media/bar.mkv" --format av_mkv --encoder x265 --quality 26 --encoder-preset medium --comb-detect --decomb --all-audio --aencoder copy --audio-fallback ac3`

Settings allows for custom configuration of:

- `HandBrakeOptions:Video`: Video encoder options following the `--encode` parameter, e.g. `x264 --quality 22 --encoder-preset medium`
- `HandBrakeOptions:Audio`: Audio encoder options following the `--aencode` parameter, e.g. `copy --audio-fallback ac3`

Get encoder options:

- List all supported encoders: `HandBrakeCLI.exe --help`
- List presets supported by an encoder: `HandBrakeCLI --encoder-preset-list svt_av1`

Example video encoder options:

- H.264: `x264 --quality 22 --encoder-preset medium`
- H.265: `x265 --quality 26 --encoder-preset medium`
- AV1: `svt_av1 --quality 30 --encoder-preset 5`

Example hardware assisted video encoding options:

- NVidia NVENC:
  - See [HandBrake](https://handbrake.fr/docs/en/latest/technical/video-nvenc.html) documentation.
  - `HandBrakeOptions:Video`: `nvenc_h264 --quality 22 --encoder-preset medium`
- Intel QuickSync:
  - See [HandBrake](https://handbrake.fr/docs/en/latest/technical/video-qsv.html) documentation.
  - `HandBrakeOptions:Video`: `qsv_h264 --quality 22 --encoder-preset medium`

Note that HandBrake is primarily used for video deinterlacing, and only as backup encoder when FFmpeg fails.\
The default `HandBrakeOptions:Audio` configuration is set to `copy --audio-fallback ac3` that will copy all supported audio tracks as is, and only encode to `ac3` if the audio codec is not natively supported.

## Language Matching

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

## Usage

Use the `PlexCleaner --help` commandline option to get a list of commands and options.\
To get help for a specific command run `PlexCleaner <command> --help`.

```text
> ./PlexCleaner --help
Description:
  Utility to optimize media files for Direct Play in Plex, Emby, Jellyfin

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
  defaultsettings   Write default values to settings file
  checkfornewtools  Check for new tool versions and download if newer
  process           Process media files
  monitor           Monitor for file changes and process changed media files
  remux             Re-Multiplex media files
  reencode          Re-Encode media files
  deinterlace       De-Interlace media files
  removesubtitles   Remove subtitles from media files
  verify            Verify media files
  createsidecar     Create new sidecar files
  updatesidecar     Update existing sidecar files
  getversioninfo    Print application and tools version information
  getsidecarinfo    Print sidecar file information
  gettagmap         Print media information tag-map
  getmediainfo      Print media information using sidecar files
  gettoolinfo       Print media information using media tools
  createschema      Write settings schema to file
  ```

### Global Options

Global options apply to all commands.

- `--logfile`:
  - Path to the log file.
- `--logappend`:
  - Append to the existing log file, default will overwrite the log file.
- `--logwarning`:
  - Only log errors and warnings to the log file, default will log all information.
- `--debug`:
  - Launch and wait for a debugger to attach.

### Process Command

```text
> ./PlexCleaner process --help
Description:
  Process media files

Usage:
  PlexCleaner process [options]

Options:
  --settingsfile <settingsfile> (REQUIRED)  Path to settings file
  --mediafiles <mediafiles> (REQUIRED)      Path to media file or folder
  --testsnippets                            Create short media file clips
  --testnomodify                            Do not make any media file modifications
  --parallel                                Enable parallel processing
  --threadcount <threadcount>               Number of threads to use for parallel processing
  --reverify                                Re-verify and repair media files in the VerifyFailed state
  --resultsfile <resultsfile>               Path to results file
  --logfile <logfile>                       Path to log file
  --logappend                               Append to existing log file
  --logwarning                              Log warnings and errors only
  --debug                                   Wait for debugger to attach
  -?, -h, --help                            Show help and usage information
```

The `process` command will process the media content using options as defined in the settings file and the optional commandline arguments:

- Delete files with extensions not in the `KeepExtensions` list.
- Re-multiplex containers in the `ReMuxExtensions` list to MKV container format.
- Remove all tags, titles, thumbnails, cover art, and attachments from the media file.
- Set IETF language tags and Matroska track flags if missing.
- Set the language to `DefaultLanguage` for any track with an undefined language.
- If multiple audio tracks of the same language but different encoding formats are present, set the default track based on `PreferredAudioFormats`.
- Remove tracks with languages not in the `KeepLanguages` list.
- Remove duplicate tracks, where duplicates are tracks of the same type and language.
- Re-multiplex the media file if required.
- Deinterlace the video track if interlaced.
- Remove EIA-608 Closed Captions from video streams.
- Re-encode video if video format matches `ReEncodeVideo`.
- Re-encode audio if audio matches the `ReEncodeAudioFormats` list.
- Verify the media container and stream integrity, if corrupt try to automatically repair, else conditionally delete the file.

Options:

- `--settingsfile`: (required)
  - Path to the settings file.
- `--mediafiles`: (required)
  - Path to file or folder containing files to process.
  - Paths with spaces should be double quoted.
  - Repeat the option to include multiple files or directories, e.g. `--mediafiles path1 --mediafiles "path with space" --mediafiles file1 --mediafiles file2`.
- `--reverify`:
  - Re-verify and repair media files that are in the `VerifyFailed` state.
  - By default files would be skipped due to processing optimization logic when using sidecar files.
- `--parallel`:
  - Process multiple files concurrently.
  - When parallel processing is enabled, the default thread count is half the number of system cores.
- `--threadcount`:
  - Override the thread count when the `--parallel` option is enabled.
- `--testsnippets`:
  - Create short media clips that limit the processing time required, useful during testing.
- `--testnomodify`:
  - Process files but do not make any file modifications, useful during testing.

Example:

```text
./PlexCleaner \
  --logfile PlexCleaner.log \
  process \
  --settingsfile PlexCleaner.json \
  --parallel \
  --mediafiles "C:\Foo With Space\Test.mkv" \
  --mediafiles D:\Media
```

### Re-Multiplex, Re-Encode, De-Interlace, Remove Subtitles Commands

These commands have no conditional logic and will process all specified media files.

- `remux`:
  - Re-multiplex the media files using MkvMerge.
  - Useful to update the file with the latest multiplexer.
- `reencode`:
  - Re-encode the media files using FFmpeg.
- `deinterlace`:
  - De-interlace interlaced media files using HandBrake.
- `removesubtitles`:
  - Remove all subtitle tracks from the media files.
  - Useful when subtitles are forced and contains offensive language or advertising.

### Monitor

- `monitor`:
  - Watch the specified folders for file changes, and periodically run the `process` command on the changed folders.
  - The `monitor` command honors the `process` options.
  - Note that the [FileSystemWatcher](https://docs.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher) used to monitor for changes may not always work as expected when changes are made via virtual or network filesystem, e.g. NFS or SMB backed volumes may not detect changes made directly to the underlying ZFS filesystem.

### Create and Update Sidecar Files

- `createsidecar`:
  - Create or overwrite and re-create sidecar files.
  - All existing state attributes will be deleted.
- `updatesidecar`:
  - Update the existing sidecar with current media tool information.
  - Existing state attributes will be retained unless the media file had been modified.

### Get Information

- `gettagmap`:
  - Calculate and print media file attribute mappings between between different media tools.
- `getmediainfo`:
  - Print media attribute information using the Sidecar file if present.
  - If sidecar is not present or out of date media tools will be used.
- `gettoolinfo`:
  - Print media attribute information using the current media tools.
- `getsidecarinfo`:
  - Print sidecar file attribute information.
- `getversioninfo`:
  - Print application version, runtime version, and media tools version information.

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

## 3rd Party Tools

- [7-Zip](https://www.7-zip.org/)
- [MediaInfo](https://mediaarea.net/en-us/MediaInfo/)
- [HandBrake](https://handbrake.fr/)
- [MKVToolNix](https://mkvtoolnix.download/)
- [FFmpeg](https://www.ffmpeg.org/)
- [ISO 639-2 language tags](https://www.loc.gov/standards/iso639-2/langhome.html)
- [ISO 639-3 language tags](https://iso639-3.sil.org/)
- [RFC 5646 language tags](https://www.rfc-editor.org/rfc/rfc5646.html)
- [Xml2CSharp](http://xmltocsharp.azurewebsites.net/)
- [quicktype](https://quicktype.io/)
- [regex101.com](https://regex101.com/)
- [JsonSchema.Net.Generation][jsonschema-link]
- [Serilog](https://serilog.net/)
- [Nerdbank.GitVersioning](https://github.com/marketplace/actions/nerdbank-gitversioning)
- [Bring Your Own Badge](https://github.com/marketplace/actions/bring-your-own-badge)
- [Docker Hub Description](https://github.com/marketplace/actions/docker-hub-description)
- [Git Auto Commit](https://github.com/marketplace/actions/git-auto-commit)
- [Docker Run Action](https://github.com/marketplace/actions/docker-run-action)

## Sample Media Files

- [Kodi](https://kodi.wiki/view/Samples)
- [JellyFish](http://jell.yfish.us/)
- [DemoWorld](https://www.demo-world.eu/2d-demo-trailers-hd/)
- [MPlayer](https://samples.mplayerhq.hu/)
- [Matroska](https://github.com/ietf-wg-cellar/matroska-test-files)

***

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
