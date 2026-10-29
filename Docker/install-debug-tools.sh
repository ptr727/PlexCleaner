#!/bin/bash

set -Eeuo pipefail

case "${1-}" in
-h | --help)
    if [[ $# -ne 1 ]]; then
        echo "install-debug-tools.sh: unknown argument: $2, it takes no arguments, see --help" >&2
        exit 2
    fi
    echo "Usage: install-debug-tools.sh"
    echo "Installs vsdbg to /vsdbg and the .NET diagnostic tools to /dotnet-tools."
    exit 0
    ;;
esac
if [[ $# -ne 0 ]]; then
    echo "install-debug-tools.sh: unknown argument: $1, it takes no arguments, see --help" >&2
    exit 2
fi

set -x

# Install VS debug tools to /vsdbg
# https://learn.microsoft.com/en-us/visualstudio/debugger/remote-debugging-dotnet-core-linux-with-ssh
# https://github.com/OmniSharp/omnisharp-vscode/wiki/Attaching-to-remote-processes
echo "Installing VS debug tools to /vsdbg"
wget -O ./getvsdbg.sh https://aka.ms/getvsdbgsh
chmod ug=rwx,o=rx getvsdbg.sh
./getvsdbg.sh -v latest -l /vsdbg
rm -f getvsdbg.sh

# Get the RID for this OS using the same technique as used by getvsdbg.sh get_dotnet_runtime_id()
__RuntimeID=
get_dotnet_runtime_id() {
    if [ "$(uname)" = "Darwin" ]; then
        if [ "$(uname -m)" = "arm64" ]; then
            __RuntimeID=osx-arm64
        else
            __RuntimeID=osx-x64
        fi
    elif [ "$(uname -m)" = "x86_64" ]; then
        __RuntimeID=linux-x64
        if [ -e /etc/os-release ]; then
            # shellcheck source=/dev/null
            . /etc/os-release
            if [ "$ID" = "alpine" ]; then
                __RuntimeID=linux-musl-x64
            fi
        fi
    elif [ "$(uname -m)" = "armv7l" ]; then
        __RuntimeID=linux-arm
    elif [ "$(uname -m)" = "aarch64" ]; then
        __RuntimeID=linux-arm64
        if [ -e /etc/os-release ]; then
            # shellcheck source=/dev/null
            . /etc/os-release
            if [ "$ID" = "alpine" ]; then
                __RuntimeID=linux-musl-arm64
            # Check to see if we have dpkg to get the real architecture on debian based linux OS.
            elif hash dpkg 2>/dev/null; then
                # Raspbian 32-bit will return aarch64 in 'uname -m', but it can only use the linux-arm debugger
                if [ "$(dpkg --print-architecture)" = "armhf" ]; then
                    echo 'Info: Overriding Runtime ID from linux-arm64 to linux-arm'
                    __RuntimeID=linux-arm
                fi
            fi
        fi
    fi
}
get_dotnet_runtime_id

# Install .NET diagnostic tools to /dotnet-tools
# https://learn.microsoft.com/en-us/dotnet/core/diagnostics/tools-overview
# https://github.com/dotnet/diagnostics/blob/main/documentation/single-file-tools.md
echo "Installing .NET diagnostic tools for $__RuntimeID to /dotnet-tools"
mkdir -p /dotnet-tools
wget -O /dotnet-tools/dotnet-counters https://aka.ms/dotnet-counters/$__RuntimeID
chmod ug=rwx,o=rx /dotnet-tools/dotnet-counters
wget -O /dotnet-tools/dotnet-dump https://aka.ms/dotnet-dump/$__RuntimeID
chmod ug=rwx,o=rx /dotnet-tools/dotnet-dump
