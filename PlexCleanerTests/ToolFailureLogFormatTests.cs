using System.Globalization;
using AwesomeAssertions;
using PlexCleaner;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;
using Xunit;

namespace PlexCleanerTests;

// The tool-failure log line renders through the app's {Message} output template, which quotes string
// values. These tests verify the ExitCode and the error summary format correctly - the error text is
// quoted as its own value and the " : " separator is not pulled inside the quotes.
public class ToolFailureLogFormatTests
{
    private sealed class CapturingSink : ILogEventSink
    {
        public LogEvent? Last { get; private set; }

        public void Emit(LogEvent logEvent) => Last = logEvent;
    }

    // Render a log call through the same {Message} template the console and file sinks use
    private static string Render(Action<ILogger> log)
    {
        CapturingSink sink = new();
        using Logger logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        log(logger);
        MessageTemplateTextFormatter formatter = new("{Message}", CultureInfo.InvariantCulture);
        using StringWriter writer = new();
        formatter.Format(sink.Last!, writer);
        return writer.ToString();
    }

    [Fact]
    public void FailedResult_WithSummary_SeparatorStaysOutsideQuotes()
    {
        // ffmpeg can exit 0 yet report a fatal error on stderr, so ExitCode 0 with a summary is valid
        string rendered = Render(logger =>
            logger.Error(
                "Failed execution of {ToolType} : ExitCode: {ExitCode} : {Error}",
                MediaTool.ToolType.FfMpeg,
                0,
                "[null] Application provided invalid, non monotonically increasing dts |"
            )
        );

        _ = rendered
            .Should()
            .Contain(
                "ExitCode: 0 : \"[null] Application provided invalid, non monotonically increasing dts |\""
            );
        // The stray-quote bug rendered "ExitCode: 0\" : ..." with the quote after the number
        _ = rendered.Should().NotContain("ExitCode: 0\"");
    }

    [Fact]
    public void FailedResult_WithoutSummary_HasNoTrailingSeparatorOrQuote()
    {
        string rendered = Render(logger =>
            logger.Error(
                "Failed execution of {ToolType} : ExitCode: {ExitCode}",
                MediaTool.ToolType.MkvMerge,
                2
            )
        );

        _ = rendered.Should().Be("Failed execution of MkvMerge : ExitCode: 2");
    }
}
