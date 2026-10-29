#!/bin/bash

set -Eeuo pipefail

case "${1-}" in
-h | --help)
    if [[ $# -ne 1 ]]; then
        echo "version.sh: unknown argument: $2, it takes no arguments, see --help" >&2
        exit 2
    fi
    echo "Usage: version.sh"
    echo "Prints the OS, .NET, PlexCleaner, and media tool versions."
    exit 0
    ;;
esac
if [[ $# -ne 0 ]]; then
    echo "version.sh: unknown argument: $1, it takes no arguments, see --help" >&2
    exit 2
fi

set -x

# shellcheck source=/dev/null
. /etc/os-release
echo "$PRETTY_NAME"
dotnet --info
/PlexCleaner/PlexCleaner --version
ffmpeg -version
HandBrakeCLI --version
mkvmerge --version
mediainfo --version
