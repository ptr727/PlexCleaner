# PlexCleaner

Utility to optimize media files for Direct Play in Plex, Emby, Jellyfin.

## License

Licensed under the [MIT License](./LICENSE)  
![GitHub License](https://img.shields.io/github/license/ptr727/PlexCleaner)

## Build Status

Code and Pipeline is on [GitHub](https://github.com/ptr727/PlexCleaner).  
Binary releases are published on [GitHub Releases](https://github.com/ptr727/PlexCleaner/releases).  
Docker images are published on [Docker Hub](https://hub.docker.com/u/ptr727/plexcleaner) and [GitHub Container Registry](https://github.com/ptr727/PlexCleaner/pkgs/container/plexcleaner).  
[![GitHub Last Commit](https://img.shields.io/github/last-commit/ptr727/PlexCleaner?logo=github)](https://github.com/ptr727/PlexCleaner/commits/main)  
[![GitHub Workflow Status](https://img.shields.io/github/actions/workflow/status/ptr727/PlexCleaner/BuildPublishPipeline.yml?branch=main&logo=github)](https://github.com/ptr727/PlexCleaner/actions)  
[![GitHub Actions Last Build](https://byob.yarr.is/ptr727/PlexCleaner/lastbuild)](https://github.com/ptr727/PlexCleaner/actions)

## Releases

[![GitHub Latest Release)](https://img.shields.io/github/v/release/ptr727/PlexCleaner?logo=github)](https://github.com/ptr727/PlexCleaner/releases)  
[![Docker Latest Release](https://img.shields.io/docker/v/ptr727/plexcleaner/latest?label=latest&logo=docker)](https://hub.docker.com/u/ptr727/plexcleaner)  
[![GitHub Latest Pre-Release)](https://img.shields.io/github/v/release/ptr727/PlexCleaner?include_prereleases&label=pre-release&logo=github)](https://github.com/ptr727/PlexCleaner/releases)  
[![Docker Latest Pre-Release](https://img.shields.io/docker/v/ptr727/plexcleaner/develop?label=develop&logo=docker&color=orange)](https://hub.docker.com/u/ptr727/plexcleaner)

## Release Notes

- Version 3.0:
  - Switched from .NET 6 to .NET 7.
    - Switched from `ubuntu:latest` to `mcr.microsoft.com/dotnet/sdk:7.0-jammy`.
    - Switched from `Regex` to `GeneratedRegex`.
    - Switched from `DllImport` to `LibraryImport`.
  - Modified the settings schema to allow custom FFmpeg and HandBrake CLI command parameters.
    - Removed the `ConvertOptions:EnableH265Encoder`, `ConvertOptions:VideoEncodeQuality` and `ConvertOptions:AudioEncodeCodec` options.
    - Replaced with `ConvertOptions:FfMpegOptions` and `ConvertOptions:HandBrakeOptions` options.
    - Custom CLI options allows for e.g. AV1 video codec, Intel QuickSync encoding, NVidia NVENC encoding, and custom encoding parameters.
    - See the [Custom FFmpeg and HandBrake CLI Parameters](#custom-ffmpeg-and-handbrake-cli-parameters) section for usage details.
    - Older settings schemas will automatically be upgraded with compatible settings to v3 on first run.
  - Added `createschema` command to create the settings JSON schema file, no longer need to use `Sandbox` project to create the schema file.
  - [*Breaking Change*] Refactored commandline arguments to only add relevant options to commands that use them vs. adding global options to all commands.
    - Maintaining commandline backwards compatibility was [complicated](https://github.com/dotnet/command-line-api/issues/2023), and the change is unfortunately a breaking change.
    - The following global options have been removed and added to their respective commands:
      - `--settingsfile` used by several commands.
      - `--parallel` used by the `process` command.
      - `--threadcount` used by the `process` command.
    - Move the option from the global options to follow the specific command, e.g.:
      - From: `PlexCleaner --settingsfile PlexCleaner.json defaultsettings ...`
      - To: `PlexCleaner defaultsettings --settingsfile PlexCleaner.json ...`
      - From: `PlexCleaner --settingsfile PlexCleaner.json --parallel --threadcount 2 process ...`
      - To: `PlexCleaner process --settingsfile PlexCleaner.json --parallel --threadcount 2 ...`
- Fixed the file process logic to continue cleanup even if verify failed.
- See [Release History](./HISTORY.md) for older Release Notes.

## Questions or Issues

Use the [Discussions](https://github.com/ptr727/PlexCleaner/discussions) forum for general questions.  
Report bugs in the [Issues](https://github.com/ptr727/PlexCleaner/issues) tracker.

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
- Dolby Vision is only supported on DV capable displays, warn when the HDR profile is `Dolby Vision` (profile 5) vs. `Dolby Vision / SMPTE ST 2086` (profile 7) that supports DV and HDR10 displays.
- EIA-608 Closed Captions embedded in video streams can't be disable or managed from the player, remove embedded closed captions from video streams.

## Performance Considerations

- To improve processing performance of large media collections, the media file attributes and processing state is stored in sidecar files. (`filename.mkv` -> `filename.PlexCleaner`)
- Sidecar files allow re-processing of the same files to be very fast as the state will be read from the sidecar vs. re-computed from the media file.
- The sidecar maintains a hash of small parts of the media file (timestamps are unreliable), and the media file will be reprocessed when a change in the media file is detected.
- Re-multiplexing is an IO intensive operation and re-encoding is a CPU intensive operation.
- On systems with high core counts the `--parallel` option can be used to process files concurrently.
- Parallel processing is useful when a single instance of FFmpeg or Handbrake does not saturate the CPU resources of the system.
- When parallel processing is enabled, the default thread count is half the number of system cores, and can be changed using the `--threadcount` option.
- The initial `process` run on a large collection can take a long time to complete.
- Interrupt processing using `Ctl-C` and resume processing by re-running the same command.

## Installation

[Docker](#docker) builds are the easiest and most up to date way to run, and can be used on any platform that supports `x86-64` images.  
Alternatively, install directly on [Windows](#windows) or [Linux](#linux) following the provided instructions.

### Docker

- Builds are published on [Docker Hub](https://hub.docker.com/r/ptr727/plexcleaner) and [GitHub Container Registry](https://github.com/ptr727/PlexCleaner/pkgs/container/plexcleaner).
- Images are tagged with specific build numbers, or `latest` for current, or `develop` for beta or pre-release builds.
  - E.g. `docker pull ptr727/plexcleaner:latest`
  - E.g. `docker pull ptr727/plexcleaner:develop`
  - E.g. `docker pull ptr727/plexcleaner:2.10.17`
- Images are updated weekly with the latest upstream updates.
- The container has all the prerequisite 3rd party tools pre-installed.
- Map your host volumes, and make sure the user has permission to access and modify media files.
- The container is intended to be used in interactive mode, for long running operations run in a `screen` session.
- See examples below for instructions on getting started.

Example, run in an interactive shell:

```console
# The host "/data/media" directory is mapped to the container "/media" directory
# Replace the volume mappings to suit your needs

# Run the bash shell in an interactive session
docker run \
  -it \
  --rm \
  --pull always \
  --name PlexCleaner \
  --volume /data/media:/media:rw \
  ptr727/plexcleaner \
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

Example, run in a screen session:

```console
# Start a new screen session
screen

# Or attach to an existing screen session
screen -r

# Make sure the media file permissions allow writing
sudo chown -R nobody:users /data/media
sudo chmod -R u=rwx,g=rwx+s,o=rx /data/media

# Run the process command in an interactive session
docker run \
  -it \
  --rm \
  --pull always \
  --name PlexCleaner \
  --user nobody:users \
  --env TZ=America/Los_Angeles \
  --volume /data/media:/media:rw \
  ptr727/plexcleaner \
  /PlexCleaner/PlexCleaner \
    --logfile /media/PlexCleaner/PlexCleaner.log \
    process \
    --settingsfile /media/PlexCleaner/PlexCleaner.json \
    --parallel \
    --mediafiles /media/Movies \
    --mediafiles /media/Series
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
  - Keep the 3rd party tools updated by periodically running the `checkfornewtools` command, or enabling the `ToolsOptions:AutoUpdate` setting.

### Linux

- Automatic downloading of Linux 3rd party tools are not currently supported, consider using the [Docker](#docker) build instead.
- Manually install the 3rd party tools by following steps similar to the [Docker](./Docker/Dockerfile) file `RUN` commands. (Note, steps are for Ubuntu, adjust as appropriate for your distribution.)
  - Install prerequisites.
  - Install [.NET Runtime](https://docs.microsoft.com/en-us/dotnet/core/install/linux).
  - Install [MediaInfo](https://mediaarea.net/en/MediaInfo/Download/Ubuntu).
  - Install [MKVToolNix](https://mkvtoolnix.download/downloads.html#ubuntu).
  - Install [FFmpeg](https://launchpad.net/~savoury1/+archive/ubuntu/ffmpeg5).
  - Install [HandBrake](https://launchpad.net/~savoury1/+archive/ubuntu/handbrake).
- Keep the 3rd party tools updated by periodically running `sudo apt update && sudo apt upgrade -y`.
- Download [PlexCleaner](https://github.com/ptr727/PlexCleaner/releases/latest) and extract the pre-compiled binaries.
- Or compile from [code](https://github.com/ptr727/PlexCleaner.git) using the [.NET SDK](https://docs.microsoft.com/en-us/dotnet/core/install/linux).
- Create a default JSON settings file using the `defaultsettings` command:
  - `./PlexCleaner defaultsettings --settingsfile PlexCleaner.json`
  - Modify the settings to suit your needs.

## Configuration

Create a default JSON configuration file by running:  
`PlexCleaner defaultsettings --settingsfile PlexCleaner.json`

Following is the [default JSON settings](./PlexCleaner.json) with usage comments:

```jsonc
{
  // JSON Schema
  "$schema": "https://raw.githubusercontent.com/ptr727/PlexCleaner/main/PlexCleaner.schema.json",
  // JSON Schema version
  "SchemaVersion": 3,
  // Tools options
  "ToolsOptions": {
    // Use system installed tools
    // Default true on Linux
    "UseSystem": false,
    // Tools folder, ignored when UseSystem is true
    "RootPath": ".\\Tools\\",
    // Tools directory relative to binary location
    "RootRelative": true,
    // Automatically check for new tools
    "AutoUpdate": false
  },
  // Convert options
  "ConvertOptions": {
    // FFmpeg commandline options
    "FfMpegOptions": {
      // Video encoding option following -c:v
      "Video": "libx264 -crf 20 -preset medium",
      // Audio encoding option following -c:a
      "Audio": "ac3",
      // Global options
      "Global": "-analyzeduration 2147483647 -probesize 2147483647",
      // Output options
      "Output": "-max_muxing_queue_size 1024 -abort_on empty_output"
    },
    // HandBrake commandline options
    "HandBrakeOptions": {
      // Video encoding options following --encode
      "Video": "x264 --quality 20 --encoder-preset medium",
      // Audio encoding option following --aencode
      "Audio": "copy --audio-fallback ac3"
    }
  },
  // Process options
  "ProcessOptions": {
    // Delete empty folders
    "DeleteEmptyFolders": true,
    // Delete non-media files
    // Any file that is not in KeepExtensions or in ReMuxExtensions or MKV will be deleted
    "DeleteUnwantedExtensions": true,
    // File extensions to keep but not process, e.g. subtitles, cover art, info, partial, etc.
    "KeepExtensions": [
      ".partial~",
      ".nfo",
      ".jpg",
      ".srt",
      ".smi",
      ".ssa",
      ".ass",
      ".vtt"
    ],
    // Enable re-mux
    "ReMux": true,
    // File extensions to remux to MKV
    "ReMuxExtensions": [
      ".avi",
      ".m2ts",
      ".ts",
      ".vob",
      ".mp4",
      ".m4v",
      ".asf",
      ".wmv",
      ".dv"
    ],
    // Enable deinterlace
    // Note deinterlace detection is not absolute
    "DeInterlace": true,
    // Enable re-encode
    "ReEncode": true,
    // Re-encode the video if the Format, Codec, and Profile values match
    // Empty fields will match with any value
    // Use FfProbe attribute naming, and the `printmediainfo` command to get media info
    "ReEncodeVideo": [
      {
        "Format": "mpeg2video"
      },
      {
        "Format": "mpeg4",
        "Codec": "dx50"
      },
      {
        "Format": "msmpeg4v3",
        "Codec": "div3"
      },
      {
        "Format": "msmpeg4v2",
        "Codec": "mp42"
      },
      {
        "Format": "vc1"
      },
      {
        "Format": "h264",
        "Profile": "Constrained Baseline@30"
      },
      {
        "Format": "wmv3"
      },
      {
        "Format": "msrle"
      },
      {
        "Format": "rawvideo"
      },
      {
        "Format": "indeo5"
      }
    ],
    // Re-encode matching audio codecs
    // If the video format is not H264/5, video will automatically be converted to H264/5 to avoid audio sync issues
    // Use FfProbe attribute naming, and the `printmediainfo` command to get media info
    "ReEncodeAudioFormats": [
      "flac",
      "mp2",
      "vorbis",
      "wmapro",
      "pcm_s16le",
      "opus",
      "wmav2",
      "pcm_u8",
      "adpcm_ms"
    ],
    // Set default language if tracks have an undefined language
    "SetUnknownLanguage": true,
    // Default track language
    "DefaultLanguage": "eng",
    // Enable removing of unwanted language tracks
    "RemoveUnwantedLanguageTracks": true,
    // Track languages to keep
    // Use ISO 639-2 3 letter short form, see https://www.loc.gov/standards/iso639-2/php/code_list.php
    "KeepLanguages": [
      "eng",
      "afr",
      "chi",
      "ind"
    ],
    // Enable removing of duplicate tracks of the same type and language
    // Priority is given to tracks marked as Default
    // Forced subtitle tracks are prioritized
    // Subtitle tracks containing "SDH" in the title are de-prioritized
    // Audio tracks containing "Commentary" in the title are de-prioritized
    "RemoveDuplicateTracks": true,
    // If no Default audio tracks are found, tracks are prioritized by codec type
    // Use MkvMerge attribute naming, and the `printmediainfo` command to get media info
    "PreferredAudioFormats": [
      "truehd atmos",
      "truehd",
      "dts-hd master audio",
      "dts-hd high resolution audio",
      "dts",
      "e-ac-3",
      "ac-3"
    ],
    // Enable removing of all tags from the media file
    // Track title information is not removed
    "RemoveTags": true,
    // Speedup media re-processing by saving media info and processed state in sidecar files
    "UseSidecarFiles": true,
    // Invalidate sidecar files when tool versions change
    "SidecarUpdateOnToolChange": false,
    // Enable verify
    "Verify": true,
    // Restore media file modified timestamp to original pre-processed value
    "RestoreFileTimestamp": false,
    // List of files to skip during processing
    // Files that previously failed verify or repair will automatically be skipped
    // Non-ascii characters must be JSON escaped, e.g. "Fiancé" into "Fianc\u00e9"
    "FileIgnoreList": [
      "\\\\server\\share1\\path1\\file1.mkv",
      "\\\\server\\share2\\path2\\file2.mkv"
    ]
  },
  // Monitor options
  "MonitorOptions": {
    // Time to wait after detecting a file change
    "MonitorWaitTime": 60,
    // Time to wait between file retry operations
    "FileRetryWaitTime": 5,
    // Number of times to retry a file operation
    "FileRetryCount": 2
  },
  // Verify options
  "VerifyOptions": {
    // Attempt to repair media files that fail verification
    "AutoRepair": true,
    // Delete media files that fail repair
    "DeleteInvalidFiles": false,
    // Add media files that fail verify or repair to the FileIgnoreList setting
    // Not required when using sidecar files
    "RegisterInvalidFiles": false,
    // Minimum required playback duration in seconds
    "MinimumDuration": 300,
    // Time in seconds to verify media streams, 0 will verify entire file
    "VerifyDuration": 0,
    // Time in seconds to find interlaced frames, 0 will process entire file
    "IdetDuration": 0,
    // Maximum bitrate in bits per second, 0 will skip computation
    "MaximumBitrate": 100000000,
    // Skip files older than the minimum file age in days, 0 will process all files
    "MinimumFileAge": 0
  }
}
```

## Custom FFmpeg and HandBrake CLI Parameters

The `ConvertOptions:FfMpegOptions` and `ConvertOptions:HandBrakeOptions` settings allows for custom CLI parameters to be used during processing.

Note that hardware assisted encoding options are operating system, hardware, and tool version specific, for example configurations see the [Jellyfin](https://jellyfin.org/docs/general/administration/hardware-acceleration/) documentation.  
The listed example configurations are from documentation and based on minimal testing with Intel QuickSync on Windows only, please discuss and post working configurations in [Discussions](https://github.com/ptr727/PlexCleaner/discussions).

### FFmpeg Options

See the [FFmpeg documentation](https://ffmpeg.org/ffmpeg.html) for complete commandline option details.  
The typical FFmpeg commandline is `ffmpeg [global_options] {[input_file_options] -i input_url} ... {[output_file_options] output_url}`.  
E.g. `ffmpeg "-analyzeduration 2147483647 -probesize 2147483647 -i "/media/foo.mkv" -max_muxing_queue_size 1024 -abort_on empty_output -hide_banner -nostats -map 0 -c:v libx265 -crf 20 -preset medium -c:a ac3 -c:s copy -f matroska "/media/bar.mkv"`

Settings allows for custom configuration of:

- `FfMpegOptions:Global`: Global options, e.g. `-analyzeduration 2147483647 -probesize 2147483647`
- `FfMpegOptions:Output`: Output options, e.g. `-max_muxing_queue_size 1024 -abort_on empty_output`
- `FfMpegOptions:Video`: Video encoder options following the `-c:v` parameter, e.g. `libx264 -crf 20 -preset medium`
- `FfMpegOptions:Audio`: Audio encoder options following the `-c:a` parameter, e.g. `ac3`

Example video encoder options:

- List all supported encoders: `ffmpeg -encoders`
- List options supported by an encoder: `ffmpeg -h encoder=libaom-av1`
- [H.264](https://trac.ffmpeg.org/wiki/Encode/H.264): `libx264 -crf 22 -preset medium`
- [H.265](https://trac.ffmpeg.org/wiki/Encode/H.265): `libx265 -crf 26 -preset medium`
- [AV1](https://trac.ffmpeg.org/wiki/Encode/AV1): `libaom-av1 -crf 30`

Example hardware assisted video encoding options:

- NVidia NVENC:
  - See [NVidia](https://developer.nvidia.com/blog/nvidia-ffmpeg-transcoding-guide/) and [FFmpeg](https://trac.ffmpeg.org/wiki/HWAccelIntro#CUDANVENCNVDEC) documentation.
  - NVENC encoder options: `ffmpeg -h encoder=h264_nvenc`
  - `FfMpegOptions:Global`: Add `-hwaccel cuda -hwaccel_output_format cuda` to the defaults.
  - `FfMpegOptions:Video`: `h264_nvenc`
- Intel QuickSync:
  - See [FFmpeg](https://trac.ffmpeg.org/wiki/Hardware/QuickSync) documentation.
  - QuickSync encoder options: `ffmpeg -h encoder=h264_qsv`
  - `FfMpegOptions:Global`: Add `-hwaccel qsv -hwaccel_output_format qsv` to the defaults.
  - `FfMpegOptions:Video`: `h264_qsv`

### HandBrake Options

See the [HandBrake documentation](https://handbrake.fr/docs/en/latest/cli/command-line-reference.html) for complete commandline option details.  
The typical HandBrake commandline is `HandBrakeCLI [options] -i <source> -o <destination>`.  
E.g. `HandBrakeCLI --input "/media/foo.mkv" --output "/media/bar.mkv" --format av_mkv --encoder x265 --quality 20 --encoder-preset medium --comb-detect --decomb --all-audio --aencoder copy --audio-fallback ac3`

Settings allows for custom configuration of:

- `HandBrakeOptions:Video`: Video encoder options following the `--encode` parameter, e.g. `x264 --quality 20 --encoder-preset medium`
- `HandBrakeOptions:Audio`: Audio encoder options following the `--aencode` parameter, e.g. `copy --audio-fallback ac3`

Example video encoder options:

- List all supported encoders: `HandBrakeCLI.exe --help`
- List presets supported by an encoder: `HandBrakeCLI --encoder-preset-list x264`
- H.264: `x264 --quality 22 --encoder-preset medium`
- H.265: `x265 --quality 26 --encoder-preset medium`
- AV1: `svt_av1 --quality 30 --encoder-preset 8`

Example hardware assisted video encoding options:

- NVidia NVENC:
  - See [HandBrake](https://handbrake.fr/docs/en/latest/technical/video-nvenc.html) documentation.
  - `HandBrakeOptions:Video`: `nvenc_h264`
- Intel QuickSync:
  - See [HandBrake](https://handbrake.fr/docs/en/latest/technical/video-qsv.html) documentation.
  - `HandBrakeOptions:Video`: `qsv_h264`

Note that HandBrake is primarily used for video deinterlacing, and only as backup encoder when FFmpeg fails.  
The default `HandBrakeOptions:Audio` configuration is set to `copy --audio-fallback ac3` that will copy all supported audio tracks as is, and only encode to `ac3` if the audio codec is not natively supported.

## Usage

Use the `PlexCleaner --help` commandline option to get a list of commands and options.  
One of the commands must be specified, and some commands have additional required options.  
To get more help for a specific command run `PlexCleaner <command> --help`.

```console
> ./PlexCleaner --help
Description:
  Utility to optimize media files for Direct Play in Plex, Emby, Jellyfin

Usage:
  PlexCleaner [command] [options]

Options:
  --logfile <logfile>  Path to log file
  --logappend          Append to the log file vs. default overwrite
  --debug              Wait for debugger to attach
  --version            Show version information
  -?, -h, --help       Show help and usage information

Commands:
  defaultsettings   Write default values to settings file
  checkfornewtools  Check for and download new tools
  process           Process media files
  monitor           Monitor and process media file changes in folders
  remux             Re-Multiplex media files
  reencode          Re-Encode media files
  deinterlace       De-Interlace media files
  createsidecar     Create new sidecar files
  getsidecarinfo    Print sidecar file attribute information
  gettagmap         Print attribute tag-map created from media files
  getmediainfo      Print media file attribute information
  gettoolinfo       Print tool file attribute information
  removesubtitles   Remove all subtitles
  createschema      Write settings JSON schema to file
```

### Process Media Files

The `process` command will process the media content using options as defined in the settings file and the optional commandline arguments:

- Delete files with extensions not in the `KeepExtensions` list.
- Re-multiplex containers in the `ReMuxExtensions` list to MKV container format.
- Remove all tags, titles, thumbnails, and attachments from the media file.
- Set the language to `DefaultLanguage` for any track with an undefined language.
- If multiple audio tracks of the same language but different encoding formats are present, set the default track based on `PreferredAudioFormats`.
- Remove tracks with languages not in the `KeepLanguages` list.
- Remove duplicate tracks, where duplicates are tracks of the same type and language.
- Re-multiplex the media file if required.
- Deinterlace the video track if interlaced.
- Remove EIA-608 Closed Captions from video streams.
- Re-encode video to H.264/5 if video format matches `ReEncodeVideo`.
- Re-encode audio to `AudioEncodeCodec` if audio matches the `ReEncodeAudioFormats` list.
- Verify the media container and stream integrity, if corrupt try to automatically repair, else conditionally delete the file.

The `--mediafiles` option can include multiple files or directories, e.g. `--mediafiles path1 --mediafiles "path with space" --mediafiles file1 --mediafiles file2`.  
Paths with spaces should be double quoted.

The `--reprocess [level]` option is used to override sidecar and conditional processing optimization logic.

- `0`: Default behavior, do not do any reprocessing.
- `1`: Re-process metadata operations, e.g. tag detection, closed caption detection, etc.
- `2`: Re-process metadata and stream operations, e.g. deinterlace detection, bitrate calculation, stream verification, etc.

The `--reverify` option is used to re-verify and repair media files that are in the `VerifyFailed` state, and by default would be skipped due to processing optimization logic.

Add the `--parallel` option to process multiple files concurrently. When parallel processing is enabled, the default thread count is half the number of cores, override the thread count using the `--threadcount` option.

Example:  
`PlexCleaner --logfile PlexCleaner.log process --settingsfile PlexCleaner.json --parallel --mediafiles "C:\Foo With Space\Test.mkv" --mediafiles D:\Media`

Run `PlexCleaner process --help` for a list of all commandline options.

```console
> ./PlexCleaner process --help
Description:
  Process media files

Usage:
  PlexCleaner process [options]

Options:
  --settingsfile <settingsfile> (REQUIRED)  Path to settings file
  --mediafiles <mediafiles> (REQUIRED)      Media file or folder to process, repeat for multiples
  --parallel                                Enable parallel processing
  --threadcount <threadcount>               Number of threads to use for parallel processing
  --testsnippets                            Create short video clips, useful during testing
  --testnomodify                            Do not make any modifications, useful during testing
  --reprocess <reprocess>                   Re-process level, 0 = none (default), 1 = metadata, 2 = streams
  --reverify                                Re-verify and repair media in VerifyFailed state
  --logfile <logfile>                       Path to log file
  --logappend                               Append to the log file vs. default overwrite
  --debug                                   Wait for debugger to attach
  -?, -h, --help                            Show help and usage information
```

### Re-Multiplex, Re-Encode, De-Interlace

The `remux` command will re-multiplex the media files using `MkvMerge`.

The `reencode` command will re-encode the media files using FFmpeg and the `ConvertOptions:FfMpegOptions` settings.

The `deinterlace` command will re-encode and de-interlace interlaced media files using HandBrake and the `ConvertOptions:HandBrakeOptions` settings with `--comb-detect --decomb` enabled.

### Monitor

The `monitor` command will watch the specified folders for changes, and process the directories with changes.

Note that the [FileSystemWatcher](https://docs.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher) is not always reliable on Linux or NAS Samba shares.  
Also note that changes made directly to the underlying filesystem will not trigger when watching the SMB shares, e.g. when a Docker container writes to a mapped volume, the SMB view of that volume will not trigger.

### Create Sidecar

The `createsidecar` command will create or re-create sidecar files.  
All existing state attributes will be deleted.

### GetTagMap, GetMediaInfo, GetToolInfo, GetSidecarInfo

The `gettagmap` command will calculate and print attribute mappings between between different media information tools.

The `getmediainfo` command will print media attribute information.

The `gettoolinfo` command will print tool attribute information.

The `getsidecarinfo` command will print sidecar attribute information.

## Remove Subtitles

The `removesubtitles` command will remove all subtitle tracks from the media files.  
This is useful when the subtitles are forced or contains offensive language or advertising.

## 3rd Party Tools

- [7-Zip](https://www.7-zip.org/)
- [MediaInfo](https://mediaarea.net/en-us/MediaInfo/)
- [HandBrake](https://handbrake.fr/)
- [MKVToolNix](https://mkvtoolnix.download/)
- [FFmpeg](https://www.ffmpeg.org/)
- [ISO language codes](http://www-01.sil.org/iso639-3/download.asp)
- [Xml2CSharp](http://xmltocsharp.azurewebsites.net/)
- [quicktype](https://quicktype.io/)
- [regex101.com](https://regex101.com/)
- [HtmlAgilityPack](https://html-agility-pack.net/)
- [Newtonsoft.Json](https://www.newtonsoft.com/json)
- [System.CommandLine](https://github.com/dotnet/command-line-api)
- [Serilog](https://serilog.net/)
- [Nerdbank.GitVersioning](https://github.com/marketplace/actions/nerdbank-gitversioning)
- [Docker Login](https://github.com/marketplace/actions/docker-login)
- [Docker Setup Buildx](https://github.com/marketplace/actions/docker-setup-buildx)
- [Setup .NET Core SDK](https://github.com/marketplace/actions/setup-net-core-sdk)
- [Build and push Docker images](https://github.com/marketplace/actions/build-and-push-docker-images)
- [Rob Savoury PPA](https://launchpad.net/~savoury1)

## Sample Media Files

- [Kodi](https://kodi.wiki/view/Samples)
- [JellyFish](http://jell.yfish.us/)
- [DemoWorld](https://www.demo-world.eu/2d-demo-trailers-hd/)
- [MPlayer](https://samples.mplayerhq.hu/)
