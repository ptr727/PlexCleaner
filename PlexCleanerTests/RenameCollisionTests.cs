using AwesomeAssertions;
using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

[Collection("Sequential")]
public sealed class RenameCollisionTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly ToolsOptions1 _toolsOptions = Program.Config.ToolsOptions;
    private readonly bool _reMux = Program.Config.ProcessOptions.ReMux;
    private readonly bool _deleteInvalidFiles = Program.Config.VerifyOptions.DeleteInvalidFiles;
    private readonly bool _deleteUnwanted = Program.Config.ProcessOptions.DeleteUnwantedExtensions;
    private readonly MediaToolInfo _mkvMergeInfo = Tools.MkvMerge.Info;

    public RenameCollisionTests()
    {
        _ = Directory.CreateDirectory(_directory);
        Program.Config.VerifyOptions.DeleteInvalidFiles = true;
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
        Program.Config.ProcessOptions.ReMux = _reMux;
        Program.Config.VerifyOptions.DeleteInvalidFiles = _deleteInvalidFiles;
        Program.Config.ProcessOptions.DeleteUnwantedExtensions = _deleteUnwanted;
        Tools.MkvMerge.Info = _mkvMergeInfo;
        Directory.Delete(_directory, true);
    }

    private string CreateFile(string name)
    {
        string fileName = Path.Combine(_directory, name);
        File.WriteAllText(fileName, name);
        return fileName;
    }

    private bool IsCaseSensitive()
    {
        _ = CreateFile("probe.CASE");
        return !File.Exists(Path.Combine(_directory, "probe.case"));
    }

    [Fact]
    public void WouldReplaceOtherFile_SameName_IsFalse()
    {
        string mediaFile = CreateFile("name.mkv");

        _ = PlexCleaner.Convert.WouldReplaceOtherFile(mediaFile, mediaFile).Should().BeFalse();
    }

    [Fact]
    public void WouldReplaceOtherFile_MissingTarget_IsFalse()
    {
        string mediaFile = CreateFile("name.avi");

        _ = PlexCleaner
            .Convert.WouldReplaceOtherFile(mediaFile, Path.ChangeExtension(mediaFile, ".mkv"))
            .Should()
            .BeFalse();
    }

    [Fact]
    public void WouldReplaceOtherFile_OtherFile_IsTrue()
    {
        string mediaFile = CreateFile("name.avi");
        string otherFile = CreateFile("name.mkv");

        _ = PlexCleaner.Convert.WouldReplaceOtherFile(mediaFile, otherFile).Should().BeTrue();
    }

    [Fact]
    public void WouldReplaceOtherFile_CaseOnlyRenameOfItself_IsFalse()
    {
        string mediaFile = CreateFile("name.MKV");

        _ = PlexCleaner
            .Convert.WouldReplaceOtherFile(mediaFile, Path.Combine(_directory, "name.mkv"))
            .Should()
            .BeFalse();
    }

    [Fact]
    public void WouldReplaceOtherFile_CaseOnlyRenameOntoOtherFile_IsTrue()
    {
        Assert.SkipUnless(IsCaseSensitive(), "Needs a case-sensitive file system");
        string mediaFile = CreateFile("name.MKV");
        string otherFile = CreateFile("name.mkv");

        _ = PlexCleaner.Convert.WouldReplaceOtherFile(mediaFile, otherFile).Should().BeTrue();
    }

    [Fact]
    public void ProcessFile_LowercaseOntoOtherFile_FailsAndKeepsBoth()
    {
        Assert.SkipUnless(IsCaseSensitive(), "Needs a case-sensitive file system");
        Program.Config.ProcessOptions.ReMux = false;
        string mediaFile = CreateFile("name.MKV");
        string otherFile = CreateFile("name.mkv");

        _ = Process
            .ProcessFile(
                mediaFile,
                out bool modified,
                out _,
                out _,
                out string processName,
                out string? failedOperation
            )
            .Should()
            .BeFalse();

        _ = failedOperation.Should().Be(nameof(ProcessFile.MakeExtensionLowercase));
        _ = modified.Should().BeFalse();
        _ = processName.Should().Be(mediaFile);
        _ = File.ReadAllText(mediaFile).Should().Be("name.MKV");
        _ = File.ReadAllText(otherFile).Should().Be("name.mkv");
    }

    [Fact]
    public void ProcessFile_RemuxOntoOtherFile_FailsAndKeepsBoth()
    {
        Program.Config.ProcessOptions.ReMux = true;
        string mediaFile = CreateFile("name.avi");
        string otherFile = CreateFile("name.mkv");

        _ = Process
            .ProcessFile(
                mediaFile,
                out bool modified,
                out _,
                out _,
                out string processName,
                out string? failedOperation
            )
            .Should()
            .BeFalse();

        _ = failedOperation.Should().Be(nameof(ProcessFile.RemuxByExtension));
        _ = modified.Should().BeFalse();
        _ = processName.Should().Be(mediaFile);
        _ = File.ReadAllText(mediaFile).Should().Be("name.avi");
        _ = File.ReadAllText(otherFile).Should().Be("name.mkv");
        _ = Directory.GetFiles(_directory, "name.tmp*").Should().BeEmpty();
    }

    [Fact]
    public void MakeExtensionLowercase_NoOtherFile_Renames()
    {
        string mediaFile = CreateFile("name.MKV");
        string lowerFile = Path.Combine(_directory, "name.mkv");
        ProcessFile processFile = new(mediaFile);

        // No tool is faked, so the Refresh() after the rename fails and the result is not asserted
        bool modified = false;
        _ = processFile.MakeExtensionLowercase(ref modified);

        _ = modified.Should().BeTrue();
        _ = processFile.FileInfo.FullName.Should().Be(lowerFile);
        _ = Directory
            .GetFiles(_directory, "name.*")
            .Select(Path.GetFileName)
            .Should()
            .Equal("name.mkv");
        _ = File.ReadAllText(lowerFile).Should().Be("name.MKV");
    }

    [Fact]
    public void MakeExtensionLowercase_FirstMoveFails_KeepsOriginalName()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Needs the Linux 255-byte file name limit");

        // Only the step name exceeds the limit
        string mediaFile = CreateFile(new string('a', 250) + ".MKV");
        ProcessFile processFile = new(mediaFile);

        bool modified = false;
        _ = processFile
            .Invoking(item => item.MakeExtensionLowercase(ref modified))
            .Should()
            .Throw<IOException>();

        _ = modified.Should().BeFalse();
        _ = processFile.FileInfo.FullName.Should().Be(mediaFile);
        _ = Directory.GetFiles(_directory).Should().Equal(mediaFile);
    }

    [Fact]
    public void MakeExtensionLowercase_SecondMoveFails_RestoresOriginalName()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Needs the Linux 255-byte file name limit");

        // Lowercase U+023A is one UTF-8 byte longer, so only the lowercase name exceeds the limit
        string mediaFile = CreateFile(new string('a', 240) + "." + new string('\u023A', 6));
        ProcessFile processFile = new(mediaFile);

        bool modified = false;
        _ = processFile
            .Invoking(item => item.MakeExtensionLowercase(ref modified))
            .Should()
            .Throw<IOException>();

        _ = modified.Should().BeFalse();
        _ = processFile.FileInfo.FullName.Should().Be(mediaFile);
        _ = Directory.GetFiles(_directory).Should().Equal(mediaFile);
    }

    [Fact]
    public void ProcessFile_StrandedStepName_KeepsFile()
    {
        Program.Config.ProcessOptions.DeleteUnwantedExtensions = true;
        Program.Config.VerifyOptions.DeleteInvalidFiles = false;
        string stepFile = ProcessFile.GetRenameStepName(Path.Combine(_directory, "name.mkv"));
        File.WriteAllText(stepFile, "step");

        _ = Process.ProcessFile(stepFile, out _, out _, out _, out _, out _);

        _ = File.ReadAllText(stepFile).Should().Be("step");
    }

    [Fact]
    public void MakeExtensionLowercase_StepNameTaken_FailsWithoutThrowing()
    {
        string mediaFile = CreateFile("name.MKV");
        string stepFile = ProcessFile.GetRenameStepName(Path.Combine(_directory, "name.mkv"));
        File.WriteAllText(stepFile, "step");
        ProcessFile processFile = new(mediaFile);

        bool modified = false;
        _ = processFile.MakeExtensionLowercase(ref modified).Should().BeFalse();

        _ = modified.Should().BeFalse();
        _ = File.ReadAllText(mediaFile).Should().Be("name.MKV");
        _ = File.ReadAllText(stepFile).Should().Be("step");
    }

    [Fact]
    public void ProcessFile_OutputCreatedDuringRemux_FailsAndKeepsBoth()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        Program.Config.ProcessOptions.ReMux = true;
        Tools.MkvMerge.Info = new MediaToolInfo(Tools.MkvMerge) { Version = "96.0" };
        string mediaFile = CreateFile("name.avi");
        string outputFile = Path.ChangeExtension(mediaFile, ".mkv");

        // The fake writes its output and a newcomer at the output name, after the collision check
        FakeTools.WriteScript(
            Tools.MkvMerge.GetToolPath(),
            $"for a; do case $a in *.tmp2) echo mkvmerge > \"$a\" ;; esac; done\necho newcomer > \"{outputFile}\"\n"
        );

        _ = Process
            .ProcessFile(mediaFile, out _, out _, out _, out _, out string? failedOperation)
            .Should()
            .BeFalse();

        _ = failedOperation.Should().Be(nameof(ProcessFile.RemuxByExtension));
        _ = File.ReadAllText(outputFile).Should().StartWith("newcomer");
        _ = File.ReadAllText(mediaFile).Should().Be("name.avi");
        _ = Directory.GetFiles(_directory, "name.tmp*").Should().BeEmpty();
    }

    [Fact]
    public void RemuxByExtension_InputDeleteFails_ReportsOutputAndModified()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        Tools.MkvMerge.Info = new MediaToolInfo(Tools.MkvMerge) { Version = "96.0" };
        FakeTools.WriteScript(
            Tools.MkvMerge.GetToolPath(),
            "for a; do case $a in *.tmp2) echo mkvmerge > \"$a\" ;; esac; done\n"
        );

        // A directory as the input makes the delete after the move throw
        string mediaFile = Path.Combine(_directory, "name.avi");
        _ = Directory.CreateDirectory(mediaFile);
        string outputFile = Path.ChangeExtension(mediaFile, ".mkv");
        ProcessFile processFile = new(mediaFile);

        // MediaInfo has no fake, so the Refresh() after the remux fails and the result is not asserted
        bool modified = false;
        _ = processFile.RemuxByExtension(false, ref modified);

        _ = modified.Should().BeTrue();
        _ = processFile.FileInfo.FullName.Should().Be(outputFile);
        _ = processFile.State.Should().HaveFlag(SidecarFile.StatesType.ReMuxed);
        _ = File.ReadAllText(outputFile).Should().StartWith("mkvmerge");
        _ = Directory.GetFiles(_directory, "name.tmp*").Should().BeEmpty();
    }
}
