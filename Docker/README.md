# PlexCleaner

Utility to optimize media files for Direct Play in Plex, Emby, Jellyfin, etc.

## Documentation

Refer to the [project page][github-link] for complete usage and configuration.

- **Source Code**: [GitHub][github-link] - source code, issues, and CI/CD pipelines.
- **Binary Releases**: [GitHub Releases][releases-link] - pre-compiled executables for Windows, Linux, and macOS.
- **Docker Images**: [Docker Hub][docker-link] - container images with all tools pre-installed.

## Docker Tags

Images are rebuilt weekly to pick up upstream base-image and tool updates.

- `latest`: built from the release [main branch][main-branch-link]. Multi-architecture (`linux/amd64`, `linux/arm64`) on the `ubuntu:rolling` base.
- `develop`: built from the pre-release [develop branch][develop-branch-link].
- `X.Y.Z`: a specific released version (SemVer2 tag).

## License

Licensed under the [MIT License][license-link].\
![GitHub License][license-shield]

<!-- Shields -->
[license-shield]: https://img.shields.io/github/license/ptr727/PlexCleaner

<!-- Internal -->
[develop-branch-link]: https://github.com/ptr727/PlexCleaner/tree/develop
[docker-link]: https://hub.docker.com/r/ptr727/plexcleaner
[github-link]: https://github.com/ptr727/PlexCleaner
[license-link]: https://github.com/ptr727/PlexCleaner/blob/main/LICENSE
[main-branch-link]: https://github.com/ptr727/PlexCleaner/tree/main
[releases-link]: https://github.com/ptr727/PlexCleaner/releases
