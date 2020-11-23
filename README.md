# PlexCleaner

Utility to optimize media files for DirectPlay on Plex.

## License

Licensed under the [MIT License](./LICENSE)  
![GitHub](https://img.shields.io/github/license/ptr727/PlexCleaner)

## Build Status

Code and Pipeline is on [GitHub](https://github.com/ptr727/PlexCleaner)  
![GitHub Last Commit](https://img.shields.io/github/last-commit/ptr727/PlexCleaner?logo=github)  
![GitHub Workflow Status](https://img.shields.io/github/workflow/status/ptr727/PlexCleaner/Build%20and%20Publish%20Pipeline?logo=github)  
![GitHub Latest Release)](https://img.shields.io/github/v/release/ptr727/PlexCleaner?logo=github)

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
- Some subtitle tracks like VOBsub cause hangs when the MuxingMode attribute is not set, re-multiplex the file.
- Automatic audio and subtitle track selection requires the track language to be set, set the language for unknown tracks.
- Automatic track selection ignores the Default track attribute and uses the first track when multiple tracks are present, remove duplicate tracks.
- Corrupt files cause playback issues, verify stream integrity, try to automatically repair, or delete.
- Some WiFi or 100mbps Ethernet connected devices with small read buffers cannot play high bitrate content, verify content bitrate does not exceed the network bitrate.

### Installation

- Install the [.NET 5.0 Runtime](https://dotnet.microsoft.com/download) and [download](https://github.com/ptr727/PlexCleaner/releases/latest) pre-compiled binaries.
- Or compile from [code](https://github.com/ptr727/PlexCleaner.git) using [Visual Studio 2019](https://visualstudio.microsoft.com/downloads/) or [Visual Studio Code](https://code.visualstudio.com/download) or the [.NET 5.0 SDK](https://dotnet.microsoft.com/download).
- Note that .NET 5. is cross platform, but the tools and usage of the tools will only work on Windows x64.

### Configuration File

Create a default configuration file by running:  
`PlexCleaner.exe --settingsfile PlexCleaner.json defaultsettings`

```jsonc
{
  "ToolsOptions": {
    // Tools folder
    "RootPath": ".\\Tools\\",
    // Tools directory relative to binary location
    "RootRelative": true
  },
  "ConvertOptions": {
    // Encoding video constant quality
    "VideoEncodeQuality": 20,
    // Encoding audio codec
    "AudioEncodeCodec": "ac3"
  },
  "ProcessOptions": {
    // Delete empty folders
    "DeleteEmptyFolders": true,
    // Delete non-media files
    "DeleteUnwantedExtensions": true,
    // Files to keep, e.g. subtitle or partial files
    "KeepExtensions": ".partial~",
    // Enable re-mux
    "ReMux": true,
    // Remux files to MKV if the extension matches
    "ReMuxExtensions": ".avi,.m2ts,.ts,.vob,.mp4,.m4v,.asf,.wmv",
    // Enable de-interlace
    // Note de-interlace detection is not absolute
    "DeInterlace": true,
    // Enable re-encode
    "ReEncode": true,
    // Re-encode the video if the format, codec, and profile values match
    // * will match anything, the number of filter entries must match
    // Use FFProbe attribute naming, and the `printmediainfo` command to get media info
    "ReEncodeVideoFormats": "mpeg2video,mpeg4,msmpeg4v3,msmpeg4v2,vc1,h264",
    "ReEncodeVideoCodecs": "*,dx50,div3,mp42,*,*",
    "ReEncodeVideoProfiles": "*,*,*,*,*,Constrained Baseline@30",
    // Re-encode matching audio codecs
    // If the video format is not H264 or H265, video will automatically be converted to H264 to avoid audio sync issues
    // Use FFProbe attribute naming, and the `printmediainfo` command to get media info
    "ReEncodeAudioFormats": "flac,mp2,vorbis,wmapro,pcm_s16le",
    // Set default language if tracks have an undefined language
    "SetUnknownLanguage": true,
    // Default track language
    "DefaultLanguage": "eng",
    // Enable removing of unwanted language tracks
    "RemoveUnwantedLanguageTracks": true,
    // Track languages to keep
    // Use ISO 639-2 3 letter short form
    "KeepLanguages": "eng,afr,chi,ind",
    // Enable removing of duplicate tracks of the same type and language
    // Priority is given to tracks marked as Default
    // Forced subtitle tracks are prioritized
    // Subtitle tracks containing "SDH" in the title are de-prioritized
    // Audio tracks containing "Commentary" in the title are de-prioritized
    "RemoveDuplicateTracks": true,
    // If no Default audio tracks are found, tracks are prioritized by codec type
    // Use MKVMerge attribute naming, and the `printmediainfo` command to get media info
    "PreferredAudioFormats": "truehd atmos,truehd,dts-hd master audio,dts-hd high resolution audio,dts,e-ac-3,ac-3",
    // Enable removing of all tags from the media file
    // Track title information is not removed
    "RemoveTags": true,
    // Speedup media metadata processing by saving media info in sidecar files
    "UseSidecarFiles": true,
    // Invalidate sidecar files when tool versions change
    "SidecarUpdateOnToolChange": false,
    // Enable verify
    "Verify": true,
    // List of media files to ignore, e.g. repeat processing failures, but media still plays
    "FileIgnoreList": [
      "\\\\server\\share1\\path1\\file1.mkv",
      "\\\\server\\share2\\path2\\file2.mkv"
    ]
  },
  "MonitorOptions": {
    // Time to wait after detecting a file change
    "MonitorWaitTime": 60,
    // Time to wait between file retry operations
    "FileRetryWaitTime": 5,
    // Number of times to retry a file operation
    "FileRetryCount": 2
  },
  "VerifyOptions": {
    // Attempt to repair media files that fail verification
    "AutoRepair": true,
    // Delete media files that fail verification and fail repair
    "DeleteInvalidFiles": true,
    // Minimum required playback duration in seconds
    "MinimumDuration": 300,
    // Time in seconds to verify media streams, 0 will verify entire file
    "VerifyDuration": 0,
    // Time in seconds to count interlaced frames, 0 will count entire file
    "IdetDuration": 0,
    // Maximum bitrate in bits per second, 0 will skip computation
    "MaximumBitrate": 100000000
  }
}
```

### Update Tools

- The 3rd party tools used by this project are not included, they must be downloaded by the end-user.
- Make sure the `Tools` folder exists, the default folder is in the same folder as the binary.
- [Download](https://www.7-zip.org/download.html) the 7-Zip commandline tool, e.g. [7z1805-extra.7z](https://www.7-zip.org/a/7z1805-extra.7z)
- Extract the contents of the archive to the `Tools\7Zip` folder.
- The 7-Zip commandline tool should be in `Tools\7Zip\x64\7za.exe`
- Update all the required tools to the latest version by running:
  - `PlexCleaner.exe --settingsfile PlexCleaner.json checkfornewtools`
  - The tool version information will be stored in `Tools\Tools.json`
- Keep the tools updated by periodically running the `checkfornewtools` command.

## Usage

### Commandline

Commandline options:  
`PlexCleaner.exe --help`

```console
PS C:\..\netcoreapp3.1> .\PlexCleaner.exe --help
PlexCleaner:
  Utility to optimize media files for DirectPlay on Plex

Usage:
  PlexCleaner [options] [command]

Options:
  --settingsfile <settingsfile> (REQUIRED)    Path to settings file
  --logfile <logfile>                         Path to log file
  --logappend                                 Append to the log file vs. default overwrite
  --version                                   Show version information
  -?, -h, --help                              Show help and usage information

Commands:
  defaultsettings     Write default values to settings file
  checkfornewtools    Check for and download new tools
  process             Process media files
  monitor             Monitor and process media file changes in folders
  remux               Re-Multiplex media files
  reencode            Re-Encode media files
  deinterlace         De-Interlace media files
  verify              Verify media files
  createsidecar       Create sidecar files
  getsidecar          Print sidecar file attribute information
  gettagmap           Print attribute tag-map created from media files
  getmediainfo        Print media file attribute information
  getbitrateinfo      Print media file bitrate information
```

The `--settingsfile` JSON settings file is required.  
The `--logfile` output log file is optional, the file will be overwritten unless `--logappend` is set.

One of the commands must be specified.

### Process Media Files

```console
PS C:\...\netcoreapp3.1> .\PlexCleaner.exe process --help
process:
  Process media files.

Usage:
  PlexCleaner process [options]

Options:
  --mediafiles <mediafiles> (REQUIRED)    List of media files or folders.
  --testsnippets                          Create short video clips, useful during testing.
  --testnomodify                          Do not make any modifications, useful during testing.
  -?, -h, --help                          Show help and usage information
```

The `process` command will use the JSON configuration settings to conditionally modify the media content.  
The `--mediafiles` option can point to a combination of files or folders.  

Example:  
`PlexCleaner.exe --settingsfile "PlexCleaner.json" --logfile "PlexCleaner.log" --appendtolog process --mediafiles "C:\Foo\Test.mkv" "D:\Media"`

The following processing will be done:

- Delete files with extensions not in the `KeepExtensions` list.
- Re-multiplex containers in the `ReMuxExtensions` list to MKV container format.
- Remove all tags from the media file.
- Set the language to `DefaultLanguage` for any track with an undefined language.
- Remove tracks with languages not in the `KeepLanguages` list.
- Remove duplicate tracks, where duplicates are tracks of the same type and language.
- Re-multiplex the media file if required.
- De-interlace the video track if interlaced.
- Re-encode video to H264 at `VideoEncodeQuality` if video matches the `ReEncodeVideoFormats`, `ReEncodeVideoCodecs`, and `ReEncodeVideoProfiles` list.
- Re-encode audio to `AudioEncodeCodec` if audio matches the `ReEncodeAudioFormats` list.
- Verify the media container and stream integrity, if corrupt try to automatically repair, else delete the file.

### Re-Multiplex, Re-Encode, De-Interlace, Verify

The `remux` command will re-multiplex the media files using `MKVMerge`.

The `reencode` command will re-encode the media files using `FFMPeg` and H264 at `VideoEncodeQuality` for video, and `AudioEncodeCodec` for audio.

The `deinterlace` command will de-interlace interlaced media files using `HandBrake --comb-detect --decomb`.

The `verify` command will use `FFmpeg` to render the file streams and report on any container or stream errors.

Unlike the `process` command, no conditional logic will be applied, the file will always be modified.

### Monitor

The `monitor` command will watch the specified folders for changes, and process the directories with changes.

Note that the [FileSystemWatcher](https://docs.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher?view=netcore-3.1) is not always reliable on Linux or NAS Samba shares.  
Also note that changes made directly to the underlying filesystem will not trigger when watching the SMB shares, e.g. when a Docker container writes to a mapped volume, the SMB view of that volume will not trigger.

### CreateSidecar, GetSidecar, GetTagMap, GetMediaInfo, GetBitrateInfo

The `createsidecar` command will create sidecar files.

The `getsidecar` command will print sidecar file attributes.

The `gettagmap` command will calculate and print attribute mappings between between different media information tools.

The `getmediainfo` command will print media attribute information.

The `getbitrateinfo` command will calculate and print media bitrate information.

## Tools and Utilities

Tools, libraries, and utilities used in the project.

### NuGet Component Dependencies

```console
PS C:\Users\piete\source\repos\PlexCleaner> dotnet list package
Project 'PlexCleaner' has the following package references
   [net5.0]:
   Top-level Package                            Requested             Resolved
   > HtmlAgilityPack                            1.11.28               1.11.28
   > InsaneGenius.Utilities                     1.6.1                 1.6.1
   > Microsoft.CodeAnalysis.FxCopAnalyzers      3.3.1                 3.3.1
   > Microsoft.SourceLink.GitHub                1.1.0-beta-20204-02   1.1.0-beta-20204-02
   > Newtonsoft.Json                            12.0.3                12.0.3
   > System.CommandLine                         2.0.0-beta1.20371.2   2.0.0-beta1.20371.2
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
- [regex101.com](https://regex101.com/)

### Sample Media Files

- [Kodi](https://kodi.wiki/view/Samples)
- [JellyFish](http://jell.yfish.us/)
- [DemoWorld](https://www.demo-world.eu/2d-demo-trailers-hd/)
- [MPlayer](https://samples.mplayerhq.hu/)
