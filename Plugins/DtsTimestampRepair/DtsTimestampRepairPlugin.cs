using Serilog;

namespace PlexCleaner.Plugins.DtsTimestampRepair;

// Retroactively repair files that an older PlexCleaner version marked RepairFailed because verify
// wrongly rejected a benign non-monotonic-DTS muxer warning. Re-verifies each RepairFailed file, clears
// the flag when the only problem is timestamps, and losslessly rewrites the timestamps (setts) when the
// DTS is demux-visible. Reuses PlexCleaner.ProcessFile.RepairTimestamps.
public sealed class DtsTimestampRepairPlugin : IProcessPlugin
{
    private ILogger _logger = Log.Logger;

    // The PlexCleaner version whose public API this plugin was built and tested against. PluginApi.Version
    // only guards the plugin contract; the ProcessFile methods this plugin calls can change in any release,
    // so pin against the tested application version as well.
    private const string TestedApplicationVersion = "3.21";

    public string Name => "DtsTimestampRepair";

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
        // this plugin calls could have changed
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

    // Reduce a version string like "3.21.1.0" to "3.21" to compare against the tested major.minor
    private static string MajorMinor(string version)
    {
        string[] parts = version.Split('.');
        return parts.Length >= 2 ? $"{parts[0]}.{parts[1]}" : version;
    }

    public bool ProcessFile(string fileName)
    {
        // The driver passes every file, skip anything that is not a Matroska file
        if (!SidecarFile.IsMkvFile(fileName))
        {
            return true;
        }

        ProcessFile processFile = new(fileName);
        if (!processFile.GetMediaProps())
        {
            return false;
        }

        // Only revisit files a previous run gave up on
        if (!processFile.State.HasFlag(SidecarFile.StatesType.RepairFailed))
        {
            return true;
        }

        // Re-verify and clear the RepairFailed flag when the failure was a benign timestamp issue,
        // losslessly repairing the timestamps when possible, RepairTimestamps keeps the sidecar in sync
        bool modified = false;
        return processFile.RepairTimestamps(ref modified);
    }
}
