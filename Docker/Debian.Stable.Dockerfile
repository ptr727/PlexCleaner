# Description: Debian latest release
# Based on: debian:stable-slim
# .NET install: Install script
# Platforms: linux/amd64, linux/arm64, linux/arm/v7
# Tag: ptr727/plexcleaner:debian

# Docker build debugging:
# --progress=plain
# --no-cache

# Test image in shell:
# docker run -it --rm --pull always --name Testing debian:stable-slim /bin/bash
# docker run -it --rm --pull always --name Testing ptr727/plexcleaner:debian /bin/bash
# export DEBIAN_FRONTEND=noninteractive

# Build Dockerfile
# docker buildx create --name "plexcleaner" --use
# docker buildx build --platform linux/amd64,linux/arm64,linux/arm/v7 --file ./Docker/Debian.Stable.Dockerfile .

# Test linux/amd64 target
# docker buildx build --load --platform linux/amd64 --tag plexcleaner:debian --file ./Docker/Debian.Stable.Dockerfile .
# docker run -it --rm --name PlexCleaner-Test plexcleaner:debian /bin/bash


# Builder layer
FROM --platform=$BUILDPLATFORM debian:stable-slim AS builder

# Layer workdir
WORKDIR /Builder

# Build platform args
ARG TARGETPLATFORM \
    TARGETARCH \
    BUILDPLATFORM

# PlexCleaner build attribute configuration
ARG BUILD_CONFIGURATION="Debug" \
    BUILD_VERSION="1.0.0.0" \
    BUILD_FILE_VERSION="1.0.0.0" \
    BUILD_ASSEMBLY_VERSION="1.0.0.0" \
    BUILD_INFORMATION_VERSION="1.0.0.0" \
    BUILD_PACKAGE_VERSION="1.0.0.0"

# Prevent EULA and confirmation prompts in installers
ENV DEBIAN_FRONTEND=noninteractive

# Upgrade
RUN apt update \
    && apt upgrade -y

# Install dependencies
RUN apt install -y --no-install-recommends \
        ca-certificates \
        lsb-release \
        wget

# Install .NET SDK
# https://learn.microsoft.com/en-us/dotnet/core/install/linux-scripted-manual
# https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-install-script
# https://github.com/dotnet/core/blob/main/release-notes/9.0/os-packages.md
# https://github.com/dotnet/dotnet-docker/blob/main/src/sdk/9.0/bookworm-slim/amd64/Dockerfile
# https://github.com/dotnet/dotnet-docker/blob/main/src/runtime-deps/9.0/bookworm-slim/amd64/Dockerfile
RUN apt install -y --no-install-recommends \
        ca-certificates \
        curl \
        libc6 \
        libgcc-s1 \
        libicu72 \
        libssl3 \
        libstdc++6 \
        tzdata \
        wget \
    && wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh \
    && chmod ug=rwx,o=rx dotnet-install.sh \
    && ./dotnet-install.sh --install-dir /usr/local/bin/dotnet --channel 9.0 \
    && rm dotnet-install.sh
ENV DOTNET_ROOT=/usr/local/bin/dotnet \
    PATH=$PATH:/usr/local/bin/dotnet:/usr/local/bin/dotnet/tools \
    DOTNET_USE_POLLING_FILE_WATCHER=true \
    DOTNET_RUNNING_IN_CONTAINER=true

# Copy source and unit tests
COPY ./Samples/. ./Samples/.
COPY ./PlexCleanerTests/. ./PlexCleanerTests/.
COPY ./PlexCleaner/. ./PlexCleaner/.

# Unit Test
COPY ./Docker/UnitTest.sh ./
RUN chmod ug=rwx,o=rx ./UnitTest.sh
RUN ./UnitTest.sh

# Build
COPY ./Docker/Build.sh ./
RUN chmod ug=rwx,o=rx ./Build.sh
RUN ./Build.sh


# Final layer
FROM debian:stable-slim AS final

# Image label
ARG LABEL_VERSION="1.0.0.0"
LABEL name="PlexCleaner" \
    version=${LABEL_VERSION} \
    description="Utility to optimize media files for Direct Play in Plex, Emby, Jellyfin" \
    maintainer="Pieter Viljoen <ptr727@users.noreply.github.com>"

# Prevent EULA and confirmation prompts in installers
ENV DEBIAN_FRONTEND=noninteractive

# Upgrade
RUN apt update \
    && apt upgrade -y

# Install dependencies
RUN apt install -y --no-install-recommends \
        ca-certificates \
        locales \
        locales-all \
        lsb-release \
        p7zip-full \
        tzdata \
        wget \
    && locale-gen --no-purge en_US en_US.UTF-8

# Set locale to UTF-8 after running locale-gen
# https://github.com/dotnet/dotnet-docker/blob/main/samples/enable-globalization.md
ENV TZ=Etc/UTC \
    LANG=en_US.UTF-8 \
    LANGUAGE=en_US:en \
    LC_ALL=en_US.UTF-8

# Install .NET runtime
# Keep dependencies in sync with SDK install step
RUN apt install -y --no-install-recommends \
        ca-certificates \
        curl \
        libc6 \
        libgcc-s1 \
        libicu72 \
        libssl3 \
        libstdc++6 \
        tzdata \
        wget \
&& wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh \
    && chmod ug=rwx,o=rx dotnet-install.sh \
    && ./dotnet-install.sh --install-dir /usr/local/bin/dotnet --runtime dotnet --channel 9.0 \
    && rm dotnet-install.sh
ENV DOTNET_ROOT=/usr/local/bin/dotnet \
    PATH=$PATH:/usr/local/bin/dotnet:/usr/local/bin/dotnet/tools \
    DOTNET_USE_POLLING_FILE_WATCHER=true \
    DOTNET_RUNNING_IN_CONTAINER=true

# Install media tools
# https://tracker.debian.org/pkg/ffmpeg
# https://tracker.debian.org/pkg/handbrake
# https://tracker.debian.org/pkg/mediainfo
# https://tracker.debian.org/pkg/mkvtoolnix
RUN apt install -y --no-install-recommends \
        ffmpeg \
        handbrake-cli \
        mediainfo \
        mkvtoolnix

# Cleanup
RUN apt autoremove -y \
    && apt clean \
    && rm -rf /var/lib/apt/lists/*

# Copy PlexCleaner from builder layer
COPY --from=builder /Builder/Publish/PlexCleaner/. /PlexCleaner

# Copy test script
COPY /Docker/Test.sh /Test/
RUN chmod -R ug=rwx,o=rx /Test

# Install debug tools
COPY ./Docker/InstallDebugTools.sh ./
RUN chmod ug=rwx,o=rx ./InstallDebugTools.sh \
    && ./InstallDebugTools.sh \
    && rm -rf ./InstallDebugTools.sh

# Copy version script
COPY /Docker/Version.sh /PlexCleaner/
RUN chmod ug=rwx,o=rx /PlexCleaner/Version.sh

# Print version information
ARG TARGETPLATFORM \
    BUILDPLATFORM
RUN if [ "$BUILDPLATFORM" = "$TARGETPLATFORM" ]; then \
        /PlexCleaner/Version.sh; \
    fi
