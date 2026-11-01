using System.Text.Json.Nodes;
using PlexCleaner;
using ptr727.Utilities;

namespace PlexCleanerTests;

internal static class FakeTools
{
    public static void WriteScript(string fileName, string body, bool executable = true)
    {
        _ = Directory.CreateDirectory(Path.GetDirectoryName(fileName)!);
        File.WriteAllText(fileName, "#!/bin/sh\n" + body);
        File.SetUnixFileMode(
            fileName,
            UnixFileMode.UserRead
                | UnixFileMode.UserWrite
                | (executable ? UnixFileMode.UserExecute : UnixFileMode.None)
        );
    }

    // The sample sidecar supplies the media props, so only the step under test needs a tool
    public static string CopyUnverifiedSample(
        string directory,
        SidecarFile.StatesType state = SidecarFile.StatesType.None
    )
    {
        Tools.FfProbe.Info = new MediaToolInfo(Tools.FfProbe) { Version = "8.0.1" };
        Tools.MkvMerge.Info = new MediaToolInfo(Tools.MkvMerge) { Version = "96.0" };
        Tools.MediaInfo.Info = new MediaToolInfo(Tools.MediaInfo) { Version = "25.10" };

        string sampleDirectory = PlexCleanerFixture.GetSamplesAbsoluteDirectory();
        string mediaFile = Path.Combine(directory, "name.mkv");
        File.Copy(Path.Combine(sampleDirectory, "Sidecar.v5.mkv"), mediaFile);

        JsonNode sidecar = JsonNode.Parse(
            File.ReadAllText(Path.Combine(sampleDirectory, "Sidecar.v5.PlexCleaner"))
        )!;
        sidecar["State"] = (int)state;
        File.WriteAllText(Path.Combine(directory, "name.PlexCleaner"), sidecar.ToJsonString());
        return mediaFile;
    }

    public static int ReadSidecarState(string sidecarPath) =>
        JsonNode.Parse(File.ReadAllText(sidecarPath))!["State"]!.GetValue<int>();

    // A shell case answering an identify with the sample tracks, so a rewrite can restore the language tags
    public static string SampleIdentifyCase(string directory)
    {
        string json = Path.Combine(directory, "MkvMergeInfoData.json");
        File.WriteAllText(
            json,
            StringCompression.Decompress(
                ReadSampleSidecar()["MkvMergeInfoData"]!.GetValue<string>()
            )
        );
        return $"case \"$*\" in *--identify*) cat \"{json}\"; exit 0 ;; esac\n";
    }

    private static JsonNode ReadSampleSidecar() =>
        JsonNode.Parse(
            File.ReadAllText(
                Path.Combine(
                    PlexCleanerFixture.GetSamplesAbsoluteDirectory(),
                    "Sidecar.v5.PlexCleaner"
                )
            )
        )!;

    // The tools return the stored sample data, so a media refresh after a replace can run
    public static void WriteSampleToolData(string directory)
    {
        JsonNode stored = ReadSampleSidecar();
        foreach (string key in new[] { "MediaInfoData", "MkvMergeInfoData", "FfProbeInfoData" })
        {
            File.WriteAllText(
                Path.Combine(directory, key + ".json"),
                StringCompression.Decompress(stored[key]!.GetValue<string>())
            );
        }
        WriteScript(
            Tools.MediaInfo.GetToolPath(),
            $"cat \"{Path.Combine(directory, "MediaInfoData.json")}\"\n"
        );
        WriteScript(
            Tools.FfProbe.GetToolPath(),
            $"cat \"{Path.Combine(directory, "FfProbeInfoData.json")}\"\n"
        );

        // A remux writes its tool name into each temp output
        WriteScript(
            Tools.MkvMerge.GetToolPath(),
            $$"""
            case "$*" in
            *--identify*) cat "{{Path.Combine(directory, "MkvMergeInfoData.json")}}" ;;
            *) for a; do case $a in *.tmp*) echo mkvmerge > "$a" ;; esac; done ;;
            esac

            """
        );
    }
}
