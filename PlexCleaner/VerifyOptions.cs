using InsaneGenius.Utilities;

namespace PlexCleaner
{
    public class VerifyOptions
    {
        public bool AutoRepair { get; set; } = true;
        public bool DeleteInvalidFiles { get; set; } = true;
        public int MinimumDuration { get; set; } = 300;
        public int VerifyDuration { get; set; } = 0;
        public int IdetDuration { get; set; } = 0;
        public int MaximumBitrate { get; set; } = 100 * Format.MB;
        public int MinimumFileAge { get; set; } = 0;
    }
}
