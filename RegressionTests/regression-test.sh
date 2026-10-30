#!/bin/bash

# The corpus lives in the snapshotted dataset hddpool/media/troublesome, with full/ and reduced/ subsets.
# Each run provisions hddpool/media/test as a zero-copy ZFS clone of the newest corpus snapshot.
# Rollbacks inside the run use the clone's @backup snapshot.
#
# Results go into a directory named after the image's "version" label, such as 3.20.7-g3235a8a055, so they stay comparable across versions.
# A reduced-corpus run appends -reduced to that directory name.
# Re-running the same build overwrites the files in that build's directory.
#
# The flow is:
#   1. Pull the image.
#   2. Read the build version from the image "version" label.
#   3. Create the version-named directory under this folder.
#   4. Copy the live PlexCleaner.json settings into the directory as a durable record.
#   5. Write buildinfo.json with the channel, version, image digest, and run timestamp.
#   6. Build the requested plugins from the matching source tag into the directory.
#   7. Run defaultsettings, provision the test dataset, run process, then run each plugin on the processed results.
#
# Settings file PlexCleaner.json is expected to use:
#   "UseSystem": true, "AutoUpdate": false, "RemoveUnwantedLanguageTracks": true,
#   "RemoveDuplicateTracks": true, "DeInterlace": true
#
# The image ships no plugin assembly, so the script builds each plugin from the matching source tag.
# That keeps the plugin API equal to the running image's.
# The build needs git and the .NET SDK on the host.
# A missing tool or an unresolvable tag skips the plugin run with a warning.

set -Eeuo pipefail

Usage() {
    cat <<'EOF'
Usage: regression-test.sh [quick|full] [tag] [full|reduced] [plugin...]
  quick    full-file scan as in production, remux and re-encode to short testsnippets (default)
  full     also remux and re-encode the complete media (slowest)
  tag      Docker image tag to test (default develop)
  corpus   full or reduced corpus subdirectory of the test clone to process (default full)
  plugin   registry plugin names to run after processing (default MatroskaHeaderCleanup)
Runs as root, since it clones and rolls back ZFS datasets.
EOF
}

UnknownArgument() {
    echo "regression-test.sh: unknown argument: '$1'" >&2
    Usage >&2
    exit 2
}

case "${1-}" in
-h | --help)
    [[ $# -eq 1 ]] || UnknownArgument "$2"
    Usage
    exit 0
    ;;
esac
for Arg in "$@"; do
    [[ "$Arg" == -* ]] && UnknownArgument "$Arg"
done

Mode="${1-quick}"
case "$Mode" in
quick | full) ;;
*) UnknownArgument "$Mode" ;;
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
# Example plugins runnable after the process test, name -> "project dll".
# A post-process plugin run only confirms the plugin loads and runs against the processed dataset.
declare -A PluginRegistry=(
    [MatroskaHeaderCleanup]="Plugins/MatroskaHeaderCleanup/MatroskaHeaderCleanup.csproj MatroskaHeaderCleanup.dll"
    [DtsTimestampRepair]="Plugins/DtsTimestampRepair/DtsTimestampRepair.csproj DtsTimestampRepair.dll"
)
DefaultPlugin="MatroskaHeaderCleanup"
PluginRepo="https://github.com/ptr727/PlexCleaner.git"
PluginBuildDir="/data/media/PlexCleaner/PluginBuild"

# Parallel file-processing thread count, doubling the default of 4 since the server has ample cores
ThreadCount=8

# Process options, always run in parallel.
# Quick mode keeps full-file scanning, and writes short test snippets to shorten remux and re-encode.
ProcessOptions=(--parallel --threadcount "$ThreadCount")
if [[ "$Mode" == "quick" ]]; then
    ProcessOptions+=(--testsnippets)
fi

# Run as this host's nonroot user and users group, and abort when either does not exist rather than run as root.
USER_NONROOT_ID=$(id -u nonroot)
USERS_GROUP_ID=$(getent group users | cut -d: -f3)

# Common docker run arguments
# Allocate a TTY only when attached to one, so the script also runs non-interactively (CI, background)
TtyArg=()
[[ -t 0 ]] && TtyArg=(-it)
DockerCommon=(
    "${TtyArg[@]}"
    --rm
    --name PlexCleaner-RegressionTest
    --user "$USER_NONROOT_ID:$USERS_GROUP_ID"
    --env TZ=America/Los_Angeles
)

# Provision the test dataset as a zero-copy clone of the newest corpus snapshot.
# Where the clone already originates from the newest snapshot, the run rolls it back to its @backup.
# A rollback works while media containers or SMB clients hold the mount, where a destroy fails.
# Where the corpus changed, the run renames the stale clone aside, since a rename succeeds where a destroy fails.
# It then clones fresh and destroys any retired clones whose holders are gone.
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

    # Clean up retired clones on a best-effort basis, which succeeds once the holders have restarted or closed.
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
# Record the channel and run metadata, so results can be ordered later, such as the last develop build.
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
# Process the dataset in place.
# The caller restores before and after, so the processed results survive for the post-process plugin runs.
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
# Build the example plugin into the version directory from the git tag matching the image version label.
# Return non-zero on any failure, which skips the plugin run.
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
# Run the custom command with one plugin against the already processed dataset, with no rollback.
# Each plugin writes its own log, so plugin runs do not overwrite each other.
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

    # Create the version directory, overwriting existing files on a re-run of the same build.
    # A reduced-corpus run gets its own results directory, so it never clobbers the full baseline.
    local VersionDir="$HostConfig/$Version"
    [[ "$Corpus" == "reduced" ]] && VersionDir="$HostConfig/$Version-reduced"
    mkdir -p "$VersionDir"

    # Copy the live settings into the version directory for a durable record of what was run
    cp "$HostConfig/$Settings" "$VersionDir/$Settings"

    WriteBuildInfo "$Tag" "$Version" "$VersionDir"

    # Version 3.20 and later append to the log file by default and support --logclear to clear on re-run.
    # Debug level logs each tool invocation with its command line, so a failure can be reproduced directly
    local LogOptions=(--logclear --loglevel Debug)

    # Build each requested plugin from the source tag matching the image, and skip any that fail to build.
    local Plugin Entry Project Dll
    local -a BuiltPlugins=()
    for Plugin in "${Plugins[@]}"; do
        Entry="${PluginRegistry[$Plugin]}"
        read -r Project Dll <<<"$Entry"
        if BuildPlugin "$Tag" "$Version" "$VersionDir" "$Project" "$Dll"; then
            BuiltPlugins+=("$Dll")
        else
            echo "Skipping plugin run, build failed: $Plugin" >&2
        fi
    done

    # Root populates the version directory, but the containers run as nonroot:users and write into it.
    # Hand it to that user so the writes succeed regardless of the parent directory's ownership or ACLs.
    chown -R nonroot:users "$VersionDir"

    RunDefaultSettings "$Tag" "$Version"
    # RunCreateSchema "$Tag" "$Version"

    # Provision a pristine clone of the corpus, process it, then run each built plugin on the results.
    # This only confirms each plugin loads and runs, since per-plugin behavior is verified in isolation.
    ProvisionDataset
    # Roll the clone back to its pristine @backup even if processing or a plugin aborts under set -e.
    # A failed run then never leaves the shared test dataset mutated, and the clean restore disarms the trap.
    trap 'RestoreDataset' EXIT
    RunProcess "$Tag" "$Version"
    for Dll in "${BuiltPlugins[@]}"; do
        RunPlugin "$Tag" "$Version" "$Dll"
    done
    RestoreDataset
    trap - EXIT
}

Tag="${2-develop}"
[[ -n "$Tag" ]] || UnknownArgument "$Tag"
Corpus="${3-full}"
case "$Corpus" in
full | reduced) ;;
*) UnknownArgument "$Corpus" ;;
esac
Plugins=("${@:4}")
[[ ${#Plugins[@]} -eq 0 ]] && Plugins=("$DefaultPlugin")
for Plugin in "${Plugins[@]}"; do
    if [[ -z "$Plugin" || -z "${PluginRegistry[$Plugin]+set}" ]]; then
        echo "Known plugins: ${!PluginRegistry[*]}" >&2
        UnknownArgument "$Plugin"
    fi
done

if [[ "$(id -u)" -ne 0 ]]; then
    echo "This script must be run as root" >&2
    exit 1
fi

set -x
echo "Starting tests in $Mode mode"
RunRegressionTests "$Tag"

echo "Done with tests"
