using AwesomeAssertions;
using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

[Collection("Sequential")]
public sealed class ReplaceOriginalTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly ToolsOptions1 _toolsOptions = Program.Config.ToolsOptions;
    private readonly bool _useSidecarFiles = Program.Config.ProcessOptions.UseSidecarFiles;
    private readonly HashSet<string> _reEncodeAudioFormats = Program
        .Config
        .ProcessOptions
        .ReEncodeAudioFormats;
    private readonly MediaToolInfo _ffProbeInfo = Tools.FfProbe.Info;
    private readonly MediaToolInfo _mkvMergeInfo = Tools.MkvMerge.Info;
    private readonly MediaToolInfo _mediaInfoInfo = Tools.MediaInfo.Info;

    public ReplaceOriginalTests()
    {
        _ = Directory.CreateDirectory(_directory);
        Program.Config.ProcessOptions.UseSidecarFiles = true;
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
        Program.Config.ProcessOptions.UseSidecarFiles = _useSidecarFiles;
        Program.Config.ProcessOptions.ReEncodeAudioFormats = _reEncodeAudioFormats;
        Directory.Delete(_directory, true);
    }

    private const SidecarFile.StatesType VerifiedState =
        SidecarFile.StatesType.Verified
        | SidecarFile.StatesType.Repaired
        | SidecarFile.StatesType.BitrateExceeded;

    private ProcessFile CreateProcessFile(
        SidecarFile.StatesType state = SidecarFile.StatesType.None
    )
    {
        ProcessFile processFile = new(FakeTools.CopyUnverifiedSample(_directory, state));
        _ = processFile.GetMediaProps().Should().BeTrue();
        return processFile;
    }

    // Each fake writes its tool name into every argument matching the patterns, MkvPropEdit logs the file it edits
    private string WriteFakeTools(string tempOutputs, int propEditExit, int mkvMergeExit = 0)
    {
        string write =
            $"for a; do a=${{a#*=}}; case $a in {tempOutputs}) echo $(basename \"$0\") > \"$a\" ;; esac; done\n";
        FakeTools.WriteScript(Tools.FfMpeg.GetToolPath(), write);
        FakeTools.WriteScript(
            Tools.MkvMerge.GetToolPath(),
            FakeTools.SampleIdentifyCase(_directory) + write + $"exit {mkvMergeExit}\n"
        );
        FakeTools.WriteScript(Tools.HandBrake.GetToolPath(), write);
        string mkvPropEdit = Tools.MkvPropEdit.GetToolPath();
        FakeTools.WriteScript(mkvPropEdit, $"echo \"$@\" > \"$0.log\"\nexit {propEditExit}\n");
        return mkvPropEdit + ".log";
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(0, 2, false)]
    [InlineData(2, 0, false)]
    public void ConvertToMkv_ReplacesInputOnlyAfterEveryStep(
        int mkvMergeExit,
        int propEditExit,
        bool success
    )
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        ProcessFile processFile = CreateProcessFile();
        string mediaFile = processFile.FileInfo.FullName;
        byte[] original = File.ReadAllBytes(mediaFile);
        string propEditLog = WriteFakeTools("*.tmp1|*.tmp4", propEditExit, mkvMergeExit);

        bool converted = PlexCleaner.Convert.ConvertToMkv(
            mediaFile,
            new SelectMediaProps(processFile.FfProbeProps, true),
            processFile.MkvMergeProps,
            out string outputName
        );

        _ = converted.Should().Be(success);
        _ = outputName.Should().Be(mediaFile);
        if (mkvMergeExit == 0)
        {
            _ = File.ReadAllText(propEditLog).Should().Contain("name.tmp1");
        }
        else
        {
            _ = File.Exists(propEditLog).Should().BeFalse();
        }
        if (success)
        {
            _ = File.ReadAllText(mediaFile).Should().StartWith("mkvmerge");
        }
        else
        {
            _ = File.ReadAllBytes(mediaFile).Should().Equal(original);
        }
        _ = Directory.GetFiles(_directory, "name.tmp*").Should().BeEmpty();
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(2, false)]
    public void DeInterlace_ReplacesOriginalOnlyAfterLanguageRestore(int propEditExit, bool success)
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
        ProcessFile processFile = CreateProcessFile(VerifiedState);
        string mediaFile = processFile.FileInfo.FullName;
        byte[] original = File.ReadAllBytes(mediaFile);
        string propEditLog = WriteFakeTools("*.tmp8|*.tmp9", propEditExit);
        FakeTools.WriteSampleToolData(_directory);
        processFile.FfProbeProps.Video[0].Interlaced = true;

        bool modified = false;
        bool deInterlaced = processFile.DeInterlace(false, ref modified);

        _ = deInterlaced.Should().Be(success);
        _ = modified.Should().Be(success);
        _ = File.ReadAllText(propEditLog).Should().Contain("name.tmp9");
        if (success)
        {
            SidecarFile.StatesType expected =
                SidecarFile.StatesType.DeInterlaced
                | SidecarFile.StatesType.ReMuxed
                | SidecarFile.StatesType.Repaired;
            _ = File.ReadAllText(mediaFile).Should().StartWith("mkvmerge");
            _ = processFile.State.Should().Be(expected);
            _ = FakeTools
                .ReadSidecarState(SidecarFile.GetSidecarName(processFile.FileInfo))
                .Should()
                .Be((int)expected);
        }
        else
        {
            _ = File.ReadAllBytes(mediaFile).Should().Equal(original);
            _ = processFile.State.Should().Be(VerifiedState);
        }
        _ = Directory.GetFiles(_directory, "name.tmp*").Should().BeEmpty();
    }

    [Theory]
    [InlineData(VerifiedState)]
    [InlineData(
        SidecarFile.StatesType.VerifyFailed
            | SidecarFile.StatesType.RepairFailed
            | SidecarFile.StatesType.PostDecodeTimestamps
    )]
    public void ReEncode_ReplacesOriginal_ClearsVerifyStates(SidecarFile.StatesType state)
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
        ProcessFile processFile = CreateProcessFile(state);
        _ = WriteFakeTools("*.tmp1", 0);
        FakeTools.WriteSampleToolData(_directory);
        Program.Config.ProcessOptions.ReEncodeAudioFormats = new HashSet<string>(
            [processFile.FfProbeProps.Audio[0].Format],
            StringComparer.OrdinalIgnoreCase
        );

        bool modified = false;
        _ = processFile.ReEncode(false, ref modified).Should().BeTrue();

        SidecarFile.StatesType expected =
            SidecarFile.StatesType.ReEncoded
            | SidecarFile.StatesType.ReMuxed
            | (state & SidecarFile.StatesType.Repaired);
        _ = modified.Should().BeTrue();
        _ = processFile.State.Should().Be(expected);
        _ = FakeTools
            .ReadSidecarState(SidecarFile.GetSidecarName(processFile.FileInfo))
            .Should()
            .Be((int)expected);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void RestoreLanguageTags_EditsOnlyWhenTracksMatch(bool changeTracks, bool edited)
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        ProcessFile processFile = CreateProcessFile();
        string propEditLog = WriteFakeTools("*.tmp1", 0);
        string tempName = Path.ChangeExtension(processFile.FileInfo.FullName, ".tmp1");
        File.WriteAllText(tempName, "ffmpeg");
        if (changeTracks)
        {
            processFile.MkvMergeProps.GetTrackList()[0].Number += 100;
        }

        _ = PlexCleaner
            .Convert.RestoreLanguageTags(tempName, processFile.MkvMergeProps)
            .Should()
            .BeTrue();

        _ = File.Exists(propEditLog).Should().Be(edited);
        if (edited)
        {
            _ = File.ReadAllText(propEditLog).Should().Contain("name.tmp1");
        }
    }

    [Fact]
    public void RestoreLanguageTags_IdentifyFails_ReturnsFalse()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        ProcessFile processFile = CreateProcessFile();
        string propEditLog = WriteFakeTools("*.tmp1", 0);
        FakeTools.WriteScript(Tools.MkvMerge.GetToolPath(), "exit 2\n");
        string tempName = Path.ChangeExtension(processFile.FileInfo.FullName, ".tmp1");
        File.WriteAllText(tempName, "ffmpeg");

        _ = PlexCleaner
            .Convert.RestoreLanguageTags(tempName, processFile.MkvMergeProps)
            .Should()
            .BeFalse();
        _ = File.Exists(propEditLog).Should().BeFalse();
    }

    [Theory]
    [InlineData("Matroska", true)]
    [InlineData("AVI", false)]
    public void ReMuxToMkv_FfMpegFallback_RestoresLanguageOnlyForMatroska(
        string container,
        bool restored
    )
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        ProcessFile processFile = CreateProcessFile();
        string mediaFile = processFile.FileInfo.FullName;
        string propEditLog = WriteFakeTools("*.tmp2|*.tmp4", 0);
        processFile.MkvMergeProps.Container = container;

        // MkvMerge fails to remux the input, then remuxes the FfMpeg output
        FakeTools.WriteScript(
            Tools.MkvMerge.GetToolPath(),
            FakeTools.SampleIdentifyCase(_directory)
                + "case \"$*\" in *.tmp4*) ;; *) exit 2 ;; esac\n"
                + "for a; do case $a in *.tmp4) echo mkvmerge > \"$a\" ;; esac; done\n"
        );

        _ = PlexCleaner
            .Convert.ReMuxToMkv(mediaFile, processFile.MkvMergeProps, out string outputName)
            .Should()
            .BeTrue();

        _ = outputName.Should().Be(mediaFile);
        _ = File.ReadAllText(mediaFile).Should().StartWith("mkvmerge");
        _ = File.Exists(propEditLog).Should().Be(restored);
        if (restored)
        {
            _ = File.ReadAllText(propEditLog).Should().Contain("name.tmp2");
        }
        _ = Directory.GetFiles(_directory, "name.tmp*").Should().BeEmpty();
    }
}
