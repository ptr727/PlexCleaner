#!/bin/sh

# Echo commands
set -x

# Exit on error
set -e

# Print version information
# shellcheck source=/dev/null
. /etc/os-release
echo "$PRETTY_NAME"
dotnet --info
/PlexCleaner/PlexCleaner --version
ffmpeg -version
HandBrakeCLI --version
mkvmerge --version
mediainfo --version
