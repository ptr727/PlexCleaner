# Refer to Debian.dotNET.Dockerfile for build plan

# Test base image in shell:
# docker run -it --rm --pull always --name Testing mcr.microsoft.com/dotnet/sdk:7.0-alpine /bin/sh
# docker run -it --rm --pull always --name Testing mcr.microsoft.com/dotnet/nightly/sdk:8.0-preview-alpine /bin/sh

# Test image in shell:
# docker run -it --rm --pull always --name Testing ptr727/plexcleaner:alpine-develop /bin/bash

# Build PlexCleaner
# dotnet publish ./PlexCleaner/PlexCleaner.csproj --configuration debug --runtime linux-x64 --self-contained false --output ./Docker/PlexCleaner

# Build Dockerfile
# docker buildx build --platform linux/amd64,linux/arm64,linux/arm/v7 --tag testing:latest --file ./Docker/Alpine.dotNET.Dockerfile .
# docker buildx build --progress plain --no-cache --platform linux/amd64,linux/arm64,linux/arm/v7 --tag testing:latest --file ./Docker/Alpine.dotNET.Dockerfile .

# Test linux/amd64 target
# docker buildx build --load --progress plain --no-cache --platform linux/amd64 --tag testing:latest --file ./Docker/Alpine.dotNET.Dockerfile .
# docker run -it --rm --name Testing testing:latest /bin/bash



# Builder layer
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/nightly/sdk:8.0-preview-alpine AS builder

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



# Final layer
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:7.0-alpine as final

# Build version
ARG LABEL_VERSION="1.0.0.0"

LABEL name="PlexCleaner" \
    version=${LABEL_VERSION} \
    description="Utility to optimize media files for Direct Play in Plex, Emby, Jellyfin" \
    maintainer="Pieter Viljoen <ptr727@users.noreply.github.com>"

# Default timezone is UTC
ENV TZ=Etc/UTC

# Update repository from 3.x to edge and add testing
# https://wiki.alpinelinux.org/wiki/Repositories
RUN sed -i 's|v3\.\d*|edge|' /etc/apk/repositories \
    && echo "https://dl-cdn.alpinelinux.org/alpine/edge/testing" >> /etc/apk/repositories

# Install prerequisites
RUN apk update \
    && apk --no-cache add \
        p7zip \
        wget

# TODO: Create locale

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

# Install media tools from edge repository
# https://pkgs.alpinelinux.org/package/edge/community/x86_64/ffmpeg
# https://pkgs.alpinelinux.org/package/edge/testing/x86_64/handbrake
# https://pkgs.alpinelinux.org/package/edge/community/x86_64/mediainfo
# https://pkgs.alpinelinux.org/package/edge/community/x86_64/mkvtoolnix
RUN apk add \
        ffmpeg \
        handbrake \
        mediainfo \
        mkvtoolnix \
    && ffmpeg -version \
    && HandBrakeCLI --version \
    && mediainfo --version \
    && mkvmerge --version

# Copy PlexCleaner from builder layer
COPY --from=builder /Builder/Publish/PlexCleaner/. /PlexCleaner
