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

- `latest`, `ubuntu`: Builds using [Microsoft .NET pre-installed on Ubuntu](https://hub.docker.com/_/microsoft-dotnet-sdk/) as base image.
  - Includes the latest [MediaInfo](https://mediaarea.net/en/MediaInfo/Download/Ubuntu).
  - includes the latest [MkvToolNix](https://mkvtoolnix.download/downloads.html#ubuntu).
  - Includes the latest FFmpeg and HandBrake installed from [Rob Savoury's](https://launchpad.net/~savoury1) private PPA repository.
  - Only `linux/amd64` platforms are supported.
- `alpine`: Builds using [Microsoft .NET pre-installed on Alpine](https://hub.docker.com/_/microsoft-dotnet-sdk/) as base image.
  - Media processing tools are installed from the standard repositories.
  - Multi-architecture image supporting `linux/amd64`, and `linux/arm64`.
- `debian`: Builds using [Microsoft .NET pre-installed on Debian](https://hub.docker.com/_/microsoft-dotnet-sdk/) as base image.
  - Media processing tools are installed from the standard repositories.
  - Multi-architecture image supporting `linux/amd64`, `linux/arm64`, and `linux/arm/v7` builds.
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

### `ptr727/plexcleaner:debian`

```text
include({{debian.ver}})
```

### `ptr727/plexcleaner:alpine`

*Alpine is [not](https://github.com/ptr727/PlexCleaner/issues/344) currently being built.*

### `ptr727/plexcleaner:arch`

```text
include({{arch.ver}})
```
