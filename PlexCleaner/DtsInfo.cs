namespace PlexCleaner;

public sealed class DtsInfo
{
    // Last DTS seen per stream index
    private readonly Dictionary<long, double> _lastDts = [];

    // Count of non-monotonic packets per stream index
    private readonly Dictionary<long, int> _nonMonotonicByStream = [];

    // Codec type per stream index, used to decide whether the audio setts filter can repair the DTS
    private readonly Dictionary<long, string> _codecTypeByStream = [];

    // Stream indexes carrying a non-monotonic DTS, with the per-stream count
    public IReadOnlyDictionary<long, int> NonMonotonicByStream => _nonMonotonicByStream;

    // True if any stream carries a non-monotonic DTS
    public bool HasNonMonotonicDts => _nonMonotonicByStream.Count > 0;

    // True when every stream carrying a non-monotonic DTS is audio, so the audio-only setts filter can
    // repair it; a video or subtitle DTS is not audio-repairable (a video setts would reorder B-frames)
    public bool NonMonotonicIsAudioOnly =>
        HasNonMonotonicDts
        && _nonMonotonicByStream.Keys.All(index =>
            _codecTypeByStream.TryGetValue(index, out string? codecType)
            && codecType.Equals("audio", StringComparison.OrdinalIgnoreCase)
        );

    public void Add(FfMpegToolJsonSchema.Packet packet)
    {
        // Assess only packets that carry a real DTS. A missing DTS (e.g. Matroska video, which stores no
        // DTS) is reconstructed by the muxer from PTS, whose display order is legitimately non-monotonic
        // for B-frames, so it cannot be judged from packet data and must not be flagged
        if (double.IsNaN(packet.DtsTime))
        {
            return;
        }

        // Record the codec type so a flagged stream can be classified as audio or not
        _codecTypeByStream[packet.StreamIndex] = packet.CodecType;

        // Flag a non-increasing DTS relative to the previous packet in the same stream
        if (
            _lastDts.TryGetValue(packet.StreamIndex, out double previous)
            && packet.DtsTime <= previous
        )
        {
            _nonMonotonicByStream[packet.StreamIndex] =
                _nonMonotonicByStream.GetValueOrDefault(packet.StreamIndex) + 1;
        }
        _lastDts[packet.StreamIndex] = packet.DtsTime;
    }
}
