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

- `latest`: Alias for `ubuntu`.
- `develop`: Alias for `ubuntu-develop`.
- `savoury`: Based on [Ubuntu Jammy 22.04 LTS](https://releases.ubuntu.com/) `ubuntu:jammy` base image.
  - Installs the latest [MediaInfo](https://mediaarea.net/en/MediaInfo/Download/Ubuntu) from the MediaInfo repository.
  - Installs the latest [MkvToolNix](https://mkvtoolnix.download/downloads.html#ubuntu) from the MkvToolNix repository.
  - Installs the latest FFmpeg and HandBrake installed from [Rob Savoury's](https://launchpad.net/~savoury1) private PPA repository.
  - Only `linux/amd64` platforms are supported.
- `ubuntu`: Based on [Ubuntu Rolling](https://releases.ubuntu.com/) `ubuntu:rolling` latest stable release base image.
  - Installs media tools from Ubuntu repository.
  - Multi-architecture image supporting `linux/amd64`, `linux/arm64`, and `linux/arm/v7` builds.
- `ubuntu-devel`: [Ubuntu Devel](http://archive.ubuntu.com/ubuntu/dists/devel/Release) `ubuntu:devel` pre-release base image.
  - Installs media tools from Ubuntu repository.
  - Multi-architecture image supporting `linux/amd64`, `linux/arm64`, and `linux/arm/v7` builds.
- `alpine`: Based on [Alpine Latest](https://alpinelinux.org/releases/) `alpine:latest` latest stable release base image.
  - Installs media tools from the Alpine repository.
  - Multi-architecture image supporting `linux/amd64`, and `linux/arm64`.
  - Handbrake on Alpine does not support `linux/arm/v7` builds.
- `alpine-edge`: [Alpine Edge](https://alpinelinux.org/releases/) `alpine-edge` pre-release base image.
  - Installs media tools from the Alpine repository.
  - Multi-architecture image supporting `linux/amd64`, and `linux/arm64`.
  - Handbrake on Alpine does not support `linux/arm/v7` builds.
- `debian`: Based on [Debian Stable](https://www.debian.org/releases/) `debian:stable-slim` latest stable release base image.
  - Installs media tools from Debian repository.
  - Multi-architecture image supporting `linux/amd64`, `linux/arm64`, and `linux/arm/v7` builds.
- `debian-testing`: [Debian Testing](https://www.debian.org/releases/) `debian:testing-slim` pre-release base image.
  - Installs media tools from Debian repository.
  - Multi-architecture image supporting `linux/amd64`, `linux/arm64`, and `linux/arm/v7` builds.
- `*-develop` : Builds from the pre-release [develop branch](https://github.com/ptr727/PlexCleaner/tree/develop).
  - E.g. `ubuntu-develop`, `debian-testing-develop`, etc.

## Platform Support

| Tag | `linux/amd64` | `linux/arm64` | `linux/arm/v7` | Size |
| --- | --- | --- | --- | --- |
| `ubuntu` | &#9745; | &#9745; | &#9745; | ~402MB |
| `alpine` | &#9745; | &#9745; | &#9744; | ~228MB |
| `debian` | &#9745; | &#9745; | &#9745; | ~398MB |
| `savoury` | &#9745; | &#9744; | &#9744; | ~462MB |

## Media Tool Versions

### `ptr727/plexcleaner:savoury`

```text
include({{savoury.ver}})
```

### `ptr727/plexcleaner:ubuntu`

```text
include({{ubuntu.ver}})
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

```text
include({{alpine.ver}})
```

### `ptr727/plexcleaner:alpine-edge`

```text
include({{alpine-edge.ver}})
```
