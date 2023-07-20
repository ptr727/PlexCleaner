# Refer to Debian.dotNET.Dockerfile for build plan

# Test image in shell:
# docker run -it --rm --pull always --name Testing mcr.microsoft.com/dotnet/sdk:7.0-jammy /bin/bash
# docker run -it --rm --pull always --name Testing mcr.microsoft.com/dotnet/sdk:8.0-preview-jammy  /bin/bash
# docker run -it --rm --pull always --name Testing ptr727/plexcleaner:savoury-develop /bin/bash
# export DEBIAN_FRONTEND=noninteractive

# Build Dockerfile
# docker buildx build --secret id=SAVOURY_PPA_AUTH,src=./Docker/auth.conf --platform linux/amd64 --tag testing:latest --file ./Docker/Ubuntu.dotNET.Savoury.Dockerfile .

# Test linux/amd64 target
# docker buildx build --secret id=SAVOURY_PPA_AUTH,src=./Docker/auth.conf --load --platform linux/amd64 --tag testing:latest --file ./Docker/Ubuntu.dotNET.Savoury.Dockerfile .
# docker run -it --rm --name Testing testing:latest /bin/bash



# Builder layer
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0-preview-jammy AS builder

# Layer workdir
WORKDIR /Builder

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

# Copy source and unit tests
COPY ./Samples/. ./Samples/.
COPY ./PlexCleanerTests/. ./PlexCleanerTests/.
COPY ./PlexCleaner/. ./PlexCleaner/.

# Enable running a .NET 7 target on .NET 8 preview
ENV DOTNET_ROLL_FORWARD=Major \
    DOTNET_ROLL_FORWARD_PRE_RELEASE=1

# Run unit tests
RUN dotnet test ./PlexCleanerTests/PlexCleanerTests.csproj;

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
FROM mcr.microsoft.com/dotnet/sdk:7.0-jammy AS final

# Image label
ARG LABEL_VERSION="1.0.0.0"
LABEL name="PlexCleaner" \
    version=${LABEL_VERSION} \
    description="Utility to optimize media files for Direct Play in Plex, Emby, Jellyfin" \
    maintainer="Pieter Viljoen <ptr727@users.noreply.github.com>"

# Prevent EULA and confirmation prompts in installers
ENV DEBIAN_FRONTEND=noninteractive

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
        tzdata \
        wget \
    && locale-gen --no-purge en_US en_US.UTF-8

# Set locale to UTF-8 after running locale-gen
# https://github.com/dotnet/dotnet-docker/blob/main/samples/enable-globalization.md
ENV TZ=Etc/UTC \
    LANG=en_US.UTF-8 \
    LANGUAGE=en_US:en \
    LC_ALL=en_US.UTF-8

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
    && rm repo-mediaarea_1.0-21_all.deb

# Install MKVToolNix
# https://mkvtoolnix.download/downloads.html#ubuntu
RUN wget -O /usr/share/keyrings/gpg-pub-moritzbunkus.gpg https://mkvtoolnix.download/gpg-pub-moritzbunkus.gpg \
    && touch /etc/apt/sources.list.d/mkvtoolnix.list \
    && sh -c 'echo "deb [arch=amd64 signed-by=/usr/share/keyrings/gpg-pub-moritzbunkus.gpg] https://mkvtoolnix.download/ubuntu/ $(lsb_release -sc) main" >> /etc/apt/sources.list.d/mkvtoolnix.list' \
    && apt-get update \
    && apt-get install -y mkvtoolnix

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
#       # SAVOURY_PPA_AUTH=${{ secrets.SAVOURY_PPA_AUTH }}
#       secrets: secrets: ${{ matrix.secrets }}=${{ secrets[matrix.secrets] }}

RUN --mount=type=secret,id=SAVOURY_PPA_AUTH ln -s /run/secrets/SAVOURY_PPA_AUTH /etc/apt/auth.conf.d/savoury.conf \
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
COPY /Docker/Version.sh /PlexCleaner
RUN chmod ugo+rwx /PlexCleaner/Version.sh

# Print installed version information
ARG TARGETPLATFORM \
    BUILDPLATFORM
RUN if [ "$BUILDPLATFORM" = "$TARGETPLATFORM" ]; then \
        /PlexCleaner/Version.sh; \
    fi
