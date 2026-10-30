#!/bin/bash

# Run dotnet-counters against the in-container PlexCleaner process.
# The single-file tool needs a writable extract dir, and the app is PID 1 in the container.
# From the host, `docker exec <container> counters` opens a live monitor of the PlexCleaner.Process meter.
# Any other dotnet-counters verb and its arguments pass through, as in `docker exec <container> counters collect`.

set -Eeuo pipefail

export DOTNET_BUNDLE_EXTRACT_BASE_DIR="${DOTNET_BUNDLE_EXTRACT_BASE_DIR:-/tmp}"

if [[ $# -eq 0 ]]; then
    set -- monitor -p 1 --counters PlexCleaner.Process
fi

exec /dotnet-tools/dotnet-counters "$@"
