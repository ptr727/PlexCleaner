# Refer to Debian.dotNET.Dockerfile for build plan

# There is no HandBrake package for arm/v7
# https://pkgs.alpinelinux.org/packages?name=handbrake&branch=edge&repo=&arch=&maintainer=

# Test image in shell:
# docker run -it --rm --pull always --name Testing mcr.microsoft.com/dotnet/sdk:7.0-alpine /bin/sh
# docker run -it --rm --pull always --name Testing mcr.microsoft.com/dotnet/sdk:8.0-preview-alpine /bin/sh
# docker run -it --rm --pull always --name Testing ptr727/plexcleaner:alpine-develop /bin/sh

# Build Dockerfile
# docker buildx build --platform linux/amd64,linux/arm64 --tag testing:latest --file ./Docker/Alpine.dotNET.Dockerfile .

# Test linux/amd64 target
# docker buildx build --progress plain --load --platform linux/amd64 --tag testing:latest --file ./Docker/Alpine.dotNET.Dockerfile .
# docker run -it --rm --name Testing testing:latest /bin/sh



# Builder layer
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0-preview-alpine AS builder

# Layer workdir
WORKDIR /Builder

# Build platform args
ARG \
    TARGETPLATFORM \
    TARGETARCH \
    BUILDPLATFORM

# PlexCleaner build attribute configuration
ARG BUILD_CONFIGURATION="Debug" \
    BUILD_VERSION="1.0.0.0" \
    BUILD_FILE_VERSION="1.0.0.0" \
    BUILD_ASSEMBLY_VERSION="1.0.0.0" \
    BUILD_INFORMATION_VERSION="1.0.0.0" \
    BUILD_PACKAGE_VERSION="1.0.0.0"

# Copy source and unit tests
COPY ./Samples/. ./Samples/.
COPY ./PlexCleanerTests/. ./PlexCleanerTests/.
COPY ./PlexCleaner/. ./PlexCleaner/.

# Enable running a .NET 7 target on .NET 8 preview
ENV DOTNET_ROLL_FORWARD=Major \
    DOTNET_ROLL_FORWARD_PRE_RELEASE=1

# Unit Test
COPY ./Docker/UnitTest.sh ./
RUN chmod ugo+rwx ./UnitTest.sh
RUN ./UnitTest.sh

# Build
COPY ./Docker/Build.sh ./
RUN chmod ugo+rwx ./Build.sh
RUN ./Build.sh


# Final layer
# https://github.com/dotnet/dotnet-docker/blob/main/src/runtime-deps/6.0/alpine3.18/amd64/Dockerfile
# https://github.com/dotnet/dotnet-docker/blob/main/src/runtime/7.0/alpine3.18/amd64/Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:7.0-alpine as final

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

# Install prerequisites
RUN apk --upgrade --no-cache add \
        icu-data-full \
        icu-libs \
        p7zip \
        tzdata \
        wget
        
# Install VS debug tools
# https://github.com/OmniSharp/omnisharp-vscode/wiki/Attaching-to-remote-processes
RUN wget https://aka.ms/getvsdbgsh \
    && sh getvsdbgsh -v latest -l /vsdbg \
    && rm getvsdbgsh

# Install media tools from latest-stable / v3.18 matching base image version
# HandBrake is only available in edge/testing
# https://pkgs.alpinelinux.org/package/v3.18/community/x86_64/ffmpeg
# https://pkgs.alpinelinux.org/package/edge/testing/x86_64/handbrake
# https://pkgs.alpinelinux.org/package/v3.18/community/x86_64/mediainfo
# https://pkgs.alpinelinux.org/package/v3.18/community/x86_64/mkvtoolnix
RUN apk --upgrade --no-cache add \
        ffmpeg --repository=http://dl-cdn.alpinelinux.org/alpine/latest-stable/community/ \
        handbrake --repository=http://dl-cdn.alpinelinux.org/alpine/edge/testing/ \
        mediainfo --repository=http://dl-cdn.alpinelinux.org/alpine/latest-stable/community/ \
        mkvtoolnix --repository=http://dl-cdn.alpinelinux.org/alpine/latest-stable/community/

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
