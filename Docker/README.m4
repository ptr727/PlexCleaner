changequote(`{{', `}}')
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

## Build Version Information

### `ptr727/plexcleaner:savoury`

```
include({{savoury.ver}})
```

### `ptr727/plexcleaner:debian`

```
include({{debian.ver}})
```

### `ptr727/plexcleaner:alpine`

```
include({{alpine.ver}})
```

### `ptr727/plexcleaner:arch`

```
include({{arch.ver}})
```
