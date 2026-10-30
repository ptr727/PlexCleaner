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
        Directory.Delete(_directory, true);
    }

    private ProcessFile CreateProcessFile()
    {
        ProcessFile processFile = new(FakeTools.CopyUnverifiedSample(_directory));
        _ = processFile.GetMediaProps().Should().BeTrue();
        return processFile;
    }

    // Each fake writes its tool name to the temp output it is given, MkvPropEdit logs the file it edits
    private static string WriteFakeTools(string tempOutputs, int propEditExit)
    {
        string write =
            $"for a; do a=${{a#*=}}; case $a in {tempOutputs}) echo $(basename \"$0\") > \"$a\" ;; esac; done\n";
        FakeTools.WriteScript(Tools.FfMpeg.GetToolPath(), write);
        FakeTools.WriteScript(Tools.MkvMerge.GetToolPath(), write);
        FakeTools.WriteScript(Tools.HandBrake.GetToolPath(), write);
        string mkvPropEdit = Tools.MkvPropEdit.GetToolPath();
        FakeTools.WriteScript(mkvPropEdit, $"echo \"$@\" > \"$0.log\"\nexit {propEditExit}\n");
        return mkvPropEdit + ".log";
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(2, false)]
    public void ConvertToMkv_ReplacesInputOnlyAfterLanguageRestore(int propEditExit, bool success)
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        ProcessFile processFile = CreateProcessFile();
        string mediaFile = processFile.FileInfo.FullName;
        byte[] original = File.ReadAllBytes(mediaFile);
        string propEditLog = WriteFakeTools("*.tmp1|*.tmp4", propEditExit);

        bool converted = PlexCleaner.Convert.ConvertToMkv(
            mediaFile,
            new SelectMediaProps(processFile.FfProbeProps, true),
            processFile.MkvMergeProps,
            out string outputName
        );

        _ = converted.Should().Be(success);
        _ = outputName.Should().Be(mediaFile);
        _ = File.ReadAllText(propEditLog).Should().Contain("name.tmp1");
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

    [Fact]
    public void DeInterlace_LanguageRestoreFails_KeepsOriginal()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        ProcessFile processFile = CreateProcessFile();
        string mediaFile = processFile.FileInfo.FullName;
        byte[] original = File.ReadAllBytes(mediaFile);
        string propEditLog = WriteFakeTools("*.tmp8|*.tmp9", 2);
        processFile.FfProbeProps.Video[0].Interlaced = true;

        bool modified = false;
        _ = processFile.DeInterlace(false, ref modified).Should().BeFalse();

        _ = modified.Should().BeFalse();
        _ = File.ReadAllText(propEditLog).Should().Contain("name.tmp9");
        _ = File.ReadAllBytes(mediaFile).Should().Equal(original);
        _ = processFile.State.Should().NotHaveFlag(SidecarFile.StatesType.DeInterlaced);
        _ = Directory.GetFiles(_directory, "name.tmp*").Should().BeEmpty();
    }
}
