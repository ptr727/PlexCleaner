changequote(`{{', `}}')
# PlexCleaner

Utility to optimize media files for Direct Play in Plex, Emby, Jellyfin, etc.

## License

Licensed under the [MIT License](https://github.com/ptr727/PlexCleaner/LICENSE)
![GitHub License](https://img.shields.io/github/license/ptr727/PlexCleaner)

## Project

Code and Pipeline is on [GitHub](https://github.com/ptr727/PlexCleaner).\
Binary releases are published on [GitHub Releases](https://github.com/ptr727/PlexCleaner/releases).\
Docker images are published on [Docker Hub](https://hub.docker.com/r/ptr727/plexcleaner).\
Images are updated weekly with the latest upstream updates.

## Docker Builds and Tags

- `latest`: Alias for `ubuntu`.
- `develop`: Alias for `ubuntu-develop`.
- `ubuntu`: Based on [Ubuntu Rolling](https://releases.ubuntu.com/) `ubuntu:rolling` latest stable release base image.
  - Multi-architecture image supporting `linux/amd64` and `linux/arm64` builds.
- `alpine`: Based on [Alpine Latest](https://alpinelinux.org/releases/) `alpine:latest` latest stable release base image.
  - Multi-architecture image supporting `linux/amd64` and `linux/arm64` builds.
- `debian`: Based on [Debian Stable](https://www.debian.org/releases/) `debian:stable-slim` latest stable release base image.
  - Multi-architecture image supporting `linux/amd64`, `linux/arm64`, and `linux/arm/v7` builds.
- `*-develop` : Builds from the pre-release [develop branch](https://github.com/ptr727/PlexCleaner/tree/develop).

## Platform Support

| Tag | `linux/amd64` | `linux/arm64` | `linux/arm/v7` | Size |
| --- | --- | --- | --- | --- |
| `ubuntu` | &#9745; | &#9745; | &#9744; | ~350MB |
| `alpine` | &#9745; | &#9745; | &#9744; | ~156MB |
| `debian` | &#9745; | &#9745; | &#9745; | ~330MB |

## Media Tool Versions

### `ptr727/plexcleaner:ubuntu`

```text
include({{ubuntu.ver}})
```

### `ptr727/plexcleaner:debian`

```text
include({{debian.ver}})
```

### `ptr727/plexcleaner:alpine`

```text
include({{alpine.ver}})
```
