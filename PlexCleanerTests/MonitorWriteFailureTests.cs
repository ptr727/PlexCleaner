using AwesomeAssertions;
using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

[Collection("Sequential")]
public sealed class MonitorWriteFailureTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly bool _deleteEmptyFolders = Program.Config.ProcessOptions.DeleteEmptyFolders;
    private readonly HashSet<string> _ignoreList = Program.Config.ProcessOptions.FileIgnoreList;
    private readonly bool _registerInvalidFiles = Program.Config.VerifyOptions.RegisterInvalidFiles;
    private readonly string _resultsFile = Program.Options.ResultsFile;
    private readonly string _settingsFile = Program.Options.SettingsFile;
    private readonly int _threadCount = Program.Options.ThreadCount;
    private readonly List<(MediaTool Tool, MediaToolInfo Info)> _toolInfos =
    [
        .. Tools.GetToolFamilyList().Select(tool => (tool, tool.Info)),
    ];

    public MonitorWriteFailureTests()
    {
        _ = Directory.CreateDirectory(_directory);
        Program.Config.ProcessOptions.FileIgnoreList = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase
        );
        Program.Config.ProcessOptions.DeleteEmptyFolders = true;
        Program.Config.VerifyOptions.RegisterInvalidFiles = true;
        Program.Options.ThreadCount = 1;
        Program.Options.ResultsFile = string.Empty;
        Process.s_ignoreListWritePending = false;

        // A directory at the settings path makes the write throw
        Program.Options.SettingsFile = Path.Combine(_directory, "settings.json");
        _ = Directory.CreateDirectory(Program.Options.SettingsFile);

        // A missing file in an existing folder is pruned, which modifies the list
        _ = Program.Config.ProcessOptions.FileIgnoreList.Add(
            Path.Combine(_directory, "missing.mkv")
        );

        _toolInfos.ForEach(item =>
            item.Tool.Info = new MediaToolInfo(item.Tool) { Version = "1.0" }
        );
    }

    public void Dispose()
    {
        Program.Config.ProcessOptions.DeleteEmptyFolders = _deleteEmptyFolders;
        Program.Config.ProcessOptions.FileIgnoreList = _ignoreList;
        Program.Config.VerifyOptions.RegisterInvalidFiles = _registerInvalidFiles;
        Program.Options.ResultsFile = _resultsFile;
        Program.Options.SettingsFile = _settingsFile;
        Program.Options.ThreadCount = _threadCount;
        Process.s_ignoreListWritePending = false;
        _toolInfos.ForEach(item => item.Tool.Info = item.Info);
        Directory.Delete(_directory, true);
    }

    [Fact]
    public void ProcessChanges_SettingsWriteFails_MonitoringContinues()
    {
        _ = PlexCleaner.Monitor.ProcessChanges([_directory]).Should().BeTrue();
        _ = Process.s_ignoreListWritePending.Should().BeTrue();
    }

    [Fact]
    public void RunProcess_SettingsWriteFails_ReturnsErrorAndDeletesEmptyFolders()
    {
        string empty = Path.Combine(_directory, "empty");
        _ = Directory.CreateDirectory(empty);

        _ = Program.RunProcess([_directory]).Should().NotBe(0);
        _ = Directory.Exists(empty).Should().BeFalse();
    }
}
