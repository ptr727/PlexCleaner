# Description: Ubuntu development release
# Based on: ubuntu:devel
# .NET install: Ubuntu repository
# Platforms: linux/amd64, linux/arm64
# Tag: ptr727/plexcleaner:ubuntu-devel

# Docker build debugging:
# --progress=plain
# --no-cache

# Test image in shell:
# docker run -it --rm --pull always --name Testing ubuntu:devel /bin/bash
# docker run -it --rm --pull always --name Testing ptr727/plexcleaner:ubuntu-devel /bin/bash
# export DEBIAN_FRONTEND=noninteractive

# Build Dockerfile
# docker buildx create --name "plexcleaner" --use
# docker buildx build --platform linux/amd64,linux/arm64 --file ./Docker/Ubuntu.Devel.Dockerfile .

# Test linux/amd64 target
# docker buildx build --load --platform linux/amd64 --tag plexcleaner:ubuntu-devel --file ./Docker/Ubuntu.Devel.Dockerfile .
# docker run -it --rm --name PlexCleaner-Test plexcleaner:ubuntu-devel /bin/bash


# Builder layer
FROM --platform=$BUILDPLATFORM ubuntu:devel AS builder

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

# Install .NET SDK
# https://packages.ubuntu.com/plucky/dotnet-sdk-9.0
RUN apt install -y --no-install-recommends dotnet-sdk-9.0

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
FROM ubuntu:devel AS final

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

# Install .NET Runtime
# https://packages.ubuntu.com/plucky/dotnet-runtime-9.0
RUN apt install -y --no-install-recommends dotnet-runtime-9.0

# Install media tools
# https://packages.ubuntu.com/plucky/ffmpeg
# https://packages.ubuntu.com/plucky/handbrake-cli
# https://packages.ubuntu.com/plucky/mediainfo
# https://packages.ubuntu.com/plucky/mkvtoolnix
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
