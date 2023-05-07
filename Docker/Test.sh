#!/bin/sh

# Execute in docker:
# docker run -it --rm --name Testing testing:latest /Test/Test.sh
# docker run \
#   -it \
#   --rm \
#   --pull always \
#   --name PlexCleaner-Test \
#   --env TZ=America/Los_Angeles \
#   --volume /data/media/test:/Test/Media:rw \
#   --volume /data/media/PlexCleaner/PlexCleaner-Develop.json:/Test/PlexCleaner.json:rw \
#   docker.io/ptr727/plexcleaner:savoury-develop \
#   /Test/Test.sh

# Use debug build for testing
PlexCleanerApp=/PlexCleaner/Debug/PlexCleaner

# Test files
MediaPath=/Test/Media

# Settings file
SettingsFile=/Test/PlexCleaner.json

# Test path
TestPath=/Test

# Echo commands
set -x

# Exit on error
set -e

# Test for "/Test/Media" directory
if [ ! -d $MediaPath ]; then
    # Download Matroska test files: https://github.com/ietf-wg-cellar/matroska-test-files
    wget --progress=bar:force -O $TestPath/matroska-test-files.zip https://github.com/ietf-wg-cellar/matroska-test-files/archive/refs/heads/master.zip
    7za e -o$MediaPath $TestPath/matroska-test-files.zip *.mkv -r
    rm $TestPath/matroska-test-files.zip
fi

# Basic commands (no settings file required)
$PlexCleanerApp --version
$PlexCleanerApp --help
$PlexCleanerApp defaultsettings --settingsfile $TestPath/PlexCleaner.default.json
$PlexCleanerApp createschema --schemafile $TestPath/PlexCleaner.schema.json

# Test for "/Test/PlexCleaner.json" file
if [ ! -e $SettingsFile ]; then
    # Use default config
    cp $TestPath/PlexCleaner.default.json $SettingsFile
fi

# Basic commands (settings file required)
$PlexCleanerApp getversioninfo --settingsfile $SettingsFile
$PlexCleanerApp checkfornewtools  --settingsfile $SettingsFile

# Not readily testable
# $PlexCleanerApp monitor --settingsfile $SettingsFile --mediafiles $MediaPath

# File processing commands (settings file required, media files required)
# Take care of order of commands to not interfere with sidecar logic

# Run process first as it is the most prominent command to test
$PlexCleanerApp process --settingsfile $SettingsFile --logfile $TestPath/PlexCleaner.log --logwarning --mediafiles $MediaPath

$PlexCleanerApp updatesidecar --settingsfile $SettingsFile --mediafiles $MediaPath
$PlexCleanerApp getsidecarinfo --settingsfile $SettingsFile --mediafiles $MediaPath
$PlexCleanerApp gettagmap --settingsfile $SettingsFile --mediafiles $MediaPath
$PlexCleanerApp getmediainfo --settingsfile $SettingsFile --mediafiles $MediaPath
$PlexCleanerApp gettoolinfo --settingsfile $SettingsFile --mediafiles $MediaPath
$PlexCleanerApp remux --settingsfile $SettingsFile --mediafiles $MediaPath
$PlexCleanerApp reencode --settingsfile $SettingsFile --mediafiles $MediaPath
$PlexCleanerApp deinterlace --settingsfile $SettingsFile --mediafiles $MediaPath
$PlexCleanerApp removesubtitles --settingsfile $SettingsFile --mediafiles $MediaPath

# Run createsidecar after sidecar state is no longer required
$PlexCleanerApp createsidecar --settingsfile $SettingsFile --mediafiles $MediaPath

# Echo the process command log for easy inspection
cat $TestPath/PlexCleaner.log
