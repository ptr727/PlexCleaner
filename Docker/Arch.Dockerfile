# Refer to Debian.dotNET.Dockerfile for build plan

# Arch is x64 only and there is no MCR with .NET preinstalled

# Test image in shell:
# docker run -it --rm --pull always --name Testing archlinux:latest /bin/bash
# docker run -it --rm --pull always --name Testing ptr727/plexcleaner:arch-develop /bin/bash

# Build Dockerfile
# docker buildx build --platform linux/amd64 --tag testing:latest --file ./Docker/Arch.Dockerfile .

# Test linux/amd64 target
# docker buildx build --progress plain --load --platform linux/amd64 --tag testing:latest --file ./Docker/Arch.Dockerfile .
# docker run -it --rm --name Testing testing:latest /bin/bash



# Builder layer
# Use base image with AUR helpers pre-installed
# https://hub.docker.com/r/greyltc/archlinux-aur
FROM greyltc/archlinux-aur:yay as builder

# Layer workdir
WORKDIR /Builder

# TODO: Switch to .NET 8.0 release
# No MCR image for Arch, install .NET Preview from AUR
# https://aur.archlinux.org/packages/dotnet-sdk-preview-bin
RUN sudo -u ab -D~ bash -c 'yay -Syu --removemake --needed --noprogressbar --noconfirm dotnet-sdk-preview-bin'

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

# Unit Test
COPY ./Docker/UnitTest.sh ./
RUN chmod ugo+rwx ./UnitTest.sh
RUN ./UnitTest.sh

# Build
COPY ./Docker/Build.sh ./
RUN chmod ugo+rwx ./Build.sh
RUN ./Build.sh


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

# TODO: Remove when .NET 8.0 has been releases
# https://aur.archlinux.org/packages/dotnet-sdk-preview-bin
RUN sudo -u ab -D~ bash -c 'yay -Syu --removemake --needed --noprogressbar --noconfirm dotnet-sdk-preview-bin'

# Install VS debug tools
# https://github.com/OmniSharp/omnisharp-vscode/wiki/Attaching-to-remote-processes
RUN wget https://aka.ms/getvsdbgsh \
    && sh getvsdbgsh -v latest -l /vsdbg \
    && rm getvsdbgsh

# Install .NET and media processing tools
# https://archlinux.org/packages/extra/x86_64/dotnet-sdk/
# https://archlinux.org/packages/extra/x86_64/ffmpeg/
# https://archlinux.org/packages/community/x86_64/mediainfo/
# https://archlinux.org/packages/community/x86_64/handbrake-cli/
# https://archlinux.org/packages/extra/x86_64/mkvtoolnix-cli/
RUN pacman --sync --noconfirm \
        # TODO: Enable when .NET 8.0 has been released
        # dotnet-sdk \
        ffmpeg \
        handbrake-cli \
        intel-media-sdk \
        mediainfo \
        mkvtoolnix-cli

# Cleanup
RUN echo "y\ny" | pacman --sync --noconfirm --clean --clean

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
