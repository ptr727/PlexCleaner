# PlexCleaner

Utility to optimize media files for Direct Play in Plex, Emby, Jellyfin.

## License

Licensed under the [MIT License](./LICENSE)  
![GitHub License](https://img.shields.io/github/license/ptr727/PlexCleaner)

## Project

Code and Pipeline is on [GitHub](https://github.com/ptr727/PlexCleaner).  
Binary releases are published on [GitHub Releases](https://github.com/ptr727/PlexCleaner/releases).  
Docker images are published on [Docker Hub](https://hub.docker.com/r/ptr727/plexcleaner) and [GitHub Container Registry](https://github.com/ptr727/PlexCleaner/pkgs/container/plexcleaner).

## Docker Builds and Tags

- `latest`: Builds from the [main](https://github.com/ptr727/PlexCleaner/tree/main) branch.
  - `latest` uses the `savoury` build.
- `develop`: Builds from the [develop](https://github.com/ptr727/PlexCleaner/tree/develop) branch.
  - `develop` uses the `savoury-develop` build.
  - Build variants can be tagged with `-develop`, e.g. `alpine-develop`.
- `savoury`: Builds using [Microsoft .NET pre-installed on Ubuntu](https://hub.docker.com/_/microsoft-dotnet-sdk/) as base image.
  - Includes the latest [MediaInfo](https://mediaarea.net/en/MediaInfo/Download/Ubuntu).
  - includes the latest [MkvToolNix](https://mkvtoolnix.download/downloads.html#ubuntu).
  - Includes the latest FFmpeg and HandBrake installed from [Rob Savoury's](https://launchpad.net/~savoury1) private PPA repository.
  - Only `linux/amd64` platforms are supported.
- `debian-`: Builds using [Microsoft .NET pre-installed on Debian](https://hub.docker.com/_/microsoft-dotnet-sdk/) as base image.
  - Media processing tools are installed from the standard repositories.
  - Multi-architecture image supporting `linux/amd64`, `linux/arm64`, and `linux/arm/v7` builds.
- `alpine-`: Builds using [Microsoft .NET pre-installed on Alpine](https://hub.docker.com/_/microsoft-dotnet-sdk/) as base image.
  - Media processing tools are installed from the standard repositories.
  - Multi-architecture image supporting `linux/amd64`, `linux/arm64`, and `linux/arm/v7` builds.
