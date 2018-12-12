# Introduction
Clean media files for Plex playback. 

# Build Status
[![Build status](https://dev.azure.com/pieterv/Plex%20Cleaner/_apis/build/status/Plex%20Cleaner%20-%20.NET%20Core%20-%20CI)](https://dev.azure.com/pieterv/Plex%20Cleaner/_build/latest?definitionId=17)

# Getting Started
TODO.

# Build and Test
https://dev.azure.com/pieterv/Plex%20Cleaner/

# Contribute
https://dev.azure.com/pieterv/Plex%20Cleaner/

# TODO
- Compile T4 as part of build
https://github.com/clariuslabs/TransformOnBuild
https://github.com/bennor/AutoT4

- Capture standard output and error, and still let the app write formatted output, e.g. FFmpeg that writes in color

- Reenable the file watcher when directory disappears
e.GetException().GetType() == typeof(SomethingPathNotAccessibleException)), retry waiting with with Directory.Exists(path)
if (e is Win32Exception)
OnError : System.ComponentModel.Win32Exception (0x80004005): The specified network name is no longer available

- Retrieve SRT subtitles using original file details, before the sourced file gets modified

- Embed SRT files in MKV file

- Consider converting DIVX to H264 or just re-tag as XVID
cfourcc -i DIVX, DX50, FMP4, cfourcc -u XVID

- Compare folder with file name and rename to match

- Check if more than two audio or subtitle tracks of the same language
Prefer DTS over AC3, if same language, change order, e.g. the breakfast club

- Keep machine from sleeping while processing

- Remove subtitles that cannot DirectPlay, e.g. SubStation Alpha ASS

# Tools

## RegEx
https://regexr.com/
https://regex101.com/
https://www.myregextester.com/
http://www.txt2re.com


## MediaInfo CLI
https://mediaarea.net/en-us/MediaInfo/Download/Windows
https://mediaarea.net/download/snapshots/binary/mediainfo/
https://mediaarea.net/download/binary/mediainfo/17.10/MediaInfo_CLI_17.10_Windows_x64.zip
https://github.com/MediaArea/MediaInfo/blob/master/History_CLI.txt
https://raw.githubusercontent.com/MediaArea/MediaInfo/master/History_CLI.txt
https://github.com/MediaArea/MediaInfoLib/blob/master/Source/Resource/Text/Stream/Audio.csv


## Handbrake CLI
https://handbrake.fr/downloads2.php
https://handbrake.fr/rotation.php?file=HandBrakeCLI-1.0.7-win-x86_64.zip
https://handbrake.fr/mirror/HandBrakeCLI-1.0.7-win-x86_64.zip


## MKVToolNix
https://mkvtoolnix.download/
https://mkvtoolnix.download/windows/releases/17.0.0/mkvtoolnix-64-bit-17.0.0.7z
https://www.fosshub.com/MKVToolNix.html
https://www.fosshub.com/MKVToolNix.html/mkvtoolnix-64-bit-17.0.0.7z
https://mkvtoolnix.download/latest-release.xml.gz


## FFMPEG
https://www.ffmpeg.org/download.html#build-windows
http://ffmpeg.zeranoe.com/builds/
http://ffmpeg.zeranoe.com/builds/win64/static/ffmpeg-3.4-win64-static.zip
http://ffmpeg.zeranoe.com/builds/win64/static/ffmpeg-latest-win64-static.7z


## ISO language codes
http://www-01.sil.org/iso639-3/download.asp


## HTTP Downlaod
http://faithlife.codes/blog/2009/06/using_if-modified-since_in_http_requests/
https://stackoverflow.com/questions/6481073/how-to-download-a-file-only-when-the-local-file-is-older


## Samples
http://kodi.wiki/view/Samples
http://jell.yfish.us/
https://www.demo-world.eu/2d-demo-trailers-hd/
http://samples.mplayerhq.hu/


# Tool Usage 

## XSD
xsd.exe /c /namespace:PlexCleaner.MediaInfoXml /language:CS mediainfo_2_0.xsd


## FFMpeg
https://trac.ffmpeg.org/wiki/audio%20types
https://ffmpeg.org/ffmpeg-formats.html
https://trac.ffmpeg.org/wiki/Encode/HighQualityAudio

ffmpeg.exe -i "C:\Users\piete\Downloads\Shaolin Soccer (2001).wmv" -map 0 -codec copy -f matroska "wmv.copy.mkv"
ffmpeg.exe -i "C:\Users\piete\Downloads\Shaolin Soccer (2001).wmv" -c:v copy -c:a aac -f matroska "wmv.aac.mkv"
ffmpeg.exe -i "C:\Users\piete\Downloads\Shaolin Soccer (2001).wmv" -c:v copy -c:a ac3 -f matroska "wmv.ac3.mkv"
ffmpeg.exe -i "C:\Users\piete\Downloads\Shaolin Soccer (2001).wmv" -c:v copy -c:a eac3 -f matroska "wmv.eac3.mkv"
ffmpeg.exe -i "\\STORAGE\Media\Troublesome\Roku - Hang - Baseline@L3 - Blaze and the Monster Machines - S01E12 - The Mystery Bandit.mp4" -map 0 -codec copy -f matroska "\\STORAGE\Media\Troublesome\Roku - Hang - Baseline@L3 - Blaze and the Monster Machines - S01E12 - The Mystery Bandit.mkv"
ffmpeg.exe -i "\\STORAGE\Media\Samples\H264+EAC3.mkv" -map 0 -codec copy -f mp4 "\\STORAGE\Media\Samples\H264+EAC3.mp4"


## Handbrake CLI
handbrakecli.exe --input "C:\Users\piete\Downloads\Shaolin Soccer (2001).wmv" --output "hbcli.eac3.mkv" --format av_mkv --encoder x264 --encoder-preset medium --quality 21.0 --subtitle 1,2,3,4 --audio 1,2,3,4 --aencoder copy --audio-fallback eac3 --start-at duration:30 --stop-at duration:30
handbrakecli.exe --input "C:\Users\piete\Downloads\Shaolin Soccer (2001).wmv" --output "hbcli.avaac.mkv" --format av_mkv --encoder x264 --encoder-preset medium --quality 21.0 --subtitle 1,2,3,4 --audio 1,2,3,4 --aencoder copy --audio-fallback av_aac --start-at duration:30 --stop-at duration:30
handbrakecli.exe --input "C:\Users\piete\Downloads\Shaolin Soccer (2001).wmv" --output "hbcli.caaac.mkv" --format av_mkv --encoder x264 --encoder-preset medium --quality 21.0 --subtitle 1,2,3,4 --audio 1,2,3,4 --aencoder copy --audio-fallback ca_aac --start-at duration:30 --stop-at duration:30
handbrakecli.exe --input "C:\Users\piete\Downloads\Shaolin Soccer (2001).wmv" --output "hbcli.ac3.mkv" --format av_mkv --encoder x264 --encoder-preset medium --quality 21.0 --subtitle 1,2,3,4 --audio 1,2,3,4 --aencoder copy --audio-fallback ac3 --start-at duration:30 --stop-at duration:30


## MKVPropEdit
mkvmerge.exe --identify ".\Test\One\ShieldTV - Transcode - 50 First Dates (2004).mkv" --identification-format json >mkvmerge.eng.json
mediainfo.exe --Output=XML ".\Test\One\ShieldTV - Transcode - 50 First Dates (2004).mkv" >mediainfo.eng.xml
mkvpropedit.exe ".\Test\One\ShieldTV - Transcode - 50 First Dates (2004).mkv" --edit track:@1 --set language=und
mkvmerge.exe --identify ".\Test\One\ShieldTV - Transcode - 50 First Dates (2004).mkv" --identification-format json >mkvmerge.und.json
mediainfo.exe --Output=XML ".\Test\One\ShieldTV - Transcode - 50 First Dates (2004).mkv" >mediainfo.und.xml


## MKV Tools
mkvmerge.exe --output "\\STORAGE\Media\Troublesome\Roku - Hang - Baseline@L3 - Blaze and the Monster Machines - S01E12 - The Mystery Bandit.mkv" "\\STORAGE\Media\Troublesome\Roku - Hang - Baseline@L3 - Blaze and the Monster Machines - S01E12 - The Mystery Bandit.mp4"
mkvmerge.exe --split parts:00:00:30-00:01:00 --output "C:\Users\piete\OneDrive\Projects\PlexCleaner\Test\One\foo.split.mkv" "C:\Users\piete\OneDrive\Projects\PlexCleaner\Test\One\foo.mkv"
mkvmerge.exe --identify "\\STORAGE\Media\Troublesome\ShieldTV - Transcode - Kill Bill Volume 1 (2003).mkv" --identification-format json >orig.json
mkvmerge.exe --identify "\\STORAGE\Media\Troublesome\zlib.mkv" --identification-format json >zlib.json
mkvmerge.exe --identify "\\STORAGE\Media\Troublesome\none.mkv" --identification-format json >none.json
mkvmerge.exe --output "zlib.mkv" --compression 2:zlib --compression 3:zlib "\\STORAGE\Media\Troublesome\ShieldTV - Transcode - Kill Bill Volume 1 (2003).mkv"
mkvmerge.exe --output "none.mkv" --compression 2:none --compression 3:none "\\STORAGE\Media\Troublesome\ShieldTV - Transcode - Kill Bill Volume 1 (2003).mkv"


## MediaInfo
mediainfo.exe --Output=XML "\\STORAGE\Media\Troublesome\ShieldTV - Transcode - Kill Bill Volume 1 (2003).mkv" json >orig.xml
mediainfo.exe --Output=XML "\\STORAGE\Media\Troublesome\zlib.mkv" json >zlib.xml
mediainfo.exe --Output=XML "\\STORAGE\Media\Troublesome\none.mkv" json >none.xml


# Crashes
Application: PlexCleaner.exe
Framework Version: v4.0.30319
Description: The process was terminated due to an unhandled exception.
Exception Info: System.IO.IOException
   at System.IO.__Error.WinIOError(Int32, System.String)
   at System.Console.GetBufferInfo(Boolean, Boolean ByRef)
   at System.Console.set_ForegroundColor(System.ConsoleColor)
   at PlexCleaner.Tools.Execute(System.String, System.String)
   at PlexCleaner.Tools.Handbrake(System.String)
   at PlexCleaner.Convert.ConvertToMkv(System.String, System.String ByRef)
   at PlexCleaner.Process.ProcessFile(System.IO.FileInfo, Boolean ByRef)
   at PlexCleaner.Process.ProcessFolders(System.Collections.Generic.List`1<System.String>)
   at PlexCleaner.Program.Process()
   at PlexCleaner.Program.Run()
   at PlexCleaner.Program.Main()


# Media Types
11/12/2017 5:15:04 PM : FFProbe:
Video, FFProbe, h264, MKVMerge, MPEG-4p10/AVC/h.264, MediaInfo, AVC
Video, FFProbe, mpeg4, MKVMerge, MPEG-4p2, MediaInfo, MPEG-4 Visual
Video, FFProbe, hevc, MKVMerge, MPEG-H/HEVC/h.265, MediaInfo, HEVC
Video, FFProbe, vc1, MKVMerge, VC-1, MediaInfo, VC-1
Video, FFProbe, msmpeg4v3, MKVMerge, 0x44495633 "DIV3", MediaInfo, MPEG-4 Visual
Audio, FFProbe, ac3, MKVMerge, AC-3, MediaInfo, AC-3
Audio, FFProbe, aac, MKVMerge, AAC, MediaInfo, AAC
Audio, FFProbe, mp3, MKVMerge, MP3, MediaInfo, MPEG Audio
Audio, FFProbe, eac3, MKVMerge, E-AC-3, MediaInfo, E-AC-3
Audio, FFProbe, dts, MKVMerge, DTS, MediaInfo, DTS
Audio, FFProbe, mp2, MKVMerge, MP2, MediaInfo, MPEG Audio
Audio, FFProbe, vorbis, MKVMerge, Vorbis, MediaInfo, Vorbis
Audio, FFProbe, truehd, MKVMerge, TrueHD, MediaInfo, TrueHD
Audio, FFProbe, pcm_s16le, MKVMerge, A_MS/ACM, MediaInfo, PCM
Audio, FFProbe, pcm_s24le, MKVMerge, A_MS/ACM, MediaInfo, PCM
Audio, FFProbe, flac, MKVMerge, FLAC, MediaInfo, FLAC
Subtitle, FFProbe, dvd_subtitle, MKVMerge, VobSub, MediaInfo, VobSub
Subtitle, FFProbe, subrip, MKVMerge, SubRip/SRT, MediaInfo, UTF-8
Subtitle, FFProbe, ass, MKVMerge, SubStationAlpha, MediaInfo, ASS
Subtitle, FFProbe, hdmv_pgs_subtitle, MKVMerge, HDMV PGS, MediaInfo, PGS

11/12/2017 5:15:09 PM : MKVMerge:
Video, MKVMerge, MPEG-4p10/AVC/h.264, FFProbe, h264, MediaInfo, AVC
Video, MKVMerge, MPEG-4p2, FFProbe, mpeg4, MediaInfo, MPEG-4 Visual
Video, MKVMerge, MPEG-H/HEVC/h.265, FFProbe, hevc, MediaInfo, HEVC
Video, MKVMerge, VC-1, FFProbe, vc1, MediaInfo, VC-1
Video, MKVMerge, 0x44495633 "DIV3", FFProbe, msmpeg4v3, MediaInfo, MPEG-4 Visual
Audio, MKVMerge, AC-3, FFProbe, ac3, MediaInfo, AC-3
Audio, MKVMerge, AAC, FFProbe, aac, MediaInfo, AAC
Audio, MKVMerge, MP3, FFProbe, mp3, MediaInfo, MPEG Audio
Audio, MKVMerge, E-AC-3, FFProbe, eac3, MediaInfo, E-AC-3
Audio, MKVMerge, DTS, FFProbe, dts, MediaInfo, DTS
Audio, MKVMerge, MP2, FFProbe, mp2, MediaInfo, MPEG Audio
Audio, MKVMerge, DTS-HD High Resolution Audio, FFProbe, dts, MediaInfo, DTS
Audio, MKVMerge, Vorbis, FFProbe, vorbis, MediaInfo, Vorbis
Audio, MKVMerge, DTS-HD Master Audio, FFProbe, dts, MediaInfo, DTS
Audio, MKVMerge, DTS-ES, FFProbe, dts, MediaInfo, DTS
Audio, MKVMerge, TrueHD, FFProbe, truehd, MediaInfo, TrueHD
Audio, MKVMerge, A_MS/ACM, FFProbe, pcm_s16le, MediaInfo, PCM
Audio, MKVMerge, TrueHD Atmos, FFProbe, truehd, MediaInfo, TrueHD
Audio, MKVMerge, FLAC, FFProbe, flac, MediaInfo, FLAC
Subtitle, MKVMerge, VobSub, FFProbe, dvd_subtitle, MediaInfo, VobSub
Subtitle, MKVMerge, SubRip/SRT, FFProbe, subrip, MediaInfo, UTF-8
Subtitle, MKVMerge, SubStationAlpha, FFProbe, ass, MediaInfo, ASS
Subtitle, MKVMerge, HDMV PGS, FFProbe, hdmv_pgs_subtitle, MediaInfo, PGS

11/12/2017 5:15:11 PM : MediaInfo:
Video, MediaInfo, AVC, FFProbe, h264, MKVMerge, MPEG-4p10/AVC/h.264
Video, MediaInfo, MPEG-4 Visual, FFProbe, mpeg4, MKVMerge, MPEG-4p2
Video, MediaInfo, HEVC, FFProbe, hevc, MKVMerge, MPEG-H/HEVC/h.265
Video, MediaInfo, VC-1, FFProbe, vc1, MKVMerge, VC-1
Video, MediaInfo, xvid, FFProbe, mpeg4, MKVMerge, MPEG-4p2
Audio, MediaInfo, AC-3, FFProbe, ac3, MKVMerge, AC-3
Audio, MediaInfo, AAC, FFProbe, aac, MKVMerge, AAC
Audio, MediaInfo, MPEG Audio, FFProbe, mp3, MKVMerge, MP3
Audio, MediaInfo, E-AC-3, FFProbe, eac3, MKVMerge, E-AC-3
Audio, MediaInfo, DTS, FFProbe, dts, MKVMerge, DTS
Audio, MediaInfo, Vorbis, FFProbe, vorbis, MKVMerge, Vorbis
Audio, MediaInfo, TrueHD, FFProbe, truehd, MKVMerge, TrueHD
Audio, MediaInfo, PCM, FFProbe, pcm_s16le, MKVMerge, A_MS/ACM
Audio, MediaInfo, FLAC, FFProbe, flac, MKVMerge, FLAC
Subtitle, MediaInfo, VobSub, FFProbe, dvd_subtitle, MKVMerge, VobSub
Subtitle, MediaInfo, UTF-8, FFProbe, subrip, MKVMerge, SubRip/SRT
Subtitle, MediaInfo, ASS, FFProbe, ass, MKVMerge, SubStationAlpha
Subtitle, MediaInfo, PGS, FFProbe, hdmv_pgs_subtitle, MKVMerge, HDMV PGS
Subtitle, MediaInfo, SSA, FFProbe, ass, MKVMerge, SubStationAlpha


Use ffmpeg to re-encode, use ffprobe formats
Known in use : ac3, aac, mp3, eac3, dts, truehd, pcm_s16le, pcm_s24le, flac, mp2, vorbis, wmapro
Reencode : flac mp2, vorbis, wmapro 

Known in use : h264, mpeg4, hevc, vc1, mpeg2video, msmpeg4v3
Reencode : mpeg2video, msmpeg4v3, h264 / Constrained Baseline

 


11/12/2017 6:32:01 PM : FFProbe:
Video, FFProbe, h264, MKVMerge, MPEG-4p10/AVC/h.264, MediaInfo, AVC
Video, FFProbe, mpeg2video, MKVMerge, MPEG-1/2, MediaInfo, MPEG Video
Video, FFProbe, hevc, MKVMerge, MPEG-H/HEVC/h.265, MediaInfo, HEVC
Video, FFProbe, vc1, MKVMerge, VC-1, MediaInfo, VC-1
Video, FFProbe, msmpeg4v3, MKVMerge, 0x44495633 "DIV3", MediaInfo, MPEG-4 Visual
Audio, FFProbe, truehd, MKVMerge, TrueHD, MediaInfo, TrueHD
Audio, FFProbe, ac3, MKVMerge, AC-3, MediaInfo, AC-3
Audio, FFProbe, dts, MKVMerge, DTS-HD Master Audio, MediaInfo, DTS
Audio, FFProbe, aac, MKVMerge, AAC, MediaInfo, AAC
Audio, FFProbe, wmapro, MKVMerge, A_MS/ACM, MediaInfo, WMA
Audio, FFProbe, mp3, MKVMerge, MP3, MediaInfo, MPEG Audio
Audio, FFProbe, eac3, MKVMerge, E-AC-3, MediaInfo, E-AC-3
Subtitle, FFProbe, hdmv_pgs_subtitle, MKVMerge, HDMV PGS, MediaInfo, PGS
Subtitle, FFProbe, subrip, MKVMerge, SubRip/SRT, MediaInfo, UTF-8
Subtitle, FFProbe, ass, MKVMerge, SubStationAlpha, MediaInfo, ASS
Subtitle, FFProbe, dvd_subtitle, MKVMerge, VobSub, MediaInfo, VobSub

11/12/2017 6:32:01 PM : MKVMerge:
Video, MKVMerge, MPEG-4p10/AVC/h.264, FFProbe, h264, MediaInfo, AVC
Video, MKVMerge, MPEG-1/2, FFProbe, mpeg2video, MediaInfo, MPEG Video
Video, MKVMerge, MPEG-H/HEVC/h.265, FFProbe, hevc, MediaInfo, HEVC
Video, MKVMerge, VC-1, FFProbe, vc1, MediaInfo, VC-1
Video, MKVMerge, 0x44495633 "DIV3", FFProbe, msmpeg4v3, MediaInfo, MPEG-4 Visual
Audio, MKVMerge, TrueHD, FFProbe, truehd, MediaInfo, TrueHD
Audio, MKVMerge, AC-3, FFProbe, ac3, MediaInfo, AC-3
Audio, MKVMerge, DTS-HD Master Audio, FFProbe, dts, MediaInfo, DTS
Audio, MKVMerge, DTS, FFProbe, dts, MediaInfo, DTS
Audio, MKVMerge, DTS-ES, FFProbe, dts, MediaInfo, DTS
Audio, MKVMerge, AAC, FFProbe, aac, MediaInfo, AAC
Audio, MKVMerge, A_MS/ACM, FFProbe, wmapro, MediaInfo, WMA
Audio, MKVMerge, MP3, FFProbe, mp3, MediaInfo, MPEG Audio
Audio, MKVMerge, TrueHD Atmos, FFProbe, truehd, MediaInfo, TrueHD
Audio, MKVMerge, E-AC-3, FFProbe, eac3, MediaInfo, E-AC-3
Subtitle, MKVMerge, HDMV PGS, FFProbe, hdmv_pgs_subtitle, MediaInfo, PGS
Subtitle, MKVMerge, SubRip/SRT, FFProbe, subrip, MediaInfo, UTF-8
Subtitle, MKVMerge, SubStationAlpha, FFProbe, ass, MediaInfo, ASS
Subtitle, MKVMerge, VobSub, FFProbe, dvd_subtitle, MediaInfo, VobSub

11/12/2017 6:32:01 PM : MediaInfo:
Video, MediaInfo, AVC, FFProbe, h264, MKVMerge, MPEG-4p10/AVC/h.264
Video, MediaInfo, MPEG Video, FFProbe, mpeg2video, MKVMerge, MPEG-1/2
Video, MediaInfo, HEVC, FFProbe, hevc, MKVMerge, MPEG-H/HEVC/h.265
Video, MediaInfo, VC-1, FFProbe, vc1, MKVMerge, VC-1
Video, MediaInfo, MPEG-4 Visual, FFProbe, msmpeg4v3, MKVMerge, 0x44495633 "DIV3"
Audio, MediaInfo, TrueHD, FFProbe, truehd, MKVMerge, TrueHD
Audio, MediaInfo, AC-3, FFProbe, ac3, MKVMerge, AC-3
Audio, MediaInfo, DTS, FFProbe, dts, MKVMerge, DTS-HD Master Audio
Audio, MediaInfo, AAC, FFProbe, aac, MKVMerge, AAC
Audio, MediaInfo, WMA, FFProbe, wmapro, MKVMerge, A_MS/ACM
Audio, MediaInfo, MPEG Audio, FFProbe, mp3, MKVMerge, MP3
Audio, MediaInfo, E-AC-3, FFProbe, eac3, MKVMerge, E-AC-3
Subtitle, MediaInfo, PGS, FFProbe, hdmv_pgs_subtitle, MKVMerge, HDMV PGS
Subtitle, MediaInfo, UTF-8, FFProbe, subrip, MKVMerge, SubRip/SRT
Subtitle, MediaInfo, ASS, FFProbe, ass, MKVMerge, SubStationAlpha
Subtitle, MediaInfo, VobSub, FFProbe, dvd_subtitle, MKVMerge, VobSub


[matroska,webm @ 000001d77fb61ca0] Could not find codec parameters for stream 2 (Subtitle: hdmv_pgs_subtitle): unspecified size
Consider increasing the value for the 'analyzeduration' and 'probesize' options


Invoke-WebRequest -Uri "https://handbrake.fr/downloads2.php" -OutFile "C:\Users\piete\OneDrive\Projects\PlexCleaner\PlexCleaner\Samples\Handbrake\downloads2.html"

7za.exe x -aoa -y "C:\Users\piete\OneDrive\Projects\PlexCleaner\Tools\mkvtoolnix-64-bit-18.0.0.7z" -o"C:\Users\piete\OneDrive\Projects\PlexCleaner\Tools\MKVToolNix"


7za.exe x -aoa -spe -y "C:\Users\piete\OneDrive\Projects\PlexCleaner\Tools\ffmpeg-3.4-win64-static.zip" -o"C:\Users\piete\OneDrive\Projects\PlexCleaner\Tools\FFmpeg"

The -spe ignore root folder option does not work with zip files

I am trying to download tools from the internet, and extract them to a known folder, using a script.
mkvtoolnix: https://mkvtoolnix.download/windows/releases/18.0.0/mkvtoolnix-64-bit-18.0.0.7z
ffmpeg: http://ffmpeg.zeranoe.com/builds/win64/static/ffmpeg-3.4-win64-static.zip

The archives contain root folders, but I do not want the versioned folder, I want a fixed name output folder.
mkvtoolnix has a "mkvtoolnix" root folder in the 7z archive.
ffmpeg has a "ffmpeg-3.4-win64-static" root folder in the zip archive.

To ignore the root folder, I use the -spe option, "Eliminate duplication of root folder for extract archive command"
This works fine for the 7z archive, but fails for the zip archive.

To extract I use:
7za.exe x -aoa -spe -y "C:\Users\Foo\ffmpeg-3.4-win64-static.zip" -o"C:\Users\Foo\Tools\FFmpeg"
7za.exe x -aoa -spe -y "C:\Users\Foo\18.0.0/mkvtoolnix-64-bit-18.0.0.7z" -o"C:\Users\Foo\Tools\MkvToolNix"

The mkvtoolnix output is correctly extracted to the ".\Tools\MkvToolNix" folder.
The ffmpeg output is extracted ".\Tools\FFmpeg\ffmpeg-3.4-win64-static", instead of ".\Tools\FFmpeg.

How do I ignore the root in 7z and zip files?
Or what other command can I use to achive the same objective?
