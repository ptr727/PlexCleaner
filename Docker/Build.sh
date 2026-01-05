#!/bin/sh

# Echo commands
set -x

# Exit on error
set -e

# Build release and debug builds
dotnet publish ./PlexCleaner/PlexCleaner.csproj \
    --arch $TARGETARCH \
    --output ./Build/Release \
    --configuration release \
    -property:PublishAot=false \
    -property:Version=$BUILD_VERSION \
    -property:FileVersion=$BUILD_FILE_VERSION \
    -property:AssemblyVersion=$BUILD_ASSEMBLY_VERSION \
    -property:InformationalVersion=$BUILD_INFORMATION_VERSION \
    -property:PackageVersion=$BUILD_PACKAGE_VERSION

dotnet publish ./PlexCleaner/PlexCleaner.csproj \
    --arch $TARGETARCH \
    --output ./Build/Debug \
    --configuration debug \
    -property:PublishAot=false \
    -property:Version=$BUILD_VERSION \
    -property:FileVersion=$BUILD_FILE_VERSION \
    -property:AssemblyVersion=$BUILD_ASSEMBLY_VERSION \
    -property:InformationalVersion=$BUILD_INFORMATION_VERSION \
    -property:PackageVersion=$BUILD_PACKAGE_VERSION

# Copy build output
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
