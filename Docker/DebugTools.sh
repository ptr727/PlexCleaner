#!/bin/sh

# Echo commands
set -x

# Exit on error
set -e

# Install VS debug tools to /vsdbg
# https://learn.microsoft.com/en-us/visualstudio/debugger/remote-debugging-dotnet-core-linux-with-ssh
# https://github.com/OmniSharp/omnisharp-vscode/wiki/Attaching-to-remote-processes
echo "Installing VS debug tools to /vsdbg"
wget -O ./getvsdbg.sh https://aka.ms/getvsdbgsh
chmod ugo+rwx getvsdbg.sh
./getvsdbg.sh -v latest -l /vsdbg
rm getvsdbg.sh

# Install .NET diagnostic tools /dotnet-tools
# https://learn.microsoft.com/en-us/dotnet/core/diagnostics/tools-overview
# https://github.com/dotnet/diagnostics/blob/main/documentation/single-file-tools.md
RID=$(dotnet --info | grep "RID" | awk '{print $2}')
echo "Installing .NET diagnostic tools for $RID to /dotnet-tools"
mkdir -p /dotnet-tools
wget -O /dotnet-tools/dotnet-counters https://aka.ms/dotnet-counters/$RID
chmod ugo+rwx /dotnet-tools/dotnet-counters
wget -O /dotnet-tools/dotnet-dump https://aka.ms/dotnet-dump/$RID
chmod ugo+rwx /dotnet-tools/dotnet-dump
