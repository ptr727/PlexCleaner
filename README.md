# PlexCleaner

Utility to optimize media files for DirectPlay on Plex.

## License

[![GitHub](https://img.shields.io/github/license/ptr727/plexcleaner)](./LICENSE)  
Licensed under the [MIT License](./LICENSE)

## Project

![GitHub last commit](https://img.shields.io/github/last-commit/ptr727/plexcleaner?logo=github)  
Code is on [GitHub](https://github.com/ptr727/PlexCleaner).  
CI is on [Azure DevOps](https://dev.azure.com/pieterv/PlexCleaner).

## Build Status

[![Build Status](https://dev.azure.com/pieterv/PlexCleaner/_apis/build/status/PlexCleaner-Master-CI?branchName=master)](https://dev.azure.com/pieterv/PlexCleaner/_build/latest?definitionId=32&branchName=master)
![GitHub release (latest SemVer)](https://img.shields.io/github/v/release/ptr727/plexcleaner?logo=github&sort=semver)

## Getting Started

### Use Cases

The objective of the tool is to modify media content such that it will [Direct Play](https://support.plex.tv/articles/200250387-streaming-media-direct-play-and-direct-stream/) in Plex.  
Different Plex server and client versions suffer different playback issues, and issues are often associated with specific media attributes. Sometimes Plex may eventually fix the issue, other times the only solution is modification of the media file.

Below are a few examples of issues I've experienced over the many years of using Plex on Roku, NVidia Shield, and Apple TV:

- Container file formats other than MKV and MP4 are not supported by the client platform, re-multiplex to MKV.
- MPEG2 licensing prevents the platform from hardware decoding the content, re-encode to H264.
- Some video codecs like MPEG-4 or VC1 cause playback issues, re-encode to H264.
- Some H264 video profiles like "Constrained Baseline@30" cause hangs on Roku, re-encode to H264 "High@40".
- Interlaced video cause playback issues, re-encode to H264 using HandBrake and de-interlace using `--comb-detect --decomb` options.
- Some audio codecs like Vorbis or WMAPro are not supported by the client platform, re-encode to AC3.
- Some subtitle tracks like VOBsub cause hangs when the MuxingMode attribute is not set, re-multiplex the track.
- Automatic audio and subtitle track selection requires the track language to be set, set the language for unknown tracks.

### Installation

- Install the [.NET Core 3.1 Runtime](https://dotnet.microsoft.com/download) and [download](https://github.com/ptr727/PlexCleaner/releases/latest) pre-compiled binaries.
- Or compile from [code](https://github.com/ptr727/PlexCleaner.git) using [Visual Studio 2019](https://visualstudio.microsoft.com/downloads/) or [Visual Studio Code](https://code.visualstudio.com/download) or the [.NET Core 3.1 SDK](https://dotnet.microsoft.com/download).

### Configuration File

Create a default configuration file by running:  
`PlexCleaner.exe --settings PlexCleaner.json writedefaults`

```jsonc
{
  "ToolsOptions": {
    // Tools folder
    "RootPath": ".\\Tools\\",
    // Tools directory relative to binary location
    "RootRelative": true
  },
  "ConvertOptions": {
    // Encoding video quality
    "VideoEncodeQuality": 20,
    // Encoding audio codec
    "AudioEncodeCodec": "ac3",
    // Create short video clips, useful during testing
    "TestSnippets": false
  },
  "ProcessOptions": {
    // Do not make any modifications, useful during testing
    "TestNoModify": false,
    // Delete empty folders
    "DeleteEmptyFolders": true,
    // Delete invalid media files, e.g. no video or audio track
    "DeleteInvalidFiles": true,
    // Delete non-media files
    "DeleteUnwantedExtensions": true,
    // Files to keep, e.g. .srt files
    "KeepExtensions": "",
    // Enable re-mux
    "ReMux": true,
    // Remux files to MKV if the extension matches
    "ReMuxExtensions": ".avi,.m2ts,.ts,.vob,.mp4,.m4v,.asf,.wmv",
    // Enable de-interlace
    "DeInterlace": true,
    // Enable re-encode
    "ReEncode": true,
    // Re-encode if the video codec and profile matches
    // * will match anything, codecs and profiles are treated like a pair
    "ReEncodeVideoCodecs": "mpeg2video,msmpeg4v3,h264,vc1",
    "ReEncodeVideoProfiles": "*,*,Constrained Baseline@30,*",
    // Re-encode matching audio codecs
    "ReEncodeAudioCodecs": "flac,mp2,vorbis,wmapro",
    // Set default language if tracks have an undefined language
    "SetUnknownLanguage": true,
    // Default track language
    "DefaultLanguage": "eng",
    // Enable removing unwanted language tracks
    "RemoveUnwantedTracks": true,
    // Track languages to keep
    "KeepLanguages": "eng,afr,chi,ind",
    // Speedup processing by saving media info in sidecar files
    "UseSidecarFiles": true
  },
  "MonitorOptions": {
    // Time to wait after detecting a file change
    "MonitorWaitTime": 60,
    // Time to wait between file retry operations
    "FileRetryWaitTime": 5,
    // Number of times to retry a file operation
    "FileRetryCount": 2
  }
```

### Update Tools

- The 3rd party tools used by this project are not included, they must be downloaded by the end-user.
- Make sure the `Tools` folder exists, the default folder is in the same folder as the binary.
- [Download](https://www.7-zip.org/download.html) the 7-Zip commandline tool, e.g. [7z1805-extra.7z](https://www.7-zip.org/a/7z1805-extra.7z)
- Extract the contents of the archive to the `Tools\7Zip` folder.
- The 7-Zip commandline tool should be in `Tools\7Zip\x64\7za.exe`
- Update all the required tools to the latest version by running:
  - `PlexCleaner.exe --settings PlexCleaner.json checkfornewtools`
- The tool version information will be stored in `Tools\Tools.json`

## Usage

### Commandline

Commandline options:  
`Plexcleaner.exe --help`

```console
C:\...\netcoreapp3.1>PlexCleaner.exe --help
PlexCleaner:
  Utility to optimize media files for DirectPlay on Plex.

Usage:
  PlexCleaner [options] [command]

Options:
  --settings <settings> (REQUIRED)    Path to settings file.
  --log <log>                         Path to log file.
  --version                           Show version information
  -?, -h, --help                      Show help and usage information

Commands:
  writedefaults       Write default values to settings file.
  checkfornewtools    Check for new tools and download if available.
  process             Process media files.
  remux               Re-Multiplex media files
  reencode            Re-Encode media files.
  writesidecar        Write sidecar files for media files.
  createtagmap        Create a tag-map from media files.
  monitor             Monitor for changes in folders and process any changed files.
```

The `--settings` JSON settings file is required.  
The `--log` output log file is optional.  
One of the commands must be specified.

### Process Media Files

The `process` command will use the JSON configuration settings to conditionally modify the media content.  
The `--files` option can point to a combination of files or folders.

Example:  
`PlexCleaner.exe --settings "PlexCleaner.json" --log "PlexCleaner.log" process --files "C:\Foo\Test.mkv" "D:\Media"`

```console
C:\...\netcoreapp3.1>PlexCleaner.exe process --help
process:
  Process media files.

Usage:
  PlexCleaner process [options]

Options:
  --files <files> (REQUIRED)    List of files or folders.
  -?, -h, --help                Show help and usage information
```

### Re-Multiplex and Re-Encode Media Files

The `remux` and `reencode` commands will re-multiplex or re-encode the media files without applying any conditional logic.

```console
C:\...\netcoreapp3.1>PlexCleaner.exe reencode --help
reencode:
  Re-Encode media files.

Usage:
  PlexCleaner reencode [options]

Options:
  --files <files> (REQUIRED)    List of files or folders.
  -?, -h, --help                Show help and usage information
```

```console
C:\...\netcoreapp3.1>PlexCleaner.exe remux --help
remux:
  Re-Multiplex media files

Usage:
  PlexCleaner remux [options]

Options:
  --files <files> (REQUIRED)    List of files or folders.
  -?, -h, --help                Show help and usage information
```

### Monitor

The `monitor` command will watch the specified folders for changes, and process the directories with changes.  
Note that the [FileSystemWatcher](https://docs.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher?view=netcore-3.1) is not always reliable on Linux or NAS Samba shares.  
Also note that changes made directly to the underlying filesystem will not trigger when watching the SMB shares, e.g. when a Docker container writes to a mapped volume, the SMB view of that volume will not trigger.

```console
C:\...\netcoreapp3.1>PlexCleaner.exe monitor --help
monitor:
  Monitor for changes in folders and process any changed files.

Usage:
  PlexCleaner monitor [options]

Options:
  --files <files> (REQUIRED)    List of files or folders.
  -?, -h, --help                Show help and usage information
```

## Tools and Utilitites

### NuGet Component Dependencies

```console
C:\...\PlexCleaner>dotnet list package
Project 'PlexCleaner' has the following package references
   [netcoreapp3.1]:
   Top-level Package                            Requested             Resolved
   > HtmlAgilityPack                            1.11.23               1.11.23
   > InsaneGenius.Utilities                     1.3.89                1.3.89
   > Microsoft.CodeAnalysis.FxCopAnalyzers      2.9.8                 2.9.8
   > Microsoft.SourceLink.GitHub                1.0.0                 1.0.0
   > Newtonsoft.Json                            12.0.3                12.0.3
   > System.CommandLine                         2.0.0-beta1.20158.1   2.0.0-beta1.20158.1
```

### 3rd Party Tools

- [7-Zip](https://www.7-zip.org/)
- [MediaInfo](https://mediaarea.net/en-us/MediaInfo/)
- [HandBrake](https://handbrake.fr/)
- [MKVToolNix](https://mkvtoolnix.download/)
- [FFmpeg](https://www.ffmpeg.org/)
- [ISO language codes](http://www-01.sil.org/iso639-3/download.asp)
- [Xml2CSharp](http://xmltocsharp.azurewebsites.net/)
- [quicktype](https://quicktype.io/)
- [regexr.com](https://regexr.com/)
- [regex101.com](https://regex101.com/)
- [myregextester.com](https://www.myregextester.com/)
- [txt2re.com](http://www.txt2re.com)

### Sample Media Files

- [Kodi](https://kodi.wiki/view/Samples)
- [JellyFish](http://jell.yfish.us/)
- [DemoWorld](https://www.demo-world.eu/2d-demo-trailers-hd/)
- [MPlayer](https://samples.mplayerhq.hu/)
