# Description: Debian Stable (12 Bookworm)
# Based on: debian:stable-slim
# .NET: Msft repository
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
# docker buildx build --platform linux/amd64,linux/arm64,linux/arm/v7 --tag testing:latest --file ./Docker/Debian.Stable.Dockerfile .

# Test linux/amd64 target
# docker buildx build --load --platform linux/amd64 --tag testing:latest --file ./Docker/Debian.Stable.Dockerfile .
# docker run -it --rm --name Testing testing:latest /bin/bash


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
RUN apt-get update \
    && apt-get upgrade -y

# Install dependencies
RUN apt-get install -y --no-install-recommends \
        ca-certificates \
        lsb-release \
        wget

# Install .NET SDK
RUN wget https://packages.microsoft.com/config/debian/$(lsb_release -sr)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb \
    && dpkg -i packages-microsoft-prod.deb \
    && rm packages-microsoft-prod.deb \
    && apt-get update \
    && apt-get install -y --no-install-recommends \
        dotnet-sdk-8.0

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
FROM --platform=$BUILDPLATFORM debian:stable-slim AS final

# Image label
ARG LABEL_VERSION="1.0.0.0"
LABEL name="PlexCleaner" \
    version=${LABEL_VERSION} \
    description="Utility to optimize media files for Direct Play in Plex, Emby, Jellyfin" \
    maintainer="Pieter Viljoen <ptr727@users.noreply.github.com>"

# Prevent EULA and confirmation prompts in installers
ENV DEBIAN_FRONTEND=noninteractive

# Upgrade
RUN apt-get update \
    && apt-get upgrade -y

# Install dependencies
RUN apt-get install -y --no-install-recommends \
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

# Install .NET Runtime
RUN wget https://packages.microsoft.com/config/debian/$(lsb_release -sr)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb \
    && dpkg -i packages-microsoft-prod.deb \
    && rm packages-microsoft-prod.deb \
    && apt-get update \
    && apt-get install -y --no-install-recommends \
        dotnet-runtime-8.0

# Install media tools
# https://tracker.debian.org/pkg/ffmpeg
# https://tracker.debian.org/pkg/handbrake
# https://tracker.debian.org/pkg/mediainfo
# https://tracker.debian.org/pkg/mkvtoolnix
RUN apt-get install -y --no-install-recommends \
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

# Copy debug tools installer script
COPY ./Docker/DebugTools.sh ./
RUN chmod ugo+rwx ./DebugTools.sh

# Copy version script
COPY /Docker/Version.sh /PlexCleaner/
RUN chmod ugo+rwx /PlexCleaner/Version.sh

# Print version information
ARG TARGETPLATFORM \
    BUILDPLATFORM
RUN if [ "$BUILDPLATFORM" = "$TARGETPLATFORM" ]; then \
        /PlexCleaner/Version.sh; \
    fi
