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
        Process.s_ignoreListWritePending = false;

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
        Process.s_ignoreListWritePending = false;
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
        string kept = Path.Combine(_directory, "kept.mkv");
        string deleted = Path.Combine(_directory, "deleted.mkv");
        File.WriteAllText(kept, string.Empty);
        bool result = Process.RegisterInvalidFiles(
            [
                CreateResult(kept, SidecarFile.StatesType.VerifyFailed),
                CreateResult(
                    deleted,
                    SidecarFile.StatesType.VerifyFailed | SidecarFile.StatesType.FileDeleted
                ),
            ],
            settingsFile
        );

        _ = result.Should().BeTrue();
        _ = Program.Config.ProcessOptions.FileIgnoreList.Should().Equal(kept);
        _ = File.Exists(settingsFile).Should().BeTrue();
    }

    [Fact]
    public void RegisterInvalidFiles_ExistingEntry_IsKept()
    {
        string deleted = Path.Combine(_directory, "deleted.mkv");
        File.WriteAllText(deleted, string.Empty);
        _ = Program.Config.ProcessOptions.FileIgnoreList.Add(deleted);

        bool result = Process.RegisterInvalidFiles(
            [
                CreateResult(
                    deleted,
                    SidecarFile.StatesType.VerifyFailed | SidecarFile.StatesType.FileDeleted
                ),
            ],
            Path.Combine(_directory, "settings.json")
        );

        _ = result.Should().BeTrue();
        _ = Program.Config.ProcessOptions.FileIgnoreList.Should().Equal(deleted);
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
                    Path.Combine(_directory, "deleted.mkv"),
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

    [Fact]
    public void RegisterInvalidFiles_CaseMismatchEntry_IsKept()
    {
        string present = Path.Combine(_directory, "Present.mkv");
        File.WriteAllText(present, string.Empty);
        string mismatch = Path.Combine(_directory, "PRESENT.MKV");
        _ = Program.Config.ProcessOptions.FileIgnoreList.Add(mismatch);

        _ = Process
            .RegisterInvalidFiles([], Path.Combine(_directory, "settings.json"))
            .Should()
            .BeTrue();
        _ = Program.Config.ProcessOptions.FileIgnoreList.Should().Equal(mismatch);
    }

    [Fact]
    public void RegisterInvalidFiles_FailedWrite_IsRetriedOnNextCall()
    {
        string settingsFile = Path.Combine(_directory, "settings.json");
        _ = Directory.CreateDirectory(settingsFile);
        List<ProcessResultJsonSchema.ProcessResult> failed =
        [
            CreateResult(
                Path.Combine(_directory, "failed.mkv"),
                SidecarFile.StatesType.VerifyFailed
            ),
        ];
        _ = Process.RegisterInvalidFiles(failed, settingsFile).Should().BeFalse();
        Directory.Delete(settingsFile);

        // Nothing is modified, the pending write still runs
        _ = Process.RegisterInvalidFiles([], settingsFile).Should().BeTrue();
        _ = File.Exists(settingsFile).Should().BeTrue();

        // The flag is cleared, so an unmodified list writes nothing
        File.Delete(settingsFile);
        _ = Process.RegisterInvalidFiles([], settingsFile).Should().BeTrue();
        _ = File.Exists(settingsFile).Should().BeFalse();
    }
}
