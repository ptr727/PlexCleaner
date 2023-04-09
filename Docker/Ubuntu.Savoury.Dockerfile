# Test in docker shell:
# docker run -it --rm --pull always --name Testing mcr.microsoft.com/dotnet/sdk:7.0-jammy /bin/bash
# export DEBIAN_FRONTEND=noninteractive

# Test in develop shell:
# docker run -it --rm --pull always --name Testing --volume /data/media:/media:rw ptr727/plexcleaner:develop /bin/bash

# Build PlexCleaner
# dotnet publish ./PlexCleaner/PlexCleaner.csproj --runtime linux-x64 --self-contained false --output ./Docker/PlexCleaner

# Build Dockerfile
# docker build --progress=plain --secret id=savoury_ppa_auth,src=./Docker/auth.conf --tag testing:latest --file=./Docker/Ubuntu.Savoury.Dockerfile .
# --no-cache --progress=plain


# Build from Ubuntu LTS as base image
# https://hub.docker.com/_/ubuntu
FROM ubuntu:latest

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
        apt-utils \
        locales \
        locales-all \
        lsb-core \
        software-properties-common \
        p7zip-full \
        wget \
    && locale-gen --no-purge en_US en_US.UTF-8

# Set locale to UTF-8 after running locale-gen
ENV LANG=en_US.UTF-8 \
    LANGUAGE=en_US:en \
    LC_ALL=en_US.UTF-8

# Follow pattern used in .NET Dockerfile
# https://github.com/dotnet/dotnet-docker/blob/main/src/sdk/7.0/jammy/amd64/Dockerfile
ENV DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_USE_POLLING_FILE_WATCHER=true \
    NUGET_XMLDOC_MODE=skip

# Install .NET runtime using PMC
# https://learn.microsoft.com/en-us/dotnet/core/install/linux-ubuntu#register-the-microsoft-package-repository
RUN wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -sr)/packages-microsoft-prod.deb \
    && dpkg -i packages-microsoft-prod.deb \
    && rm packages-microsoft-prod.deb \
    && touch /etc/apt/preferences \
    && echo "Package: *" >> /etc/apt/preferences \
    && echo "Pin: origin \"packages.microsoft.com\"" >> /etc/apt/preferences \
    && echo "Pin-Priority: 1001" >> /etc/apt/preferences \
    && cat /etc/apt/preferences \
    && cat /etc/apt/sources.list.d/microsoft-prod.list \
    && apt-get update \
    && apt-get install -y dotnet-sdk-7.0 \
    && dotnet --info

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
    && touch /etc/apt/sources.list.d/mkvtoolnix.list \
    && sh -c 'echo "deb [arch=amd64 signed-by=/usr/share/keyrings/gpg-pub-moritzbunkus.gpg] https://mkvtoolnix.download/ubuntu/ $(lsb_release -sc) main" >> /etc/apt/sources.list.d/mkvtoolnix.list' \
    && apt-get update \
    && apt-get install -y mkvtoolnix \
    && mkvmerge --version

# Register Rob Savoury's public PPA's
RUN add-apt-repository -y ppa:savoury1/graphics \
    && add-apt-repository -y ppa:savoury1/multimedia \
    && add-apt-repository -y ppa:savoury1/ffmpeg4 \
    && add-apt-repository -y ppa:savoury1/ffmpeg6 \
    && add-apt-repository -y ppa:savoury1/handbrake \
    && apt-get update

# Register Rob Savoury's private PPA
# https://launchpad.net/~savoury1
# https://launchpad.net/~/+archivesubscriptions

# Install FfMpeg and HandBrake
# https://launchpad.net/~savoury1/+archive/ubuntu/ffmpeg6
# https://launchpad.net/~savoury1/+archive/ubuntu/handbrake

# TODO: How to apt-get that requires auth.conf without persisting the secret and then later deleting it?
# https://docs.docker.com/build/ci/github-actions/secrets/
# https://docs.docker.com/build/ci/github-actions/secrets/

# Run in one step to avoid caching layers with secrets that are not deleted, exposure is unknown
# auth.conf: "machine private-ppa.launchpadcontent.net login [username] password [password]"
RUN --mount=type=secret,id=savoury_ppa_auth cat /run/secrets/savoury_ppa_auth >> /etc/apt/auth.conf.d/savoury.conf \
    && touch /etc/apt/sources.list.d/savoury.list \
    && sh -c 'echo "deb https://private-ppa.launchpadcontent.net/savoury1/ffmpeg/ubuntu $(lsb_release -sc) main" >> /etc/apt/sources.list.d/savoury.list' \
    && apt-get update \
    && apt-get install -y \
        ffmpeg \
        handbrake-cli \
    && rm /etc/apt/auth.conf.d/savoury.conf \
    && ffmpeg -version \
    && HandBrakeCLI --version

# Cleanup
RUN apt-get autoremove -y \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Copy PlexCleaner
COPY ./Docker/PlexCleaner /PlexCleaner
RUN /PlexCleaner/PlexCleaner --version