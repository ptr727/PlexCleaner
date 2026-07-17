#!/bin/bash

# Regression test harness: process the ZFS test dataset through a single PlexCleaner Docker
# image tag and write per-version results/logs for diffing against other runs.
#
# Usage: RegressionTest.sh [quick|full] [tag] [corpus] [plugin...]
#   quick (default)  full-file scan as in production, remux/re-encode to short testsnippets
#   full   also remux/re-encode the complete media (slowest)
#   tag    docker image tag to test, default develop; the runner specifies one tag per run
#   corpus full (default) or reduced - which corpus subdirectory of the test clone to process
#   plugin optional plugin name(s) from the registry to run after processing, default MatroskaHeaderCleanup
#
# Test data provisioning (2026-07-16): the corpus lives in the snapshotted dataset
# hddpool/media/troublesome (full/ + reduced/); each run provisions hddpool/media/test as an
# instant zero-copy ZFS CLONE of the newest corpus snapshot (no rsync, no drift - the clone
# replaces the old PrepTestDataset.sh flow). Rollbacks inside the run use the clone's @backup.
#
# Results are written into a directory named after the image build version (the "version"
# label, e.g. 3.20.7-g3235a8a055 - the same tag used for source code sync and plugin build).
# This makes results durable and version-to-version comparable, and replaces the old manual
# step of copying results into a version-named folder when happy with a release. Re-running
# the same build overwrites the files in that build's directory.
#
# The flow is:
#   1. pull the image
#   2. read the build version from the image "version" label
#   3. create the version-named directory under this folder
#   4. copy the live PlexCleaner.json settings into the directory (durable record)
#   5. write buildinfo.json (channel, version, image digest, run timestamp)
#   6. build the requested plugins from the matching source tag into the directory
#   7. run process + defaultsettings, then each plugin on the processed results, saving into the directory
#
# Settings file PlexCleaner.json is expected to use:
#   "UseSystem": true, "AutoUpdate": false, "RemoveUnwantedLanguageTracks": true,
#   "RemoveDuplicateTracks": true, "DeInterlace": true
# Test data provisioned per run as a ZFS clone (see above); PrepTestDataset.sh is retired.
#
# The plugin assembly is not shipped in the image, so the script clones the PlexCleaner source,
# checks out the git tag matching the image version label (the release tag), and builds the example
# MatroskaHeaderCleanup plugin from it, so the plugin API matches the running image. Requires git and
# the .NET SDK on the host; the custom run is skipped with a warning if they are missing or the version
# tag cannot be checked out.

set -euxo pipefail

# Run as root to allow ZFS snapshot rollback
if [[ "$(id -u)" -ne 0 ]]; then
  echo "This script must be run as root" >&2
  exit 1
fi

# Test mode, default quick
Mode="${1:-quick}"
case "$Mode" in
  quick | full) ;;
  *)
    echo "Usage: $0 [quick|full] [tag]" >&2
    exit 1
    ;;
esac

# Paths
PlexCleanerApp="/PlexCleaner/Debug/PlexCleaner"
MediaPath="/Test/Media"
ConfigPath="/Test/Config"
HostMedia="/data/media/test"
HostConfig="/data/media/PlexCleaner/RegressionTest"
CorpusDataset="hddpool/media/troublesome"
TestDataset="hddpool/media/test"
Snapshot="hddpool/media/test@backup"
Image="docker.io/ptr727/plexcleaner"
# Live master settings copied into each build's version directory
Settings="PlexCleaner.json"
# Example plugins runnable after the process test, name -> "project dll". The post-process plugin runs
# only confirm a plugin loads and runs against the processed dataset; per-plugin behaviour is verified
# in isolation elsewhere, not here.
declare -A PluginRegistry=(
  [MatroskaHeaderCleanup]="Plugins/MatroskaHeaderCleanup/MatroskaHeaderCleanup.csproj MatroskaHeaderCleanup.dll"
  [DtsTimestampRepair]="Plugins/DtsTimestampRepair/DtsTimestampRepair.csproj DtsTimestampRepair.dll"
)
DefaultPlugin="MatroskaHeaderCleanup"
PluginRepo="https://github.com/ptr727/PlexCleaner.git"
PluginBuildDir="/data/media/PlexCleaner/PluginBuild"

# Parallel file-processing thread count; server has ample cores, so double the default of 4
ThreadCount=8

# Process options, always run in parallel
# quick mode keeps full-file scanning but writes short testsnippets to shorten remux and re-encode
ProcessOptions=(--parallel --threadcount "$ThreadCount")
if [[ "$Mode" == "quick" ]]; then
  ProcessOptions+=(--testsnippets)
fi

# Common docker run arguments
# Allocate a TTY only when attached to one, so the script also runs non-interactively (CI, background)
TtyArg=()
[[ -t 0 ]] && TtyArg=(-it)
DockerCommon=(
  "${TtyArg[@]}"
  --rm
  --name PlexCleaner-RegressionTest
  --user nobody:users
  --env TZ=America/Los_Angeles
)

# Provision the test dataset as a zero-copy clone of the newest corpus snapshot.
# Fast path (every run): the existing clone already originates from the newest snapshot -> just
# roll back to its @backup (rollback works even while long-running media containers or SMB
# clients hold the mount in their namespaces; destroy does NOT - Plex/Jellyfin/smbd bind
# /data/media at start).
# Corpus-change path: rename the stale clone aside (rename succeeds where destroy is blocked),
# clone fresh, and opportunistically destroy any retired clones once the holders are gone.
ProvisionDataset() {
  local CorpusSnap Origin
  CorpusSnap="$(zfs list -H -t snapshot -o name -s creation "$CorpusDataset" | tail -1)"
  if [[ -z "$CorpusSnap" ]]; then
    echo "No snapshot found on $CorpusDataset - snapshot the corpus first" >&2
    exit 1
  fi

  Origin="$(zfs get -H -o value origin "$TestDataset" 2>/dev/null || true)"
  if [[ "$Origin" == "$CorpusSnap" ]]; then
    echo "Test clone current ($CorpusSnap) - rolling back"
    zfs rollback "$Snapshot"
    return
  fi

  echo "Provisioning $TestDataset as a clone of $CorpusSnap"
  if zfs list "$TestDataset" >/dev/null 2>&1; then
    zfs rename "$TestDataset" "$TestDataset-retired-$(date +%s)"
  fi
  zfs clone "$CorpusSnap" "$TestDataset"
  zfs snapshot "$Snapshot"

  # best-effort cleanup of retired clones (succeeds once the holders have restarted/closed)
  local Retired
  for Retired in $(zfs list -H -o name 2>/dev/null | grep -E "^$TestDataset-retired-" || true); do
    zfs destroy -r "$Retired" 2>/dev/null &&
      echo "Destroyed retired clone $Retired" ||
      echo "Retired clone $Retired still held; will retry next run" >&2
  done
}

# Restore the test dataset (clone) to its pristine post-provision state
RestoreDataset() {
  echo "Restoring test dataset"
  zfs rollback "$Snapshot"
}

# GetImageVersion Tag -> prints the build version from the image "version" label
GetImageVersion() {
  local Tag="$1"
  docker image inspect "$Image:$Tag" --format '{{ index .Config.Labels "version" }}' | tr -d '\r'
}

# WriteBuildInfo Tag Version VersionDir
# Record the channel and run metadata so results can be identified and ordered later,
# e.g. "the last develop build" vs "the last latest build".
WriteBuildInfo() {
  local Tag="$1"
  local Version="$2"
  local VersionDir="$3"

  local ImageId Digest Created Now
  ImageId="$(docker image inspect "$Image:$Tag" --format '{{ .Id }}' | tr -d '\r')"
  Digest="$(docker image inspect "$Image:$Tag" --format '{{ if .RepoDigests }}{{ index .RepoDigests 0 }}{{ end }}' | tr -d '\r')"
  Created="$(docker image inspect "$Image:$Tag" --format '{{ .Created }}' | tr -d '\r')"
  Now="$(date -u +%Y-%m-%dT%H:%M:%SZ)"

  cat >"$VersionDir/buildinfo.json" <<EOF
{
  "Channel": "$Tag",
  "Version": "$Version",
  "Image": "$Image:$Tag",
  "ImageId": "$ImageId",
  "RepoDigest": "$Digest",
  "ImageCreated": "$Created",
  "RunTimestamp": "$Now",
  "Mode": "$Mode",
  "Corpus": "$Corpus"
}
EOF
}

# RunProcess Tag Version
# Processes the dataset in place; the caller restores before and after so the processed results survive
# for any post-process plugin runs
RunProcess() {
  local Tag="$1"
  local Version="$2"
  local ConfigDir="$ConfigPath/$Version"
  [[ "$Corpus" == "reduced" ]] && ConfigDir="$ConfigPath/$Version-reduced"

  echo "Running PlexCleaner process on $Image:$Tag ($Mode)"
  docker run \
    "${DockerCommon[@]}" \
    --volume "$HostMedia:$MediaPath:rw" \
    --volume "$HostConfig:$ConfigPath:rw" \
    "$Image:$Tag" \
    "$PlexCleanerApp" process \
    --settingsfile="$ConfigDir/$Settings" \
    --logfile="$ConfigDir/PlexCleaner_process.log" \
    "${LogOptions[@]}" \
    --mediafiles="$MediaPath/$Corpus" \
    --resultsfile="$ConfigDir/Results_process.json" \
    "${ProcessOptions[@]}"
}

# BuildPlugin Tag Version VersionDir
# Clone the PlexCleaner source, check out the git tag matching the image version label, and build the
# example plugin into the version directory, so the plugin API matches the running image. Returns
# non-zero (skip) on any failure.
BuildPlugin() {
  local Tag="$1"
  local Version="$2"
  local VersionDir="$3"
  local Project="$4"
  local Dll="$5"

  if ! command -v git >/dev/null || ! command -v dotnet >/dev/null; then
    echo "Skipping plugin build: git and the .NET SDK are required on the host" >&2
    return 1
  fi

  echo "Building $Dll from $Image:$Tag at tag $Version"

  # Clone once, then fetch tags and check out the release tag matching the image version
  if [[ ! -d "$PluginBuildDir/.git" ]]; then
    git clone "$PluginRepo" "$PluginBuildDir" || return 1
  fi
  git -C "$PluginBuildDir" fetch --force --tags origin || return 1
  git -C "$PluginBuildDir" checkout --force "$Version" || return 1

  # Build the plugin (Debug matches the image binary under /PlexCleaner/Debug)
  dotnet build "$PluginBuildDir/$Project" --configuration Debug || return 1

  # Copy the built assembly into the version directory for the custom run
  local Built
  Built="$(find "$PluginBuildDir/.artifacts/bin" -name "$Dll" -print -quit)"
  if [[ -z "$Built" ]]; then
    echo "Skipping plugin build: built assembly $Dll not found" >&2
    return 1
  fi
  cp "$Built" "$VersionDir/$Dll"
}

# RunPlugin Tag Version Dll
# Run the custom command with one plugin against the current (already processed) dataset, no rollback,
# to confirm the plugin loads and runs. Per-plugin log so multiple plugin runs do not overwrite each other
RunPlugin() {
  local Tag="$1"
  local Version="$2"
  local Dll="$3"
  local ConfigDir="$ConfigPath/$Version"
  [[ "$Corpus" == "reduced" ]] && ConfigDir="$ConfigPath/$Version-reduced"

  echo "Running PlexCleaner custom plugin $Dll on $Image:$Tag"
  docker run \
    "${DockerCommon[@]}" \
    --volume "$HostMedia:$MediaPath:rw" \
    --volume "$HostConfig:$ConfigPath:rw" \
    "$Image:$Tag" \
    "$PlexCleanerApp" custom \
    --settingsfile="$ConfigDir/$Settings" \
    --logfile="$ConfigDir/PlexCleaner_custom_${Dll%.dll}.log" \
    "${LogOptions[@]}" \
    --mediafiles="$MediaPath/$Corpus" \
    --pluginassembly="$ConfigDir/$Dll" \
    --parallel --threadcount "$ThreadCount"
}

# RunDefaultSettings Tag Version
RunDefaultSettings() {
  local Tag="$1"
  local Version="$2"
  local ConfigDir="$ConfigPath/$Version"
  [[ "$Corpus" == "reduced" ]] && ConfigDir="$ConfigPath/$Version-reduced"

  echo "Running PlexCleaner defaultsettings on $Image:$Tag"
  docker run \
    "${DockerCommon[@]}" \
    --volume "$HostConfig:$ConfigPath:rw" \
    "$Image:$Tag" \
    "$PlexCleanerApp" defaultsettings \
    --settingsfile="$ConfigDir/PlexCleaner.defaults.json"
}

# RunCreateSchema Tag Version
RunCreateSchema() {
  local Tag="$1"
  local Version="$2"
  local ConfigDir="$ConfigPath/$Version"
  [[ "$Corpus" == "reduced" ]] && ConfigDir="$ConfigPath/$Version-reduced"

  echo "Running PlexCleaner createschema on $Image:$Tag"
  docker run \
    "${DockerCommon[@]}" \
    --volume "$HostConfig:$ConfigPath:rw" \
    "$Image:$Tag" \
    "$PlexCleanerApp" createschema \
    --schemafile="$ConfigDir/PlexCleaner.schema.json"
}

# RunRegressionTests Tag
RunRegressionTests() {
  local Tag="$1"

  docker pull "$Image:$Tag"

  # Name the results directory after the image build version (used for source sync too)
  local Version
  Version="$(GetImageVersion "$Tag")"
  if [[ -z "$Version" ]]; then
    echo "Could not read the version label from $Image:$Tag" >&2
    exit 1
  fi
  echo "Build version for $Image:$Tag is $Version"

  # Create the version directory, overwriting existing files on a re-run of the same build
  # reduced-corpus runs get their own results directory so they never clobber the full baseline
  local VersionDir="$HostConfig/$Version"
  [[ "$Corpus" == "reduced" ]] && VersionDir="$HostConfig/$Version-reduced"
  mkdir -p "$VersionDir"

  # Copy the live settings into the version directory for a durable record of what was run
  cp "$HostConfig/$Settings" "$VersionDir/$Settings"

  WriteBuildInfo "$Tag" "$Version" "$VersionDir"

  # v3.20+ appends to the log file by default and supports --logclear to clear on re-run
  # Debug level logs each tool invocation with its command line, so a failure can be reproduced directly
  local LogOptions=(--logclear --loglevel Debug)

  # Build each requested plugin from the source tag matching the image; skip any that fail to build
  local Plugin Entry Project Dll
  local -a BuiltPlugins=()
  for Plugin in "${Plugins[@]}"; do
    Entry="${PluginRegistry[$Plugin]:-}"
    if [[ -z "$Entry" ]]; then
      echo "Skipping unknown plugin: $Plugin" >&2
      continue
    fi
    read -r Project Dll <<<"$Entry"
    if BuildPlugin "$Tag" "$Version" "$VersionDir" "$Project" "$Dll"; then
      BuiltPlugins+=("$Dll")
    else
      echo "Skipping plugin run, build failed: $Plugin" >&2
    fi
  done

  RunDefaultSettings "$Tag" "$Version"
  # RunCreateSchema "$Tag" "$Version"

  # Provision a pristine clone of the corpus, process it, then run each built plugin on the
  # processed results to confirm it loads and runs; per-plugin behaviour is verified in isolation
  ProvisionDataset
  RunProcess "$Tag" "$Version"
  for Dll in "${BuiltPlugins[@]}"; do
    RunPlugin "$Tag" "$Version" "$Dll"
  done
  RestoreDataset
}

echo "Starting tests in $Mode mode"

# Single image tag to test (default develop), corpus selection (full|reduced subdirectory of the
# test clone, default full), and optional plugins to run after processing (default the
# MatroskaHeaderCleanup example), e.g.
#   RegressionTest.sh quick develop reduced DtsTimestampRepair MatroskaHeaderCleanup
Tag="${2:-develop}"
Corpus="${3:-full}"
case "$Corpus" in
  full | reduced) ;;
  *)
    echo "Usage: $0 [quick|full] [tag] [full|reduced] [plugin...]" >&2
    exit 1
    ;;
esac
Plugins=("${@:4}")
[[ ${#Plugins[@]} -eq 0 ]] && Plugins=("$DefaultPlugin")
RunRegressionTests "$Tag"

echo "Done with tests"
