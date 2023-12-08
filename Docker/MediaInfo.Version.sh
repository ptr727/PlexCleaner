#!/bin/sh

# Echo commands
set -x

# Exit on error
set -e

# Print version information
dotnet --info
ffmpeg -version
HandBrakeCLI --version
mkvmerge --version
mediainfo --version
