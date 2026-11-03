using AwesomeAssertions;
using PlexCleaner;
using Serilog.Events;
using Xunit;

namespace PlexCleanerTests;

[Collection("Sequential")]
public class MediaToolInfoTests
{
    private static readonly DateTime s_modifiedTime = new(2024, 8, 11, 12, 0, 0, DateTimeKind.Utc);

    internal static MediaToolInfo NewToolInfo() =>
        new(new SevenZip.Tool())
        {
            FileName = "7za.exe",
            ModifiedTime = s_modifiedTime,
            Size = 1024,
            Url = "https://example.com/7z2408-extra.7z",
            Version = "24.08-full",
        };

    [Fact]
    public void Constructor_From_MediaTool_Sets_Type_And_Family()
    {
        MediaToolInfo toolInfo = new(new SevenZip.Tool());

        _ = toolInfo.ToolType.Should().Be(MediaTool.ToolType.SevenZip);
        _ = toolInfo.ToolFamily.Should().Be(MediaTool.ToolFamily.SevenZip);
    }

    [Fact]
    public void CompareTo_Identical_Info_Returns_Zero()
    {
        MediaToolInfo other = NewToolInfo();
        other.FileName = other.FileName.ToUpperInvariant();
        other.Version = other.Version.ToUpperInvariant();

        _ = NewToolInfo().CompareTo(other).Should().Be(0);
    }

    [Fact]
    public void CompareTo_Different_FileName_Orders_By_FileName()
    {
        MediaToolInfo other = NewToolInfo();
        other.FileName = "8za.exe";

        _ = NewToolInfo().CompareTo(other).Should().BeNegative();
        _ = other.CompareTo(NewToolInfo()).Should().BePositive();
    }

    [Fact]
    public void CompareTo_Different_ModifiedTime_Orders_By_ModifiedTime()
    {
        MediaToolInfo other = NewToolInfo();
        other.ModifiedTime = s_modifiedTime.AddDays(1);

        _ = NewToolInfo().CompareTo(other).Should().BeNegative();
    }

    [Fact]
    public void CompareTo_Different_Size_Orders_By_Size()
    {
        MediaToolInfo other = NewToolInfo();
        other.Size = 2048;

        _ = NewToolInfo().CompareTo(other).Should().BeNegative();
    }

    [Fact]
    public void CompareTo_Different_Version_Orders_By_Version()
    {
        MediaToolInfo other = NewToolInfo();
        other.Version = "24.09-full";

        _ = NewToolInfo().CompareTo(other).Should().BeNegative();
    }

    [Fact]
    public void Copy_Copies_Every_Field()
    {
        MediaToolInfo source = NewToolInfo();
        MediaToolInfo target = new();

        target.Copy(source);

        _ = target.Should().BeEquivalentTo(source);
    }

    [Fact]
    public void WriteLine_Logs_Prefix_And_Version()
    {
        IReadOnlyList<LogEvent> events = LogCapture.Capture(() =>
            NewToolInfo().WriteLine("Latest Version")
        );

        List<string> messages = LogCapture.Messages(events, LogEventLevel.Information);
        _ = messages.Should().ContainSingle();
        _ = messages[0].Should().Contain("Latest Version").And.Contain("24.08-full");
    }
}
