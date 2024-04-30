# Debian Bookworm 12.0
# .NET installed using MCR .NET base image
# linux/amd64,linux/arm64,linux/arm/v7
# ptr727/plexcleaner:debian

# Multi-architecture and multi-stage docker build
# https://www.docker.com/blog/faster-multi-platform-builds-dockerfile-cross-compilation-guide/
# https://devblogs.microsoft.com/dotnet/improving-multiplatform-container-support/
# https://docs.docker.com/build/building/multi-stage/

# .NET does not support QEMU
# Only run compiled code when BUILDPLATFORM == TARGETPLATFORM
# https://gitlab.com/qemu-project/qemu/-/issues/249

# Loading all targets are not supported, test only linux/amd64 target
# https://github.com/docker/buildx/issues/59

# Troubleshooting, add "build --progress plain --no-cache"

# Test image in shell:
# docker run -it --rm --pull always --name Testing mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim /bin/bash
# docker run -it --rm --pull always --name Testing mcr.microsoft.com/dotnet/runtime:8.0-bookworm-slim /bin/bash
# docker run -it --rm --pull always --name Testing ptr727/plexcleaner:debian-develop /bin/bash
# export DEBIAN_FRONTEND=noninteractive

# Create and use multi platform build environment
# docker buildx create --name "plexcleaner" --use

# Build Dockerfile
# docker buildx build --platform linux/amd64,linux/arm64,linux/arm/v7 --tag testing:latest --file ./Docker/Debian.dotNET.Dockerfile .

# Test linux/amd64 target
# docker buildx build --progress plain --load --platform linux/amd64 --tag testing:latest --file ./Docker/Debian.dotNET.Dockerfile .
# docker run -it --rm --name Testing testing:latest /bin/bash


# Builder layer
# https://github.com/dotnet/dotnet-docker/tree/main/src/sdk/8.0/bookworm-slim/amd64/Dockerfile
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS builder

# Layer workdir
WORKDIR /Builder

# Global builder variables
# https://docs.docker.com/engine/reference/builder/#automatic-platform-args-in-the-global-scope

# Build platform args
ARG \
    # Platform of the build result. Eg linux/amd64, linux/arm/v7, windows/amd64
    TARGETPLATFORM \
    # Architecture component of TARGETPLATFORM
    TARGETARCH \
    # Platform of the node performing the build
    BUILDPLATFORM

# PlexCleaner build attribute configuration
ARG BUILD_CONFIGURATION="Debug" \
    BUILD_VERSION="1.0.0.0" \
    BUILD_FILE_VERSION="1.0.0.0" \
    BUILD_ASSEMBLY_VERSION="1.0.0.0" \
    BUILD_INFORMATION_VERSION="1.0.0.0" \
    BUILD_PACKAGE_VERSION="1.0.0.0"

# Upgrade
RUN apt-get update \
    && apt-get upgrade -y

# Copy source and unit tests
COPY ./Samples/. ./Samples/.
COPY ./PlexCleanerTests/. ./PlexCleanerTests/.
COPY ./PlexCleaner/. ./PlexCleaner/.

# Unit Test
COPY ./Docker/UnitTest.sh ./
RUN chmod ugo+rwx ./UnitTest.sh
RUN ./UnitTest.sh

# Build
COPY ./Docker/Build.sh ./
RUN chmod ugo+rwx ./Build.sh
RUN ./Build.sh


# Final layer
# Build from .NET Debian base image
# https://hub.docker.com/_/microsoft-dotnet
# https://hub.docker.com/_/microsoft-dotnet-sdk/
# https://github.com/dotnet/dotnet-docker
# https://mcr.microsoft.com/en-us/product/dotnet/sdk/tags
# https://github.com/dotnet/dotnet-docker/tree/main/src/runtime/8.0/bookworm-slim/amd64/Dockerfile
FROM mcr.microsoft.com/dotnet/runtime:8.0-bookworm-slim as final

# Image label
ARG LABEL_VERSION="1.0.0.0"
LABEL name="PlexCleaner" \
    version=${LABEL_VERSION} \
    description="Utility to optimize media files for Direct Play in Plex, Emby, Jellyfin" \
    maintainer="Pieter Viljoen <ptr727@users.noreply.github.com>"

ENV \
    # Default timezone is UTC
    TZ=Etc/UTC \
    # Prevent EULA and confirmation prompts in installers
    DEBIAN_FRONTEND=noninteractive

# Upgrade
RUN apt-get update \
    && apt-get upgrade -y

# Install dependencies
RUN apt-get install -y --no-install-recommends \
        ca-certificates \
        libicu72 \
        locales \
        locales-all \
        p7zip-full \
        tzdata \
        wget \
    && locale-gen --no-purge en_US en_US.UTF-8

# Set locale to UTF-8 after running locale-gen
# https://github.com/dotnet/dotnet-docker/blob/main/samples/enable-globalization.md
ENV LANG=en_US.UTF-8 \
    LANGUAGE=en_US:en \
    LC_ALL=en_US.UTF-8

# Install VS debug tools
# https://github.com/OmniSharp/omnisharp-vscode/wiki/Attaching-to-remote-processes
RUN wget https://aka.ms/getvsdbgsh \
    && sh getvsdbgsh -v latest -l /vsdbg \
    && rm getvsdbgsh

# Install media tools
# https://tracker.debian.org/pkg/ffmpeg
# https://tracker.debian.org/pkg/handbrake
# https://tracker.debian.org/pkg/mediainfo
# https://tracker.debian.org/pkg/mkvtoolnix
RUN apt-get install -y \
        ffmpeg \
        handbrake-cli \
        mediainfo \
        mkvtoolnix

# Cleanup
RUN apt-get autoremove -y \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Copy PlexCleaner from builder layer
COPY --from=builder /Builder/Publish/PlexCleaner/. /PlexCleaner

# Copy test script
COPY /Docker/Test.sh /Test/
RUN chmod -R ugo+rwx /Test

# Copy version script
COPY /Docker/Version.sh /PlexCleaner/
RUN chmod ugo+rwx /PlexCleaner/Version.sh

# Print version information
ARG TARGETPLATFORM \
    BUILDPLATFORM
RUN if [ "$BUILDPLATFORM" = "$TARGETPLATFORM" ]; then \
        /PlexCleaner/Version.sh; \
    fi
