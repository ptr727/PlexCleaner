#!/bin/bash

set -Eeuo pipefail

case "${1-}" in
-h | --help)
    if [[ $# -ne 1 ]]; then
        echo "build.sh: unknown argument: $2, it takes no arguments, see --help" >&2
        exit 2
    fi
    echo "Usage: build.sh"
    echo "Builds, tests, and publishes PlexCleaner in the Docker builder stage."
    exit 0
    ;;
esac
if [[ $# -ne 0 ]]; then
    echo "build.sh: unknown argument: $1, it takes no arguments, see --help" >&2
    exit 2
fi

set -x

# Build the solution
dotnet build ./PlexCleaner/PlexCleaner.csproj

# Test the solution
# Use --project, since no 10.x SDK honors a positional project under Microsoft.Testing.Platform.
# Some SDKs ignore a positional path and resolve from the current directory, where /Builder holds no solution or project at its root.
dotnet test --project ./PlexCleanerTests/PlexCleanerTests.csproj

# Build release and debug builds
dotnet publish ./PlexCleaner/PlexCleaner.csproj \
    --arch "$TARGETARCH" \
    -property:PublishDir="$(pwd)/Build/Release/" \
    --configuration release \
    -property:PublishAot=false \
    -property:Version="$BUILD_VERSION" \
    -property:FileVersion="$BUILD_FILE_VERSION" \
    -property:AssemblyVersion="$BUILD_ASSEMBLY_VERSION" \
    -property:InformationalVersion="$BUILD_INFORMATION_VERSION" \
    -property:PackageVersion="$BUILD_PACKAGE_VERSION"

dotnet publish ./PlexCleaner/PlexCleaner.csproj \
    --arch "$TARGETARCH" \
    -property:PublishDir="$(pwd)/Build/Debug/" \
    --configuration debug \
    -property:PublishAot=false \
    -property:Version="$BUILD_VERSION" \
    -property:FileVersion="$BUILD_FILE_VERSION" \
    -property:AssemblyVersion="$BUILD_ASSEMBLY_VERSION" \
    -property:InformationalVersion="$BUILD_INFORMATION_VERSION" \
    -property:PackageVersion="$BUILD_PACKAGE_VERSION"

# Copy configured build target as default output
mkdir -p ./Publish/PlexCleaner/Debug
mkdir -p ./Publish/PlexCleaner/Release
if [ "$BUILD_CONFIGURATION" = "Debug" ] || [ "$BUILD_CONFIGURATION" = "debug" ]; then
    cp -r ./Build/Debug/* ./Publish/PlexCleaner
else
    cp -r ./Build/Release/* ./Publish/PlexCleaner
fi
cp -r ./Build/Debug/* ./Publish/PlexCleaner/Debug
cp -r ./Build/Release/* ./Publish/PlexCleaner/Release
ls -la ./Publish/PlexCleaner
