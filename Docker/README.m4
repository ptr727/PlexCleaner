changequote(`{{', `}}')
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

- `latest`: Same as `ubuntu`.
- `develop`: Same as `ubuntu-develop`.
- `ubuntu`: Builds using [Microsoft .NET pre-installed on Ubuntu LTS](https://hub.docker.com/_/microsoft-dotnet-sdk/) as base image.
  - Includes the latest [MediaInfo](https://mediaarea.net/en/MediaInfo/Download/Ubuntu).
  - includes the latest [MkvToolNix](https://mkvtoolnix.download/downloads.html#ubuntu).
  - Includes the latest FFmpeg and HandBrake installed from [Rob Savoury's](https://launchpad.net/~savoury1) private PPA repository.
  - Only `linux/amd64` platforms are supported.
- `ubuntu-rolling`: [Ubuntu Rolling](https://releases.ubuntu.com/) latest release build.
- `ubuntu-devel`: [Ubuntu Devel](http://archive.ubuntu.com/ubuntu/dists/devel/Release) pre-release build.
- `alpine`: Builds using [Microsoft .NET pre-installed on Alpine](https://hub.docker.com/_/microsoft-dotnet-sdk/) as base image.
  - Media processing tools are installed from the standard repositories.
  - Multi-architecture image supporting `linux/amd64`, and `linux/arm64`.
- `alpine-edge`: [Alpine Edge](https://wiki.alpinelinux.org/wiki/Repositories#Edge) pre-release build.
- `debian`: Builds using [Microsoft .NET pre-installed on Debian](https://hub.docker.com/_/microsoft-dotnet-sdk/) as base image.
  - Media processing tools are installed from the standard repositories.
  - Multi-architecture image supporting `linux/amd64`, `linux/arm64`, and `linux/arm/v7` builds.
- `debian-testing`: [Debian Testing](https://wiki.debian.org/DebianTesting) pre-release build.
- `arch`: Builds using [Arch Linux](https://hub.docker.com/_/archlinux) as base image.
  - Media processing tools are installed from the standard repositories.
  - Only `linux/amd64` platforms are supported.

## Platform Support

| Tag | `linux/amd64` | `linux/arm64` | `linux/arm/v7` | Size |
| --- | --- | --- | --- | --- |
| `ubuntu` | &#9745; | &#9744; | &#9744; | ~643MB |
| `alpine` | &#9745; | &#9745; | &#9744; | ~228MB |
| `debian` | &#9745; | &#9745; | &#9745; | ~467MB |
| `arch` | &#9745; | &#9744; | &#9744; | ~1.1GB |

## Media Tool Versions

### `ptr727/plexcleaner:ubuntu` (`latest`)

```text
include({{ubuntu.ver}})
```

### `ptr727/plexcleaner:ubuntu-rolling`

```text
include({{ubuntu-rolling.ver}})
```

### `ptr727/plexcleaner:ubuntu-devel`

```text
include({{ubuntu-devel.ver}})
```

### `ptr727/plexcleaner:debian`

```text
include({{debian.ver}})
```

### `ptr727/plexcleaner:debian-testing`

```text
include({{debian-testing.ver}})
```

### `ptr727/plexcleaner:alpine`

*Alpine is [not](https://github.com/ptr727/PlexCleaner/issues/344) currently being built.*

### `ptr727/plexcleaner:alpine-edge`

```text
include({{alpine-edge.ver}})
```

### `ptr727/plexcleaner:arch`

```text
include({{arch.ver}})
```
