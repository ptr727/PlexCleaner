# PlexCleaner

Utility to optimize media files for Direct Play in Plex, Emby, Jellyfin.

## License

Licensed under the [MIT License](./LICENSE)  
![GitHub License](https://img.shields.io/github/license/ptr727/PlexCleaner)

## Build

Code and Pipeline is on [GitHub](https://github.com/ptr727/PlexCleaner).  
Binary releases are published on [GitHub Releases](https://github.com/ptr727/PlexCleaner/releases).  
Docker images are published on [Docker Hub](https://hub.docker.com/r/ptr727/plexcleaner).

## Status

[![Release Status](https://img.shields.io/github/actions/workflow/status/ptr727/PlexCleaner/BuildGitHubRelease.yml?logo=github&label=Releases%20Build)](https://github.com/ptr727/PlexCleaner/actions)  
[![Docker Status](https://img.shields.io/github/actions/workflow/status/ptr727/PlexCleaner/BuildDockerPush.yml?logo=github&label=Docker%20Build)](https://github.com/ptr727/PlexCleaner/actions)  
[![Last Commit](https://img.shields.io/github/last-commit/ptr727/PlexCleaner?logo=github&label=Last%20Commit)](https://github.com/ptr727/PlexCleaner/commits/main)  
[![Last Build](https://byob.yarr.is/ptr727/PlexCleaner/lastbuild)](https://github.com/ptr727/PlexCleaner/actions)

## Releases

[![GitHub Release)](https://img.shields.io/github/v/release/ptr727/PlexCleaner?logo=github&label=GitHub%20Release)](https://github.com/ptr727/PlexCleaner/releases)  
[![GitHub Pre-Release)](https://img.shields.io/github/v/release/ptr727/PlexCleaner?include_prereleases&label=GitHub%20Pre-Release&logo=github)](https://github.com/ptr727/PlexCleaner/releases)  
[![Docker Latest](https://img.shields.io/docker/v/ptr727/plexcleaner/latest?label=Docker%20Latest&logo=docker)](https://hub.docker.com/r/ptr727/plexcleaner)  
[![Docker Develop](https://img.shields.io/docker/v/ptr727/plexcleaner/develop?label=Docker%20Develop&logo=docker&color=orange)](https://hub.docker.com/r/ptr727/plexcleaner)

## Release Notes

- Version 3.1:
  - Added `--preprocess` option to the `monitor` command, that will pre-process all monitored folders.
- See [Release History](./HISTORY.md) for older Release Notes.

## Questions or Issues

- Use the [Discussions](https://github.com/ptr727/PlexCleaner/discussions) forum for general questions.
- Refer to the [Issues](https://github.com/ptr727/PlexCleaner/issues) tracker for known problems.
- Report bugs in the [Issues](https://github.com/ptr727/PlexCleaner/issues) tracker.

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
Alternatively, install directly on [Windows](#windows) or [Linux](#linux) following the provided instructions.

### Docker

- Builds are published on [Docker Hub](https://hub.docker.com/r/ptr727/plexcleaner).
- See the Docker [README](./Docker/README.md) for image and tag details.
- Images are updated weekly with the latest upstream updates.
- The container has all the prerequisite 3rd party tools pre-installed.
- Map your host volumes, and make sure the user has permission to access and modify media files.
- The container is intended to be used in interactive mode, for long running operations run in a `screen` session.
- See examples below for instructions on getting started.

Example, run in an interactive shell:

```text
# The host "/data/media" directory is mapped to the container "/media" directory
# Replace the volume mappings to suit your needs

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

Example, run in a screen session:

```text
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
  --pull always
  --log-driver json-file --log-opt max-size=10m \
  --name PlexCleaner \
  --user nobody:users \
  --env TZ=America/Los_Angeles \
  --volume /data/media:/media:rw \
  docker.io/ptr727/plexcleaner \
  /PlexCleaner/PlexCleaner \
    --logfile /media/PlexCleaner/PlexCleaner.log \
    --logwarning \
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

- Automatic downloading of Linux 3rd party tools are not supported, consider using the [Docker](#docker) build instead.
- Manually install the 3rd party tools, e.g. following steps similar to the [Docker](./Docker) file commands.
- Download [PlexCleaner](https://github.com/ptr727/PlexCleaner/releases/latest) and extract the pre-compiled binaries matching your platform.
- Or compile from [code](https://github.com/ptr727/PlexCleaner.git) using the [.NET SDK](https://docs.microsoft.com/en-us/dotnet/core/install/linux).
- Create a default JSON settings file using the `defaultsettings` command:
  - `./PlexCleaner defaultsettings --settingsfile PlexCleaner.json`
  - Modify the settings to suit your needs.

## Configuration

Create a default JSON configuration file by running:  
`PlexCleaner defaultsettings --settingsfile PlexCleaner.json`

Refer to the commented default JSON [settings file](./PlexCleaner.defaults.json) for usage.

## Custom FFmpeg and HandBrake CLI Parameters

The `ConvertOptions:FfMpegOptions` and `ConvertOptions:HandBrakeOptions` settings allows for custom CLI parameters to be used during processing.

Note that hardware assisted encoding options are operating system, hardware, and tool version specific.  
Refer to the Jellyfin hardware acceleration [docs](https://jellyfin.org/docs/general/administration/hardware-acceleration/) for hints on usage.  
The example configurations are from documentation and minimal testing with Intel QuickSync on Windows only, please discuss and post working configurations in [Discussions](https://github.com/ptr727/PlexCleaner/discussions).

### FFmpeg Options

See the [FFmpeg documentation](https://ffmpeg.org/ffmpeg.html) for complete commandline option details.  
The typical FFmpeg commandline is `ffmpeg [global_options] {[input_file_options] -i input_url} ... {[output_file_options] output_url}`.  
E.g. `ffmpeg "-analyzeduration 2147483647 -probesize 2147483647 -i "/media/foo.mkv" -max_muxing_queue_size 1024 -abort_on empty_output -hide_banner -nostats -map 0 -c:v libx265 -crf 26 -preset medium -c:a ac3 -c:s copy -f matroska "/media/bar.mkv"`

Settings allows for custom configuration of:

- `FfMpegOptions:Global`: Global options, e.g. `-analyzeduration 2147483647 -probesize 2147483647`
- `FfMpegOptions:Output`: Output options, e.g. `-max_muxing_queue_size 1024 -abort_on empty_output`
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
  - `FfMpegOptions:Global`: `-analyzeduration 2147483647 -probesize 2147483647 -hwaccel cuda -hwaccel_output_format cuda`
  - `FfMpegOptions:Video`: `h264_nvenc -crf 22 -preset medium`
- Intel QuickSync:
  - See [FFmpeg](https://trac.ffmpeg.org/wiki/Hardware/QuickSync) documentation.
  - View QuickSync encoder options: `ffmpeg -h encoder=h264_qsv`
  - `FfMpegOptions:Global`: `-analyzeduration 2147483647 -probesize 2147483647 -hwaccel qsv -hwaccel_output_format qsv`
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

Note that HandBrake is primarily used for video deinterlacing, and only as backup encoder when FFmpeg fails.  
The default `HandBrakeOptions:Audio` configuration is set to `copy --audio-fallback ac3` that will copy all supported audio tracks as is, and only encode to `ac3` if the audio codec is not natively supported.

## Language Matching

Language tag matching supports [IETF / RFC 5646 / BCP 47](https://en.wikipedia.org/wiki/IETF_language_tag) tag formats as implemented by [MkvMerge](https://gitlab.com/mbunkus/mkvtoolnix/-/wikis/Languages-in-Matroska-and-MKVToolNix).  
During processing the absence of IETF language tags will treated as a track warning, and an RFC 5646 IETF language will be temporarily assigned based on the ISO639-2B tag.  
If `ProcessOptions.SetIetfLanguageTags` is enabled MkvMerge will be used to remux the file using the `--normalize-language-ietf extlang` option, see the [MkvMerge docs](https://mkvtoolnix.download/doc/mkvpropedit.html) for more details.

Tags are in the form of `language-extlang-script-region-variant-extension-privateuse`, and matching happens left to right.  
E.g. `pt` will match `pt` Portuguese, or `pt-BR` Brazilian Portuguese, or `pt-BR` Portugal Portuguese.  
E.g. `pt-BR` will only match only `pt-BR` Brazilian Portuguese.  
E.g. `zh` will match `zh` Chinese, or `zh-Hans` simplified Chinese, or `zh-Hant` for traditional Chinese, and other variants.  
E.g. `zh-Hans` will only match `zh-Hans` simplified Chinese.

Normalized tags will be expanded for matching.  
E.g. `cmn-Hant` will be expanded to `zh-cmn-Hant` allowing matching with `zh`.

See the [W3C Language tags in HTML and XML](https://www.w3.org/International/articles/language-tags/) and [BCP47 language subtag lookup](https://r12a.github.io/app-subtags/) for more details.

## Usage

Use the `PlexCleaner --help` commandline option to get a list of commands and options.  
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
  --parallel                                Enable parallel processing
  --threadcount <threadcount>               Number of threads to use for parallel processing
  --testsnippets                            Create short media clips
  --testnomodify                            Do not make any file modifications
  --reverify                                Re-verify and repair media files in the VerifyFailed state
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
  - Watch the specified folders for changes, and process the directories with changes.
  - The [FileSystemWatcher](https://docs.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher) is not always reliable on Linux or NAS Samba shares.
  - Changes made directly to the underlying filesystem will not trigger when watching the SMB shares, e.g. when a Docker container writes to a mapped volume, the SMB view of that volume will not trigger.

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

## 3rd Party Tools

- [7-Zip](https://www.7-zip.org/)
- [MediaInfo](https://mediaarea.net/en-us/MediaInfo/)
- [HandBrake](https://handbrake.fr/)
- [MKVToolNix](https://mkvtoolnix.download/)
- [FFmpeg](https://www.ffmpeg.org/)
- [ISO 639-3 language codes](http://www-01.sil.org/iso639-3/download.asp)
- [RFC 5646 language tags](https://www.w3.org/International/articles/language-tags/)
- [Xml2CSharp](http://xmltocsharp.azurewebsites.net/)
- [quicktype](https://quicktype.io/)
- [regex101.com](https://regex101.com/)
- [Newtonsoft.Json](https://www.newtonsoft.com/json)
- [System.CommandLine](https://github.com/dotnet/command-line-api)
- [Serilog](https://serilog.net/)
- [Nerdbank.GitVersioning](https://github.com/marketplace/actions/nerdbank-gitversioning)
- [Bring Your Own Badge](https://github.com/marketplace/actions/bring-your-own-badge)
- [Docker Hub Description](https://github.com/marketplace/actions/docker-hub-description)
- [Git Auto Commit](https://github.com/marketplace/actions/git-auto-commit)
- [Docker Run Action](https://github.com/marketplace/actions/docker-run-action)
- [Microsoft .NET Linux Docker Images](https://hub.docker.com/_/microsoft-dotnet)
- [Rob Savoury's PPA](https://launchpad.net/~savoury1)
- [Arch Linux](https://archlinux.org/)

## Sample Media Files

- [Kodi](https://kodi.wiki/view/Samples)
- [JellyFish](http://jell.yfish.us/)
- [DemoWorld](https://www.demo-world.eu/2d-demo-trailers-hd/)
- [MPlayer](https://samples.mplayerhq.hu/)
