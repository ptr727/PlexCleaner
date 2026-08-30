# Custom FFmpeg and HandBrake CLI Parameters

Custom encoding settings for FFmpeg and Handbrake.

## Understanding Custom Settings

The `ConvertOptions:FfMpegOptions` and `ConvertOptions:HandBrakeOptions` settings allow custom CLI parameters for media processing. This is useful for:

- Hardware-accelerated encoding (GPU encoding via NVENC, QuickSync, etc.).
- Custom quality/speed tradeoffs (CRF values, presets).
- Alternative codecs (AV1, VP9, etc.).

> **ℹ️ Note**: Hardware encoding options are operating system, hardware, and tool version specific.\
Refer to the Jellyfin hardware acceleration [docs](https://jellyfin.org/docs/general/administration/hardware-acceleration/) for hints on usage.
The example configurations are from documentation and minimal testing with Intel QuickSync on Windows only, please discuss and post working configurations in [GitHub Discussions](https://github.com/ptr727/PlexCleaner/discussions).

## FFmpeg Options

See the [FFmpeg documentation](https://ffmpeg.org/ffmpeg.html) for complete commandline option details.

The typical FFmpeg commandline is:

```text
ffmpeg [global_options] {[input_file_options] -i input_url} ... {[output_file_options] output_url}
```

E.g.:

```shell
ffmpeg -analyzeduration 2147483647 -probesize 2147483647 -i /media/foo.mkv -max_muxing_queue_size 1024 -abort_on empty_output -hide_banner -nostats -map 0 -c:v libx265 -crf 26 -preset medium -c:a ac3 -c:s copy -f matroska /media/bar.mkv
```

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

## HandBrake Options

See the [HandBrake documentation](https://handbrake.fr/docs/en/latest/cli/command-line-reference.html) for complete commandline option details.

The typical HandBrake commandline is:

```text
HandBrakeCLI [options] -i <source> -o <destination>
```

E.g.

```shell
HandBrakeCLI --input /media/foo.mkv --output /media/bar.mkv --format av_mkv --encoder x265 --quality 26 --encoder-preset medium --comb-detect --decomb --all-audio --aencoder copy --audio-fallback ac3
```

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

> **ℹ️ Note**: HandBrake is primarily used for video deinterlacing, and only as backup encoder when FFmpeg fails.\
The default `HandBrakeOptions:Audio` configuration is set to `copy --audio-fallback ac3` that will copy all supported audio tracks as is, and only encode to `ac3` if the audio codec is not natively supported.
