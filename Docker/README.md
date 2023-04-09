# PlexCleaner

Utility to optimize media files for Direct Play in Plex, Emby, Jellyfin.

## License

Licensed under the [MIT License](./LICENSE)  
![GitHub License](https://img.shields.io/github/license/ptr727/PlexCleaner)

## Project

PlexCleaner project is on [GitHub](https://github.com/ptr727/PlexCleaner/)

## Docker Builds and Tags

- `[x-]latest`: `main` release branch build.
- `[x-]develop`: `develop` testing or beta branch build.
- `arch-`: Build using [Arch Linux](./Arch.Dockerfile). (default for `latest` and `develop` builds)
- `debian-`: Build using [Microsoft .NET Debian](./Mcr.Debian.Dockerfile).
- `savoury-`: Build using [Ubuntu](./Ubuntu.Savoury.Dockerfile) and includes [Rob Savoury's](https://launchpad.net/~savoury1) latest FFmpeg and HandBrake PPA builds.
- `multi-`: Multi-architecture build using [Microsoft .NET Debian](./Mcr.Debian.Dockerfile) and includes `linux/amd64`, `linux/arm64`, and `linux/arm/v7` builds.
