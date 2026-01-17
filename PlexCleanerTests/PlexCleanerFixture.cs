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

    /// <summary>
    /// Creates a temporary copy of sample files in a temp directory, preserving filenames.
    /// Returns the temp directory path and a cleanup action to delete it when done.
    /// </summary>
    public (string TempDirectory, Action Cleanup) CreateTempSampleFilesCopy(
        params string[] fileNames
    )
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        _ = Directory.CreateDirectory(tempDirectory);

        foreach (string fileName in fileNames)
        {
            string sourcePath = GetSampleFilePath(fileName);
            string destPath = Path.Combine(tempDirectory, fileName);
            File.Copy(sourcePath, destPath);
        }

        return (
            tempDirectory,
            () =>
            {
                try
                {
                    if (Directory.Exists(tempDirectory))
                    {
                        Directory.Delete(tempDirectory, recursive: true);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(
                        "Failed to delete temp directory {TempDirectory}: {Exception}",
                        tempDirectory,
                        ex
                    );
                }
            }
        );
    }
}
