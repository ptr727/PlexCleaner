// Current schema version
global using ConfigFileJsonSchema = PlexCleaner.ConfigFileJsonSchema4;

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using PlexCleaner;

namespace PlexCleanerTests;

public class PlexCleanerTests : IDisposable
{
    public PlexCleanerTests()
    {
        // Create defaults for Program Options and Config
        Program.Options = new CommandLineOptions();
        Program.Config = new ConfigFileJsonSchema();
        Program.Config.SetDefaults();
    }

    public void Dispose() => GC.SuppressFinalize(this);

    public static FileInfo GetSampleFileInfo(string fileName) => new(GetSampleFilePath(fileName));

    public static string GetSampleFilePath(string fileName)
    {
        // Get the assembly directory
        var entryAssembly = Assembly.GetEntryAssembly();
        Debug.Assert(entryAssembly != null);
        string? assemblyDirectory = Path.GetDirectoryName(entryAssembly.Location);
        Debug.Assert(assemblyDirectory != null);

        // Get the Samples directory
        string samplesDirectory = Path.GetFullPath(Path.Combine(assemblyDirectory, SamplesDirectory));

        // Create the file path
        return Path.GetFullPath(Path.Combine(samplesDirectory, fileName));
    }

    // Relative path to Samples
    private const string SamplesDirectory = "../../../../Samples/PlexCleaner";
}
