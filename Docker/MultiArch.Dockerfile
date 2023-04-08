# https://github.com/dotnet/dotnet-docker/issues/4388
# https://devblogs.microsoft.com/dotnet/improving-multiplatform-container-support/
# https://github.com/dotnet/dotnet-docker/blob/main/samples/enable-globalization.md
# https://docs.docker.com/build/building/multi-stage/


# Test in docker shell:
# docker run -it --rm --pull always --name Testing mcr.microsoft.com/dotnet/sdk:latest /bin/bash
# export DEBIAN_FRONTEND=noninteractive

# Test in develop shell:
# docker run -it --rm --pull always --name Testing --volume /data/media:/media:rw ptr727/plexcleaner:develop /bin/bash

# Build PlexCleaner
# dotnet publish ./PlexCleaner/PlexCleaner.csproj --runtime linux-x64 --self-contained false --output ./Docker/PlexCleaner

# Build Dockerfile
# docker buildx ls
# docker buildx create --name plexcleaner
# docker buildx use plexcleaner
# docker buildx inspect --bootstrap
# docker buildx build --platform linux/amd64,linux/arm64,linux/arm/v7 --tag testing:latest --file ./Docker/MultiArch.Dockerfile .
# --progress plain 
# docker buildx stop

# Test one of the build targets
# https://github.com/docker/buildx/issues/59
# docker buildx build --load --progress plain --no-cache --platform linux/amd64 --tag testing:latest --file ./Docker/MultiArch.Dockerfile .
# docker run -it --rm --name Testing testing:latest /bin/bash



# Builder layer
# Build using .NET 8 nighltly SDK, need 8.0.P3 or 7.0.300 to be released
# FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:7.0 AS builder
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/nightly/sdk:8.0-preview-alpine AS builder
WORKDIR /Builder

# Architecture, injected from build
ARG TARGETARCH

# ${{ endsWith(github.ref, 'refs/heads/main') && 'Release' || 'Debug' }}
ARG BUILD_CONFIGURATION="Release"

# ${{ steps.nbgv.outputs.AssemblyVersion }}
ARG BUILD_VERSION="1.0.0.0"

# ${{ steps.nbgv.outputs.AssemblyFileVersion }}
ARG BUILD_FILE_VERSION="1.0.0.0"
ARG BUILD_ASSEMBLY_VERSION="1.0.0.0"

# ${{ steps.nbgv.outputs.AssemblyInformationalVersion }}
ARG BUILD_INFORMATION_VERSION="1.0.0.0"

# ${{ steps.nbgv.outputs.SemVer2 }}
ARG BUILD_PACKAGE_VERSION="1.0.0.0"

# Copy source
COPY ./PlexCleaner/. .

# TODO: Examples restore explicitly, why, should not be required?
# Restore dependencies for platform
# RUN dotnet restore --arch $TARGETARCH

# Build and publish
RUN dotnet publish ./PlexCleaner.csproj \
#    --no-restore \
    --arch $TARGETARCH \
    --self-contained false \
    --output ./Publish \
    --configuration $BUILD_CONFIGURATION \
    -property:Version=$BUILD_VERSION \
    -property:FileVersion=$BUILD_FILE_VERSION \
    -property:AssemblyVersion=$BUILD_ASSEMBLY_VERSION \
    -property:InformationalVersion=$BUILD_INFORMATION_VERSION \
    -property:PackageVersion=$BUILD_PACKAGE_VERSION

# Running a .NET 7 target on .NET 8 preview
ENV DOTNET_ROLL_FORWARD=Major
ENV DOTNET_ROLL_FORWARD_PRE_RELEASE=1
RUN ./Publish/PlexCleaner --version


# Runtime layer
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

# Copy PlexCleaner from builder layer
COPY --from=builder /Builder/Publish/. /PlexCleaner

# TODO: Why is the file not found?
# RUN /PlexCleaner/PlexCleaner --version

