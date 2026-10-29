using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

public partial class ArchitectureDiagramTests
{
    private static readonly string s_docsDirectory = Path.Combine(AppContext.BaseDirectory, "Docs");

    private static readonly Type[] s_unqualifiedTypes =
    [
        typeof(ProcessFile),
        typeof(Process),
        typeof(Program),
        typeof(SidecarFile),
        typeof(ProcessDriver),
        typeof(PlexCleaner.Monitor),
        typeof(PlexCleaner.Convert),
    ];

    [GeneratedRegex(@"operation = nameof\(processFile\.(\w+)\);")]
    private static partial Regex ProcessStepRegex();

    [GeneratedRegex(@"\bP(\d{2})\[""(\w+)\(\)")]
    private static partial Regex PipelineNodeRegex();

    [GeneratedRegex(@"[\[{(]""(?:(\w+)\.)?(\w+)\(\)")]
    private static partial Regex MethodLabelRegex();

    [Fact]
    public void PipelineDiagram_MatchesProcessFileStepOrder()
    {
        List<string> codeSteps =
        [
            .. ProcessStepRegex()
                .Matches(ReadDoc("Process.cs"))
                .Select(match => match.Groups[1].Value),
        ];
        _ = codeSteps.Should().NotBeEmpty();

        string pipeline = GetMermaidBlocks("### Per-File Pipeline").First();
        List<(int Id, string Name)> nodes =
        [
            .. PipelineNodeRegex()
                .Matches(pipeline)
                .Select(match =>
                    (
                        int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
                        match.Groups[2].Value
                    )
                )
                .OrderBy(node => node.Item1),
        ];

        _ = nodes.Select(node => node.Id).Should().Equal(Enumerable.Range(1, nodes.Count));
        _ = nodes.Select(node => node.Name).Should().Equal(codeSteps);
    }

    [Fact]
    public void ProcessDiagrams_NameExistingMethods()
    {
        List<Match> labels =
        [
            .. GetMermaidBlocks("## Process Command Logic")
                .SelectMany(block => MethodLabelRegex().Matches(block)),
        ];
        _ = labels.Should().NotBeEmpty();

        List<string> missing =
        [
            .. labels
                .Where(match =>
                    !MethodExists(
                        match.Groups[1].Success ? match.Groups[1].Value : null,
                        match.Groups[2].Value
                    )
                )
                .Select(match => match.Value[2..])
                .Distinct(),
        ];
        _ = missing.Should().BeEmpty(string.Join(", ", missing));
    }

    private static bool MethodExists(string? typeName, string method)
    {
        const BindingFlags flags =
            BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.Static
            | BindingFlags.Instance;
        Type? qualified =
            typeName == null
                ? null
                : typeof(ProcessFile).Assembly.GetType($"PlexCleaner.{typeName}");
        Type[] types =
            typeName == null ? s_unqualifiedTypes
            : qualified == null ? []
            : [qualified];
        return types.Any(type =>
            type.GetMethods(flags).Any(info => info.Name.Equals(method, StringComparison.Ordinal))
        );
    }

    private static string ReadDoc(string fileName) =>
        File.ReadAllText(Path.Combine(s_docsDirectory, fileName)).ReplaceLineEndings("\n");

    private static List<string> GetMermaidBlocks(string heading)
    {
        int level = heading.IndexOf(' ');
        List<string> blocks = [];
        List<string>? block = null;
        bool inSection = false;
        bool inFence = false;
        foreach (string line in ReadDoc("ARCHITECTURE.md").Split('\n'))
        {
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                inFence = !inFence;
                if (inSection && inFence && line == "```mermaid")
                {
                    block = [];
                }
                else if (!inFence && block != null)
                {
                    blocks.Add(string.Join('\n', block));
                    block = null;
                }
                continue;
            }
            if (!inFence && line.StartsWith('#'))
            {
                int lineLevel = line.IndexOf(' ');
                if (line == heading)
                {
                    inSection = true;
                }
                else if (inSection && lineLevel > 0 && lineLevel <= level)
                {
                    break;
                }
                continue;
            }
            block?.Add(line);
        }
        _ = blocks.Should().NotBeEmpty($"ARCHITECTURE.md must hold Mermaid under {heading}");
        return blocks;
    }
}
