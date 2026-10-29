#!/bin/bash

set -Eeuo pipefail

case "${1-}" in
-h | --help)
    if [[ $# -ne 1 ]]; then
        echo "test.sh: unknown argument: $2, it takes no arguments, see --help" >&2
        exit 2
    fi
    cat <<'EOF'
Usage: test.sh
Runs the PlexCleaner commands against the media in /Test/Media, downloading the Matroska test files when it is missing.
Uses /Test/PlexCleaner.json when present, otherwise the default settings.
Writes its output under /tmp/PlexCleaner-Test, so it runs as a non-root user.
Replace nonroot:users with the user and group that own the media.

Test a local build:
  docker run -it --rm --user "$(id -u nonroot):$(getent group users | cut -d: -f3)" --name PlexCleaner-Test plexcleaner:ubuntu /Test/test.sh

Test a published image against local media and settings:
  docker run -it --rm --pull always --user "$(id -u nonroot):$(getent group users | cut -d: -f3)" --name PlexCleaner-Test \
    --env TZ=America/Los_Angeles \
    --volume /data/media/test:/Test/Media:rw \
    --volume /data/media/PlexCleaner/PlexCleaner-Develop.json:/Test/PlexCleaner.json:rw \
    docker.io/ptr727/plexcleaner:ubuntu /Test/test.sh
EOF
    exit 0
    ;;
esac
if [[ $# -ne 0 ]]; then
    echo "test.sh: unknown argument: $1, it takes no arguments, see --help" >&2
    exit 2
fi

# Use debug build for testing
PlexCleanerApp=/PlexCleaner/Debug/PlexCleaner

# Test files
MediaPath=/Test/Media

# Settings file
SettingsFile=/Test/PlexCleaner.json

# The image's /Test is root-owned, so output goes under /tmp, which any user can write.
# A fixed path keeps a mounted settings file from registering new media paths on every run.
TestPath=/tmp/PlexCleaner-Test
mkdir -p "$TestPath"

set -x

# Test for "/Test/Media" directory
if [ ! -d "$MediaPath" ]; then
    MediaPath="$TestPath/Media"
    # Download Matroska test files: https://github.com/ietf-wg-cellar/matroska-test-files
    wget --progress=bar:force -O "$TestPath/matroska-test-files.zip" https://github.com/ietf-wg-cellar/matroska-test-files/archive/refs/heads/master.zip
    7z e -aoa -o"$MediaPath" "$TestPath/matroska-test-files.zip" '*.mkv' -r
    rm "$TestPath/matroska-test-files.zip"
fi

# Basic commands (no settings file required)
$PlexCleanerApp --version
$PlexCleanerApp --help
$PlexCleanerApp defaultsettings --settingsfile "$TestPath/PlexCleaner.default.json"
$PlexCleanerApp createschema --schemafile "$TestPath/PlexCleaner.schema.json"

# Test for "/Test/PlexCleaner.json" file
if [ ! -e "$SettingsFile" ]; then
    # Use default config
    SettingsFile="$TestPath/PlexCleaner.json"
    cp "$TestPath/PlexCleaner.default.json" "$SettingsFile"
fi

# Basic commands (settings file required)
$PlexCleanerApp getversioninfo --settingsfile "$SettingsFile"

# Not supported on Linux
# $PlexCleanerApp checkfornewtools --settingsfile "$SettingsFile"

# File processing commands (settings file required, media files required)
# Take care of order of commands to not interfere with sidecar logic

# Run process command first
$PlexCleanerApp process --settingsfile "$SettingsFile" --logfile "$TestPath/PlexCleaner.log" --logwarning --mediafiles "$MediaPath" --testsnippets --resultsfile "$TestPath/Results.json"

# Info/Testing commands
$PlexCleanerApp verify --settingsfile "$SettingsFile" --mediafiles "$MediaPath" --quickscan
$PlexCleanerApp testmediainfo --settingsfile "$SettingsFile" --mediafiles "$MediaPath"
$PlexCleanerApp updatesidecar --settingsfile "$SettingsFile" --mediafiles "$MediaPath"
$PlexCleanerApp getsidecarinfo --settingsfile "$SettingsFile" --mediafiles "$MediaPath"
$PlexCleanerApp gettagmap --settingsfile "$SettingsFile" --mediafiles "$MediaPath"
$PlexCleanerApp getmediainfo --settingsfile "$SettingsFile" --mediafiles "$MediaPath"
$PlexCleanerApp gettoolinfo --settingsfile "$SettingsFile" --mediafiles "$MediaPath"

# Run createsidecar after sidecar state is no longer required
$PlexCleanerApp createsidecar --settingsfile "$SettingsFile" --mediafiles "$MediaPath"

# Processing commands
$PlexCleanerApp remux --settingsfile "$SettingsFile" --mediafiles "$MediaPath"
$PlexCleanerApp reencode --settingsfile "$SettingsFile" --mediafiles "$MediaPath"
$PlexCleanerApp deinterlace --settingsfile "$SettingsFile" --mediafiles "$MediaPath"
$PlexCleanerApp removesubtitles --settingsfile "$SettingsFile" --mediafiles "$MediaPath"
$PlexCleanerApp removeclosedcaptions --settingsfile "$SettingsFile" --mediafiles "$MediaPath" --quickscan

# Not readily testable
# $PlexCleanerApp monitor --settingsfile "$SettingsFile" --mediafiles "$MediaPath"
