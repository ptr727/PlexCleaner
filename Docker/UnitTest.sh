#!/bin/sh

# Echo commands
set -x

# Exit on error
set -e

# Test
dotnet test ./PlexCleanerTests/PlexCleanerTests.csproj
