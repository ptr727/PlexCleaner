# Test in docker shell:
# docker run -it --rm ubuntu:focal /bin/bash
# export DEBIAN_FRONTEND=noninteractive

# Test Docker build:
# dotnet publish ./PlexCleaner/PlexCleaner.csproj --runtime linux-x64 --self-contained false --output ./Docker/PlexCleaner
# docker build ./Docker
# Ignore docker layer cache
# docker build --no-cache ./Docker

# Build from Ubuntu Focal LTS as base image
FROM ubuntu:focal

# Update the version at build time
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
    && apt-get install -y apt-utils wget apt-transport-https lsb-release software-properties-common p7zip-full locales locales-all \
    && locale-gen --no-purge en_US en_US.UTF-8

# Set locale to UTF-8 after running locale-gen
ENV LANG=en_US.UTF-8 \
    LANGUAGE=en_US:en \
    LC_ALL=en_US.UTF-8

# TODO: Build could be optimized by combining RUN layers, but that makes troubleshooting more difficult

# Install .NET 6 Runtime
# https://docs.microsoft.com/en-us/dotnet/core/install/linux-ubuntu
# TODO: Substitute ubuntu with $(lsb_release -si | tr '[:upper:]' '[:lower:]')

# Follow pattern used in .NET Dockerfile
# https://github.com/dotnet/dotnet-docker/blob/main/src/sdk/6.0/focal/amd64/Dockerfile
ENV DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_USE_POLLING_FILE_WATCHER=true \
    NUGET_XMLDOC_MODE=skip

RUN wget -q https://packages.microsoft.com/config/ubuntu/$(lsb_release -sr)/packages-microsoft-prod.deb \
    && dpkg -i packages-microsoft-prod.deb \
    && apt-get update \
    && apt-get install -y dotnet-runtime-6.0 \
    && dotnet --info

# Install MediaInfo
# https://mediaarea.net/en/MediaInfo/Download/Ubuntu
# https://mediaarea.net/en/Repos
RUN wget -q https://mediaarea.net/repo/deb/repo-mediaarea_1.0-19_all.deb \
    && dpkg -i repo-mediaarea_1.0-19_all.deb \
    && apt-get update \
    && apt-get install -y mediainfo \
    && mediainfo --version

# Install MKVToolNix
# https://mkvtoolnix.download/downloads.html#ubuntu

# Avoid apt-key output should not be parsed warning
ARG APT_KEY_DONT_WARN_ON_DANGEROUS_USAGE=1

RUN wget -q -O - https://mkvtoolnix.download/gpg-pub-moritzbunkus.txt | apt-key add - \
    && sh -c 'echo "deb https://mkvtoolnix.download/ubuntu/ $(lsb_release -sc) main" >> /etc/apt/sources.list.d/bunkus.org.list' \
    && apt-get update \
    && apt-get install -y mkvtoolnix \
    && mkvmerge --version

# Install FfMpeg
# https://launchpad.net/~savoury1/+archive/ubuntu/ffmpeg5
RUN add-apt-repository -y ppa:savoury1/graphics \
    && add-apt-repository -y ppa:savoury1/multimedia \
    && add-apt-repository -y ppa:savoury1/ffmpeg4 \
    && add-apt-repository -y ppa:savoury1/ffmpeg5 \
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
