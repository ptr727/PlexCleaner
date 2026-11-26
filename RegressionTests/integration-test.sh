#!/bin/bash

set -Eeuo pipefail

usage() {
    cat <<'EOF'
Usage: integration-test.sh IMAGE WORKDIR
Runs the image's /Test/test.sh under dotnet-coverage over a synthetic corpus plus the Matroska test files.
Then checks every file's process result against RegressionTests/synthetic/expected-results.json.

IMAGE is a PlexCleaner image tag, such as a local build of this checkout.
WORKDIR must not exist, and receives the media, the results, the log, and the coverage report:
  WORKDIR/out/image.xml     Cobertura report, source paths under /Builder
  WORKDIR/out/Results.json  process results
  WORKDIR/out/PlexCleaner.log

Requires docker, python3, and the restored local .NET tools, from: dotnet tool restore
Exits 0 on success, 64 for a refused argument, and 1 when the run fails.
EOF
}

Refused=0

Refuse() {
    echo "$1" >&2
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
    if ((Status == 64 && !Refused)); then
        exit 1
    fi
}

OnSignal() {
    if ((${#pids[@]})); then
        kill -s TERM "${pids[@]}" 2>/dev/null || :
        wait "${pids[@]}" || :
    fi
    echo "integration-test.sh: interrupted by SIG$1" >&2
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
        Refuse "integration-test.sh: unknown argument: $2, see --help"
    fi
    usage
    exit 0
    ;;
-*)
    Refuse "integration-test.sh: unknown option: $1, see --help"
    ;;
esac
if [[ $# -ne 2 ]]; then
    Refuse "integration-test.sh: expected IMAGE and WORKDIR, see --help"
fi
image="$1"
work="$2"

repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# Pinned, so the expected results do not move with the upstream repository.
matroska_commit=e6965e5ca666322ed93e2748a10a4f132309e005

version="$(python3 -c 'import json, sys; print(json.load(open(sys.argv[1]))["tools"]["dotnet-coverage"]["version"])' "$repo/.config/dotnet-tools.json")"
tool_dir="${NUGET_PACKAGES:-$HOME/.nuget/packages}/dotnet-coverage/$version"
tool_dll=""
if [[ -d "$tool_dir" ]]; then
    tool_dll="$(find "$tool_dir" -name dotnet-coverage.dll -print -quit)"
fi
if [[ -z "$tool_dll" ]]; then
    echo "integration-test.sh: dotnet-coverage $version is not restored, run: dotnet tool restore" >&2
    exit 1
fi

if [[ -e "$work" ]]; then
    Refuse "integration-test.sh: $work exists, give a new directory"
fi
mkdir -p "$work/media" "$work/out"
work="$(cd "$work" && pwd)"

# The image has no python3, so a root throwaway container adds it, then returns the media to the caller.
# shellcheck disable=SC2016 # The container's shell expands the variables, not this one.
RunChild docker run --init --rm --env OWNER="$(id -u):$(id -g)" --env MATROSKA_COMMIT="$matroska_commit" \
    --volume "$repo/RegressionTests/synthetic:/synthetic:ro" --volume "$work/media:/media" \
    --entrypoint /bin/bash "$image" -c '
set -Eeuo pipefail
trap "chown -R \"\$OWNER\" /media" EXIT
apt-get update -qq
apt-get install -y -qq --no-install-recommends python3 >/dev/null
python3 -B /synthetic/synthesize.py --corpus /media
wget -q -O /tmp/matroska.zip "https://github.com/ietf-wg-cellar/matroska-test-files/archive/$MATROSKA_COMMIT.zip"
7z e -bd -aoa -o/media /tmp/matroska.zip "*.mkv" -r >/dev/null
'

# RegisterInvalidFiles writes to the settings file, so the run gets a copy.
cp "$repo/Docker/PlexCleaner.coverage.json" "$work/out/PlexCleaner.json"
RunChild docker run --rm --user "$(id -u):$(id -g)" --env HOME=/tmp --env DOTNET_COVERAGE_TELEMETRY_OPTOUT=1 \
    --volume "$(dirname "$tool_dll"):/dotnet-coverage:ro" --volume "$work/media:/Test/Media" \
    --volume "$work/out:/tmp/PlexCleaner-Test" --volume "$work/out/PlexCleaner.json:/Test/PlexCleaner.json" \
    --entrypoint dotnet "$image" /dotnet-coverage/dotnet-coverage.dll \
    collect --output-format cobertura --output /tmp/PlexCleaner-Test/image.xml /Test/test.sh
if [[ ! -s "$work/out/image.xml" ]]; then
    echo "integration-test.sh: dotnet-coverage wrote no coverage report" >&2
    exit 1
fi

python3 "$repo/RegressionTests/check_results.py" \
    --expected "$repo/RegressionTests/synthetic/expected-results.json" --results "$work/out/Results.json"
