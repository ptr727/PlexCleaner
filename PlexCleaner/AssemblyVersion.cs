using System.Reflection;
using System.Runtime.InteropServices;

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

    public static string GetName() => GetAssembly().GetName().Name ?? string.Empty;

    public static string GetInformationalVersion() =>
        // E.g. 1.2.3+abc123.abc123
        GetAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? string.Empty;

    public static string GetFileVersion() =>
        // E.g. 1.2.3.4
        GetAssembly().GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version
        ?? string.Empty;

    public static string GetReleaseVersion() =>
        // E.g. 1.2.3 part of 1.2.3+abc123.abc123
        // Use major.minor.build from informational version
        GetInformationalVersion().Split('+', '-')[0];

    private static Assembly GetAssembly()
    {
        Assembly? assembly = Assembly.GetEntryAssembly();
        assembly ??= Assembly.GetExecutingAssembly();
        return assembly;
    }
}
