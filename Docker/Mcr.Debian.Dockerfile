# Test in docker shell:
# docker run -it --rm --pull always --name Testing mcr.microsoft.com/dotnet/sdk:latest /bin/bash
# export DEBIAN_FRONTEND=noninteractive

# Test in develop shell:
# docker run -it --rm --pull always --name Testing --volume /data/media:/media:rw ptr727/plexcleaner:develop /bin/bash

# Build PlexCleaner
# dotnet publish ./PlexCleaner/PlexCleaner.csproj --runtime linux-x64 --self-contained false --output ./Docker/PlexCleaner

# Build Dockerfile
# docker build --tag testing:latest --file=./Docker/Mcr.Debian.Dockerfile .
# --no-cache --progress=plain



# Build from .NET Debian base image mcr.microsoft.com/dotnet/sdk:latest
# https://hub.docker.com/_/microsoft-dotnet
# https://hub.docker.com/_/microsoft-dotnet-sdk/
# https://github.com/dotnet/dotnet-docker
# https://mcr.microsoft.com/en-us/product/dotnet/sdk/tags
FROM mcr.microsoft.com/dotnet/sdk:latest

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

# Register additional repos
# https://serverfault.com/questions/22414/how-can-i-run-debian-stable-but-install-some-packages-from-testing
RUN touch /etc/apt/preferences.d/stable.pref \
    && echo "Package: *" >> /etc/apt/preferences.d/stable.pref \
    && echo "Pin: release a=stable" >> /etc/apt/preferences.d/stable.pref \
    && echo "Pin-Priority: 900" >> /etc/apt/preferences.d/stable.pref \
    && touch /etc/apt/preferences.d/testing.pref \
    && echo "Package: *" >> /etc/apt/preferences.d/testing.pref \
    && echo "Pin: release a=testing" >> /etc/apt/preferences.d/testing.pref \
    && echo "Pin-Priority: 400" >> /etc/apt/preferences.d/testing.pref \
    && touch /etc/apt/preferences.d/unstable.pref \
    && echo "Package: *" >> /etc/apt/preferences.d/unstable.pref \
    && echo "Pin: release a=unstable" >> /etc/apt/preferences.d/unstable.pref \
    && echo "Pin-Priority: 50" >> /etc/apt/preferences.d/unstable.pref \
    && touch /etc/apt/preferences.d/experimental.pref \
    && echo "Package: *" >> /etc/apt/preferences.d/experimental.pref \
    && echo "Pin: release a=experimental" >> /etc/apt/preferences.d/experimental.pref \
    && echo "Pin-Priority: 1" >> /etc/apt/preferences.d/experimental.pref \
    && cp /etc/apt/sources.list /etc/apt/sources.list.d/stable.list \
    && mv /etc/apt/sources.list /etc/apt/sources.list.orig \
    && touch /etc/apt/sources.list.d/testing.list \
    && echo "deb http://deb.debian.org/debian testing main" >> /etc/apt/sources.list.d/testing.list \
    && touch /etc/apt/sources.list.d/unstable.list \
    && echo "deb http://deb.debian.org/debian unstable main" >> /etc/apt/sources.list.d/unstable.list \
    && touch /etc/apt/sources.list.d/experimental.list \
    && echo "deb http://deb.debian.org/debian experimental main" >> /etc/apt/sources.list.d/experimental.list

# Install prerequisites
RUN apt-get update \
    && apt-get upgrade -y \
    && apt-get install -y \
        apt-utils \
        locales \
        locales-all \
        p7zip-full \
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

# Install media tools from testing
# https://tracker.debian.org/pkg/ffmpeg
# https://tracker.debian.org/pkg/handbrake
# https://tracker.debian.org/pkg/mediainfo
# https://tracker.debian.org/pkg/mkvtoolnix
RUN apt-get install -t testing -y \
        ffmpeg \
        handbrake-cli \
        mediainfo \
        mkvtoolnix \
    && ffmpeg -version \
    && HandBrakeCLI --version \
    && mediainfo --version \
    && mkvmerge --version

# Cleanup
RUN apt-get autoremove -y \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Copy PlexCleaner
COPY ./Docker/PlexCleaner /PlexCleaner
RUN /PlexCleaner/PlexCleaner --version