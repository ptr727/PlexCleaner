# Build from teh latest LTS Ubuntu base image
FROM ubuntu:latest

ARG LABEL_VERSION="1.0.0.0"

LABEL name="PlexCleaner" \
    version=${LABEL_VERSION} \
    description="VSCode Server with .NET Core SDK and PowerShell Pre-Installed" \
    maintainer="Pieter Viljoen <ptr727@users.noreply.github.com>"

# See: https://github.com/dotnet/dotnet-docker/blob/master/3.0/sdk/bionic/amd64/Dockerfile
ENV DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_USE_POLLING_FILE_WATCHER=true \
    NUGET_XMLDOC_MODE=skip

# The RUN commands will be cached unless we change the cache tag or the command
# Use the version for the cache tag, allowing new content to be downloaded
ARG CACHE_DATE=${LABEL_VERSION}

# Prevent EULA and confirmation prompts in installers
ENV DEBIAN_FRONTEND=noninteractive

# Install .NET and 3rd party tools
RUN apt-get update \
    # Install prerequisites
    && apt-get install -y wget git apt-transport-https lsb-release software-properties-common p7zip-full \
    # Install .NET 5
    && wget -q https://packages.microsoft.com/config/ubuntu/$(lsb_release -sr)/packages-microsoft-prod.deb \
    && dpkg -i packages-microsoft-prod.deb \
    && apt-get update \
    && apt-get install -y dotnet-sdk-5.0 \
    && dotnet --version \
    # Install FfMpeg
    && add-apt-repository -y ppa:savoury1/ffmpeg4 \
    && apt-get update \
    && apt-get install -y ffmpeg \
    && ffmpeg -version \
    # Install MediaInfo
    && wget -q https://mediaarea.net/repo/deb/repo-mediaarea_1.0-13_all.deb \
    && dpkg -i repo-mediaarea_1.0-13_all.deb \
    && apt-get update \
    && apt-get install -y mediainfo \
    && mediainfo --version \
    # Install HandBrake
    && add-apt-repository -y ppa:stebbins/handbrake-releases \
    && apt-get update \
    && apt-get install -y handbrake-cli \
    && HandBrakeCLI --version \
    # Install MKVToolNix
    && wget -q -O - https://mkvtoolnix.download/gpg-pub-moritzbunkus.txt | sudo apt-key add - \
    && sh -c 'echo "deb https://mkvtoolnix.download/ubuntu/ $(lsb_release -sc) main" >> /etc/apt/sources.list.d/bunkus.org.list' \
    && apt-get update \
    && apt-get install -y mkvtoolnix \
    && mkvmerge --version \
    # Cleanup
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Clone and build code
RUN git clone https://github.com/ptr727/PlexCleaner.git \
    && cd PlexCleaner \
    && dotnet build
