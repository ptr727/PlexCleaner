using Serilog;

namespace PlexCleaner.Plugins.MatroskaHeaderCleanup;

// Example plugin: re-check and repair the Matroska seek-index structure on files that were already
// Verified, without a full re-verification. Reuses PlexCleaner.ProcessFile.RepairMatroskaStructure.
public sealed class MatroskaCleanupPlugin : IProcessPlugin
{
    private ILogger _logger = Log.Logger;

    public string Name => "MatroskaHeaderCleanup";

    public bool Initialize(IPluginHost host)
    {
        _logger = host.Logger;

        // Refuse to run against an incompatible host contract
        if (host.PluginApiVersion != PluginApi.Version)
        {
            _logger.Error(
                "Incompatible plugin API version : host {HostVersion} != plugin {PluginVersion}",
                host.PluginApiVersion,
                PluginApi.Version
            );
            return false;
        }

        _logger.Information(
            "{Name} initialized : {AppVersion} : {Os}",
            Name,
            host.ApplicationVersion,
            host.OperatingSystem
        );
        return true;
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
