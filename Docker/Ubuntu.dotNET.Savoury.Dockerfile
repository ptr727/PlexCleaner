# Test base image in shell:
# docker run -it --rm --pull always --name Testing mcr.microsoft.com/dotnet/sdk:7.0-jammy /bin/bash
# export DEBIAN_FRONTEND=noninteractive

# Test image in shell:
# docker run -it --rm --pull always --name Testing ptr727/plexcleaner:savoury-develop /bin/bash

# Build Dockerfile
# docker buildx build --secret id=savoury_ppa_auth,src=./Docker/auth.conf --platform linux/amd64 --tag testing:latest --file ./Docker/Ubuntu.dotNET.Savoury.Dockerfile .
# docker buildx build --secret id=savoury_ppa_auth,src=./Docker/auth.conf --progress plain --no-cache --platform linux/amd64 --tag testing:latest --file ./Docker/Ubuntu.dotNET.Savoury.Dockerfile .

# Test linux/amd64 target
# docker buildx build --secret id=savoury_ppa_auth,src=./Docker/auth.conf --load --progress plain --no-cache --platform linux/amd64 --tag testing:latest --file ./Docker/Ubuntu.dotNET.Savoury.Dockerfile .
# docker run -it --rm --name Testing testing:latest /bin/bash



# Builder layer
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/nightly/sdk:8.0-preview-jammy AS builder

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
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:7.0-jammy AS final

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

# Install prerequisites
RUN apt-get update \
    && apt-get upgrade -y \
    && apt-get install -y \
        apt-utils \
        locales \
        locales-all \
        lsb-core \
        software-properties-common \
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

# Install MediaInfo
# https://mediaarea.net/en/MediaInfo/Download/Ubuntu
# https://mediaarea.net/en/Repos
RUN wget https://mediaarea.net/repo/deb/repo-mediaarea_1.0-21_all.deb \
    && dpkg -i repo-mediaarea_1.0-21_all.deb \
    && apt-get update \
    && apt-get install -y mediainfo \
    && rm repo-mediaarea_1.0-21_all.deb \
    && mediainfo --version

# Install MKVToolNix
# https://mkvtoolnix.download/downloads.html#ubuntu
RUN wget -O /usr/share/keyrings/gpg-pub-moritzbunkus.gpg https://mkvtoolnix.download/gpg-pub-moritzbunkus.gpg \
    && touch /etc/apt/sources.list.d/mkvtoolnix.list \
    && sh -c 'echo "deb [arch=amd64 signed-by=/usr/share/keyrings/gpg-pub-moritzbunkus.gpg] https://mkvtoolnix.download/ubuntu/ $(lsb_release -sc) main" >> /etc/apt/sources.list.d/mkvtoolnix.list' \
    && apt-get update \
    && apt-get install -y mkvtoolnix \
    && mkvmerge --version

# Install FfMpeg and HandBrake from Rob Savoury's private PPA
# https://launchpad.net/~savoury1
# https://launchpad.net/~/+archivesubscriptions
# https://launchpad.net/~savoury1/+archive/ubuntu/ffmpeg6
# https://launchpad.net/~savoury1/+archive/ubuntu/handbrake

# Use docker secrets and link the secret file to the filesystem auth.conf
# auth.conf: "machine private-ppa.launchpadcontent.net login [username] password [password]"
# https://docs.docker.com/build/ci/github-actions/secrets/
# Github actions configuration:
#     uses: docker/build-push-action@v4
#     with:
#       secrets: |
#         "savoury_ppa_auth=${{ secrets.SAVOURY_PPA_AUTH }}"

RUN --mount=type=secret,id=savoury_ppa_auth ln -s /run/secrets/savoury_ppa_auth /etc/apt/auth.conf.d/savoury.conf \
    && touch /etc/apt/sources.list.d/savoury.list \
    && sh -c 'echo "deb https://private-ppa.launchpadcontent.net/savoury1/ffmpeg/ubuntu $(lsb_release -sc) main" >> /etc/apt/sources.list.d/savoury.list' \
    && add-apt-repository -y ppa:savoury1/graphics \
    && add-apt-repository -y ppa:savoury1/multimedia \
    && add-apt-repository -y ppa:savoury1/ffmpeg4 \
    && add-apt-repository -y ppa:savoury1/ffmpeg6 \
    && add-apt-repository -y ppa:savoury1/handbrake \
    && apt-get update \
    && apt-get install -y \
        ffmpeg \
        handbrake-cli \
    && ffmpeg -version \
    && HandBrakeCLI --version

# Cleanup
RUN apt-get autoremove -y \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Copy PlexCleaner from builder layer
COPY --from=builder /Builder/Publish/PlexCleaner/. /PlexCleaner
