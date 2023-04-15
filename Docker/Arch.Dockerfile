# Arch is x64 only and there is no MCR with .NET preinstalled

# Test base image in shell:
# docker run -it --rm --pull always --name Testing archlinux:latest /bin/bash

# Test image in shell:
# docker run -it --rm --pull always --name Testing ptr727/plexcleaner:arch-develop /bin/bash

# Build Dockerfile
# docker buildx build --platform linux/amd64 --tag testing:latest --file ./Docker/Arch.Dockerfile .
# docker buildx build --progress plain --no-cache --platform linux/amd64 --tag testing:latest --file ./Docker/Arch.Dockerfile .

# Test linux/amd64 target
# docker buildx build --load --progress plain --no-cache --platform linux/amd64 --tag testing:latest --file ./Docker/Arch.Dockerfile .
# docker run -it --rm --name Testing testing:latest /bin/bash



# Builder layer
FROM archlinux:latest as builder

# Layer workdir
WORKDIR /Builder

# Install .NET SDK
RUN pacman-key --init \
    && pacman --sync --noconfirm --refresh --sysupgrade \
    && pacman --sync --noconfirm dotnet-sdk

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

# Run unit tests
RUN dotnet test ./PlexCleanerTests/PlexCleanerTests.csproj;

# Build release and debug builds
RUN dotnet publish ./PlexCleaner/PlexCleaner.csproj \
        --self-contained false \
        --output ./Build/Release \
        --configuration release \
        -property:Version=$BUILD_VERSION \
        -property:FileVersion=$BUILD_FILE_VERSION \
        -property:AssemblyVersion=$BUILD_ASSEMBLY_VERSION \
        -property:InformationalVersion=$BUILD_INFORMATION_VERSION \
        -property:PackageVersion=$BUILD_PACKAGE_VERSION \
    && dotnet publish ./PlexCleaner/PlexCleaner.csproj \
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
# https://hub.docker.com/_/archlinux
FROM archlinux:latest as final

# Image label
ARG LABEL_VERSION="1.0.0.0"
LABEL name="PlexCleaner" \
    version=${LABEL_VERSION} \
    description="Utility to optimize media files for Direct Play in Plex, Emby, Jellyfin" \
    maintainer="Pieter Viljoen <ptr727@users.noreply.github.com>"

# Default timezone is UTC
ENV TZ=Etc/UTC

# Install prerequisites and do base configuration
RUN pacman-key --init \
    && echo 'en_US.UTF-8 UTF-8' | tee -a /etc/locale.gen \
    && locale-gen \
    && echo 'LANG=en_US.UTF-8' | tee /etc/locale.conf \
    && pacman --sync --noconfirm --refresh --sysupgrade \
    && pacman --sync --noconfirm \
        p7zip \
        wget

# Set locale to UTF-8 after running locale-gen
ENV LANG=en_US.UTF-8 \
    LANGUAGE=en_US:en \
    LC_ALL=en_US.UTF-8

# Install VS debug tools
# https://github.com/OmniSharp/omnisharp-vscode/wiki/Attaching-to-remote-processes
RUN wget https://aka.ms/getvsdbgsh \
    && sh getvsdbgsh -v latest -l /vsdbg \
    && rm getvsdbgsh

# Install .NET and media processing tools
RUN pacman --sync --noconfirm \
        # https://bugs.archlinux.org/task/77662
        boost-libs \
        dotnet-sdk \
        ffmpeg \
        handbrake-cli \
        intel-media-sdk \
        mediainfo \
        mkvtoolnix-cli

# noconfirm selects default option, not yes
# echo "y\ny" | pacman -Scc
# find /var/cache/pacman/pkg -mindepth 1 -delete

# Cleanup
RUN echo "y\ny" | pacman --sync --noconfirm --clean --clean

# Copy PlexCleaner from builder layer
COPY --from=builder /Builder/Publish/PlexCleaner/. /PlexCleaner

# Verify installed versions
RUN dotnet --info \
    && mediainfo --version \
    && mkvmerge --version \
    && ffmpeg -version \
    && /PlexCleaner/PlexCleaner --version
