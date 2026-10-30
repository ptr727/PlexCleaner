using AwesomeAssertions;
using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

[Collection("Sequential")]
public sealed class VerifyToolErrorTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly ToolsOptions1 _toolsOptions = Program.Config.ToolsOptions;
    private readonly bool _deleteInvalidFiles = Program.Config.VerifyOptions.DeleteInvalidFiles;

    public VerifyToolErrorTests()
    {
        _ = Directory.CreateDirectory(_directory);
        Program.Config.VerifyOptions.DeleteInvalidFiles = true;

        // An empty tools folder makes every tool fail to launch
        Program.Config.ToolsOptions = new ToolsOptions1
        {
            UseSystem = false,
            RootRelative = false,
            RootPath = _directory,
        };
    }

    public void Dispose()
    {
        Program.Config.ToolsOptions = _toolsOptions;
        Program.Config.VerifyOptions.DeleteInvalidFiles = _deleteInvalidFiles;
        Directory.Delete(_directory, true);
    }

    private string CreateFile(string name)
    {
        string fileName = Path.Combine(_directory, name);
        File.WriteAllText(fileName, "");
        return fileName;
    }

    [Fact]
    public void VerifyMedia_ToolDoesNotRun_IsToolError() =>
        _ = Tools.FfMpeg.VerifyMedia(CreateFile("name.mkv")).Should().Be(VerifyResult.ToolError);

    [Fact]
    public void DeleteFailedFile_AfterToolError_KeepsFiles()
    {
        string mediaFile = CreateFile("name.mkv");
        string sidecarFile = CreateFile("name.PlexCleaner");
        ProcessFile processFile = new(mediaFile);

        bool modified = false;
        _ = processFile.RepairTimestamps(ref modified).Should().BeFalse();
        _ = processFile.DeleteFailedFile().Should().BeFalse();

        _ = File.Exists(mediaFile).Should().BeTrue();
        _ = File.Exists(sidecarFile).Should().BeTrue();
    }
}
