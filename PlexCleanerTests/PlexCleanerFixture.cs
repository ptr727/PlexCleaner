using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using PlexCleaner;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using Xunit;

// Create instance once per assembly
[assembly: AssemblyFixture(typeof(PlexCleanerTests.PlexCleanerFixture))]

namespace PlexCleanerTests;

public class PlexCleanerFixture : IDisposable
{
    public PlexCleanerFixture()
    {
        // Create default commandline options and config
        Program.Options = new CommandLineOptions();
        Program.Config = new ConfigFileJsonSchema();
        Program.Config.SetDefaults();

        // Create default logger
        Serilog.Debugging.SelfLog.Enable(Console.Error);
        Log.Logger = new LoggerConfiguration()
            .Enrich.WithThreadId()
            .WriteTo.Console(
                theme: AnsiConsoleTheme.Code,
                restrictedToMinimumLevel: LogEventLevel.Debug,
                outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] <{ThreadId}> {Message}{NewLine}{Exception}",
                formatProvider: CultureInfo.InvariantCulture
            )
            .CreateLogger();
        InsaneGenius.Utilities.LogOptions.Logger = Log.Logger;

        // Get the Samples directory
        Assembly? entryAssembly = Assembly.GetEntryAssembly();
        Debug.Assert(entryAssembly != null);
        string? assemblyDirectory = Path.GetDirectoryName(entryAssembly.Location);
        Debug.Assert(assemblyDirectory != null);
        _samplesDirectory = Path.GetFullPath(Path.Combine(assemblyDirectory, SamplesDirectory));
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        Log.CloseAndFlush();
    }

    public string GetSampleFilePath(string fileName) =>
        Path.GetFullPath(Path.Combine(_samplesDirectory, fileName));

    private readonly string _samplesDirectory;

    // Relative path to Samples
    private const string SamplesDirectory = "../../../../Samples/PlexCleaner";
}
