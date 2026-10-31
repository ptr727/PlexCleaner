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
        Program.Config.ProcessOptions.FileIgnoreList = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase
        );
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
    public void RegisterInvalidFiles_ExistingEntry_IsKept()
    {
        _ = Program.Config.ProcessOptions.FileIgnoreList.Add("deleted.mkv");

        bool result = Process.RegisterInvalidFiles(
            [
                CreateResult(
                    "deleted.mkv",
                    SidecarFile.StatesType.VerifyFailed | SidecarFile.StatesType.FileDeleted
                ),
            ],
            Path.Combine(_directory, "settings.json")
        );

        _ = result.Should().BeTrue();
        _ = Program.Config.ProcessOptions.FileIgnoreList.Should().Equal("deleted.mkv");
    }

    [Fact]
    public void FinishProcessing_SettingsWriteFails_ReturnsFalse()
    {
        // A directory at the settings path makes the write throw
        Program.Options.SettingsFile = Path.Combine(_directory, "settings.json");
        Program.Options.ResultsFile = string.Empty;
        _ = Directory.CreateDirectory(Program.Options.SettingsFile);
        Program.Config.VerifyOptions.RegisterInvalidFiles = true;
        ProcessResultJsonSchema resultsJson = new();
        resultsJson.Results.Results.Add(
            CreateResult("failed.mkv", SidecarFile.StatesType.VerifyFailed)
        );

        _ = Process.FinishProcessing(resultsJson).Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FinishProcessing_OneWriteFails_OtherWriteStillRuns(bool settingsFails)
    {
        // A directory at the path, or a missing parent directory, makes the write throw
        string settingsFile = Path.Combine(_directory, "settings.json");
        string resultsFile = Path.Combine(_directory, "results.json");
        if (settingsFails)
        {
            _ = Directory.CreateDirectory(settingsFile);
        }
        else
        {
            resultsFile = Path.Combine(_directory, "missing", "results.json");
        }
        Program.Options.SettingsFile = settingsFile;
        Program.Options.ResultsFile = resultsFile;
        Program.Config.VerifyOptions.RegisterInvalidFiles = true;
        ProcessResultJsonSchema resultsJson = new();
        resultsJson.Results.Results.Add(
            CreateResult("failed.mkv", SidecarFile.StatesType.VerifyFailed)
        );

        _ = Process.FinishProcessing(resultsJson).Should().BeFalse();
        _ = File.Exists(settingsFile).Should().Be(!settingsFails);
        _ = File.Exists(resultsFile).Should().Be(settingsFails);
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
    public void ProcessFiles_ResultsWriteFails_ReportsWriteFailure()
    {
        Program.Config.VerifyOptions.RegisterInvalidFiles = false;
        Program.Options.ResultsFile = Path.Combine(_directory, "missing", "results.json");

        _ = Process.ProcessFiles([], out bool writeFailed).Should().BeTrue();
        _ = writeFailed.Should().BeTrue();
    }

    [Fact]
    public void ProcessFiles_ResultsWriteSucceeds_ReportsNoFailure()
    {
        Program.Config.VerifyOptions.RegisterInvalidFiles = false;
        Program.Options.ResultsFile = Path.Combine(_directory, "results.json");

        _ = Process.ProcessFiles([], out bool writeFailed).Should().BeTrue();
        _ = writeFailed.Should().BeFalse();
        _ = File.Exists(Program.Options.ResultsFile).Should().BeTrue();
    }

    [Fact]
    public void RegisterInvalidFiles_MissingFileInExistingFolder_IsPruned()
    {
        string present = Path.Combine(_directory, "present.mkv");
        File.WriteAllText(present, string.Empty);
        string missing = Path.Combine(_directory, "missing.mkv");
        string unmounted = Path.Combine(_directory, "unmounted", "share.mkv");
        Program.Config.ProcessOptions.FileIgnoreList.UnionWith([present, missing, unmounted]);
        string settingsFile = Path.Combine(_directory, "settings.json");

        bool result = Process.RegisterInvalidFiles([], settingsFile);

        _ = result.Should().BeTrue();
        _ = Program
            .Config.ProcessOptions.FileIgnoreList.Should()
            .BeEquivalentTo(present, unmounted);
        _ = File.Exists(settingsFile).Should().BeTrue();
    }

    [Fact]
    public void RegisterInvalidFiles_NothingToPrune_WritesNothing()
    {
        string present = Path.Combine(_directory, "present.mkv");
        File.WriteAllText(present, string.Empty);
        _ = Program.Config.ProcessOptions.FileIgnoreList.Add(present);
        string settingsFile = Path.Combine(_directory, "settings.json");

        _ = Process.RegisterInvalidFiles([], settingsFile).Should().BeTrue();
        _ = File.Exists(settingsFile).Should().BeFalse();
    }
}
