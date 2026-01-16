# EIA-608 and CTA-708 Closed Captions

[EIA-608](https://en.wikipedia.org/wiki/EIA-608) and [CTA-708](https://en.wikipedia.org/wiki/CTA-708) subtitles, commonly referred to as Closed Captions (CC), are typically used for broadcast television.

> **ℹ️ TL;DR**: Closed captions (CC) are subtitles embedded in the video stream (not separate tracks). They can cause issues with some players that always display them or cannot disable them. PlexCleaner detects and removes them using the `RemoveClosedCaptions` option with the FFprobe `subcc` filter (most reliable method). Detection requires scanning the entire file (~10-30% of playback time, faster with `--quickscan`). Removal uses FFmpeg's `filter_units` filter without re-encoding.

## Understanding Closed Captions

Media containers typically contain separate discrete subtitle tracks, but closed captions can be encoded into the primary video stream.

Removal of closed captions may be desirable for various reasons, including undesirable content, or players that always burn in closed captions during playback.

Unlike normal subtitle tracks, detection and removal of closed captions are non-trivial.

## Technical Details

> **ℹ️ Note**: I have no expertise in video engineering; the following information was gathered by research and experimentation.

The currently implemented method of closed caption detection uses [FFprobe and the `subcc` filter](#ffprobe-subcc) to detect closed caption frames in the video stream.

> **ℹ️ Note**: The `subcc` filter does not support partial file analysis. When the `quickscan` option is enabled, a small file snippet is first created and used for analysis, reducing processing times.

The [FFmpeg `filter_units` filter](#ffmpeg-filter_units) is used for closed caption removal.

## Closed Caption Detection

### FFprobe

FFprobe used to identify closed caption presence in normal console output, but [does not support](https://github.com/ptr727/PlexCleaner/issues/94) closed caption reporting when using `-print_format json`, and recently [removed reporting](https://github.com/ptr727/PlexCleaner/issues/497) of closed caption presence completely, prompting research into alternatives.

E.g. `ffprobe filename`

```text
Stream #0:0(eng): Video: h264 (High), yuv420p(tv, bt709, progressive), 1920x1080, Closed Captions, SAR 1:1 DAR 16:9, 29.97 fps, 29.97 tbr, 1k tbn (default)
```

### MediaInfo

MediaInfo supports closed caption detection, but only for [some container types](https://github.com/MediaArea/MediaInfoLib/issues/2264) (e.g. TS and DV), and [only scans](https://github.com/MediaArea/MediaInfoLib/issues/1881) the first 30s of the video looking for video frames containing closed captions.

E.g. `mediainfo --Output=JSON filename`

MediaInfo does [not support](https://github.com/MediaArea/MediaInfoLib/issues/1881#issuecomment-2816754336) general input piping (e.g. MKV -> FFmpeg -> TS -> MediaInfo), and requires a temporary TS file to be created on disk and used as standard input.

In my testing I found that remuxing 30s of video from MKV to TS did produce reliable results.

E.g.

```json
{
    "@type": "Text",
    "ID": "256-1",
    "Format": "EIA-708",
    "MuxingMode": "A/53 / DTVCC Transport",
},
```

### CCExtractor

[CCExtractor](https://ccextractor.org/) supports closed caption detection using `-out=report`.

E.g. `ccextractor -12 -out=report filename`

In my testing I found using MKV containers directly as input produced unreliable results, either no output generated or false negatives.

CCExtractor does support input piping, but I found it to be unreliable with broken pipes, and requires a temporary TS file to be created on disk and used as standard input.

Even in TS format on disk, it is very sensitive to stream anomalies, e.g. `Error: Broken AVC stream - forbidden_zero_bit not zero ...`, making it unreliable.

E.g.

```text
EIA-608: Yes
CEA-708: Yes
```

## FFprobe `readeia608`

FFmpeg [`readeia608` filter](https://ffmpeg.org/ffmpeg-filters.html#readeia608) can be used in FFprobe to report EIA-608 frame information.

E.g.

```shell
ffprobe -loglevel error -f lavfi -i "movie=filename,readeia608" -show_entries frame=best_effort_timestamp_time,duration_time:frame_tags=lavfi.readeia608.0.line,lavfi.readeia608.0.cc,lavfi.readeia608.1.line,lavfi.readeia608.1.cc -print_format json
```

The `movie=filename[out0+subcc]` convention requires [special escaping](https://superuser.com/questions/1893137/how-to-quote-a-file-name-containing-single-quotes-in-ffmpeg-ffprobe-movie-filena) of the filename to not interfere with commandline or filter graph parsing.

In my testing I found only one [IMX sample](https://archive.org/details/vitc_eia608_sample) that produced the expected results, making it unreliable.

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

### FFprobe `subcc`

FFmpeg [`subcc` filter](https://www.ffmpeg.org/ffmpeg-devices.html#Options-10) can be used in FFprobe to create subtitle streams from the closed captions embedded in video streams.

E.g.

```shell
ffprobe -loglevel error -select_streams s:0 -f lavfi -i "movie=filename[out0+subcc]" -show_packets -print_format json
```

E.g.

```shell
ffmpeg -abort_on empty_output -y -f lavfi -i "movie=filename[out0+subcc]" -map 0:s -c:s srt outfilename
```

The `ffmpeg -t` and `ffprobe -read_intervals` options limiting scan time does [not work](https://superuser.com/questions/1893673/how-to-time-limit-the-input-stream-duration-when-using-movie-filenameout0subcc) on the input stream when using the `subcc` filter, and scanning the entire file can take a very long time.

In my testing I found the results to be reliable across a wide variety of files.

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

### FFprobe `analyze_frames`

FFprobe [recently added](https://github.com/FFmpeg/FFmpeg/commit/90af8e07b02e690a9fe60aab02a8bccd2cbf3f01) the `analyze_frames` [option](https://ffmpeg.org/ffprobe.html#toc-Main-options) that reports on the presence of closed captions in video streams.

As of writing this functionality has not yet been released, but is only in nightly builds.

E.g.

```shell
ffprobe -loglevel error -show_streams -analyze_frames -read_intervals %180 filename -print_format json
```

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

The FFprobe `analyze_frames` method of detection will be implemented when broadly supported.

## Closed Caption Removal

### FFmpeg `filter_units`

FFmpeg [`filter_units` filter](https://ffmpeg.org/ffmpeg-bitstream-filters.html#filter_005funits) can be used to [remove closed captions](https://stackoverflow.com/questions/48177694/removing-eia-608-closed-captions-from-h-264-without-reencode) from video streams.

E.g.

```shell
ffmpeg -loglevel error -i [in-filename] -c copy -map 0 -bsf:v filter_units=remove_types=6 [out-filename]
```

Closed captions SEI unit for H264 is `6`, `39` for H265, and `178` for MPEG2.

> **ℹ️ Note**: [Wiki](https://trac.ffmpeg.org/wiki/HowToExtractAndRemoveClosedCaptions) and [issue](https://trac.ffmpeg.org/ticket/5283); as of writing HDR10+ metadata may be lost when removing closed captions from H265 content.
