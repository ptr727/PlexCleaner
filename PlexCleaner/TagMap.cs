namespace PlexCleaner
{
    public static partial class Info
    {
        public class TagMap
        {
            public string Primary { get; set; }
            public MediaInfo.ParserType PrimaryTool { get; set; }
            public string Secondary { get; set; }
            public MediaInfo.ParserType SecondaryTool { get; set; }
            public string Tertiary { get; set; }
            public MediaInfo.ParserType TertiaryTool { get; set; }
        }
    }
}