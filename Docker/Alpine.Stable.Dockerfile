# Description: Alpine Stable (3.20)
# Based on: alpine:latest
# .NET: Alpine repository
# Platforms: linux/amd64, linux/arm64
# Tag: ptr727/plexcleaner:alpine

# Test image in shell:
# docker run -it --rm --pull always --name Testing alpine:latest /bin/sh
# docker run -it --rm --pull always --name Testing ptr727/plexcleaner:alpine /bin/sh

# Build Dockerfile
# docker buildx create --name "plexcleaner" --use
# docker buildx build --platform linux/amd64,linux/arm64 --tag testing:latest --file ./Docker/Alpine.Stable.Dockerfile .

# Test linux/amd64 target
# docker buildx build --load --platform linux/amd64 --tag testing:latest --file ./Docker/Alpine.Stable.Dockerfile .
# docker run -it --rm --name Testing testing:latest /bin/sh


# Builder layer
FROM --platform=$BUILDPLATFORM alpine:latest AS builder

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

# Upgrade
RUN apk update \
    && apk upgrade

# Install .NET SDK
# https://pkgs.alpinelinux.org/package/edge/community/x86_64/dotnet8-sdk
RUN apk add dotnet8-sdk

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
FROM alpine:latest as final

# Image label
ARG LABEL_VERSION="1.0.0.0"
LABEL name="PlexCleaner" \
    version=${LABEL_VERSION} \
    description="Utility to optimize media files for Direct Play in Plex, Emby, Jellyfin" \
    maintainer="Pieter Viljoen <ptr727@users.noreply.github.com>"

# Enable .NET globalization, set default locale to en_US.UTF-8, and timezone to UTC
# https://github.com/dotnet/dotnet-docker/blob/main/samples/dotnetapp/Dockerfile.alpine-icu
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    LANG=en_US.UTF-8 \
    LANGUAGE=en_US:en \
    LC_ALL=en_US.UTF-8 \
    TZ=Etc/UTC

# Upgrade
RUN apk update \
    && apk upgrade

# Install dependencies
RUN apk add \
        icu-data-full \
        icu-libs \
        p7zip \
        tzdata \
        wget

# Install .NET Runtime
# https://pkgs.alpinelinux.org/package/edge/community/x86_64/dotnet8-runtime
RUN apk add dotnet8-runtime

# Install VS debug tools
# https://github.com/OmniSharp/omnisharp-vscode/wiki/Attaching-to-remote-processes
RUN wget https://aka.ms/getvsdbgsh \
    && sh getvsdbgsh -v latest -l /vsdbg \
    && rm getvsdbgsh

# Install media tools
# https://pkgs.alpinelinux.org/package/v3.20/community/x86_64/ffmpeg
# https://pkgs.alpinelinux.org/package/v3.20/community/x86_64/mediainfo
# https://pkgs.alpinelinux.org/package/v3.20/community/x86_64/mkvtoolnix
# https://pkgs.alpinelinux.org/package/v3.20/community/x86_64/handbrake
RUN apk add \
        ffmpeg\
        handbrake \
        mediainfo \
        mkvtoolnix

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
