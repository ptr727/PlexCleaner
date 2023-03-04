# Test in docker shell:
# docker run -it --rm --pull always --name Testing mcr.microsoft.com/dotnet/sdk:7.0-jammy /bin/bash
# export DEBIAN_FRONTEND=noninteractive


# Test in develop shell:
# docker run -it --rm --pull always --name Testing --volume /data/media:/media:rw ptr727/plexcleaner:develop /bin/bash


# Test Docker build:
# dotnet publish ./PlexCleaner/PlexCleaner.csproj --runtime linux-x64 --self-contained false --output ./Docker/PlexCleaner
# docker build --progress=plain ./Docker

# Ignore docker layer cache:
# docker build --no-cache --progress=plain ./Docker

# Build from .NET Ubuntu Jammy base image mcr.microsoft.com/dotnet/sdk:7.0-jammy
# https://mcr.microsoft.com/en-us/product/dotnet/sdk/tags
FROM mcr.microsoft.com/dotnet/sdk:7.0-jammy

# Set the version at build time
ARG LABEL_VERSION="1.0.0.0"

LABEL name="PlexCleaner" \
    version=${LABEL_VERSION} \
    description="Utility to optimize media files for Direct Play in Plex, Emby, Jellyfin" \
    maintainer="Pieter Viljoen <ptr727@users.noreply.github.com>"

# Default timezone is UTC
ENV TZ=Etc/UTC

# Prevent EULA and confirmation prompts in installers
ARG DEBIAN_FRONTEND=noninteractive

# Install prerequisites
RUN apt-get update \
    && apt-get upgrade -y \
    && apt-get install -y \
        apt-transport-https \
        apt-utils \
        locales \
        locales-all \
        lsb-core \
        p7zip-full \
        software-properties-common \
        wget \
    && locale-gen --no-purge en_US en_US.UTF-8

# Set locale to UTF-8 after running locale-gen
ENV LANG=en_US.UTF-8 \
    LANGUAGE=en_US:en \
    LC_ALL=en_US.UTF-8

# Verify .NET version
RUN dotnet --info

# Install VS debug tools
# https://github.com/OmniSharp/omnisharp-vscode/wiki/Attaching-to-remote-processes
RUN wget https://aka.ms/getvsdbgsh \
    && sh getvsdbgsh -v latest -l /vsdbg \
    && rm getvsdbgsh

# Install MediaInfo
# https://mediaarea.net/en/MediaInfo/Download/Ubuntu
# https://mediaarea.net/en/Repos
RUN wget https://mediaarea.net/repo/deb/repo-mediaarea_1.0-21_all.deb \
    && dpkg -i repo-mediaarea_1.0-21_all.deb \
    && apt-get update \
    && apt-get install -y mediainfo \
    && rm repo-mediaarea_1.0-21_all.deb \
    && mediainfo --version

# Install MKVToolNix
# https://mkvtoolnix.download/downloads.html#ubuntu
RUN wget -O /usr/share/keyrings/gpg-pub-moritzbunkus.gpg https://mkvtoolnix.download/gpg-pub-moritzbunkus.gpg \
    && sh -c 'echo "deb [arch=amd64 signed-by=/usr/share/keyrings/gpg-pub-moritzbunkus.gpg] https://mkvtoolnix.download/ubuntu/ $(lsb_release -sc) main" >> /etc/apt/sources.list.d/mkvtoolnix.list' \
    && apt-get update \
    && apt-get install -y mkvtoolnix \
    && mkvmerge --version

# Install FfMpeg
# https://launchpad.net/~savoury1/+archive/ubuntu/ffmpeg6
RUN add-apt-repository -y ppa:savoury1/ffmpeg4 \
    && add-apt-repository -y ppa:savoury1/ffmpeg6 \
    && apt-get update \
    && apt-get install -y ffmpeg \
    && ffmpeg -version

# Install HandBrake
# Depends on FfMpeg, install FfMpeg first
# https://launchpad.net/~savoury1/+archive/ubuntu/handbrake
RUN add-apt-repository -y ppa:savoury1/handbrake \
    && apt-get update \
    && apt-get install -y handbrake-cli \
    && HandBrakeCLI --version

# Cleanup
RUN apt-get autoremove -y \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Copy PlexCleaner
# Build externally
# dotnet publish ./PlexCleaner/PlexCleaner.csproj --runtime linux-x64 --self-contained false --output ./Docker/PlexCleaner
COPY PlexCleaner /PlexCleaner
RUN /PlexCleaner/PlexCleaner --version
