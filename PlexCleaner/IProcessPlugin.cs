using System.Diagnostics.CodeAnalysis;

namespace PlexCleaner;

public static class PluginApi
{
    // Bumped when the IProcessPlugin or IPluginHost contract changes in a breaking way
    public const int Version = 1;
}

public interface IPluginHost
{
    // Compare against the PluginApi.Version the plugin was built against
    int PluginApiVersion { get; }

    // Informational host details for finer compatibility decisions
    string ApplicationVersion { get; }
    string OperatingSystem { get; }
    string Runtime { get; }

    // Plugin log events flow to the host sinks and end-of-run summary. Fully qualified so plugin authors
    // are not left guessing between Serilog.ILogger and Microsoft.Extensions.Logging.ILogger
    [SuppressMessage(
        "Style",
        "IDE0001:Simplify Names",
        Justification = "Disambiguate the public plugin logger type"
    )]
    Serilog.ILogger Logger { get; }
}

public interface IProcessPlugin
{
    // Used in logs and as the processing task name
    string Name { get; }

    // Called once before processing, return false to abort when incompatible with the host
    bool Initialize(IPluginHost host);

    // Called once per media file, reuse the public processing API, return false on failure
    bool ProcessFile(string fileName);
}
