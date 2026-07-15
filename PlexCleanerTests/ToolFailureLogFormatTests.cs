using System.Globalization;
using AwesomeAssertions;
using CliWrap.Buffered;
using PlexCleaner;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;
using Xunit;

namespace PlexCleanerTests;

// The tool-failure log line renders through the app's {Message} output template, which quotes string
// values. These tests exercise MediaTool.LogFailedResult and verify the ExitCode and error summary
// format correctly - the error text is quoted as its own value and the " : " separator is not pulled
// inside the quotes. Sequential because they swap the global Serilog Log.Logger.
[Collection("Sequential")]
public class ToolFailureLogFormatTests
{
    private sealed class CapturingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    // Minimal concrete tool exposing the protected LogFailedResult for testing
    private sealed class TestTool : MediaTool
    {
        public override ToolFamily GetToolFamily() => ToolFamily.FfMpeg;

        public override ToolType GetToolType() => ToolType.FfMpeg;

        protected override string GetToolNameWindows() => "test.exe";

        protected override string GetToolNameLinux() => "test";

        public override bool GetInstalledVersion(out MediaToolInfo mediaToolInfo)
        {
            mediaToolInfo = null!;
            return false;
        }

        protected override bool GetLatestVersionWindows(out MediaToolInfo mediaToolInfo)
        {
            mediaToolInfo = null!;
            return false;
        }

        public bool InvokeLogFailedResult(
            BufferedCommandResult result,
            string fileName,
            string operation
        ) => LogFailedResult(result, fileName, operation);
    }

    // Call LogFailedResult with the given exit code and stderr, capturing the emitted event by
    // temporarily redirecting the static Serilog logger, then render it through the {Message} template
    // the console and file sinks use
    private static string RenderLogFailedResult(
        int exitCode,
        string stderr,
        string fileName = "file.mkv",
        string stdout = "",
        string operation = "Verify"
    )
    {
        CapturingSink sink = new();
        ILogger original = Log.Logger;
        Logger capture = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        Log.Logger = capture;
        try
        {
            TestTool tool = new();
            BufferedCommandResult result = new(
                exitCode,
                DateTimeOffset.MinValue,
                DateTimeOffset.MinValue,
                stdout,
                stderr
            );
            _ = tool.InvokeLogFailedResult(result, fileName, operation).Should().BeFalse();
        }
        finally
        {
            Log.Logger = original;
            capture.Dispose();
        }

        LogEvent logEvent = sink.Events.First(item =>
            item.MessageTemplate.Text.StartsWith("Failed execution of", StringComparison.Ordinal)
        );
        MessageTemplateTextFormatter formatter = new("{Message}", CultureInfo.InvariantCulture);
        using StringWriter writer = new();
        formatter.Format(logEvent, writer);
        return writer.ToString();
    }

    [Fact]
    public void LogFailedResult_WithStderr_SeparatorStaysOutsideQuotes()
    {
        // ffmpeg can exit 0 yet report a fatal error on stderr, so ExitCode 0 with a summary is valid
        const string Stderr =
            "[null] Application provided invalid, non monotonically increasing dts";
        string rendered = RenderLogFailedResult(0, Stderr);

        // The error text is quoted as its own value, opening after the " : " separator
        _ = rendered.Should().Contain("ExitCode: 0 : \"");
        _ = rendered.Should().Contain(Stderr);
        // The file name follows as the last quoted value
        _ = rendered.Should().EndWith("\"file.mkv\"");
        // The stray-quote bug rendered "ExitCode: 0\" : ..." with the quote right after the number
        _ = rendered.Should().NotContain("ExitCode: 0\"");
    }

    [Fact]
    public void LogFailedResult_WithoutStderr_HasNoErrorValueButKeepsFileName()
    {
        // With no output on either stream the error value is omitted, leaving the operation, exit code
        // and file name
        string rendered = RenderLogFailedResult(2, string.Empty);
        _ = rendered
            .Should()
            .Be("Failed execution of FfMpeg : Verify : ExitCode: 2 : \"file.mkv\"");
    }

    [Fact]
    public void LogFailedResult_ErrorOnStdout_FallsBackToStdout()
    {
        // A tool that writes its error to stdout with an empty stderr (e.g. mkvtoolnix) is still logged
        const string StdoutError = "Error: the file could not be opened for reading";
        string rendered = RenderLogFailedResult(2, string.Empty, stdout: StdoutError);
        _ = rendered.Should().Contain(StdoutError);
    }
}
