# Test linux/amd64 target
# docker buildx build --progress plain --load --platform linux/amd64 --tag testing:latest --file ./Docker/Alpine.MediaInfo.Dockerfile .
# docker run -it --rm --name Testing testing:latest /bin/sh

# Builder layer
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS builder

# Layer workdir
WORKDIR /Builder

# Build platform args
ARG \
    TARGETPLATFORM \
    TARGETARCH \
    BUILDPLATFORM

# MediaInfo from repo often fails with segmentation fault errors
# https://github.com/ptr727/PlexCleaner/issues/153
# https://github.com/MediaArea/MediaInfo/issues/707
# TODO: Build from source

# Final layer
# https://github.com/dotnet/dotnet-docker/blob/main/src/runtime-deps/6.0/alpine3.18/amd64/Dockerfile
# https://github.com/dotnet/dotnet-docker/blob/main/src/runtime/8.0/alpine3.18/amd64/Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine as final

ENV LANG=en_US.UTF-8 \
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

# Copy version script
COPY /Docker/MediaInfo.Version.sh /Version.sh
RUN chmod ugo+rwx /Version.sh

# Print version information
ARG TARGETPLATFORM \
    BUILDPLATFORM
RUN if [ "$BUILDPLATFORM" = "$TARGETPLATFORM" ]; then \
        /Version.sh; \
    fi
