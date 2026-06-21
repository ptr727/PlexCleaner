using System.Buffers.Binary;
using NEbml.Core;

namespace PlexCleaner;

// Structural Matroska read mirroring Jellyfin's MatroskaKeyframeExtractor (MIT licensed)
// Reproduces the same EBML parse the player performs, throwing on files that cannot be Direct Played
// https://github.com/jellyfin/jellyfin/tree/master/src/Jellyfin.MediaEncoding.Keyframes/Matroska
internal static class MatroskaKeyframe
{
    // Element IDs from the Matroska specification, look up names by value
    // https://www.matroska.org/technical/elements.html
    // https://github.com/ietf-wg-cellar/matroska-specification/blob/master/ebml_matroska.xml
    private const ulong SegmentContainer = 0x18538067;
    private const ulong SeekHead = 0x114D9B74;
    private const ulong Seek = 0x4DBB;
    private const ulong Info = 0x1549A966;
    private const ulong TimestampScale = 0x2AD7B1;
    private const ulong Duration = 0x4489;
    private const ulong Tracks = 0x1654AE6B;
    private const ulong TrackEntry = 0xAE;
    private const ulong TrackNumber = 0xD7;
    private const ulong TrackType = 0x83;
    private const ulong TrackTypeVideo = 0x1;
    private const ulong Cues = 0x1C53BB6B;
    private const ulong CueTime = 0xB3;
    private const ulong CuePoint = 0xBB;
    private const ulong CueTrackPositions = 0xB7;
    private const ulong CuePointTrackNumber = 0xF7;

    public static void Parse(string filePath)
    {
        using FileStream stream = File.OpenRead(filePath);
        using EbmlReader reader = new(stream);

        // All positions are relative to the Segment container
        (long infoPosition, long tracksPosition, long cuesPosition) = ReadSeekHead(reader);

        // The external lib does not support seeking backwards, read in file order
        if (infoPosition < tracksPosition)
        {
            ReadInfo(reader, infoPosition);
            _ = FindFirstVideoTrackNumber(reader, tracksPosition);
        }
        else
        {
            _ = FindFirstVideoTrackNumber(reader, tracksPosition);
            ReadInfo(reader, infoPosition);
        }

        // Walk the cues, malformed files throw here
        _ = reader.ReadAt(cuesPosition);
        reader.EnterContainer();
        while (FindElement(reader, CuePoint))
        {
            reader.EnterContainer();

            // Mandatory element
            _ = FindElement(reader, CueTime);
            _ = reader.ReadUInt();

            // Mandatory element
            _ = FindElement(reader, CueTrackPositions);
            reader.EnterContainer();
            if (FindElement(reader, CuePointTrackNumber))
            {
                _ = reader.ReadUInt();
            }

            reader.LeaveContainer();
            reader.LeaveContainer();
        }

        reader.LeaveContainer();
    }

    private static bool FindElement(EbmlReader reader, ulong identifier)
    {
        while (reader.ReadNext())
        {
            if (reader.ElementId.EncodedValue == identifier)
            {
                return true;
            }
        }

        return false;
    }

    private static uint ReadUIntFromBinary(EbmlReader reader)
    {
        byte[] buffer = new byte[4];
        _ = reader.ReadBinary(buffer, 0, 4);
        return BinaryPrimitives.ReadUInt32BigEndian(buffer);
    }

    private static (long InfoPosition, long TracksPosition, long CuesPosition) ReadSeekHead(
        EbmlReader reader
    )
    {
        if (reader.ElementPosition != 0)
        {
            throw new InvalidOperationException("File position must be at 0");
        }

        // Skip the header
        if (!FindElement(reader, SegmentContainer))
        {
            throw new InvalidOperationException("Expected a segment container");
        }

        reader.EnterContainer();

        long? tracksPosition = null;
        long? cuesPosition = null;
        long? infoPosition = null;

        // The first element should be a SeekHead otherwise we have to search manually
        if (!FindElement(reader, SeekHead))
        {
            throw new InvalidOperationException("Expected a SeekHead");
        }

        reader.EnterContainer();
        while (FindElement(reader, Seek))
        {
            reader.EnterContainer();
            _ = reader.ReadNext();
            ulong type = ReadUIntFromBinary(reader);
            switch (type)
            {
                case Tracks:
                    _ = reader.ReadNext();
                    tracksPosition = (long)reader.ReadUInt();
                    break;
                case Info:
                    _ = reader.ReadNext();
                    infoPosition = (long)reader.ReadUInt();
                    break;
                case Cues:
                    _ = reader.ReadNext();
                    cuesPosition = (long)reader.ReadUInt();
                    break;
                default:
                    break;
            }

            reader.LeaveContainer();

            if (tracksPosition.HasValue && cuesPosition.HasValue && infoPosition.HasValue)
            {
                break;
            }
        }

        reader.LeaveContainer();

        return !tracksPosition.HasValue || !cuesPosition.HasValue || !infoPosition.HasValue
            ? throw new InvalidOperationException(
                "SeekHead is missing or does not contain Info, Tracks and Cues positions"
            )
            : ((long InfoPosition, long TracksPosition, long CuesPosition))
                (infoPosition.Value, tracksPosition.Value, cuesPosition.Value);
    }

    private static void ReadInfo(EbmlReader reader, long position)
    {
        _ = reader.ReadAt(position);
        reader.EnterContainer();

        // Mandatory element
        _ = FindElement(reader, TimestampScale);
        _ = reader.ReadUInt();

        if (FindElement(reader, Duration))
        {
            _ = reader.ReadFloat();
        }

        reader.LeaveContainer();
    }

    private static ulong FindFirstVideoTrackNumber(EbmlReader reader, long tracksPosition)
    {
        _ = reader.ReadAt(tracksPosition);
        reader.EnterContainer();
        while (FindElement(reader, TrackEntry))
        {
            reader.EnterContainer();

            // Mandatory element
            _ = FindElement(reader, TrackNumber);
            ulong trackNumber = reader.ReadUInt();

            // Mandatory element
            _ = FindElement(reader, TrackType);
            ulong trackType = reader.ReadUInt();

            reader.LeaveContainer();
            if (trackType == TrackTypeVideo)
            {
                reader.LeaveContainer();
                return trackNumber;
            }
        }

        reader.LeaveContainer();

        throw new InvalidOperationException("No video track found");
    }
}
