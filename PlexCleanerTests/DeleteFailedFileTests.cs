using System.Globalization;
using AwesomeAssertions;
using PlexCleaner;
using Serilog.Events;
using Xunit;

namespace PlexCleanerTests;

[Collection("Sequential")]
public sealed class DeleteFailedFileTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly bool _deleteInvalidFiles = Program.Config.VerifyOptions.DeleteInvalidFiles;
    private readonly bool _deleteEmptyFolders = Program.Config.ProcessOptions.DeleteEmptyFolders;
    private readonly bool _deleteUnwantedExtensions = Program
        .Config
        .ProcessOptions
        .DeleteUnwantedExtensions;
    private readonly int _threadCount = Program.Options.ThreadCount;

    public DeleteFailedFileTests()
    {
        _ = Directory.CreateDirectory(_directory);
        Program.Config.VerifyOptions.DeleteInvalidFiles = true;
    }

    public void Dispose()
    {
        Program.Config.VerifyOptions.DeleteInvalidFiles = _deleteInvalidFiles;
        Program.Config.ProcessOptions.DeleteEmptyFolders = _deleteEmptyFolders;
        Program.Config.ProcessOptions.DeleteUnwantedExtensions = _deleteUnwantedExtensions;
        Program.Options.ThreadCount = _threadCount;
        foreach (string file in Directory.EnumerateFiles(_directory))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }
        Directory.Delete(_directory, true);
    }

    private string CreateFile(string name, bool readOnly = false)
    {
        string fileName = Path.Combine(_directory, name);
        File.WriteAllText(fileName, "");
        if (readOnly)
        {
            File.SetAttributes(fileName, FileAttributes.ReadOnly);
        }
        return fileName;
    }

    [Fact]
    public void ReadOnlyMediaFile_IsSkippedNotDeleted()
    {
        string mediaFile = CreateFile("name.mkv", readOnly: true);
        string sidecarFile = CreateFile("name.PlexCleaner");

        _ = Process
            .ProcessFile(mediaFile, out _, out _, out _, out _, out string? failedOperation)
            .Should()
            .BeFalse();

        _ = failedOperation.Should().Be(nameof(ProcessFile.IsWriteable));
        _ = File.Exists(mediaFile).Should().BeTrue();
        _ = File.Exists(sidecarFile).Should().BeTrue();
    }

    [Fact]
    public void ReadOnlySidecarFile_IsSkippedNotDeleted()
    {
        string mediaFile = CreateFile("name.mkv");
        string sidecarFile = CreateFile("name.PlexCleaner", readOnly: true);

        _ = Process
            .ProcessFile(mediaFile, out _, out _, out _, out _, out string? failedOperation)
            .Should()
            .BeFalse();

        _ = failedOperation.Should().Be(nameof(ProcessFile.IsSidecarWriteable));
        _ = File.Exists(mediaFile).Should().BeTrue();
        _ = File.Exists(sidecarFile).Should().BeTrue();
    }

    [Fact]
    public void NonMkvFile_KeepsSiblingMkvSidecar()
    {
        string mediaFile = CreateFile("name.avi");
        string sidecarFile = CreateFile("name.PlexCleaner");

        _ = new ProcessFile(mediaFile)
            .DeleteFailedFile(nameof(ProcessFile.VerifyAndRepair))
            .Should()
            .BeTrue();

        _ = File.Exists(mediaFile).Should().BeFalse();
        _ = File.Exists(sidecarFile).Should().BeTrue();
    }

    [Fact]
    public void MkvFile_DeletesItsSidecar()
    {
        string mediaFile = CreateFile("name.mkv");
        string sidecarFile = CreateFile("name.PlexCleaner");

        _ = new ProcessFile(mediaFile)
            .DeleteFailedFile(nameof(ProcessFile.VerifyAndRepair))
            .Should()
            .BeTrue();

        _ = File.Exists(mediaFile).Should().BeFalse();
        _ = File.Exists(sidecarFile).Should().BeFalse();
    }

    [Fact]
    public void SidecarDeleteThrows_ReturnsTrueAndSetsFileDeleted()
    {
        string mediaFile = CreateFile("name.mkv");
        _ = Directory.CreateDirectory(Path.Combine(_directory, "name.PlexCleaner"));

        ProcessFile processFile = new(mediaFile);
        _ = processFile.DeleteFailedFile(nameof(ProcessFile.VerifyAndRepair)).Should().BeTrue();

        _ = File.Exists(mediaFile).Should().BeFalse();
        _ = processFile.State.Should().HaveFlag(SidecarFile.StatesType.FileDeleted);
    }

    private static List<string> Messages(IReadOnlyList<LogEvent> events, LogEventLevel level) =>
        [
            .. events
                .Where(e => e.Level == level)
                .Select(e => e.RenderMessage(CultureInfo.InvariantCulture)),
        ];

    [Fact]
    public void MkvFile_LogsOperationAndSidecarDelete()
    {
        string mediaFile = CreateFile("name.mkv");
        string sidecarFile = CreateFile("name.PlexCleaner");

        IReadOnlyList<LogEvent> events = LogCapture.Capture(() =>
            new ProcessFile(mediaFile).DeleteFailedFile(nameof(ProcessFile.VerifyAndRepair))
        );

        _ = Messages(events, LogEventLevel.Warning)
            .Should()
            .ContainSingle()
            .Which.Should()
            .StartWith("Deleting media file : Failed VerifyAndRepair : ")
            .And.Contain(mediaFile);
        _ = Messages(events, LogEventLevel.Information)
            .Should()
            .ContainSingle()
            .Which.Should()
            .StartWith("Deleting sidecar file : Media file deleted : ")
            .And.Contain(sidecarFile);
    }

    [Fact]
    public void ToolError_LogsKeepReason()
    {
        string mediaFile = CreateFile("name.mkv");

        bool deleted = true;
        IReadOnlyList<LogEvent> events = LogCapture.Capture(() =>
        {
            using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
            ToolErrorScope.Record();
            deleted = new ProcessFile(mediaFile).DeleteFailedFile(
                nameof(ProcessFile.VerifyAndRepair)
            );
        });

        _ = deleted.Should().BeFalse();
        _ = File.Exists(mediaFile).Should().BeTrue();
        _ = Messages(events, LogEventLevel.Information)
            .Should()
            .ContainSingle()
            .Which.Should()
            .StartWith("Keeping failed file : Tool error : ");
    }

    [Fact]
    public void SidecarDeleteThrows_LogsFailedDelete()
    {
        string mediaFile = CreateFile("name.mkv");
        string sidecarName = Path.Combine(_directory, "name.PlexCleaner");
        _ = Directory.CreateDirectory(sidecarName);

        IReadOnlyList<LogEvent> events = LogCapture.Capture(() =>
            new ProcessFile(mediaFile).DeleteFailedFile(nameof(ProcessFile.VerifyAndRepair))
        );

        _ = Messages(events, LogEventLevel.Error)
            .Should()
            .Contain(message =>
                message.StartsWith("Failed to delete sidecar file : ", StringComparison.Ordinal)
                && message.Contains(sidecarName, StringComparison.Ordinal)
            );
    }

    [Fact]
    public void NonMkvDeleteThrows_LogsFailedDeleteAndPropagates()
    {
        Program.Config.ProcessOptions.DeleteUnwantedExtensions = true;
        string nonMkvName = Path.Combine(_directory, "name.avi");
        _ = Directory.CreateDirectory(nonMkvName);

        Exception? thrown = null;
        IReadOnlyList<LogEvent> events = LogCapture.Capture(() =>
        {
            bool modified = false;
            try
            {
                _ = new ProcessFile(nonMkvName).DeleteNonMkvFile(ref modified);
            }
            catch (UnauthorizedAccessException e)
            {
                thrown = e;
            }
        });

        _ = thrown.Should().NotBeNull();
        _ = Messages(events, LogEventLevel.Warning)
            .Should()
            .ContainSingle()
            .Which.Should()
            .StartWith("Deleting non-MKV file : Unwanted extension : ");
        _ = Messages(events, LogEventLevel.Error)
            .Should()
            .ContainSingle()
            .Which.Should()
            .StartWith("Failed to delete non-MKV file : ")
            .And.Contain(nonMkvName);
    }

    [Fact]
    public void DeleteEmptyFolders_LogsEachFolder()
    {
        Program.Config.ProcessOptions.DeleteEmptyFolders = true;
        Program.Options.ThreadCount = 1;
        string outer = Path.Combine(_directory, "outer");
        string inner = Path.Combine(outer, "inner");
        _ = Directory.CreateDirectory(inner);

        bool result = false;
        IReadOnlyList<LogEvent> events = LogCapture.Capture(() =>
            result = Process.DeleteEmptyFolders([_directory])
        );

        _ = result.Should().BeTrue();
        _ = Directory.Exists(outer).Should().BeFalse();
        List<string> deletes =
        [
            .. Messages(events, LogEventLevel.Information)
                .Where(message =>
                    message.StartsWith("Deleting folder : Empty : ", StringComparison.Ordinal)
                ),
        ];
        _ = deletes.Should().HaveCount(2);
        _ = deletes[0].Should().Contain(inner);
        _ = deletes[1].Should().Contain(outer);
    }
}
