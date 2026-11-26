#!/bin/bash

set -Eeuo pipefail

Refused=0

Refuse() {
    echo "version.sh: unknown argument: $1, it takes no arguments, see --help" >&2
    Refused=1
    exit 64
}

OnExit() {
    local Status=$?
    if ((Status == 64 && !Refused)); then
        exit 1
    fi
}

OnSignal() {
    echo "version.sh: interrupted by SIG$1" >&2
    trap - "$1"
    kill -s "$1" "$$" || :
    exit "$2"
}

trap OnExit EXIT
trap 'OnSignal INT 130' INT
trap 'OnSignal TERM 143' TERM

case "${1-}" in
-h | --help)
    if [[ $# -ne 1 ]]; then
        Refuse "$2"
    fi
    echo "Usage: version.sh"
    echo "Prints the OS, .NET, PlexCleaner, and media tool versions."
    echo "Exits 0 on success, 64 for a refused argument, and 1 when the run fails."
    exit 0
    ;;
esac
if [[ $# -ne 0 ]]; then
    Refuse "$1"
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
