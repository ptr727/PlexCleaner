namespace PlexCleaner
{
    public class VerifyOptions
    {
        public bool DeleteInvalidFiles { get; set; } = true;
        public int MinimumDuration { get; set; } = 300;
        public int VerifyDuration { get; set; } = 60;
        public int IdetDuration { get; set; } = 60;
    }
}
