
# PlexCleaner

Utility to optimize media files for Direct Play in Plex, Emby, Jellyfin.

## License

Licensed under the [MIT License](./LICENSE)  
![GitHub License](https://img.shields.io/github/license/ptr727/PlexCleaner)

## Project

Code and Pipeline is on [GitHub](https://github.com/ptr727/PlexCleaner).  
Binary releases are published on [GitHub Releases](https://github.com/ptr727/PlexCleaner/releases).  
Docker images are published on [Docker Hub](https://hub.docker.com/r/ptr727/plexcleaner).  
Images are updated weekly with the latest upstream updates.

## Docker Builds and Tags

- `latest`: Builds from the [main](https://github.com/ptr727/PlexCleaner/tree/main) branch.
  - `latest` uses the `savoury` Ubuntu x64 build.
- `develop`: Builds from the [develop](https://github.com/ptr727/PlexCleaner/tree/develop) branch.
  - `develop` uses the `savoury-develop` Ubuntu x64 build.
  - Build variants can be tagged with `-develop`, e.g. `alpine-develop`.
- `savoury`: Builds using [Microsoft .NET pre-installed on Ubuntu](https://hub.docker.com/_/microsoft-dotnet-sdk/) as base image.
  - Includes the latest [MediaInfo](https://mediaarea.net/en/MediaInfo/Download/Ubuntu).
  - includes the latest [MkvToolNix](https://mkvtoolnix.download/downloads.html#ubuntu).
  - Includes the latest FFmpeg and HandBrake installed from [Rob Savoury's](https://launchpad.net/~savoury1) private PPA repository.
  - Only `linux/amd64` platforms are supported.
- `alpine`: Builds using [Microsoft .NET pre-installed on Alpine](https://hub.docker.com/_/microsoft-dotnet-sdk/) as base image.
  - Media processing tools are installed from the standard repositories.
  - Multi-architecture image supporting `linux/amd64`, and `linux/arm64`.
  - There is currently no `linux/arm/v7` package for [HandBrake](https://pkgs.alpinelinux.org/packages?name=handbrake&branch=edge&repo=&arch=&maintainer=).
- `debian`: Builds using [Microsoft .NET pre-installed on Debian](https://hub.docker.com/_/microsoft-dotnet-sdk/) as base image.
  - Media processing tools are installed from the standard repositories.
  - Multi-architecture image supporting `linux/amd64`, `linux/arm64`, and `linux/arm/v7` builds.
- `arch`: Builds using [Arch Linux](https://hub.docker.com/_/archlinux) as base image.
  - Media processing tools are installed from the standard repositories.
  - Only `linux/amd64` platforms are supported.

## Platform Support

| Tag | `linux/amd64` | `linux/arm64` | `linux/arm/v7` | Size |
| --- | --- | --- | --- | --- |
| `latest` | &#9745; | &#9744; | &#9744; | ~797MB |
| `savoury` | &#9745; | &#9744; | &#9744; | ~797MB |
| `alpine` | &#9745; | &#9745; | &#9744; | ~371MB |
| `debian` | &#9745; | &#9745; | &#9745; | ~780MB |
| `arch` | &#9745; | &#9744; | &#9744; | ~1.1GB |

## Media Tool Versions

### `ptr727/plexcleaner:latest`

```text
PlexCleaner: PlexCleaner : 3.2.24+bdeb1446ce (Release)
dotNET: 7.0.305
HandBrakeCLI: HandBrake 20230223192356-5c2b5d2d0-1.6.x
MediaInfo: MediaInfo Command line, MediaInfoLib - v23.06
MkvMerge: mkvmerge v78.0 ('Running') 64-bit
FfMpeg: ffmpeg version 6.0-0ubuntu1~22.04.sav1.1 Copyright (c) 2000-2023 the FFmpeg developers built with gcc 11 (Ubuntu 11.3.0-1ubuntu1~22.04) configuration: --prefix=/usr --extra-version='0ubuntu1~22.04.sav1.1' --toolchain=hardened --libdir=/usr/lib/x86_64-linux-gnu --incdir=/usr/include/x86_64-linux-gnu --arch=amd64 --enable-gpl --disable-stripping --enable-amf --enable-gnutls --enable-ladspa --enable-lcms2 --enable-libaom --enable-libass --enable-libbluray --enable-libbs2b --enable-libcaca --enable-libcdio --enable-libcodec2 --enable-libdav1d --enable-libflite --enable-libfontconfig --enable-libfreetype --enable-libfribidi --enable-libgme --enable-libgsm --enable-libjack --enable-libmp3lame --enable-libmysofa --enable-libopenjpeg --enable-libopenmpt --enable-libopus --enable-libpulse --enable-librabbitmq --enable-librubberband --enable-libshine --enable-libsnappy --enable-libsoxr --enable-libspeex --enable-libsrt --enable-libssh --enable-libtheora --enable-libtwolame --enable-libvidstab --enable-libvorbis --enable-libvpx --enable-libwebp --enable-libx265 --enable-libxml2 --enable-libxvid --enable-libzimg --enable-libzmq --enable-libzvbi --enable-lv2 --enable-omx --enable-openal --enable-opencl --enable-opengl --enable-sdl2 --enable-sndio --enable-pocketsphinx --enable-librsvg --enable-libilbc --enable-libjxl --enable-librist --enable-vapoursynth --enable-libvmaf --enable-crystalhd --enable-libmfx --enable-libsvtav1 --enable-libdc1394 --enable-libdrm --enable-libiec61883 --enable-chromaprint --enable-frei0r --enable-libx264 --enable-libplacebo --enable-librav1e --enable-shared libavutil 58. 2.100 / 58. 2.100 libavcodec 60. 3.100 / 60. 3.100 libavformat 60. 3.100 / 60. 3.100 libavdevice 60. 1.100 / 60. 1.100 libavfilter 9. 3.100 / 9. 3.100 libswscale 7. 1.100 / 7. 1.100 libswresample 4. 10.100 / 4. 10.100 libpostproc 57. 1.100 / 57. 1.100

```

### `ptr727/plexcleaner:savoury`

```text
PlexCleaner: PlexCleaner : 3.2.24+bdeb1446ce (Release)
dotNET: 7.0.305
HandBrakeCLI: HandBrake 20230223192356-5c2b5d2d0-1.6.x
MediaInfo: MediaInfo Command line, MediaInfoLib - v23.06
MkvMerge: mkvmerge v78.0 ('Running') 64-bit
FfMpeg: ffmpeg version 6.0-0ubuntu1~22.04.sav1.1 Copyright (c) 2000-2023 the FFmpeg developers built with gcc 11 (Ubuntu 11.3.0-1ubuntu1~22.04) configuration: --prefix=/usr --extra-version='0ubuntu1~22.04.sav1.1' --toolchain=hardened --libdir=/usr/lib/x86_64-linux-gnu --incdir=/usr/include/x86_64-linux-gnu --arch=amd64 --enable-gpl --disable-stripping --enable-amf --enable-gnutls --enable-ladspa --enable-lcms2 --enable-libaom --enable-libass --enable-libbluray --enable-libbs2b --enable-libcaca --enable-libcdio --enable-libcodec2 --enable-libdav1d --enable-libflite --enable-libfontconfig --enable-libfreetype --enable-libfribidi --enable-libgme --enable-libgsm --enable-libjack --enable-libmp3lame --enable-libmysofa --enable-libopenjpeg --enable-libopenmpt --enable-libopus --enable-libpulse --enable-librabbitmq --enable-librubberband --enable-libshine --enable-libsnappy --enable-libsoxr --enable-libspeex --enable-libsrt --enable-libssh --enable-libtheora --enable-libtwolame --enable-libvidstab --enable-libvorbis --enable-libvpx --enable-libwebp --enable-libx265 --enable-libxml2 --enable-libxvid --enable-libzimg --enable-libzmq --enable-libzvbi --enable-lv2 --enable-omx --enable-openal --enable-opencl --enable-opengl --enable-sdl2 --enable-sndio --enable-pocketsphinx --enable-librsvg --enable-libilbc --enable-libjxl --enable-librist --enable-vapoursynth --enable-libvmaf --enable-crystalhd --enable-libmfx --enable-libsvtav1 --enable-libdc1394 --enable-libdrm --enable-libiec61883 --enable-chromaprint --enable-frei0r --enable-libx264 --enable-libplacebo --enable-librav1e --enable-shared libavutil 58. 2.100 / 58. 2.100 libavcodec 60. 3.100 / 60. 3.100 libavformat 60. 3.100 / 60. 3.100 libavdevice 60. 1.100 / 60. 1.100 libavfilter 9. 3.100 / 9. 3.100 libswscale 7. 1.100 / 7. 1.100 libswresample 4. 10.100 / 4. 10.100 libpostproc 57. 1.100 / 57. 1.100

```

### `ptr727/plexcleaner:debian`

```text
PlexCleaner: PlexCleaner : 3.2.24+bdeb1446ce (Release)
dotNET: 7.0.305
HandBrakeCLI: HandBrake 1.6.1
MediaInfo: MediaInfo Command line, MediaInfoLib - v23.04
MkvMerge: mkvmerge v77.0 ('Elemental') 64-bit
FfMpeg: ffmpeg version 5.1.3-1 Copyright (c) 2000-2022 the FFmpeg developers built with gcc 12 (Debian 12.2.0-14) configuration: --prefix=/usr --extra-version=1 --toolchain=hardened --libdir=/usr/lib/x86_64-linux-gnu --incdir=/usr/include/x86_64-linux-gnu --arch=amd64 --enable-gpl --disable-stripping --enable-gnutls --enable-ladspa --enable-libaom --enable-libass --enable-libbluray --enable-libbs2b --enable-libcaca --enable-libcdio --enable-libcodec2 --enable-libdav1d --enable-libflite --enable-libfontconfig --enable-libfreetype --enable-libfribidi --enable-libglslang --enable-libgme --enable-libgsm --enable-libjack --enable-libmp3lame --enable-libmysofa --enable-libopenjpeg --enable-libopenmpt --enable-libopus --enable-libpulse --enable-librabbitmq --enable-librist --enable-librubberband --enable-libshine --enable-libsnappy --enable-libsoxr --enable-libspeex --enable-libsrt --enable-libssh --enable-libsvtav1 --enable-libtheora --enable-libtwolame --enable-libvidstab --enable-libvorbis --enable-libvpx --enable-libwebp --enable-libx265 --enable-libxml2 --enable-libxvid --enable-libzimg --enable-libzmq --enable-libzvbi --enable-lv2 --enable-omx --enable-openal --enable-opencl --enable-opengl --enable-sdl2 --disable-sndio --enable-libjxl --enable-pocketsphinx --enable-librsvg --enable-libmfx --enable-libdc1394 --enable-libdrm --enable-libiec61883 --enable-chromaprint --enable-frei0r --enable-libx264 --enable-libplacebo --enable-librav1e --enable-shared libavutil 57. 28.100 / 57. 28.100 libavcodec 59. 37.100 / 59. 37.100 libavformat 59. 27.100 / 59. 27.100 libavdevice 59. 7.100 / 59. 7.100 libavfilter 8. 44.100 / 8. 44.100 libswscale 6. 7.100 / 6. 7.100 libswresample 4. 7.100 / 4. 7.100 libpostproc 56. 6.100 / 56. 6.100

```

### `ptr727/plexcleaner:alpine`

```text
PlexCleaner: PlexCleaner : 3.2.24+bdeb1446ce (Release)
dotNET: 7.0.305
HandBrakeCLI: HandBrake 1.6.1
MediaInfo:
MkvMerge: mkvmerge v77.0 ('Elemental') 64-bit
FfMpeg: ffmpeg version 6.0 Copyright (c) 2000-2023 the FFmpeg developers built with gcc 13.1.1 (Alpine 13.1.1_git20230603) 20230603 configuration: --prefix=/usr --disable-librtmp --disable-lzma --disable-static --disable-stripping --enable-avfilter --enable-gnutls --enable-gpl --enable-libaom --enable-libass --enable-libbluray --enable-libdav1d --enable-libdrm --enable-libfontconfig --enable-libfreetype --enable-libfribidi --enable-libmp3lame --enable-libopenmpt --enable-libopus --enable-libplacebo --enable-libpulse --enable-librist --enable-libsoxr --enable-libsrt --enable-libssh --enable-libtheora --enable-libv4l2 --enable-libvidstab --enable-libvorbis --enable-libvpx --enable-libwebp --enable-libx264 --enable-libx265 --enable-libxcb --enable-libxml2 --enable-libxvid --enable-libzimg --enable-libzmq --enable-lto --enable-pic --enable-postproc --enable-pthreads --enable-shared --enable-vaapi --enable-vdpau --enable-vulkan --optflags=-O3 --enable-libjxl --enable-libsvtav1 --enable-libvpl libavutil 58. 2.100 / 58. 2.100 libavcodec 60. 3.100 / 60. 3.100 libavformat 60. 3.100 / 60. 3.100 libavdevice 60. 1.100 / 60. 1.100 libavfilter 9. 3.100 / 9. 3.100 libswscale 7. 1.100 / 7. 1.100 libswresample 5. 0.100 / 5. 0.100 libpostproc 57. 1.100 / 57. 1.100

```

### `ptr727/plexcleaner:arch`

```text
PlexCleaner: PlexCleaner : 3.2.24+bdeb1446ce (Release)
dotNET: 7.0.107
HandBrakeCLI: HandBrake 1.6.1
MediaInfo: MediaInfo Command line, MediaInfoLib - v23.06
MkvMerge: mkvmerge v77.0 ('Elemental') 64-bit
FfMpeg: ffmpeg version n6.0 Copyright (c) 2000-2023 the FFmpeg developers built with gcc 13.1.1 (GCC) 20230429 configuration: --prefix=/usr --disable-debug --disable-static --disable-stripping --enable-amf --enable-avisynth --enable-cuda-llvm --enable-lto --enable-fontconfig --enable-gmp --enable-gnutls --enable-gpl --enable-ladspa --enable-libaom --enable-libass --enable-libbluray --enable-libbs2b --enable-libdav1d --enable-libdrm --enable-libfreetype --enable-libfribidi --enable-libgsm --enable-libiec61883 --enable-libjack --enable-libjxl --enable-libmfx --enable-libmodplug --enable-libmp3lame --enable-libopencore_amrnb --enable-libopencore_amrwb --enable-libopenjpeg --enable-libopenmpt --enable-libopus --enable-libpulse --enable-librav1e --enable-librsvg --enable-libsoxr --enable-libspeex --enable-libsrt --enable-libssh --enable-libsvtav1 --enable-libtheora --enable-libv4l2 --enable-libvidstab --enable-libvmaf --enable-libvorbis --enable-libvpx --enable-libwebp --enable-libx264 --enable-libx265 --enable-libxcb --enable-libxml2 --enable-libxvid --enable-libzimg --enable-nvdec --enable-nvenc --enable-opencl --enable-opengl --enable-shared --enable-version3 --enable-vulkan libavutil 58. 2.100 / 58. 2.100 libavcodec 60. 3.100 / 60. 3.100 libavformat 60. 3.100 / 60. 3.100 libavdevice 60. 1.100 / 60. 1.100 libavfilter 9. 3.100 / 9. 3.100 libswscale 7. 1.100 / 7. 1.100 libswresample 4. 10.100 / 4. 10.100 libpostproc 57. 1.100 / 57. 1.100

```
