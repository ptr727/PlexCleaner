#!/bin/bash

set -Eeuo pipefail

Refused=0

Refuse() {
    echo "test.sh: unknown argument: $1, it takes no arguments, see --help" >&2
    Refused=1
    exit 64
}

pids=()
RunChild() {
    local Pid Status=0
    "$@" <&0 &
    Pid=$!
    pids=("$Pid")
    wait "$Pid" || Status=$?
    pids=()
    return "$Status"
}

OnExit() {
    local Status=$?
    if [[ -n "${TestPath-}" ]]; then
        rm -f -- "$TestPath/matroska-test-files.zip"
    fi
    if ((Status == 64 && !Refused)); then
        exit 1
    fi
}

OnSignal() {
    if ((${#pids[@]})); then
        kill -s TERM "${pids[@]}" 2>/dev/null || :
        wait "${pids[@]}" || :
    fi
    echo "test.sh: interrupted by SIG$1" >&2
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
    cat <<'EOF'
Usage: test.sh
Runs the PlexCleaner commands against the media in /Test/Media, downloading the Matroska test files when it is missing.
Uses /Test/PlexCleaner.json when present, otherwise the default settings.
Writes its output under $TMPDIR/PlexCleaner-Test, or /tmp when TMPDIR is unset, so it runs as a non-root user.
Exits 0 on success, 64 for a refused argument, and 1 when the run fails.
Run it as the nonroot user and users group that own the media:
  USER_NONROOT_ID=1001  # Replace with your own, from: id -u nonroot
  USERS_GROUP_ID=100    # Replace with your own, from: getent group users | cut -d: -f3

Test a local build:
  docker run -it --rm --user "${USER_NONROOT_ID:?}:${USERS_GROUP_ID:?}" --name PlexCleaner-Test plexcleaner:ubuntu /Test/test.sh

Test a published image against local media and settings:
  docker run -it --rm --pull always --user "${USER_NONROOT_ID:?}:${USERS_GROUP_ID:?}" --name PlexCleaner-Test \
    --env TZ=Etc/UTC \
    --volume /path/to/media:/Test/Media:rw \
    --volume /path/to/PlexCleaner.json:/Test/PlexCleaner.json:rw \
    docker.io/ptr727/plexcleaner:ubuntu /Test/test.sh
EOF
    exit 0
    ;;
esac
if [[ $# -ne 0 ]]; then
    Refuse "$1"
fi

# Use debug build for testing
PlexCleanerApp=/PlexCleaner/Debug/PlexCleaner

# Test files
MediaPath=/Test/Media

# Settings file
SettingsFile=/Test/PlexCleaner.json

# The image's /Test is root-owned, so output goes under /tmp, which any user can write.
# A fixed path keeps a mounted settings file from registering new media paths on every run.
TestPath="${TMPDIR:-/tmp}/PlexCleaner-Test"
mkdir -p "$TestPath"

set -x

# Test for "/Test/Media" directory
if [ ! -d "$MediaPath" ]; then
    MediaPath="$TestPath/Media"
    # Download Matroska test files: https://github.com/ietf-wg-cellar/matroska-test-files
    RunChild wget --progress=bar:force -O "$TestPath/matroska-test-files.zip" https://github.com/ietf-wg-cellar/matroska-test-files/archive/refs/heads/master.zip
    RunChild 7z e -aoa -o"$MediaPath" "$TestPath/matroska-test-files.zip" '*.mkv' -r
    rm "$TestPath/matroska-test-files.zip"
fi

# Basic commands (no settings file required)
RunChild $PlexCleanerApp --version
RunChild $PlexCleanerApp --help
RunChild $PlexCleanerApp defaultsettings --settingsfile "$TestPath/PlexCleaner.default.json"
RunChild $PlexCleanerApp createschema --schemafile "$TestPath/PlexCleaner.schema.json"

# Test for "/Test/PlexCleaner.json" file
if [ ! -e "$SettingsFile" ]; then
    # Use default config
    SettingsFile="$TestPath/PlexCleaner.json"
    cp "$TestPath/PlexCleaner.default.json" "$SettingsFile"
fi

# Basic commands (settings file required)
RunChild $PlexCleanerApp getversioninfo --settingsfile "$SettingsFile"

# Not supported on Linux
# $PlexCleanerApp checkfornewtools --settingsfile "$SettingsFile"

# File processing commands (settings file required, media files required)
# Take care of order of commands to not interfere with sidecar logic

# Run process command first
RunChild $PlexCleanerApp process --settingsfile "$SettingsFile" --logfile "$TestPath/PlexCleaner.log" --logwarning --mediafiles "$MediaPath" --testsnippets --resultsfile "$TestPath/Results.json"

# Info/Testing commands
RunChild $PlexCleanerApp verify --settingsfile "$SettingsFile" --mediafiles "$MediaPath" --quickscan
RunChild $PlexCleanerApp testmediainfo --settingsfile "$SettingsFile" --mediafiles "$MediaPath"
RunChild $PlexCleanerApp updatesidecar --settingsfile "$SettingsFile" --mediafiles "$MediaPath"
RunChild $PlexCleanerApp getsidecarinfo --settingsfile "$SettingsFile" --mediafiles "$MediaPath"
RunChild $PlexCleanerApp gettagmap --settingsfile "$SettingsFile" --mediafiles "$MediaPath"
RunChild $PlexCleanerApp getmediainfo --settingsfile "$SettingsFile" --mediafiles "$MediaPath"
RunChild $PlexCleanerApp gettoolinfo --settingsfile "$SettingsFile" --mediafiles "$MediaPath"

# Run createsidecar after sidecar state is no longer required
RunChild $PlexCleanerApp createsidecar --settingsfile "$SettingsFile" --mediafiles "$MediaPath"

# Processing commands
RunChild $PlexCleanerApp remux --settingsfile "$SettingsFile" --mediafiles "$MediaPath"
RunChild $PlexCleanerApp reencode --settingsfile "$SettingsFile" --mediafiles "$MediaPath"
RunChild $PlexCleanerApp deinterlace --settingsfile "$SettingsFile" --mediafiles "$MediaPath"
RunChild $PlexCleanerApp removesubtitles --settingsfile "$SettingsFile" --mediafiles "$MediaPath"
RunChild $PlexCleanerApp removeclosedcaptions --settingsfile "$SettingsFile" --mediafiles "$MediaPath" --quickscan

# Not readily testable
# $PlexCleanerApp monitor --settingsfile "$SettingsFile" --mediafiles "$MediaPath"
