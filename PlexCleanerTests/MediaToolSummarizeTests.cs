using AwesomeAssertions;
using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

public class MediaToolSummarizeTests
{
    [Fact]
    public void Summarize_Keeps_The_First_And_Last_Lines_Of_Long_Output()
    {
        string text = string.Join(
            Environment.NewLine,
            Enumerable.Range(1, 20).Select(n => $"line {n}")
        );

        string[] summary = MediaTool
            .Summarize(text)
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        _ = summary
            .Should()
            .Equal(
                "line 1",
                "line 2",
                "line 13",
                "line 14",
                "line 15",
                "line 16",
                "line 17",
                "line 18",
                "line 19",
                "line 20"
            );
    }

    [Fact]
    public void Summarize_Keeps_Short_Output_Whole()
    {
        string text = string.Join(
            Environment.NewLine,
            Enumerable.Range(1, 5).Select(n => $"line {n}")
        );

        string[] summary = MediaTool
            .Summarize(text)
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        _ = summary.Should().Equal("line 1", "line 2", "line 3", "line 4", "line 5");
    }
}
