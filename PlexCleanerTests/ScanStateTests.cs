using System.Text.Json.Nodes;
using AwesomeAssertions;
using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

[Collection("Sequential")]
public sealed class ScanStateTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly ToolsOptions1 _toolsOptions = Program.Config.ToolsOptions;
    private readonly bool _useSidecarFiles = Program.Config.ProcessOptions.UseSidecarFiles;
    private readonly bool _deInterlace = Program.Config.ProcessOptions.DeInterlace;
    private readonly bool _removeClosedCaptions = Program
        .Config
        .ProcessOptions
        .RemoveClosedCaptions;
    private readonly bool _sidecarUpdateOnToolChange = Program
        .Config
        .ProcessOptions
        .SidecarUpdateOnToolChange;
    private readonly bool _quickScan = Program.Options.QuickScan;
    private readonly MediaToolInfo _ffProbeInfo = Tools.FfProbe.Info;
    private readonly MediaToolInfo _mkvMergeInfo = Tools.MkvMerge.Info;
    private readonly MediaToolInfo _mediaInfoInfo = Tools.MediaInfo.Info;

    public ScanStateTests()
    {
        _ = Directory.CreateDirectory(_directory);
        Program.Config.ProcessOptions.UseSidecarFiles = true;
        Program.Config.ProcessOptions.DeInterlace = true;
        Program.Config.ProcessOptions.RemoveClosedCaptions = true;
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
        Program.Config.ProcessOptions.DeInterlace = _deInterlace;
        Program.Config.ProcessOptions.RemoveClosedCaptions = _removeClosedCaptions;
        Program.Config.ProcessOptions.SidecarUpdateOnToolChange = _sidecarUpdateOnToolChange;
        Program.Options.QuickScan = _quickScan;
        Directory.Delete(_directory, true);
    }

    private static ProcessFile OpenProcessFile(string mediaFile)
    {
        ProcessFile processFile = new(mediaFile);
        _ = processFile.GetMediaProps().Should().BeTrue();
        return processFile;
    }

    private static string WriteFailingTool(string toolPath)
    {
        FakeTools.WriteScript(toolPath, "touch \"$0.ran\"\nexit 1\n");
        return toolPath + ".ran";
    }

    [Fact]
    public void RemoveClosedCaptions_CleanScan_SetsScannedCaptionsAndSkipsNextRun()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
        string mediaFile = FakeTools.CopyUnverifiedSample(_directory);
        string ffProbe = Tools.FfProbe.GetToolPath();
        FakeTools.WriteScript(ffProbe, "echo '{ \"streams\": [ { \"closed_captions\": 0 } ] }'\n");

        bool modified = false;
        ProcessFile processFile = OpenProcessFile(mediaFile);
        _ = processFile.RemoveClosedCaptions(true, ref modified).Should().BeTrue();
        _ = processFile.State.Should().Be(SidecarFile.StatesType.ScannedCaptions);

        string marker = WriteFailingTool(ffProbe);
        ProcessFile nextRun = OpenProcessFile(mediaFile);
        _ = nextRun.State.Should().Be(SidecarFile.StatesType.ScannedCaptions);
        _ = nextRun.RemoveClosedCaptions(true, ref modified).Should().BeTrue();
        _ = File.Exists(marker).Should().BeFalse();
        _ = modified.Should().BeFalse();
    }

    [Fact]
    public void DeInterlace_CleanScan_SetsScannedInterlaceAndSkipsNextRun()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
        string mediaFile = FakeTools.CopyUnverifiedSample(_directory);
        string ffMpeg = Tools.FfMpeg.GetToolPath();
        FakeTools.WriteScript(
            ffMpeg,
            """
            echo '[Parsed_idet_0 @ 0] Repeated Fields: Neither: 1000 Top: 0 Bottom: 0' >&2
            echo '[Parsed_idet_0 @ 0] Single frame detection: TFF: 0 BFF: 0 Progressive: 1000 Undetermined: 0' >&2
            echo '[Parsed_idet_0 @ 0] Multi frame detection: TFF: 0 BFF: 0 Progressive: 1000 Undetermined: 0' >&2

            """
        );

        bool modified = false;
        ProcessFile processFile = OpenProcessFile(mediaFile);
        _ = processFile.DeInterlace(true, ref modified).Should().BeTrue();
        _ = processFile.State.Should().Be(SidecarFile.StatesType.ScannedInterlace);

        string marker = WriteFailingTool(ffMpeg);
        ProcessFile nextRun = OpenProcessFile(mediaFile);
        _ = nextRun.State.Should().Be(SidecarFile.StatesType.ScannedInterlace);
        _ = nextRun.DeInterlace(true, ref modified).Should().BeTrue();
        _ = File.Exists(marker).Should().BeFalse();
        _ = modified.Should().BeFalse();
    }

    [Fact]
    public void RemoveClosedCaptions_ScanFails_LeavesStateUnchanged()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
        string mediaFile = FakeTools.CopyUnverifiedSample(_directory);
        _ = WriteFailingTool(Tools.FfProbe.GetToolPath());

        bool modified = false;
        ProcessFile processFile = OpenProcessFile(mediaFile);
        _ = processFile.RemoveClosedCaptions(true, ref modified).Should().BeFalse();
        _ = processFile.State.Should().Be(SidecarFile.StatesType.None);
        _ = OpenProcessFile(mediaFile).State.Should().Be(SidecarFile.StatesType.None);
    }

    [Fact]
    public void RemoveClosedCaptions_QuickScan_DoesNotSetScannedCaptions()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
        Program.Options.QuickScan = true;
        string mediaFile = FakeTools.CopyUnverifiedSample(_directory);
        FakeTools.WriteScript(
            Tools.FfProbe.GetToolPath(),
            "echo '{ \"streams\": [ { \"closed_captions\": 0 } ] }'\n"
        );

        bool modified = false;
        ProcessFile processFile = OpenProcessFile(mediaFile);
        _ = processFile.RemoveClosedCaptions(true, ref modified).Should().BeTrue();
        _ = processFile.State.Should().Be(SidecarFile.StatesType.None);
        _ = OpenProcessFile(mediaFile).State.Should().Be(SidecarFile.StatesType.None);
    }

    [Fact]
    public void Read_ToolChange_ClearsVerifiedAndScannedFlags()
    {
        Program.Config.ProcessOptions.SidecarUpdateOnToolChange = true;
        string mediaFile = FakeTools.CopyUnverifiedSample(_directory);
        string sidecarFile = Path.ChangeExtension(mediaFile, ".PlexCleaner");
        JsonNode sidecar = JsonNode.Parse(File.ReadAllText(sidecarFile))!;
        sidecar["State"] = (int)(
            SidecarFile.StatesType.SetLanguage
            | SidecarFile.StatesType.Verified
            | SidecarFile.StatesType.ScannedInterlace
            | SidecarFile.StatesType.ScannedCaptions
        );
        File.WriteAllText(sidecarFile, sidecar.ToJsonString());
        Tools.FfProbe.Info = new MediaToolInfo(Tools.FfProbe) { Version = "99.0" };

        SidecarFile sidecarFileState = new(mediaFile);
        _ = sidecarFileState.Read(out bool current).Should().BeTrue();
        _ = current.Should().BeFalse();
        _ = sidecarFileState.State.Should().Be(SidecarFile.StatesType.SetLanguage);
    }
}
