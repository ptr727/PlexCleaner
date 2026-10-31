using AwesomeAssertions;
using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

[Collection("Sequential")]
public sealed class PostRunWriteTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly HashSet<string> _ignoreList = Program.Config.ProcessOptions.FileIgnoreList;
    private readonly bool _registerInvalidFiles = Program.Config.VerifyOptions.RegisterInvalidFiles;
    private readonly string _resultsFile = Program.Options.ResultsFile;
    private readonly string _settingsFile = Program.Options.SettingsFile;
    private readonly int _threadCount = Program.Options.ThreadCount;
    private readonly List<(MediaTool Tool, MediaToolInfo Info)> _toolInfos =
    [
        .. Tools.GetToolFamilyList().Select(tool => (tool, tool.Info)),
    ];

    public PostRunWriteTests()
    {
        _ = Directory.CreateDirectory(_directory);
        Program.Config.ProcessOptions.FileIgnoreList = new HashSet<string>(StringComparer.Ordinal);
        Program.Options.ThreadCount = 1;

        // The results file lists the tool versions
        _toolInfos.ForEach(item =>
            item.Tool.Info = new MediaToolInfo(item.Tool) { Version = "1.0" }
        );
    }

    public void Dispose()
    {
        Program.Config.ProcessOptions.FileIgnoreList = _ignoreList;
        Program.Config.VerifyOptions.RegisterInvalidFiles = _registerInvalidFiles;
        Program.Options.ResultsFile = _resultsFile;
        Program.Options.SettingsFile = _settingsFile;
        Program.Options.ThreadCount = _threadCount;
        _toolInfos.ForEach(item => item.Tool.Info = item.Info);
        Directory.Delete(_directory, true);
    }

    private static ProcessResultJsonSchema.ProcessResult CreateResult(
        string fileName,
        SidecarFile.StatesType state
    ) =>
        new()
        {
            Result = false,
            OriginalFileName = fileName,
            NewFileName = fileName,
            State = state,
        };

    [Fact]
    public void RegisterInvalidFiles_DeletedFile_IsSkipped()
    {
        string settingsFile = Path.Combine(_directory, "settings.json");
        bool result = Process.RegisterInvalidFiles(
            [
                CreateResult("kept.mkv", SidecarFile.StatesType.VerifyFailed),
                CreateResult(
                    "deleted.mkv",
                    SidecarFile.StatesType.VerifyFailed | SidecarFile.StatesType.FileDeleted
                ),
            ],
            settingsFile
        );

        _ = result.Should().BeTrue();
        _ = Program.Config.ProcessOptions.FileIgnoreList.Should().Equal("kept.mkv");
        _ = File.Exists(settingsFile).Should().BeTrue();
    }

    [Fact]
    public void RegisterInvalidFiles_OnlyDeletedFiles_WritesNothing()
    {
        string settingsFile = Path.Combine(_directory, "settings.json");
        bool result = Process.RegisterInvalidFiles(
            [
                CreateResult(
                    "deleted.mkv",
                    SidecarFile.StatesType.VerifyFailed | SidecarFile.StatesType.FileDeleted
                ),
            ],
            settingsFile
        );

        _ = result.Should().BeTrue();
        _ = Program.Config.ProcessOptions.FileIgnoreList.Should().BeEmpty();
        _ = File.Exists(settingsFile).Should().BeFalse();
    }

    [Fact]
    public void RegisterInvalidFiles_SettingsWriteFails_ReturnsFalse()
    {
        // A directory at the settings path makes the write throw
        string settingsFile = Path.Combine(_directory, "settings.json");
        _ = Directory.CreateDirectory(settingsFile);

        bool result = Process.RegisterInvalidFiles(
            [CreateResult("failed.mkv", SidecarFile.StatesType.VerifyFailed)],
            settingsFile
        );

        _ = result.Should().BeFalse();
    }

    [Fact]
    public void ProcessFiles_ResultsWriteFails_ReturnsFalse()
    {
        Program.Config.VerifyOptions.RegisterInvalidFiles = false;
        Program.Options.ResultsFile = Path.Combine(_directory, "missing", "results.json");

        _ = Process.ProcessFiles([]).Should().BeFalse();
    }

    [Fact]
    public void ProcessFiles_ResultsWriteSucceeds_ReturnsTrue()
    {
        Program.Config.VerifyOptions.RegisterInvalidFiles = false;
        Program.Options.ResultsFile = Path.Combine(_directory, "results.json");

        _ = Process.ProcessFiles([]).Should().BeTrue();
        _ = File.Exists(Program.Options.ResultsFile).Should().BeTrue();
    }
}
