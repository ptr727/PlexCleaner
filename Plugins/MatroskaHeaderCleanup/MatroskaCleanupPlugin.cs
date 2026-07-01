using Serilog;

namespace PlexCleaner.Plugins.MatroskaHeaderCleanup;

// Example plugin: re-check and repair the Matroska seek-index structure on files that were already
// Verified, without a full re-verification. Reuses PlexCleaner.ProcessFile.RepairMatroskaStructure.
public sealed class MatroskaCleanupPlugin : IProcessPlugin
{
    private ILogger _logger = Log.Logger;

    // The PlexCleaner version whose public API this plugin was built and tested against. PluginApi.Version
    // only guards the plugin contract; PlexCleaner's public internals (the ProcessFile methods this plugin
    // calls) can change in any release, so a plugin that calls them should also pin against the tested
    // application version. This is hard-coded as an example; a real plugin would inject the referenced
    // PlexCleaner version at compile time.
    private const string TestedApplicationVersion = "3.20";

    public string Name => "MatroskaHeaderCleanup";

    public bool Initialize(IPluginHost host)
    {
        _logger = host.Logger;

        // Refuse to run against an incompatible plugin contract
        if (host.PluginApiVersion != PluginApi.Version)
        {
            _logger.Error(
                "Incompatible plugin API version : host {HostVersion} != plugin {PluginVersion}",
                host.PluginApiVersion,
                PluginApi.Version
            );
            return false;
        }

        // Warn when the running PlexCleaner version differs from the tested version, since the public API
        // this plugin calls could have changed. A stricter plugin could return false here to hard-pin.
        if (
            !MajorMinor(host.ApplicationVersion)
                .Equals(TestedApplicationVersion, StringComparison.Ordinal)
        )
        {
            _logger.Warning(
                "Plugin tested against PlexCleaner {TestedVersion} but host is {HostVersion}, internal APIs may differ",
                TestedApplicationVersion,
                host.ApplicationVersion
            );
        }

        _logger.Information(
            "{Name} initialized : {AppVersion} : {Os}",
            Name,
            host.ApplicationVersion,
            host.OperatingSystem
        );
        return true;
    }

    // Reduce a version string like "3.20.1.0" to "3.20" to compare against the tested major.minor
    private static string MajorMinor(string version)
    {
        string[] parts = version.Split('.');
        return parts.Length >= 2 ? $"{parts[0]}.{parts[1]}" : version;
    }

    public bool ProcessFile(string fileName)
    {
        ProcessFile processFile = new(fileName);
        if (!processFile.GetMediaProps())
        {
            return false;
        }
        bool modified = false;
        return processFile.RepairMatroskaStructure(ref modified);
    }
}
