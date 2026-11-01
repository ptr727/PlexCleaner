using System.Diagnostics.Metrics;
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

    private static void AssertFirstFileFails(Exception exception, string taskName)
    {
        long errors = 0;
        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Name == "plexcleaner.files.errors")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, _, _) => errors += measurement);
        listener.Start();

        List<string> processed = [];
        _ = ProcessDriver
            .ProcessFiles(
                ["first.mkv", "second.mkv"],
                taskName,
                false,
                fileName =>
                {
                    processed.Add(fileName);
                    return fileName == "first.mkv" ? throw exception : true;
                }
            )
            .Should()
            .BeTrue();

        _ = processed.Should().Equal("first.mkv", "second.mkv");
        _ = errors.Should().Be(1);
    }

    [Fact]
    public void ProcessFiles_TaskThrows_FailsOneFileAndContinues() =>
        AssertFirstFileFails(
            new IOException(),
            nameof(ProcessFiles_TaskThrows_FailsOneFileAndContinues)
        );

    [Fact]
    public void ProcessFiles_TaskThrowsCancelWithoutCancel_FailsOneFileAndContinues() =>
        AssertFirstFileFails(
            new TaskCanceledException(),
            nameof(ProcessFiles_TaskThrowsCancelWithoutCancel_FailsOneFileAndContinues)
        );

    [Fact]
    public void ProcessFile_StepThrows_FailsAndKeepsFile()
    {
        Assert.SkipWhen(
            OperatingSystem.IsWindows() || Environment.IsPrivilegedProcess,
            "A privileged process or Windows renames in a locked folder"
        );
        string mediaFile = Path.Combine(_directory, "name.MKV");
        File.WriteAllText(mediaFile, "");

        // A folder without write permission makes the lowercase rename throw
        File.SetUnixFileMode(_directory, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            _ = Process
                .ProcessFile(mediaFile, out _, out _, out _, out _, out string? failedOperation)
                .Should()
                .BeFalse();

            _ = failedOperation.Should().Be(nameof(ProcessFile.MakeExtensionLowercase));
            _ = File.Exists(mediaFile).Should().BeTrue();
        }
        finally
        {
            File.SetUnixFileMode(
                _directory,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            );
        }
    }
}
