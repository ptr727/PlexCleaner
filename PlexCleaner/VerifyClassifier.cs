namespace PlexCleaner;

internal static class VerifyClassifier
{
    // Non-monotonic-DTS muxer warnings emitted by the -f null muxer at error loglevel
    private static readonly List<string> s_timestampSignatures =
    [
        "non monotonically increasing dts to muxer",
    ];

    public static VerifyResult Classify(string stderr)
    {
        Accumulator accumulator = new();
        if (!string.IsNullOrEmpty(stderr))
        {
            foreach (string line in stderr.Split('\n'))
            {
                accumulator.Add(line);
            }
        }
        return accumulator.Result;
    }

    public sealed class Accumulator
    {
        private bool _decodeError;
        private bool _timestamp;

        public string? FirstError { get; private set; }

        public VerifyResult Result =>
            _decodeError ? VerifyResult.DecodeError
            : _timestamp ? VerifyResult.TimestampOnly
            : VerifyResult.Clean;

        public void Add(string line)
        {
            line = line.Trim();
            if (line.Length == 0)
            {
                return;
            }

            // A benign muxer timestamp warning does not by itself fail verify
            if (
                s_timestampSignatures.Any(sig =>
                    line.Contains(sig, StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                _timestamp = true;
                return;
            }

            // Anything else at error loglevel is treated as decode corruption, fail closed
            _decodeError = true;
            FirstError ??= line;
        }
    }
}
