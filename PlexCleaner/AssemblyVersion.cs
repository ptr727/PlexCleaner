using System;
using System.IO;
using System.Reflection;

namespace PlexCleaner;

internal class AssemblyVersion
{
    public static string GetDetailedVersion()
    {
        return $"{GetName()} : {GetFileVersion()} ({GetBuildType()})";
    }

    public static string GetBuildType()
    {
#if DEBUG
        const string build = "Debug";
#else
        const string build = "Release";
#endif
        return build;
    }

    public static string GetName()
    {
        return GetAssembly().GetName().Name;
    }

    public static string GetInformationalVersion()
    {
        // E.g. 1.2.3+abc123.abc123
        var versionAttribute = GetAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        return versionAttribute?.InformationalVersion;
    }

    public static string GetFileVersion()
    {
        // E.g. 1.2.3.4
        var versionAttribute = GetAssembly().GetCustomAttribute<AssemblyFileVersionAttribute>();
        return versionAttribute?.Version;
    }

    public static string GetReleaseVersion()
    {
        // E.g. 1.2.3 part of 1.2.3+abc123.abc123
        // Use major.minor.build from informational version
        var informationalVersion = GetInformationalVersion();
        return informationalVersion.Split('+', '-')[0];
    }

    public static DateTime GetBuildDate()
    {
        // Use assembly modified time as build date
        // https://stackoverflow.com/questions/1600962/displaying-the-build-date
        return File.GetLastWriteTime(GetAssembly().Location).ToLocalTime();
    }

    private static Assembly GetAssembly()
    {
        var assembly = Assembly.GetEntryAssembly();
        assembly ??= Assembly.GetExecutingAssembly();
        return assembly;
    }
}
