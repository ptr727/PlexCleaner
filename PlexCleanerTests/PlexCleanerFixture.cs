using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using InsaneGenius.Utilities;
using PlexCleaner;
using PlexCleanerTests;
using Serilog;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using Xunit;
using ConfigFileJsonSchema = PlexCleaner.ConfigFileJsonSchema4;

// Create instance once per assembly
[assembly: AssemblyFixture(typeof(PlexCleanerFixture))]

namespace PlexCleanerTests;

public class PlexCleanerFixture : IDisposable
{
    // Relative path to Samples
    private const string SamplesDirectory = "../../../../Samples/PlexCleaner";

    private readonly string _samplesDirectory;

    public PlexCleanerFixture()
    {
        // Create default commandline options and config
        Program.Options = new CommandLineOptions();
        Program.Config = new ConfigFileJsonSchema();
        Program.Config.SetDefaults();

        // Create default logger
        SelfLog.Enable(Console.Error);
        Log.Logger = new LoggerConfiguration()
            .Enrich.WithThreadId()
            .WriteTo.Console(
                theme: AnsiConsoleTheme.Code,
                restrictedToMinimumLevel: LogEventLevel.Debug,
                outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] <{ThreadId}> {Message}{NewLine}{Exception}",
                formatProvider: CultureInfo.InvariantCulture
            )
            .CreateLogger();
        LogOptions.Logger = Log.Logger;

        // Get the Samples directory
        Assembly entryAssembly = Assembly.GetEntryAssembly();
        Debug.Assert(entryAssembly != null);
        string assemblyDirectory = Path.GetDirectoryName(entryAssembly.Location);
        Debug.Assert(!string.IsNullOrEmpty(assemblyDirectory));
        _samplesDirectory = Path.GetFullPath(Path.Combine(assemblyDirectory, SamplesDirectory));
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        Log.CloseAndFlush();
    }

    public string GetSampleFilePath(string fileName) =>
        Path.GetFullPath(Path.Combine(_samplesDirectory, fileName));
}
