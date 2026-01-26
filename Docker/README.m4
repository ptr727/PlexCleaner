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

## Usage

Refer to the [project](https://github.com/ptr727/PlexCleaner) page.

## Docker Tags

- `latest`:
  - Based on [Ubuntu Rolling](https://releases.ubuntu.com/) `ubuntu:rolling` latest stable release base image.
  - Multi-architecture image supporting `linux/amd64` and `linux/arm64` builds.
  - Builds from the release [main branch](https://github.com/ptr727/PlexCleaner/tree/main).
- `develop`:
  - Builds from the pre-release [develop branch](https://github.com/ptr727/PlexCleaner/tree/develop).

## Image Information

```text
include({{latest.ver}})
```
