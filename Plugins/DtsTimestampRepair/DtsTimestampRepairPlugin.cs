using Serilog;

namespace PlexCleaner.Plugins.DtsTimestampRepair;

// Revisit files a previous run marked RepairFailed and losslessly repair a demux-visible non-monotonic
// DTS with the setts filter, clearing the flag only on a successful repair or a clean re-verify. A
// detected DTS that cannot be repaired stays RepairFailed. Reuses PlexCleaner.ProcessFile.RepairTimestamps.
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

        // Re-verify and losslessly repair a demux-visible DTS. Clear RepairFailed only on success.
        // An unrepairable DTS stays reported. RepairTimestamps keeps the sidecar in sync.
        bool modified = false;
        return processFile.RepairTimestamps(ref modified);
    }
}
