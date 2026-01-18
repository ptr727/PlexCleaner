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

[assembly: AssemblyFixture(typeof(PlexCleanerFixture))]

namespace PlexCleanerTests;

// One instance for all tests in the assembly
public class PlexCleanerFixture : IDisposable
{
    internal static string GetSamplesAbsoluteDirectory()
    {
        // Relative path to the Samples directory from the assembly output directory
        const string samplesDirectory = "../../../../Samples/PlexCleaner";

        // Get absolute path
        Assembly entryAssembly = Assembly.GetEntryAssembly();
        Debug.Assert(entryAssembly != null);
        string assemblyDirectory = Path.GetDirectoryName(entryAssembly.Location);
        Debug.Assert(!string.IsNullOrEmpty(assemblyDirectory));
        return Path.GetFullPath(Path.Combine(assemblyDirectory, samplesDirectory));
    }

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
        GetSamplesDirectory = GetSamplesAbsoluteDirectory();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        Log.CloseAndFlush();
    }

    /// <summary>
    /// Gets the path to a sample file in the read-only samples directory.
    /// Use this only when files will not be modified.
    /// </summary>
    public string GetSampleFilePath(string fileName) =>
        Path.GetFullPath(Path.Combine(GetSamplesDirectory, fileName));

    public string GetSamplesDirectory { get; }
}

// One instance per test and copy of samples in temp directory
public class SamplesFixture : IDisposable
{
    public SamplesFixture()
    {
        GetSamplesDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        CopySamples();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        try
        {
            if (Directory.Exists(GetSamplesDirectory))
            {
                Directory.Delete(GetSamplesDirectory, recursive: true);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(
                "Failed to delete temp samples directory {TempDirectory}: {Exception}",
                GetSamplesDirectory,
                ex
            );
        }
    }

    private void CopySamples()
    {
        string sourceSamplesDirectory = PlexCleanerFixture.GetSamplesAbsoluteDirectory();
        _ = Directory.CreateDirectory(GetSamplesDirectory);
        try
        {
            foreach (
                string sourceFilePath in Directory.EnumerateFiles(
                    sourceSamplesDirectory,
                    "*",
                    SearchOption.AllDirectories
                )
            )
            {
                string relativePath = Path.GetRelativePath(sourceSamplesDirectory, sourceFilePath);
                string destFilePath = Path.Combine(GetSamplesDirectory, relativePath);
                string destDirPath = Path.GetDirectoryName(destFilePath);

                if (!string.IsNullOrEmpty(destDirPath) && !Directory.Exists(destDirPath))
                {
                    _ = Directory.CreateDirectory(destDirPath);
                }

                File.Copy(sourceFilePath, destFilePath, overwrite: true);
            }
        }
        catch (Exception ex)
        {
            try
            {
                if (Directory.Exists(GetSamplesDirectory))
                {
                    Directory.Delete(GetSamplesDirectory, recursive: true);
                }
            }
            catch (Exception cleanupEx)
            {
                Log.Warning(
                    "Failed to cleanup temp directory during exception handling: {Exception}",
                    cleanupEx
                );
            }

            Log.Error("Failed to copy samples to temp directory: {Exception}", ex);
            throw;
        }
    }

    /// <summary>
    /// Gets the path to a sample file in the temp samples directory.
    /// </summary>
    public string GetSampleFilePath(string fileName) =>
        Path.GetFullPath(Path.Combine(GetSamplesDirectory, fileName));

    public string GetSamplesDirectory { get; }
}
