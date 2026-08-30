#!/bin/sh

# Echo commands
set -x

# Exit on error
set -e

# Build the solution
dotnet build ./PlexCleaner/PlexCleaner.csproj

# Test the solution
# --project, not a positional path. Under Microsoft.Testing.Platform (global.json) no 10.x SDK honors a
# positional project, and the two SDKs this repo meets differ only in how loudly they say so: 10.0.400
# ignores the path and resolves from the current directory instead, while the newer dotnet-sdk-10.0 apt
# package the builder above installs fails with "Specifying a project for 'dotnet test' should be via
# '--project'". The quiet arm is the worse one here, since /Builder carries no solution and no root
# project for that fallback to find.
dotnet test --project ./PlexCleanerTests/PlexCleanerTests.csproj

# Build release and debug builds
dotnet publish ./PlexCleaner/PlexCleaner.csproj \
    --arch $TARGETARCH \
    -property:PublishDir=$(pwd)/Build/Release/ \
    --configuration release \
    -property:PublishAot=false \
    -property:Version=$BUILD_VERSION \
    -property:FileVersion=$BUILD_FILE_VERSION \
    -property:AssemblyVersion=$BUILD_ASSEMBLY_VERSION \
    -property:InformationalVersion=$BUILD_INFORMATION_VERSION \
    -property:PackageVersion=$BUILD_PACKAGE_VERSION

dotnet publish ./PlexCleaner/PlexCleaner.csproj \
    --arch $TARGETARCH \
    -property:PublishDir=$(pwd)/Build/Debug/ \
    --configuration debug \
    -property:PublishAot=false \
    -property:Version=$BUILD_VERSION \
    -property:FileVersion=$BUILD_FILE_VERSION \
    -property:AssemblyVersion=$BUILD_ASSEMBLY_VERSION \
    -property:InformationalVersion=$BUILD_INFORMATION_VERSION \
    -property:PackageVersion=$BUILD_PACKAGE_VERSION

# Copy configured build target as default output
mkdir -p ./Publish/PlexCleaner/Debug
mkdir -p ./Publish/PlexCleaner/Release
if [ "$BUILD_CONFIGURATION" = "Debug" ] || [ "$BUILD_CONFIGURATION" = "debug" ]
then
    cp -r ./Build/Debug/* ./Publish/PlexCleaner
else
    cp -r ./Build/Release/* ./Publish/PlexCleaner
fi
cp -r ./Build/Debug/* ./Publish/PlexCleaner/Debug
cp -r ./Build/Release/* ./Publish/PlexCleaner/Release
ls -la ./Publish/PlexCleaner
