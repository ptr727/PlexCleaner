using AwesomeAssertions;
using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

[Collection("Sequential")]
public sealed class OrphanSidecarTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public OrphanSidecarTests() => _ = Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, true);

    [Fact]
    public void ProcessFile_OrphanSidecar_DeletesAndSucceeds()
    {
        string sidecarFile = SidecarFile.GetSidecarName(Path.Combine(_directory, "name.mkv"));
        File.WriteAllText(sidecarFile, "");

        _ = Process
            .ProcessFile(
                sidecarFile,
                out bool modified,
                out bool ignored,
                out SidecarFile.StatesType state,
                out _,
                out string? failedOperation
            )
            .Should()
            .BeTrue();

        _ = File.Exists(sidecarFile).Should().BeFalse();
        _ = modified.Should().BeTrue();
        _ = ignored.Should().BeFalse();
        _ = state.Should().HaveFlag(SidecarFile.StatesType.FileDeleted);
        _ = failedOperation.Should().BeNull();
    }

    [Fact]
    public void ProcessFile_SidecarWithMkv_IgnoresAndKeeps()
    {
        string mediaFile = Path.Combine(_directory, "name.mkv");
        string sidecarFile = SidecarFile.GetSidecarName(mediaFile);
        File.WriteAllText(mediaFile, "");
        File.WriteAllText(sidecarFile, "");

        _ = Process
            .ProcessFile(sidecarFile, out _, out bool ignored, out _, out _, out _)
            .Should()
            .BeTrue();

        _ = ignored.Should().BeTrue();
        _ = File.Exists(sidecarFile).Should().BeTrue();
    }
}
