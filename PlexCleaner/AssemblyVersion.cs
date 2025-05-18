using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Serilog;

namespace PlexCleaner;

public static class AssemblyVersion
{
    public static string GetAppVersion() => $"{GetName()} : {GetFileVersion()} ({GetBuildType()})";

    public static string GetRuntimeVersion() =>
        $"{RuntimeInformation.FrameworkDescription} : {RuntimeInformation.RuntimeIdentifier}";

    public static string GetBuildType()
    {
#if DEBUG
        const string build = "Debug";
#else
        const string build = "Release";
#endif
        return build;
    }

    public static string GetName() => GetAssembly().GetName().Name;

    public static string GetInformationalVersion() =>
        // E.g. 1.2.3+abc123.abc123
        GetAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

    public static string GetFileVersion() =>
        // E.g. 1.2.3.4
        GetAssembly().GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;

    public static string GetReleaseVersion() =>
        // E.g. 1.2.3 part of 1.2.3+abc123.abc123
        // Use major.minor.build from informational version
        GetInformationalVersion().Split('+', '-')[0];

    public static DateTime GetBuildDate() =>
        // Use assembly modified time as build date
        // https://stackoverflow.com/questions/1600962/displaying-the-build-date
        File.GetLastWriteTime(GetAssembly().Location).ToLocalTime();

    public static string GetNormalizedRuntimeIdentifier()
    {
        // https://learn.microsoft.com/en-us/dotnet/core/rid-catalog
        string rid = RuntimeInformation.RuntimeIdentifier;
        if (
            rid
            is "win-x64"
                or "win-x86"
                or "win-arm64"
                or "linux-x64"
                or "linux-musl-x64"
                or "linux-musl-arm64"
                or "linux-arm"
                or "linux-arm64"
                or "linux-bionic-arm64"
                or "linux-loongarch64"
                or "osx-x64"
                or "osx-arm64"
        )
        {
            // Already normalized
            return rid;
        }

        // RID needs to be normalized
        // E.g.
        // alpine.3.21-x64 -> linux-musl-x64
        // alpine.3.21-arm64 -> linux-musl-arm64
        // ubuntu.24.10-x64 -> linux-x64
        // ubuntu.24.10-arm64 -> linux-arm64

        // Determine architecture
        if (!rid.Contains('-'))
        {
            Log.Error("Unable to determine RID architecture : {RID}", rid);
            return rid;
        }
        string architecture = rid[(rid.LastIndexOf('-') + 1)..];

        // Determine OS and variant
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return $"win-{architecture}";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return rid.Contains("alpine") ? $"linux-musl-{architecture}" : $"linux-{architecture}";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return $"osx-{architecture}";
        }

        Log.Error("Unable to determine RID OS variant : {RID}", rid);
        return rid;
    }

    private static Assembly GetAssembly()
    {
        Assembly assembly = Assembly.GetEntryAssembly();
        assembly ??= Assembly.GetExecutingAssembly();
        return assembly;
    }
}
