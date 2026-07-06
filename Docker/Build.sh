#!/bin/sh

# Echo commands
set -x

# Exit on error
set -e

# Build the solution
dotnet build ./PlexCleaner/PlexCleaner.csproj

# Test the solution
dotnet test ./PlexCleanerTests/PlexCleanerTests.csproj

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
