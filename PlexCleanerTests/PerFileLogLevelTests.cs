using AwesomeAssertions;
using PlexCleaner;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace PlexCleanerTests;

// Fabricates Serilog LogEvents so the filter can be tested without a logger or media.
internal static class LogEventFactory
{
    private static readonly MessageTemplate s_template = new MessageTemplateParser().Parse("test");

    public static LogEvent Create(LogEventLevel level, string? sourceContext = null)
    {
        LogEventProperty[] properties = sourceContext is null
            ? []
            :
            [
                new LogEventProperty(
                    Constants.SourceContextPropertyName,
                    new ScalarValue(sourceContext)
                ),
            ];
        return new LogEvent(DateTimeOffset.UtcNow, level, null, s_template, properties);
    }
}

public class PerFileLogLevelTests
{
    private static readonly string s_overrideContext = typeof(Extensions.LogOverride).FullName!;

    private static LogEvent Event(LogEventLevel level, string? sourceContext = null) =>
        LogEventFactory.Create(level, sourceContext);

    [Theory]
    [InlineData(LogEventLevel.Warning, LogEventLevel.Verbose, false)]
    [InlineData(LogEventLevel.Warning, LogEventLevel.Debug, false)]
    [InlineData(LogEventLevel.Warning, LogEventLevel.Information, false)]
    [InlineData(LogEventLevel.Warning, LogEventLevel.Warning, true)]
    [InlineData(LogEventLevel.Warning, LogEventLevel.Error, true)]
    [InlineData(LogEventLevel.Warning, LogEventLevel.Fatal, true)]
    [InlineData(LogEventLevel.Information, LogEventLevel.Debug, false)]
    [InlineData(LogEventLevel.Information, LogEventLevel.Information, true)]
    [InlineData(LogEventLevel.Information, LogEventLevel.Warning, true)]
    public void NoSession_HonorsFloor(LogEventLevel floor, LogEventLevel level, bool expected)
    {
        PerFileLogLevel.Filter filter = new(floor);
        _ = filter.IsEnabled(Event(level)).Should().Be(expected);
    }

    [Fact]
    public void LogOverride_NoSession_AlwaysPasses()
    {
        PerFileLogLevel.Filter filter = new(LogEventLevel.Warning);
        foreach (LogEventLevel level in Enum.GetValues<LogEventLevel>())
        {
            _ = filter.IsEnabled(Event(level, s_overrideContext)).Should().BeTrue();
        }
    }

    [Fact]
    public void LogOverride_InSession_PassesWithoutElevating()
    {
        PerFileLogLevel.Filter filter = new(LogEventLevel.Warning);
        using IDisposable scope = PerFileLogLevel.BeginScope(LogEventLevel.Warning);

        foreach (LogEventLevel level in Enum.GetValues<LogEventLevel>())
        {
            _ = filter.IsEnabled(Event(level, s_overrideContext)).Should().BeTrue();
        }

        _ = filter.IsEnabled(Event(LogEventLevel.Information)).Should().BeFalse();
    }

    [Fact]
    public void Session_SuppressesBelowFloor()
    {
        PerFileLogLevel.Filter filter = new(LogEventLevel.Warning);
        using IDisposable scope = PerFileLogLevel.BeginScope(LogEventLevel.Warning);
        _ = filter.IsEnabled(Event(LogEventLevel.Information)).Should().BeFalse();
        _ = filter.IsEnabled(Event(LogEventLevel.Debug)).Should().BeFalse();
    }

    [Fact]
    public void Session_WarningSelfElevates()
    {
        PerFileLogLevel.Filter filter = new(LogEventLevel.Warning);
        using IDisposable scope = PerFileLogLevel.BeginScope(LogEventLevel.Warning);
        _ = filter.IsEnabled(Event(LogEventLevel.Information)).Should().BeFalse();
        _ = filter.IsEnabled(Event(LogEventLevel.Warning)).Should().BeTrue();
        _ = filter.IsEnabled(Event(LogEventLevel.Information)).Should().BeTrue();
    }

    [Fact]
    public void Session_ErrorSelfElevates()
    {
        PerFileLogLevel.Filter filter = new(LogEventLevel.Warning);
        using IDisposable scope = PerFileLogLevel.BeginScope(LogEventLevel.Warning);
        _ = filter.IsEnabled(Event(LogEventLevel.Information)).Should().BeFalse();
        _ = filter.IsEnabled(Event(LogEventLevel.Error)).Should().BeTrue();
        _ = filter.IsEnabled(Event(LogEventLevel.Information)).Should().BeTrue();
    }

    [Fact]
    public void Session_ExplicitElevate_RaisesToInformationOnly()
    {
        PerFileLogLevel.Filter filter = new(LogEventLevel.Warning);
        using IDisposable scope = PerFileLogLevel.BeginScope(LogEventLevel.Warning);
        _ = filter.IsEnabled(Event(LogEventLevel.Information)).Should().BeFalse();

        PerFileLogLevel.Elevate();

        _ = filter.IsEnabled(Event(LogEventLevel.Information)).Should().BeTrue();
        _ = filter.IsEnabled(Event(LogEventLevel.Debug)).Should().BeFalse();
    }

    [Fact]
    public void Elevate_OutsideSession_IsNoOp()
    {
        PerFileLogLevel.Filter filter = new(LogEventLevel.Warning);
        PerFileLogLevel.Elevate();
        _ = filter.IsEnabled(Event(LogEventLevel.Information)).Should().BeFalse();
        _ = filter.IsEnabled(Event(LogEventLevel.Warning)).Should().BeTrue();
    }

    [Fact]
    public void NestedScope_RestoresOuterOnDispose()
    {
        PerFileLogLevel.Filter filter = new(LogEventLevel.Warning);
        using IDisposable outer = PerFileLogLevel.BeginScope(LogEventLevel.Warning);
        _ = filter.IsEnabled(Event(LogEventLevel.Information)).Should().BeFalse();

        using (PerFileLogLevel.BeginScope(LogEventLevel.Information))
        {
            _ = filter.IsEnabled(Event(LogEventLevel.Information)).Should().BeTrue();
        }

        _ = filter.IsEnabled(Event(LogEventLevel.Information)).Should().BeFalse();
    }

    [Fact]
    public void NewSession_StartsUnelevated()
    {
        PerFileLogLevel.Filter filter = new(LogEventLevel.Warning);
        using (PerFileLogLevel.BeginScope(LogEventLevel.Warning))
        {
            PerFileLogLevel.Elevate();
            _ = filter.IsEnabled(Event(LogEventLevel.Information)).Should().BeTrue();
        }

        using IDisposable fresh = PerFileLogLevel.BeginScope(LogEventLevel.Warning);
        _ = filter.IsEnabled(Event(LogEventLevel.Information)).Should().BeFalse();
    }

    [Fact]
    public void SidecarStateChange_DoesNotElevate()
    {
        // State changes no longer elevate logging; only Warning/Error events do. Detections are logged
        // as explicit warnings, which is what raises the per-file level.
        PerFileLogLevel.Filter filter = new(LogEventLevel.Warning);
        SidecarFile sidecar = new("/does-not-exist.mkv");
        using IDisposable scope = PerFileLogLevel.BeginScope(LogEventLevel.Warning);

        _ = filter.IsEnabled(Event(LogEventLevel.Information)).Should().BeFalse();
        sidecar.State = SidecarFile.StatesType.ReMuxed;
        _ = filter.IsEnabled(Event(LogEventLevel.Information)).Should().BeFalse();
    }
}

// Sidecar.State.PlexCleaner carries a persisted state; regenerate it if the sidecar schema changes.
public class PerFileLogLevelSidecarTests : SamplesFixture
{
    [Fact]
    public void LoadingPersistedState_DoesNotElevate()
    {
        PerFileLogLevel.Filter filter = new(LogEventLevel.Warning);
        // Only the sidecar fixture exists (no paired .mkv); verify:false reads the sidecar and
        // skips verification, loading the persisted state without media access or verify logging.
        SidecarFile sidecar = new(GetSampleFilePath("Sidecar.State.mkv"));
        using IDisposable scope = PerFileLogLevel.BeginScope(LogEventLevel.Warning);

        _ = sidecar.Read(out _, false).Should().BeTrue();
        _ = sidecar.State.Should().HaveFlag(SidecarFile.StatesType.SetLanguage);
        _ = sidecar.State.Should().HaveFlag(SidecarFile.StatesType.ReMuxed);
        _ = filter.IsEnabled(LogEventFactory.Create(LogEventLevel.Information)).Should().BeFalse();
    }
}
