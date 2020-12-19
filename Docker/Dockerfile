# Build from latest LTS Ubuntu base image
FROM ubuntu:latest

# Update the version at build time
ARG LABEL_VERSION="1.0.0.0"

LABEL name="PlexCleaner" \
    version=${LABEL_VERSION} \
    description="Utility to optimize media files for DirectPlay on Plex" \
    maintainer="Pieter Viljoen <ptr727@users.noreply.github.com>"

# See: https://github.com/dotnet/dotnet-docker/blob/master/3.0/sdk/bionic/amd64/Dockerfile
ENV DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_USE_POLLING_FILE_WATCHER=true \
    NUGET_XMLDOC_MODE=skip

# Prevent EULA and confirmation prompts in installers
ENV DEBIAN_FRONTEND=noninteractive

# Install prerequisites
RUN apt-get update \
    && apt-get upgrade -y \
    && apt-get install -y wget apt-transport-https lsb-release software-properties-common p7zip-full \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Install .NET 5
RUN wget -q https://packages.microsoft.com/config/ubuntu/$(lsb_release -sr)/packages-microsoft-prod.deb \
    && dpkg -i packages-microsoft-prod.deb \
    && apt-get update \
    && apt-get install -y dotnet-5.0 \
    && dotnet --version \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Install FfMpeg
RUN add-apt-repository -y ppa:savoury1/ffmpeg4 \
    && apt-get update \
    && apt-get install -y ffmpeg \
    && ffmpeg -version \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Install MediaInfo
RUN wget -q https://mediaarea.net/repo/deb/repo-mediaarea_1.0-13_all.deb \
    && dpkg -i repo-mediaarea_1.0-13_all.deb \
    && apt-get update \
    && apt-get install -y mediainfo \
    && mediainfo --version \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Install HandBrake
RUN add-apt-repository -y ppa:stebbins/handbrake-releases \
    && apt-get update \
    && apt-get install -y handbrake-cli \
    && HandBrakeCLI --version \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Install MKVToolNix
RUN wget -q -O - https://mkvtoolnix.download/gpg-pub-moritzbunkus.txt | apt-key add - \
    && sh -c 'echo "deb https://mkvtoolnix.download/ubuntu/ $(lsb_release -sc) main" >> /etc/apt/sources.list.d/bunkus.org.list' \
    && apt-get update \
    && apt-get install -y mkvtoolnix \
    && mkvmerge --version \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Copy PlexCleaner
COPY PlexCleaner /PlexCleaner
