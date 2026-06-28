# PlexCleaner

Utility to optimize media files for Direct Play in Plex, Emby, Jellyfin, etc.

## Documentation

Refer to the [project page](https://github.com/ptr727/PlexCleaner) for complete usage and configuration.

- **Source Code**: [GitHub](https://github.com/ptr727/PlexCleaner) - source code, issues, and CI/CD pipelines.
- **Binary Releases**: [GitHub Releases](https://github.com/ptr727/PlexCleaner/releases) - pre-compiled executables for Windows, Linux, and macOS.
- **Docker Images**: [Docker Hub](https://hub.docker.com/r/ptr727/plexcleaner) - container images with all tools pre-installed.

## Docker Tags

Images are rebuilt weekly to pick up upstream base-image and tool updates.

- `latest`: built from the release [main branch](https://github.com/ptr727/PlexCleaner/tree/main). Multi-architecture (`linux/amd64`, `linux/arm64`) on the `ubuntu:rolling` base.
- `develop`: built from the pre-release [develop branch](https://github.com/ptr727/PlexCleaner/tree/develop).
- `X.Y.Z`: a specific released version (SemVer2 tag).

## License

Licensed under the [MIT License](https://github.com/ptr727/PlexCleaner/blob/main/LICENSE).\
![GitHub License](https://img.shields.io/github/license/ptr727/PlexCleaner)
