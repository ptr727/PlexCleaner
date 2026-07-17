namespace PlexCleaner;

public enum VerifyResult
{
    // Decode succeeded with no diagnostic output
    Clean,

    // The only diagnostics are muxer timestamp warnings (non-monotonic DTS)
    TimestampOnly,

    // A genuine decode or demux corruption signature, or any unrecognized diagnostic, verify fails
    DecodeError,
}
