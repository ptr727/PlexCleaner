# Description: Ubuntu LTS and Savoury PPA (Jammy 22.04)
# Based on:ubuntu:jammy
# .NET: Ubuntu repository
# Platforms: linux/amd64
# Tag: ptr727/plexcleaner:savoury

# Test image in shell:
# docker run -it --rm --pull always --name Testing ubuntu:jammy /bin/bash
# docker run -it --rm --pull always --name Testing ptr727/plexcleaner:savoury /bin/bash
# export DEBIAN_FRONTEND=noninteractive

# Build Dockerfile
# docker buildx create --name "plexcleaner" --use
# docker buildx build --secret id=SAVOURY_PPA_AUTH,src=./Docker/auth.conf --platform linux/amd64 --tag testing:latest --file ./Docker/Ubuntu.Savoury.Dockerfile .

# Test linux/amd64 target
# docker buildx build --secret id=SAVOURY_PPA_AUTH,src=./Docker/auth.conf --load --platform linux/amd64 --tag testing:latest --file ./Docker/Ubuntu.Savoury.Dockerfile .
# docker run -it --rm --name Testing testing:latest /bin/bash


# Builder layer
FROM --platform=$BUILDPLATFORM ubuntu:jammy AS builder

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
RUN apt-get update \
    && apt-get upgrade -y

# Install .NET SDK
RUN apt-get install -y --no-install-recommends \
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
FROM --platform=$BUILDPLATFORM ubuntu:jammy AS final

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
        gpg-agent \
        locales \
        locales-all \
        lsb-release \
        software-properties-common \
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
RUN apt-get install -y --no-install-recommends \
        dotnet-runtime-8.0

# Install VS debug tools
# https://github.com/OmniSharp/omnisharp-vscode/wiki/Attaching-to-remote-processes
RUN wget https://aka.ms/getvsdbgsh \
    && sh getvsdbgsh -v latest -l /vsdbg \
    && rm getvsdbgsh

# Install MediaInfo
# https://mediaarea.net/en/MediaInfo/Download/Ubuntu
# https://mediaarea.net/en/Repos
RUN wget -O repo-mediaarea_all.deb https://mediaarea.net/repo/deb/repo-mediaarea_1.0-24_all.deb \
    && dpkg -i repo-mediaarea_all.deb \
    && apt-get update \
    && apt-get install -y --no-install-recommends mediainfo \
    && rm repo-mediaarea_all.deb

# Install MKVToolNix
# https://mkvtoolnix.download/downloads.html#ubuntu
RUN wget -O /usr/share/keyrings/gpg-pub-moritzbunkus.gpg https://mkvtoolnix.download/gpg-pub-moritzbunkus.gpg \
    && touch /etc/apt/sources.list.d/mkvtoolnix.list \
    && sh -c 'echo "deb [arch=amd64 signed-by=/usr/share/keyrings/gpg-pub-moritzbunkus.gpg] https://mkvtoolnix.download/ubuntu/ $(lsb_release -sc) main" >> /etc/apt/sources.list.d/mkvtoolnix.list' \
    && apt-get update \
    && apt-get install -y --no-install-recommends mkvtoolnix

# Install FfMpeg and HandBrake from Rob Savoury's private PPA
# https://launchpad.net/~savoury1
# https://launchpad.net/~/+archivesubscriptions
# https://launchpad.net/~savoury1/+archive/ubuntu/ffmpeg6
# https://launchpad.net/~savoury1/+archive/ubuntu/handbrake

# Use docker secrets and link the secret file to the filesystem auth.conf
# auth.conf: "machine private-ppa.launchpadcontent.net login [username] password [password]"
# https://docs.docker.com/build/building/secrets/
# buildx build --secret id=SAVOURY_PPA_AUTH,src=./Docker/auth.conf
# https://docs.docker.com/build/ci/github-actions/secrets/
# uses: docker/build-push-action@v5
# with:
#   SAVOURY_PPA_AUTH=${{ secrets.SAVOURY_PPA_AUTH }}

RUN --mount=type=secret,id=SAVOURY_PPA_AUTH ln -s /run/secrets/SAVOURY_PPA_AUTH /etc/apt/auth.conf.d/savoury.conf \
    && touch /etc/apt/sources.list.d/savoury.list \
    && sh -c 'echo "deb https://private-ppa.launchpadcontent.net/savoury1/ffmpeg/ubuntu $(lsb_release -sc) main" >> /etc/apt/sources.list.d/savoury.list' \
    && add-apt-repository -y ppa:savoury1/graphics \
    && add-apt-repository -y ppa:savoury1/multimedia \
    && add-apt-repository -y ppa:savoury1/ffmpeg4 \
    && add-apt-repository -y ppa:savoury1/ffmpeg7 \
    && add-apt-repository -y ppa:savoury1/handbrake \
    && apt-get update \
    && apt-get install -y --no-install-recommends \
        ffmpeg \
        handbrake-cli

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
