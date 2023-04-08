# Test in docker shell:
# docker run -it --rm --pull always --name Testing archlinux:latest /bin/bash

# Test in develop shell:
# docker run -it --rm --pull always --name Testing --volume /data/media:/media:rw ptr727/plexcleaner:develop /bin/bash

# Build PlexCleaner
# dotnet publish ./PlexCleaner/PlexCleaner.csproj --runtime linux-x64 --self-contained false --output ./Docker/PlexCleaner

# Build Dockerfile
# docker build --tag testing:latest --file=./Docker/Arch.Dockerfile .
# --no-cache --progress=plain



# https://hub.docker.com/_/archlinux
FROM archlinux:latest

# Set the version at build time
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

# https://bugs.archlinux.org/task/77662
# boost-libs

# Install .NET and media processing tools
RUN pacman --sync --noconfirm \
        boost-libs \
        dotnet-sdk \
        ffmpeg \
        handbrake-cli \
        intel-media-sdk \
        mediainfo \
        mkvtoolnix-cli

# Verify installed versions
RUN dotnet --info \
    && mediainfo --version \
    && mkvmerge --version \
    && ffmpeg -version

# Install VS debug tools
# https://github.com/OmniSharp/omnisharp-vscode/wiki/Attaching-to-remote-processes
RUN wget https://aka.ms/getvsdbgsh \
    && sh getvsdbgsh -v latest -l /vsdbg \
    && rm getvsdbgsh

# noconfirm selects default option, not yes
# echo "y\ny" | pacman -Scc
# find /var/cache/pacman/pkg -mindepth 1 -delete

# Cleanup
RUN echo "y\ny" | pacman --sync --noconfirm --clean --clean

# Copy PlexCleaner
COPY ./Docker/PlexCleaner /PlexCleaner
RUN /PlexCleaner/PlexCleaner --version