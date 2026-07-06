namespace PlexCleaner;

public class TagMap
{
    public string Primary { get; set; } = string.Empty;
    public MediaTool.ToolType PrimaryTool { get; set; }
    public string Secondary { get; set; } = string.Empty;
    public MediaTool.ToolType SecondaryTool { get; set; }
    public string Tertiary { get; set; } = string.Empty;
    public MediaTool.ToolType TertiaryTool { get; set; }
    public int Count { get; set; }
}
