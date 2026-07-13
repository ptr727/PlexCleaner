namespace PlexCleaner;

public sealed class DtsInfo
{
    // Last DTS seen per stream index
    private readonly Dictionary<long, double> _lastDts = [];

    // Count of non-monotonic packets per stream index
    private readonly Dictionary<long, int> _nonMonotonicByStream = [];

    // Stream indexes carrying a non-monotonic DTS, with the per-stream count
    public IReadOnlyDictionary<long, int> NonMonotonicByStream => _nonMonotonicByStream;

    // True if any stream carries a non-monotonic DTS
    public bool HasNonMonotonicDts => _nonMonotonicByStream.Count > 0;

    public void Add(FfMpegToolJsonSchema.Packet packet)
    {
        // Fall back to PTS when DTS is absent, matching how the muxer derives DTS
        double dts = !double.IsNaN(packet.DtsTime) ? packet.DtsTime : packet.PtsTime;
        if (double.IsNaN(dts))
        {
            return;
        }

        // Flag a non-increasing DTS relative to the previous packet in the same stream
        if (_lastDts.TryGetValue(packet.StreamIndex, out double previous) && dts <= previous)
        {
            _nonMonotonicByStream[packet.StreamIndex] =
                _nonMonotonicByStream.GetValueOrDefault(packet.StreamIndex) + 1;
        }
        _lastDts[packet.StreamIndex] = dts;
    }
}
