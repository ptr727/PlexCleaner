#!/bin/sh

# Execute in docker:
# docker run -it --rm --pull always --name PlexCleaner-Test --user nobody:users --env TZ=America/Los_Angeles docker.io/ptr727/plexcleaner:savoury-develop /Test/Test.sh

# Download test files
# https://github.com/ietf-wg-cellar/matroska-test-files
# wget --progress=bar:force -O matroska-test-files.zip https://github.com/ietf-wg-cellar/matroska-test-files/archive/refs/heads/master.zip
# unzip -o matroska-test-files.zip
# rm matroska-test-files.zip
# mv ./matroska-test-files-master/test_files/ ./Media

# Echo commands
set -x

# Run through all commands
/PlexCleaner/PlexCleaner --version
/PlexCleaner/PlexCleaner --help
/PlexCleaner/PlexCleaner defaultsettings --settingsfile PlexCleaner.json
/PlexCleaner/PlexCleaner createschema --schemafile PlexCleaner.schema.json
/PlexCleaner/PlexCleaner getversioninfo --settingsfile PlexCleaner.json
/PlexCleaner/PlexCleaner checkfornewtools  --settingsfile=PlexCleaner.json
/PlexCleaner/PlexCleaner createsidecar --settingsfile PlexCleaner.json --mediafiles ./Media
/PlexCleaner/PlexCleaner process --settingsfile PlexCleaner.json --mediafiles ./Media
# /PlexCleaner/PlexCleaner monitor --settingsfile PlexCleaner.json --mediafiles ./Media
/PlexCleaner/PlexCleaner updatesidecar --settingsfile PlexCleaner.json --mediafiles ./Media
/PlexCleaner/PlexCleaner getsidecarinfo --settingsfile PlexCleaner.json --mediafiles ./Media
/PlexCleaner/PlexCleaner gettagmap --settingsfile PlexCleaner.json --mediafiles ./Media
/PlexCleaner/PlexCleaner getmediainfo --settingsfile PlexCleaner.json --mediafiles ./Media
/PlexCleaner/PlexCleaner gettoolinfo --settingsfile PlexCleaner.json --mediafiles ./Media
/PlexCleaner/PlexCleaner remux --settingsfile PlexCleaner.json --mediafiles ./Media
/PlexCleaner/PlexCleaner reencode --settingsfile PlexCleaner.json --mediafiles ./Media
/PlexCleaner/PlexCleaner deinterlace --settingsfile PlexCleaner.json --mediafiles ./Media
/PlexCleaner/PlexCleaner removesubtitles --settingsfile PlexCleaner.json --mediafiles ./Media
