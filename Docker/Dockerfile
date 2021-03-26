# Build from latest LTS Ubuntu base image
FROM ubuntu:latest

# Update the version at build time
ARG LABEL_VERSION="1.0.0.0"

LABEL name="PlexCleaner" \
    version=${LABEL_VERSION} \
    description="Utility to optimize media files for DirectPlay on Plex" \
    maintainer="Pieter Viljoen <ptr727@users.noreply.github.com>"

# https://github.com/dotnet/dotnet-docker/blob/master/src/sdk/5.0/focal/amd64/Dockerfile
ENV DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_USE_POLLING_FILE_WATCHER=true \
    NUGET_XMLDOC_MODE=skip

# Default timezone is UTC
ENV TZ=Etc/UTC

# Prevent EULA and confirmation prompts in installers
ARG DEBIAN_FRONTEND=noninteractive

# Avoid apt-key output should not be parsed warning
ARG APT_KEY_DONT_WARN_ON_DANGEROUS_USAGE=1

# Install prerequisites
RUN apt-get update \
    && apt-get upgrade -y \
    && apt-get install -y apt-utils wget apt-transport-https lsb-release software-properties-common p7zip-full locales locales-all \
    && locale-gen --no-purge en_US en_US.UTF-8 \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Set locale to UTF-8 after running locale-gen
ENV LANG=en_US.UTF-8 \
    LANGUAGE=en_US:en \
    LC_ALL=en_US.UTF-8

# Install .NET 5
# https://docs.microsoft.com/en-us/dotnet/core/install/linux-ubuntu
RUN wget -q https://packages.microsoft.com/config/ubuntu/$(lsb_release -sr)/packages-microsoft-prod.deb \
    && dpkg -i packages-microsoft-prod.deb \
    && apt-get update \
    && apt-get upgrade -y \
    && apt-get install -y aspnetcore-runtime-5.0 \
    && dotnet --info \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Install FfMpeg
# https://launchpad.net/~savoury1/+archive/ubuntu/ffmpeg4
RUN add-apt-repository -y ppa:savoury1/graphics \
    && add-apt-repository -y ppa:savoury1/multimedia \
    && add-apt-repository -y ppa:savoury1/ffmpeg4 \
    && apt-get update \
    && apt-get upgrade -y \
    && apt-get install -y ffmpeg \
    && ffmpeg -version \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Install MediaInfo
# https://mediaarea.net/en/MediaInfo/Download/Ubuntu
RUN wget -q https://mediaarea.net/repo/deb/repo-mediaarea_1.0-16_all.deb \
    && dpkg -i repo-mediaarea_1.0-16_all.deb \
    && apt-get update \
    && apt-get upgrade -y \
    && apt-get install -y mediainfo \
    && mediainfo --version \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Install HandBrake
# https://launchpad.net/~stebbins/+archive/ubuntu/handbrake-releases
RUN add-apt-repository -y ppa:stebbins/handbrake-releases \
    && apt-get update \
    && apt-get upgrade -y \
    && apt-get install -y handbrake-cli \
    && HandBrakeCLI --version \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Install MKVToolNix
# https://mkvtoolnix.download/downloads.html#ubuntu
RUN wget -q -O - https://mkvtoolnix.download/gpg-pub-moritzbunkus.txt | apt-key add - \
    && sh -c 'echo "deb https://mkvtoolnix.download/ubuntu/ $(lsb_release -sc) main" >> /etc/apt/sources.list.d/bunkus.org.list' \
    && apt-get update \
    && apt-get upgrade -y \
    && apt-get install -y mkvtoolnix \
    && mkvmerge --version \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Copy PlexCleaner
COPY PlexCleaner /PlexCleaner
