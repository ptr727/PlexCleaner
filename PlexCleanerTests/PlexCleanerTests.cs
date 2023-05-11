using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using PlexCleaner;

// TODO: Create test script
/*
./PlexCleaner --version
./PlexCleaner --help
./PlexCleaner defaultsettings --settingsfile PlexCleaner.default.json
./PlexCleaner getversioninfo --settingsfile PlexCleaner.json
./PlexCleaner checkfornewtools  --settingsfile=PlexCleaner.json
./PlexCleaner process --settingsfile PlexCleaner.json --mediafiles D:/Test --testsnippets
./PlexCleaner monitor --settingsfile PlexCleaner.json --mediafiles D:/Test
./PlexCleaner remux --settingsfile PlexCleaner.json --mediafiles D:/Test
./PlexCleaner reencode --settingsfile PlexCleaner.json --mediafiles D:/Test
./PlexCleaner deinterlace --settingsfile PlexCleaner.json --mediafiles D:/Test
./PlexCleaner removesubtitles --settingsfile PlexCleaner.json --mediafiles D:/Test
./PlexCleaner createsidecar --settingsfile PlexCleaner.json --mediafiles D:/Test
./PlexCleaner updatesidecar --settingsfile PlexCleaner.json --mediafiles D:/Test
./PlexCleaner getsidecarinfo --settingsfile PlexCleaner.json --mediafiles D:/Test
./PlexCleaner gettagmap --settingsfile PlexCleaner.json --mediafiles D:/Test
./PlexCleaner getmediainfo --settingsfile PlexCleaner.json --mediafiles D:/Test
./PlexCleaner gettoolinfo --settingsfile PlexCleaner.json --mediafiles D:/Test
./PlexCleaner createschema --schemafile PlexCleaner.schema.json
*/

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

    public void Dispose()
    {
    }

    public static FileInfo GetSampleFileInfo(string fileName)
    {
        return new FileInfo(GetSampleFilePath(fileName));
    }

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
