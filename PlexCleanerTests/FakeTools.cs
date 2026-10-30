using System.Text.Json.Nodes;
using PlexCleaner;

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
    public static string CopyUnverifiedSample(string directory)
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
        sidecar["State"] = (int)SidecarFile.StatesType.None;
        File.WriteAllText(Path.Combine(directory, "name.PlexCleaner"), sidecar.ToJsonString());
        return mediaFile;
    }
}
