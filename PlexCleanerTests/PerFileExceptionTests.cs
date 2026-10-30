using AwesomeAssertions;
using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

[Collection("Sequential")]
public sealed class PerFileExceptionTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly bool _reMux = Program.Config.ProcessOptions.ReMux;
    private readonly bool _deleteInvalidFiles = Program.Config.VerifyOptions.DeleteInvalidFiles;
    private readonly int _threadCount = Program.Options.ThreadCount;

    public PerFileExceptionTests()
    {
        _ = Directory.CreateDirectory(_directory);
        Program.Config.ProcessOptions.ReMux = false;
        Program.Config.VerifyOptions.DeleteInvalidFiles = true;
        Program.Options.ThreadCount = 1;
    }

    public void Dispose()
    {
        Program.Config.ProcessOptions.ReMux = _reMux;
        Program.Config.VerifyOptions.DeleteInvalidFiles = _deleteInvalidFiles;
        Program.Options.ThreadCount = _threadCount;
        Directory.Delete(_directory, true);
    }

    [Fact]
    public void ProcessFiles_TaskThrows_FailsOneFileAndContinues()
    {
        List<string> processed = [];
        _ = ProcessDriver
            .ProcessFiles(
                ["first.mkv", "second.mkv"],
                nameof(ProcessFiles_TaskThrows_FailsOneFileAndContinues),
                false,
                fileName =>
                {
                    processed.Add(fileName);
                    return fileName == "first.mkv" ? throw new IOException() : true;
                }
            )
            .Should()
            .BeTrue();

        _ = processed.Should().Equal("first.mkv", "second.mkv");
    }

    [Fact]
    public void ProcessFile_StepThrows_FailsAndKeepsFile()
    {
        string mediaFile = Path.Combine(_directory, "name.MKV");
        File.WriteAllText(mediaFile, "");

        // A directory on the rename target makes the lowercase rename throw
        _ = Directory.CreateDirectory(Path.ChangeExtension(mediaFile, ".tmp7"));

        _ = Process
            .ProcessFile(mediaFile, out _, out _, out _, out _, out string? failedOperation)
            .Should()
            .BeFalse();

        _ = failedOperation.Should().Be(nameof(ProcessFile.MakeExtensionLowercase));
        _ = File.Exists(mediaFile).Should().BeTrue();
    }
}
