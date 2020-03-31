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
    "RootRelative": true,
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
    // Delete files if procesing fails
    "DeleteFailedFiles": true,
    // Delete non-media files
    "DeleteUnwantedExtensions": true,
    // Files to keep, e.g. .srt files
    "KeepExtensions": "",
    // Enable re-mux
    "ReMux": true,
    // Remux files to MKV if the extension matches
    "ReMuxExtensions": ".avi,.m2ts,.ts,.vob,.mp4,.m4v,.asf,.wmv",
    // Enable re-encode
    "ReEncode": true,
    // Enable de-interlace
    "DeInterlace": true,
    // Re-encode if the video codec and profile matches
    // * will match anything, codecs and profiles are treated like a pair
    "ReEncodeVideoCodecs": "mpeg2video,msmpeg4v3,h264",
    "ReEncodeVideoProfiles": "*,*,Constrained Baseline@30",
    // Re-encode matching audio codecs
    "ReEncodeAudioCodecs": "flac,mp2,vorbis,wmapro",
    // Set default language if tracks have an undefined language
    "SetUnknownLanguage": true,
    // Default track language
    "DefaultLanguage": "eng",
    // Enable removing unwanted tracks
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
}
```

### Update Tools

- Make sure the `Tools` folder exists, the default folder is in the same folder as the binary.
- [Download](https://www.7-zip.org/download.html) the 7-Zip commandline tool, e.g. [7z1805-extra.7z](https://www.7-zip.org/a/7z1805-extra.7z)
- Extract the contents of the archive to the `Tools\7Zip` folder.
- The 7-Zip commandline tool should be in `Tools\7Zip\x64\7za.exe`
- Update all the required tools to the latest version by running:
  - `PlexCleaner.exe --settings PlexCleaner.json checkfornewtools`
- The tool version information will be stored in `Tools\Tools.json`

## Usage

`Plexcleaner.exe --help`

```console
C:\...\netcoreapp3.1>PlexCleaner.exe --help
PlexCleaner:
  Optimize media files for DirectPlay on Plex

Usage:
  PlexCleaner [options] [command]

Options:
  --settings <settings> (REQUIRED)    Path to settings file
  --version                           Show version information
  -?, -h, --help                      Show help and usage information

Commands:
  writedefaults       Write default values to settings file
  checkfornewtools    Check for new tools and download if available
  process             Process media files
  remux               Re-Multiplex media files
  reencode            Re-Encode media files
  writesidecar        Write sidecar files for media files
  createtagmap        Create a tag-map from media files
  monitor             Monitor for changes in folders and process any changed files
```

```console
C:\...\netcoreapp3.1>PlexCleaner.exe --help process
process:
  Process media files

Usage:
  PlexCleaner process [options]

Options:
  --files <files> (REQUIRED)    List of files or folders
  -?, -h, --help                Show help and usage information
```

The `--files` option is required for any of the media processing commands. The parameter can point to a combination of files or folders.

Example:  
`PlexCleaner.exe --settings PlexCleaner.json process --files "C:\Foo\Test.mkv" "D:\Media"`

## TODO

- Capture standard and error output, but still let the app write formatted output, e.g. FFmpeg that writes in color.
- Reenable the file watcher when directory disappears.`System.ComponentModel.Win32Exception (0x80004005): The specified network name is no longer available`
- Retrieve SRT subtitles using original file details, before the sourced file gets modified.
- Embed SRT subtitles in the MKV file.
- Convert DIVX to H264 or just re-tag as DIVX as XVID using e.g. `cfourcc`
- Compare folder name with file name and rename to match.
- Check if more than two audio or subtitle tracks of the same language exists, and prefer DTS over AC3 by changing the track order.
- Keep machine from sleeping while processing.
- Remove subtitles that cannot DirectPlay, e.g. SubStation Alpha ASS.
- Remove readonly attribute before trying to delete file.

## Tools and Utilitites

Tools and utilitites used in the project.

- [7-Zip](https://www.7-zip.org/)
- [MediaInfo](https://mediaarea.net/en-us/MediaInfo/)
- [HandBrake](https://handbrake.fr/)
- [MKVToolNix](https://mkvtoolnix.download/)
- [FFmpeg](https://www.ffmpeg.org/)
- [ISO language codes](http://www-01.sil.org/iso639-3/download.asp)
- [Xml2CSharp](http://xmltocsharp.azurewebsites.net/)
- [quicktype](https://quicktype.io/)
- RegEx Tools
  - [regexr.com](https://regexr.com/)
  - [regex101.com](https://regex101.com/)
  - [myregextester.com](https://www.myregextester.com/)
  - [txt2re.com](http://www.txt2re.com)
- Media Sample Files
  - [Kodi](https://kodi.wiki/view/Samples)
  - [JellyFish](http://jell.yfish.us/)
  - [DemoWorld](https://www.demo-world.eu/2d-demo-trailers-hd/)
  - [MPlayer](https://samples.mplayerhq.hu/)
