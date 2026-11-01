using AwesomeAssertions;
using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

[Collection("Sequential")]
public sealed class DeleteFailedFileTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly bool _deleteInvalidFiles = Program.Config.VerifyOptions.DeleteInvalidFiles;

    public DeleteFailedFileTests()
    {
        _ = Directory.CreateDirectory(_directory);
        Program.Config.VerifyOptions.DeleteInvalidFiles = true;
    }

    public void Dispose()
    {
        Program.Config.VerifyOptions.DeleteInvalidFiles = _deleteInvalidFiles;
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

        _ = new ProcessFile(mediaFile).DeleteFailedFile().Should().BeTrue();

        _ = File.Exists(mediaFile).Should().BeFalse();
        _ = File.Exists(sidecarFile).Should().BeTrue();
    }

    [Fact]
    public void MkvFile_DeletesItsSidecar()
    {
        string mediaFile = CreateFile("name.mkv");
        string sidecarFile = CreateFile("name.PlexCleaner");

        _ = new ProcessFile(mediaFile).DeleteFailedFile().Should().BeTrue();

        _ = File.Exists(mediaFile).Should().BeFalse();
        _ = File.Exists(sidecarFile).Should().BeFalse();
    }

    [Fact]
    public void SidecarDeleteThrows_ReturnsTrueAndSetsFileDeleted()
    {
        string mediaFile = CreateFile("name.mkv");
        _ = Directory.CreateDirectory(Path.Combine(_directory, "name.PlexCleaner"));

        ProcessFile processFile = new(mediaFile);
        _ = processFile.DeleteFailedFile().Should().BeTrue();

        _ = File.Exists(mediaFile).Should().BeFalse();
        _ = processFile.State.Should().HaveFlag(SidecarFile.StatesType.FileDeleted);
    }
}
