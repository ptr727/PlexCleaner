using NEbml.Core;
using Serilog;

namespace PlexCleaner;

// Read-only logical validation of a Matroska seek index using the NEbml (MIT) EBML reader
// Media players locate keyframes through the SeekHead and Cues elements, a file whose seek index
// cannot be resolved will not Direct Play even when the elementary streams decode cleanly
// This validates the structure and returns a verdict, it does not depend on the reader throwing
// Element IDs and the SeekHead / Cues layout are from the Matroska element specification
// https://www.matroska.org/technical/elements.html
// https://github.com/ietf-wg-cellar/matroska-specification/blob/master/ebml_matroska.xml
internal static class MatroskaStructure
{
    // EBML element IDs (encoded values) from the Matroska specification
    private const ulong Segment = 0x18538067;
    private const ulong SeekHead = 0x114D9B74;
    private const ulong SeekEntry = 0x4DBB;
    private const ulong SeekId = 0x53AB;
    private const ulong SeekPosition = 0x53AC;
    private const ulong Info = 0x1549A966;
    private const ulong Tracks = 0x1654AE6B;
    private const ulong Cues = 0x1C53BB6B;
    private const ulong CuePoint = 0xBB;

    // Returns false when the seek index is missing or unusable, i.e. the file should be remuxed
    public static bool IsSeekIndexValid(string fileName)
    {
        try
        {
            using FileStream stream = File.OpenRead(fileName);
            using EbmlReader reader = new(stream);

            // Descend into the Segment, all seek positions are relative to it
            if (!FindElement(reader, Segment))
            {
                return false;
            }
            reader.EnterContainer();

            // A usable seek index references the Info, Tracks and Cues elements
            if (!TryReadSeekPositions(reader, out long info, out long tracks, out long cues))
            {
                return false;
            }

            // A player reads Info and Tracks before seeking forward to the Cues
            // Cues placed before the Tracks or Info cannot be reached and break keyframe seeking
            if (cues < info || cues < tracks)
            {
                return false;
            }

            // Confirm the Cues element is present at the referenced position and has cue points
            // ReadAt is relative to the entered Segment container, matching the Segment-relative SeekHead positions
            _ = reader.ReadAt(cues);
            if (reader.ElementId.EncodedValue != Cues)
            {
                return false;
            }

            // Confirm at least one cue point is present, an empty index is not usable
            // The first entry is enough, no need to walk a long index
            reader.EnterContainer();
            bool anyCuePoint = FindElement(reader, CuePoint);
            reader.LeaveContainer();

            return anyCuePoint;
        }
        catch (Exception e) when (Log.Logger.LogAndHandle(e))
        {
            // Structure too malformed for the reader to parse
            return false;
        }
    }

    // Advance within the current container to the next element with the given id, false at end of container
    private static bool FindElement(EbmlReader reader, ulong elementId)
    {
        while (reader.ReadNext())
        {
            if (reader.ElementId.EncodedValue == elementId)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryReadSeekPositions(
        EbmlReader reader,
        out long info,
        out long tracks,
        out long cues
    )
    {
        info = -1;
        tracks = -1;
        cues = -1;
        if (!FindElement(reader, SeekHead))
        {
            return false;
        }

        // Each Seek entry maps a target element id to its byte position within the Segment
        reader.EnterContainer();
        while (FindElement(reader, SeekEntry))
        {
            reader.EnterContainer();
            ulong targetId = 0;
            long targetPosition = -1;
            while (reader.ReadNext())
            {
                switch (reader.ElementId.EncodedValue)
                {
                    case SeekId:
                        targetId = ReadEbmlId(reader);
                        break;
                    case SeekPosition:
                        targetPosition = (long)reader.ReadUInt();
                        break;
                    default:
                        break;
                }
            }
            reader.LeaveContainer();

            switch (targetId)
            {
                case Info:
                    info = targetPosition;
                    break;
                case Tracks:
                    tracks = targetPosition;
                    break;
                case Cues:
                    cues = targetPosition;
                    break;
                default:
                    break;
            }
        }
        reader.LeaveContainer();

        // All three must be referenced for a usable seek index
        return info >= 0 && tracks >= 0 && cues >= 0;
    }

    // The SeekID element holds the raw encoded EBML id of the target element
    private static ulong ReadEbmlId(EbmlReader reader)
    {
        long size = reader.ElementSize;
        if (size is < 1 or > 4)
        {
            return 0;
        }

        byte[] buffer = new byte[4];
        _ = reader.ReadBinary(buffer, 0, (int)size);
        ulong id = 0;
        for (int i = 0; i < size; i++)
        {
            id = (id << 8) | buffer[i];
        }
        return id;
    }
}
