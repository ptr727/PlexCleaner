using System.Text.RegularExpressions;

namespace PlexCleaner;

internal static partial class VerifyClassifier
{
    // Non-monotonic-DTS muxer warnings emitted by the -f null muxer at error loglevel
    private static readonly List<string> s_timestampSignatures =
    [
        "non monotonically increasing dts to muxer",
    ];

    // Mask pointer addresses and standalone numbers so lines differing only by those collapse to one
    // key; word boundaries keep digits inside identifiers like h264 or mpeg2 so distinct codecs stay apart
    [GeneratedRegex(@"0x[0-9a-fA-F]+|\b[0-9]+\b")]
    private static partial Regex VariableDataRegex();

    private static string NormalizeKey(string line) => VariableDataRegex().Replace(line, "*");

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
        // Backstop against a pathological file with many distinct error types
        private const int MaxErrorLines = 50;

        private bool _timestamp;

        // Unique decode-error lines kept for the failure log, deduped by normalized key, insertion ordered
        private readonly HashSet<string> _errorKeys = [];
        private readonly List<string> _errors = [];

        public bool HasErrors => _errors.Count > 0;

        public IReadOnlyList<string> Errors => _errors;

        public VerifyResult Result =>
            HasErrors ? VerifyResult.DecodeError
            : _timestamp ? VerifyResult.TimestampOnly
            : VerifyResult.Clean;

        public void Add(string line)
        {
            line = line.Trim();
            if (line.Length == 0)
            {
                return;
            }

            // ffmpeg collapses consecutive identical messages into this marker, it repeats the previous
            // line's classification which is already recorded, so ignore it
            if (line.StartsWith("Last message repeated", StringComparison.Ordinal))
            {
                return;
            }

            // A muxer timestamp warning classifies as TimestampOnly, distinct from a decode error
            if (
                s_timestampSignatures.Any(sig =>
                    line.Contains(sig, StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                _timestamp = true;
                return;
            }

            // Any other line at error loglevel is a decode error, fail closed; keep the unique lines for
            // the log, deduped by normalized key and capped as a backstop
            if (_errors.Count < MaxErrorLines && _errorKeys.Add(NormalizeKey(line)))
            {
                _errors.Add(line);
            }
        }
    }
}
