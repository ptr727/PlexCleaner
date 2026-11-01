using AwesomeAssertions;
using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

[Collection("Sequential")]
public sealed class SidecarRenameStateTests : IDisposable
{
    private const SidecarFile.StatesType Seeded =
        SidecarFile.StatesType.ReMuxed | SidecarFile.StatesType.FileReNamed;

    private readonly string _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly ToolsOptions1 _toolsOptions = Program.Config.ToolsOptions;
    private readonly MediaToolInfo _ffProbeInfo = Tools.FfProbe.Info;
    private readonly MediaToolInfo _mkvMergeInfo = Tools.MkvMerge.Info;
    private readonly MediaToolInfo _mediaInfoInfo = Tools.MediaInfo.Info;

    public SidecarRenameStateTests()
    {
        _ = Directory.CreateDirectory(_directory);
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
        Tools.FfProbe.Info = _ffProbeInfo;
        Tools.MkvMerge.Info = _mkvMergeInfo;
        Tools.MediaInfo.Info = _mediaInfoInfo;
        Directory.Delete(_directory, true);
    }

    [Fact]
    public void Open_SeededStateOverMatchingSidecar_KeepsBothStates()
    {
        // The sidecar name ignores the extension case, so the hash matches
        string mediaFile = FakeTools.CopyUnverifiedSample(
            _directory,
            SidecarFile.StatesType.Verified
        );
        string sidecarPath = Path.Combine(_directory, "name.PlexCleaner");
        string renamed = Path.Combine(_directory, "name.MKV");
        string tempName = Path.ChangeExtension(mediaFile, ".tmp7");
        File.Move(mediaFile, tempName);
        File.Move(tempName, renamed);

        SidecarFile sidecarFile = new(renamed) { State = Seeded };
        _ = sidecarFile.Open().Should().BeTrue();

        SidecarFile.StatesType expected = Seeded | SidecarFile.StatesType.Verified;
        _ = sidecarFile.State.Should().Be(expected);
        _ = FakeTools.ReadSidecarState(sidecarPath).Should().Be((int)expected);
    }

    [Fact]
    public void Open_SeededStateOverStaleSidecar_KeepsSeededAndMarksModified()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        // A remuxed file meets the stale sidecar of the file it replaced
        string mediaFile = FakeTools.CopyUnverifiedSample(
            _directory,
            SidecarFile.StatesType.Verified
        );
        string sidecarPath = Path.Combine(_directory, "name.PlexCleaner");
        File.AppendAllText(mediaFile, "changed");

        FakeTools.WriteSampleToolData(_directory);

        SidecarFile sidecarFile = new(mediaFile) { State = Seeded };
        _ = sidecarFile.Open(true).Should().BeTrue();

        // The stale Verified flag is cleared, the seeded flags are kept
        SidecarFile.StatesType expected = Seeded | SidecarFile.StatesType.FileModified;
        _ = sidecarFile.State.Should().Be(expected);
        _ = FakeTools.ReadSidecarState(sidecarPath).Should().Be((int)expected);
    }

    [Fact]
    public void Open_SeededVerifyStatesOverStaleSidecar_DropsVerifyStates()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        string mediaFile = FakeTools.CopyUnverifiedSample(
            _directory,
            SidecarFile.StatesType.Verified
        );
        string sidecarPath = Path.Combine(_directory, "name.PlexCleaner");
        File.AppendAllText(mediaFile, "changed");

        FakeTools.WriteSampleToolData(_directory);

        SidecarFile sidecarFile = new(mediaFile)
        {
            State =
                Seeded
                | SidecarFile.StatesType.Repaired
                | SidecarFile.StatesType.Verified
                | SidecarFile.StatesType.VerifyFailed
                | SidecarFile.StatesType.RepairFailed
                | SidecarFile.StatesType.BitrateExceeded
                | SidecarFile.StatesType.PostDecodeTimestamps,
        };
        _ = sidecarFile.Open(true).Should().BeTrue();

        SidecarFile.StatesType expected =
            Seeded | SidecarFile.StatesType.Repaired | SidecarFile.StatesType.FileModified;
        _ = sidecarFile.State.Should().Be(expected);
        _ = FakeTools.ReadSidecarState(sidecarPath).Should().Be((int)expected);
    }
}
