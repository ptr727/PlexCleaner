#!/bin/sh

#if [ "$(id -u)" -ne 0 ]; then
#  echo 'This script must be run as root' >&2
#  exit 1
#fi

# Prepare test datatset
# hddpool/media/ is mounted as /data/media
# sudo zfs create hddpool/media/test
# Copy troublesome media files to test
# chown -R nobody:users /data/media/Troublesome
# chmod -R u=rwx,g=rwx+s,o=rx /data/media/Troublesome
# rsync -av --delete --progress /data/media/Troublesome/. /data/media/test
# chown -R nobody:users /data/media/test
# chmod -R u=rwx,g=rwx+s,o=rx /data/media/test
# Snaphot test dataset
# zfs snapshot hddpool/media/test@backup
# zfs list hddpool/media/test -t snapshot
# Restore snapshot
# zfs rollback hddpool/media/test@backup
# Run tests
# Restore snapshot
# zfs rollback hddpool/media/test@backup
# Repeat

echo "Restoring Test Dataset"
sudo zfs rollback hddpool/media/test@backup

echo "Pulling Docker Image"
docker pull ptr727/plexcleaner:develop

echo "Running PlexCleaner"
docker run \
  -it \
  --rm \
  --name PlexCleaner-Develop-Test \
  --user nobody:users \
  --env TZ=America/Los_Angeles \
  --volume /data/media/test:/media:rw \
  --volume /data/media/PlexCleaner:/settings:rw \
  ptr727/plexcleaner:develop \
  /PlexCleaner/PlexCleaner \
    --settingsfile /settings/PlexCleaner-Develop.json \
    --logfile /settings/PlexCleaner-Develop-Test.log \
    --parallel \
    process \
    --testsnippets \
    --mediafiles /media

echo "Running PlexCleaner Reprocess"
docker run \
  -it \
  --rm \
  --name PlexCleaner-Develop-Test \
  --user nobody:users \
  --env TZ=America/Los_Angeles \
  --volume /data/media/test:/media:rw \
  --volume /data/media/PlexCleaner:/settings:rw \
  ptr727/plexcleaner:develop \
  /PlexCleaner/PlexCleaner \
    --settingsfile /settings/PlexCleaner-Develop.json \
    --logfile /settings/PlexCleaner-Develop-Test-1.log \
    --parallel \
    process \
    --reprocess 1 \
    --testsnippets \
    --mediafiles /media
