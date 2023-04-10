# https://devblogs.microsoft.com/dotnet/improving-multiplatform-container-support/
# https://github.com/dotnet/dotnet-docker/blob/main/samples/enable-globalization.md
# https://docs.docker.com/build/building/multi-stage/


# Test base image in shell:
# docker run -it --rm --pull always --name Testing mcr.microsoft.com/dotnet/sdk:latest /bin/bash
# docker run -it --rm --pull always --name Testing mcr.microsoft.com/dotnet/nightly/sdk:8.0-preview /bin/bash
# export DEBIAN_FRONTEND=noninteractive

# Test image in shell:
# docker run -it --rm --pull always --name Testing ptr727/plexcleaner:debian-develop /bin/bash

# Build PlexCleaner
# dotnet publish ./PlexCleaner/PlexCleaner.csproj --configuration debug --runtime linux-x64 --self-contained false --output ./Docker/PlexCleaner

# Build Dockerfile
# docker buildx ls
# docker buildx create --name plexcleaner
# docker buildx use plexcleaner
# docker buildx inspect --bootstrap
# docker buildx build --platform linux/amd64,linux/arm64,linux/arm/v7 --tag testing:latest --file ./Docker/Debian.dotNET.Dockerfile .
# docker buildx build --progress plain --no-cache --platform linux/amd64,linux/arm64,linux/arm/v7 --tag testing:latest --file ./Docker/Debian.dotNET.Dockerfile .
# docker buildx stop

# Loading all targets are not supported, test one target
# TODO: https://github.com/docker/buildx/issues/59
# docker buildx build --load --progress plain --no-cache --platform linux/amd64 --tag testing:latest --file ./Docker/Debian.dotNET.Dockerfile .
# docker run -it --rm --name Testing testing:latest /bin/bash



# Builder layer
# Build using .NET 8 nighltly SDK, need 8.0.P3 or 7.0.300 to be released
# TODO: # https://github.com/dotnet/dotnet-docker/issues/4388
# FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:7.0 AS builder
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/nightly/sdk:8.0-preview AS builder

# Layer workdir
WORKDIR /Builder

# Architecture, injected from build
ARG TARGETARCH

# Debug or Release
ARG BUILD_CONFIGURATION="Debug"

# Package versions
ARG BUILD_VERSION="1.0.0.0"
ARG BUILD_FILE_VERSION="1.0.0.0"
ARG BUILD_ASSEMBLY_VERSION="1.0.0.0"
ARG BUILD_INFORMATION_VERSION="1.0.0.0"
ARG BUILD_PACKAGE_VERSION="1.0.0.0"

# Copy source and unit tests
COPY ./Samples/. ./Samples/.
COPY ./PlexCleanerTests/. ./PlexCleanerTests/.
COPY ./PlexCleaner/. ./PlexCleaner/.

# Enable running a .NET 7 target on .NET 8 preview
ENV DOTNET_ROLL_FORWARD=Major
ENV DOTNET_ROLL_FORWARD_PRE_RELEASE=1

# Run unit tests
RUN dotnet test ./PlexCleanerTests/PlexCleanerTests.csproj

# Verify dotnet run executes
RUN dotnet run --project ./PlexCleaner/PlexCleaner.csproj --version

# Build release and debug builds
RUN dotnet publish ./PlexCleaner/PlexCleaner.csproj \
        --arch $TARGETARCH \
        --self-contained false \
        --output ./Build/Release \
        --configuration release \
        -property:Version=$BUILD_VERSION \
        -property:FileVersion=$BUILD_FILE_VERSION \
        -property:AssemblyVersion=$BUILD_ASSEMBLY_VERSION \
        -property:InformationalVersion=$BUILD_INFORMATION_VERSION \
        -property:PackageVersion=$BUILD_PACKAGE_VERSION \
    && dotnet publish ./PlexCleaner/PlexCleaner.csproj \
        --arch $TARGETARCH \
        --self-contained false \
        --output ./Build/Debug \
        --configuration debug \
        -property:Version=$BUILD_VERSION \
        -property:FileVersion=$BUILD_FILE_VERSION \
        -property:AssemblyVersion=$BUILD_ASSEMBLY_VERSION \
        -property:InformationalVersion=$BUILD_INFORMATION_VERSION \
        -property:PackageVersion=$BUILD_PACKAGE_VERSION

# Copy build output
RUN mkdir -p ./Publish/PlexCleaner\Debug \
    && mkdir -p ./Publish/PlexCleaner\Release \
    && if [ "$BUILD_CONFIGURATION" = "Debug" ] || [ "$BUILD_CONFIGURATION" = "debug" ]; \
    then \
        cp -r ./Build/Debug ./Publish/PlexCleaner; \
    else \
        cp -r ./Build/Release ./Publish/PlexCleaner; \
    fi \
    && cp -r ./Build/Release ./Publish/PlexCleaner/Release \
    && cp -r ./Build/Debug ./Publish/PlexCleaner/Debug

# Verify built binay executes
RUN ./Publish/PlexCleaner/PlexCleaner --version


# Final layer
# Build from .NET Debian base image mcr.microsoft.com/dotnet/sdk:latest
# https://hub.docker.com/_/microsoft-dotnet
# https://hub.docker.com/_/microsoft-dotnet-sdk/
# https://github.com/dotnet/dotnet-docker
# https://mcr.microsoft.com/en-us/product/dotnet/sdk/tags
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:7.0 as final

# Build version
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

# Install media tools from testing repository
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
COPY --from=builder /Builder/Publish/PlexCleaner/. /PlexCleaner

# Verify PlexCleaner runs
RUN /PlexCleaner/PlexCleaner --version

# TODO: Resolve Qemu errors?
# qemu-aarch64: Could not open '/lib/ld-linux-aarch64.so.1': No such file or directory
# qemu-arm: Could not open '/lib/ld-linux-armhf.so.3': No such file or directory