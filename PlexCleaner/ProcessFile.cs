using System.Diagnostics;
using Serilog.Events;

namespace PlexCleaner;

public class ProcessFile
{
    // HDR10 (SMPTE ST 2086) or HDR10+ (SMPTE ST 2094) (Using MediaInfo tags)
    public static readonly List<string> Hdr10FormatList =
    [
        MediaInfo.HDR10Format,
        MediaInfo.HDR10PlusFormat,
    ];

    // ReEncode audio unless video is H264, H265 or AV1 (using MediaInfo tags)
    public static readonly List<string> ReEncodeVideoOnAudioReEncodeList =
    [
        MediaInfo.H264Format,
        MediaInfo.H265Format,
        MediaInfo.AV1Format,
    ];

    private SidecarFile _sidecarFile;

    // Classification of the most recent stream verify, used to choose the repair strategy
    private VerifyResult _lastVerifyResult;

    public ProcessFile(string mediaFile)
    {
        FileInfo = new FileInfo(mediaFile);
        _sidecarFile = new SidecarFile(FileInfo);
    }

    public MediaProps FfProbeProps { get; private set; } = null!;
    public MediaProps MkvMergeProps { get; private set; } = null!;
    public MediaProps MediaInfoProps { get; private set; } = null!;
    public SidecarFile.StatesType State => _sidecarFile.State;
    public FileInfo FileInfo { get; private set; }

    public bool DeleteMismatchedSidecarFile(ref bool modified)
    {
        // Is this a sidecar file
        if (!SidecarFile.IsSidecarFile(FileInfo))
        {
            // Nothing to do
            return true;
        }

        // Get the matching MKV file
        string mediaFile = SidecarFile.GetMkvName(FileInfo);

        // Does the media file exist
        if (File.Exists(mediaFile))
        {
            // File exists, nothing more to do
            return true;
        }

        // Media file does not exists, delete this sidecar file
        Log.Information(
            "Deleting sidecar file with no matching MKV file : {FileName}",
            FileInfo.FullName
        );

        // Delete the file
        File.Delete(FileInfo.FullName);

        // File deleted, do not continue processing
        modified = true;
        _sidecarFile.State |= SidecarFile.StatesType.FileDeleted;
        return false;
    }

    public bool DeleteNonMkvFile(ref bool modified)
    {
        // If MKV file nothing to do
        if (SidecarFile.IsMkvFile(FileInfo))
        {
            return true;
        }

        // Only delete if the option is enabled else just skip
        if (!Program.Config.ProcessOptions.DeleteUnwantedExtensions)
        {
            Log.Warning("Skipping non-MKV file : {FileName}", FileInfo.FullName);
            return false;
        }

        // Non-MKV file, delete
        Log.Warning("Deleting non-MKV file : {FileName}", FileInfo.FullName);

        // Delete the file
        File.Delete(FileInfo.FullName);

        // File deleted, do not continue processing
        modified = true;
        _sidecarFile.State |= SidecarFile.StatesType.FileDeleted;
        return false;
    }

    public bool MakeExtensionLowercase(ref bool modified)
    {
        // Is the extension lowercase
        string lowerExtension = FileInfo.Extension.ToLowerInvariant();
        if (FileInfo.Extension.Equals(lowerExtension, StringComparison.Ordinal))
        {
            return true;
        }

        // Detected: file extension is not lowercase
        Log.Warning(
            "Uppercase file extension detected : Extension: {Extension} : {FileName}",
            FileInfo.Extension,
            FileInfo.FullName
        );

        // Make the extension lowercase
        Log.Information("Making file extension lowercase : {FileName}", FileInfo.FullName);

        // Rename the file
        // Windows is case insensitive, so we need to rename in two steps
        string tempName = Path.ChangeExtension(FileInfo.FullName, ".tmp7");
        Debug.Assert(tempName != FileInfo.FullName);
        File.Move(FileInfo.FullName, tempName, true);
        string lowerName = Path.ChangeExtension(FileInfo.FullName, lowerExtension);
        Debug.Assert(lowerName != tempName);
        File.Move(tempName, lowerName, true);

        // Modified filename
        modified = true;
        return Refresh(lowerName);
    }

    public bool IsWriteable() => FileInfo is { Exists: true, IsReadOnly: false };

    public bool IsSidecarAvailable() => _sidecarFile.Exists();

    public bool IsSidecarWriteable() => _sidecarFile.IsWriteable();

    public bool RemuxByExtension(bool conditional, ref bool modified)
    {
        // Optional
        if (conditional && !Program.Config.ProcessOptions.ReMux)
        {
            return true;
        }

        // Does the extension match
        if (!Program.Config.ProcessOptions.ReMuxExtensions.Contains(FileInfo.Extension))
        {
            // Nothing to do
            return true;
        }

        // Detected: extension in the ReMux extension list
        Log.Warning(
            "ReMux extension detected : Extension: {Extension} : {FileName}",
            FileInfo.Extension,
            FileInfo.FullName
        );

        // ReMux the file
        Log.Information("Remux file matched by extension : {FileName}", FileInfo.FullName);

        // ReMux the file, use the new filename
        if (!Convert.ReMuxToMkv(FileInfo.FullName, out string outputName))
        {
            // Error
            return false;
        }

        // Extension may have changed
        // Refresh
        modified = true;
        _sidecarFile.State |= SidecarFile.StatesType.ReMuxed;
        return Refresh(outputName);
    }

    public bool RemuxNonMkvContainer(ref bool modified)
    {
        // Make sure that MKV named files are Matroska containers
        if (MkvMergeProps.IsContainerMkv())
        {
            // Nothing to do
            return true;
        }

        // Optional, but required for correct processing
        if (!Program.Config.ProcessOptions.ReMux)
        {
            // Error, MKV files must be Matroska, enable ReMux option
            Log.Error(
                "MKV file is not in Matroska format, ReMux option not enabled : Container: {Container} : {FileName}",
                MkvMergeProps.Container,
                FileInfo.FullName
            );
            return false;
        }

        // Detected: MKV file is not a Matroska container
        Log.Warning(
            "Non-Matroska container detected : Container: {Container} : {FileName}",
            MkvMergeProps.Container,
            FileInfo.FullName
        );

        // ReMux the file
        Log.Information(
            "ReMux {Container} to Matroska : {FileName}",
            MkvMergeProps.Container,
            FileInfo.FullName
        );

        // ReMux the file, use the new filename
        if (!Convert.ReMuxToMkv(FileInfo.FullName, out string outputName))
        {
            // Error
            return false;
        }

        // Extension may have changed
        // Refresh
        modified = true;
        _sidecarFile.State |= SidecarFile.StatesType.ReMuxed;
        return Refresh(outputName);
    }

    public bool HasMetadataErrors() =>
        FfProbeProps.AnyErrors || MkvMergeProps.AnyErrors || MediaInfoProps.AnyErrors;

    public bool HasMetadataErrors(TrackProps.StateType stateType) =>
        FfProbeProps.GetTrackList().Any(item => item.State == stateType)
        || MkvMergeProps.GetTrackList().Any(item => item.State == stateType)
        || MediaInfoProps.GetTrackList().Any(item => item.State == stateType);

    public void ClearMetadataErrors()
    {
        // Clear all the error flags, including per track HasErrors which AnyErrors checks
        FfProbeProps.HasErrors = false;
        MkvMergeProps.HasErrors = false;
        MediaInfoProps.HasErrors = false;
        ClearTrackErrors(FfProbeProps);
        ClearTrackErrors(MkvMergeProps);
        ClearTrackErrors(MediaInfoProps);

        static void ClearTrackErrors(MediaProps mediaProps) =>
            mediaProps
                .GetTrackList()
                .ForEach(item =>
                {
                    item.HasErrors = false;
                    item.State = TrackProps.StateType.None;
                });
    }

    public bool RepairMetadataErrors(ref bool modified)
    {
        // Conditional
        if (!Program.Config.VerifyOptions.AutoRepair)
        {
            // If repair is not enabled just clear any error states
            ClearMetadataErrors();
            return true;
        }

        // Do not re-attempt repairs on a file that previously failed to converge, else monitor mode loops
        if (_sidecarFile.State.HasFlag(SidecarFile.StatesType.VerifyFailed))
        {
            ClearMetadataErrors();
            return true;
        }

        // Any metadata errors to repair
        if (!HasMetadataErrors())
        {
            // Nothing to do
            return true;
        }

        // Normalize invalid language tags in place, remuxing cannot fix these
        if (!RepairLanguageTags(ref modified))
        {
            return false;
        }

        // Any metadata errors left to repair after normalizing language tags
        if (!HasMetadataErrors())
        {
            // Done
            return true;
        }

        // Start with remuxing
        if (!RepairMetadataRemux(ref modified))
        {
            return false;
        }

        // Any metadata errors left to repair after remuxing
        if (!HasMetadataErrors())
        {
            // Done
            return true;
        }

        // Set track flags
        return RepairMetadataFlags(ref modified);
    }

    private static bool HasInvalidLanguageTag(TrackProps item) =>
        // ISO 639 or IETF tag is set but cannot be mapped to its counterpart
        (
            !Language.IsUndefined(item.Language)
            && string.IsNullOrEmpty(Language.Lookup.GetIetfFromIso(item.Language))
        )
        || (
            !Language.IsUndefined(item.LanguageIetf)
            && string.IsNullOrEmpty(Language.Lookup.GetIsoFromIetf(item.LanguageIetf))
        );

    private static string ResolveLanguageTag(TrackProps item)
    {
        // A valid IETF tag wins and recovers the ISO 639 tag
        if (
            !Language.IsUndefined(item.LanguageIetf)
            && !string.IsNullOrEmpty(Language.Lookup.GetIsoFromIetf(item.LanguageIetf))
        )
        {
            return item.LanguageIetf;
        }

        // A valid ISO 639 tag recovers the IETF tag
        string ietfFromIso = Language.Lookup.GetIetfFromIso(item.Language);
        if (!Language.IsUndefined(item.Language) && !string.IsNullOrEmpty(ietfFromIso))
        {
            return ietfFromIso;
        }

        // Neither is valid
        return Language.Undefined;
    }

    public bool RepairLanguageTags(ref bool modified)
    {
        // Invalid tags cannot be fixed by remuxing as mkvmerge preserves them, set them in place
        // Use MkvMerge for IETF language tags
        // Selected is Invalid, NotSelected is Keep
        SelectMediaProps selectMediaProps = new(MkvMergeProps, HasInvalidLanguageTag);
        if (selectMediaProps.Selected.Count == 0)
        {
            // Nothing to do
            return true;
        }

        // Detected: tracks with invalid language tags
        Log.Warning(
            "Invalid language tags detected : Tracks: {Tracks} : {FileName}",
            selectMediaProps.Selected.Count,
            FileInfo.FullName
        );

        Log.Information("Setting invalid language tags : {FileName}", FileInfo.FullName);
        selectMediaProps.WriteLine("Invalid", "Keep");

        // Recover from the valid tag, else undefined
        // MkvPropEdit sets the legacy language property to this value, the --normalize-language-ietf global
        // option then derives a consistent IETF tag, overwriting the invalid one
        selectMediaProps
            .Selected.GetTrackList()
            .ForEach(item => item.LanguageIetf = ResolveLanguageTag(item));

        // Include undefined to overwrite the invalid tag
        if (
            !Tools.MkvPropEdit.SetTrackLanguage(
                FileInfo.FullName,
                selectMediaProps.Selected,
                includeUndefined: true
            )
        )
        {
            // Error
            return false;
        }

        // Refresh
        modified = true;
        _sidecarFile.State |= SidecarFile.StatesType.SetLanguage;
        if (!Refresh(true))
        {
            return false;
        }

        // Stop if the tags are still invalid
        return !MkvMergeProps.GetTrackList().Exists(HasInvalidLanguageTag)
            || SetVerifyFailed("setting language tags");
    }

    private bool SetVerifyFailed(string operation)
    {
        // Mark verify failed so the file is reported and no longer re-processed
        Log.Error(
            "Verification failed, marking as VerifyFailed : {Operation} : {FileName}",
            operation,
            FileInfo.FullName
        );
        _sidecarFile.State |= SidecarFile.StatesType.VerifyFailed;
        _sidecarFile.State &= ~SidecarFile.StatesType.Verified;
        _ = Refresh(false);

        // Always returns false to signal the repair did not succeed
        return false;
    }

    public bool RepairMetadataRemux(ref bool modified)
    {
        // Conditional
        if (!Program.Config.ProcessOptions.ReMux)
        {
            // Done
            return true;
        }

        // Per MkvToolNix docs remux is the recommended approach to correcting language tags
        // Honor Program.Config.ProcessOptions.SetIetfLanguageTags as lots of older media does not have IETF tags set

        // Any tracks need remuxing, evaluate each once and reuse for the detection warning
        bool removeErrors = HasMetadataErrors(TrackProps.StateType.Remove);
        bool remuxErrors = HasMetadataErrors(TrackProps.StateType.ReMux);
        bool setLanguageErrors =
            Program.Config.ProcessOptions.SetIetfLanguageTags
            && HasMetadataErrors(TrackProps.StateType.SetLanguage);
        if (!removeErrors && !remuxErrors && !setLanguageErrors)
        {
            // Done
            return true;
        }

        // Start with keeping all tracks
        // Selected is Keep
        // NotSelected is Remove
        SelectMediaProps selectMediaProps = new(MkvMergeProps, true);

        // TODO: Remove is currently only set by MediaInfo for subtitle tracks that need to be removed
        // Mapping of track Id's are non-trivial, use the Matroska header track number to find the matching tracks
        List<TrackProps> mediaInfoRemoveList = MediaInfoProps
            .GetTrackList()
            .FindAll(item => item.State == TrackProps.StateType.Remove);
        List<TrackProps> mkvMergeRemoveList = MkvMergeProps.MatchMediaInfoToMkvMerge(
            mediaInfoRemoveList
        );
        mkvMergeRemoveList.ForEach(item => item.State = TrackProps.StateType.Remove);
        Debug.Assert(mediaInfoRemoveList.Count == mkvMergeRemoveList.Count);

        // To be removed tracks
        selectMediaProps.Move(mkvMergeRemoveList, false);

        // Do not call SetState() on items that are not in scope, further processing is done by state

        // Detected: metadata errors requiring a remux
        Log.Warning(
            "Metadata errors detected : Remove: {Remove}, ReMux: {ReMux}, SetLanguage: {SetLanguage} : {FileName}",
            removeErrors,
            remuxErrors,
            setLanguageErrors,
            FileInfo.FullName
        );

        // ReMux the file
        Log.Information("Remux to repair metadata errors : {FileName}", FileInfo.FullName);
        selectMediaProps.WriteLine("Keep", "Remove");

        // Conditional with tracks or all tracks
        if (
            !Convert.ReMuxToMkv(
                FileInfo.FullName,
                selectMediaProps.NotSelected.Count > 0 ? selectMediaProps : null,
                out string outputName
            )
        )
        {
            // Error
            return false;
        }

        // Refresh
        modified = true;
        _sidecarFile.State |= SidecarFile.StatesType.ReMuxed;
        if (!Refresh(outputName))
        {
            return false;
        }

        // Stop if the remux did not resolve the errors it targets, else monitor mode loops
        // Mirror the trigger condition above, including SetLanguage when SetIetfLanguageTags is enabled
        return (
                !HasMetadataErrors(TrackProps.StateType.Remove)
                && !HasMetadataErrors(TrackProps.StateType.ReMux)
                && !(
                    Program.Config.ProcessOptions.SetIetfLanguageTags
                    && HasMetadataErrors(TrackProps.StateType.SetLanguage)
                )
            ) || SetVerifyFailed("remuxing");
    }

    public bool RemuxRemoveExtraVideoTracks(ref bool modified)
    {
        if (MkvMergeProps.Video.Count <= 1)
        {
            // Nothing to do
            return true;
        }

        // Conditional
        if (!Program.Config.ProcessOptions.ReMux)
        {
            // Error, multiple video tracks are not supported, enable ReMux option
            Log.Error(
                "Multiple video tracks not supported, ReMux option not enabled : Video: {TrackCount} : {FileName}",
                MkvMergeProps.Video.Count,
                FileInfo.FullName
            );
            return false;
        }

        // Start with keeping all tracks
        // Selected is Keep
        // NotSelected is Remove
        SelectMediaProps selectMediaProps = new(MkvMergeProps, true);

        // Remove all but first video track
        List<VideoProps> mkvMergeRemoveList = [.. MkvMergeProps.Video.Skip(1)];
        mkvMergeRemoveList.ForEach(item => item.State = TrackProps.StateType.Remove);

        // To be removed tracks
        selectMediaProps.Move(mkvMergeRemoveList, false);

        // Detected: multiple video tracks
        Log.Warning(
            "Extra video tracks detected : Video: {Video} : {FileName}",
            MkvMergeProps.Video.Count,
            FileInfo.FullName
        );

        // ReMux the file
        Log.Information("Remux to remove extra video tracks : {FileName}", FileInfo.FullName);
        selectMediaProps.WriteLine("Keep", "Remove");
        if (!Convert.ReMuxToMkv(FileInfo.FullName, selectMediaProps, out string outputName))
        {
            // Error
            return false;
        }

        // Refresh
        modified = true;
        _sidecarFile.State |= SidecarFile.StatesType.ReMuxed;
        return Refresh(outputName);
    }

    public bool RepairMetadataFlags(ref bool modified)
    {
        // Conditional
        if (!Program.Config.ProcessOptions.SetTrackFlags)
        {
            // Done
            return true;
        }

        // Any tracks to set flags on
        if (!HasMetadataErrors(TrackProps.StateType.SetFlags))
        {
            // Nothing to do
            return true;
        }

        // Detected: tracks needing flags set
        Log.Warning(
            "Track flags to be set detected : Tracks: {Tracks} : {FileName}",
            MkvMergeProps.Video.Count(item => item.State == TrackProps.StateType.SetFlags)
                + MkvMergeProps.Audio.Count(item => item.State == TrackProps.StateType.SetFlags)
                + MkvMergeProps.Subtitle.Count(item => item.State == TrackProps.StateType.SetFlags),
            FileInfo.FullName
        );

        // Already flagged?
        if (_sidecarFile.State.HasFlag(SidecarFile.StatesType.SetFlags))
        {
            Log.Warning(
                "Metadata errors re-detected after setting flags : {FileName}",
                FileInfo.FullName
            );
        }

        // Setting flags
        Log.Information("Setting track flags on media file : {FileName}", FileInfo.FullName);

        // Set flags using MkvMergeInfo
        Debug.Assert(
            MkvMergeProps.GetTrackList().Any(item => item.State == TrackProps.StateType.SetFlags)
        );
        if (!Tools.MkvPropEdit.SetTrackFlags(FileInfo.FullName, MkvMergeProps))
        {
            // Error
            return false;
        }

        // Refresh
        modified = true;
        _sidecarFile.State |= SidecarFile.StatesType.SetFlags;
        return Refresh(true);
    }

    public bool RepairDefaultFlags(ref bool modified)
    {
        // Conditional
        if (!Program.Config.ProcessOptions.SetTrackFlags)
        {
            // Done
            return true;
        }

        // Normalize Default flags using MkvMergeInfo
        List<TrackProps> clearList = FindRedundantDefaultTracks(MkvMergeProps);
        if (clearList.Count == 0)
        {
            // Nothing to do
            return true;
        }

        // Detected: redundant Default flags, report Default-flagged count over total track count per
        // type so the reason is visible (1/1 = lone track, 2/4 = multiple defaults)
        Log.Warning(
            "Redundant Default flags detected : Video: {VideoDefault}/{VideoTotal}, Audio: {AudioDefault}/{AudioTotal}, Subtitle: {SubtitleDefault}/{SubtitleTotal} : {FileName}",
            MkvMergeProps.Video.Count(item => item.Flags.HasFlag(TrackProps.FlagsType.Default)),
            MkvMergeProps.Video.Count,
            MkvMergeProps.Audio.Count(item => item.Flags.HasFlag(TrackProps.FlagsType.Default)),
            MkvMergeProps.Audio.Count,
            MkvMergeProps.Subtitle.Count(item => item.Flags.HasFlag(TrackProps.FlagsType.Default)),
            MkvMergeProps.Subtitle.Count,
            FileInfo.FullName
        );

        // Re-detected after a previous clearing pass, e.g. reintroduced by re-encoding
        if (_sidecarFile.State.HasFlag(SidecarFile.StatesType.ClearedDefaultFlags))
        {
            Log.Warning("Default flags re-detected after clearing : {FileName}", FileInfo.FullName);
        }

        // Clear the redundant Default flags
        Log.Information("Clearing redundant Default flags : {FileName}", FileInfo.FullName);
        clearList.ForEach(item => item.WriteLine("Default"));
        if (!Tools.MkvPropEdit.ClearDefaultFlags(FileInfo.FullName, clearList))
        {
            // Error
            return false;
        }

        // Refresh
        modified = true;
        _sidecarFile.State |= SidecarFile.StatesType.ClearedDefaultFlags;
        return Refresh(true);
    }

    // Select the Default-flagged tracks to clear, leaving at most one meaningful default per type
    internal static List<TrackProps> FindRedundantDefaultTracks(MediaProps mediaProps)
    {
        List<TrackProps> clearList = [];

        // Video keeps the first default, audio keeps the preferred format, subtitles keep none
        clearList.AddRange(SelectClearableDefaults(mediaProps.Video, defaults => defaults[0]));
        clearList.AddRange(SelectClearableDefaults(mediaProps.Audio, FindPreferredAudio));
        clearList.AddRange(SelectClearableDefaults(mediaProps.Subtitle, _ => null));

        return clearList;
    }

    private static IEnumerable<TrackProps> SelectClearableDefaults(
        IEnumerable<TrackProps> trackList,
        Func<List<TrackProps>, TrackProps?> selectKeeper
    )
    {
        List<TrackProps> tracks = [.. trackList];
        List<TrackProps> defaults =
        [
            .. tracks.Where(item => item.Flags.HasFlag(TrackProps.FlagsType.Default)),
        ];

        // A lone track needs no Default flag
        if (tracks.Count == 1 && defaults.Count == 1)
        {
            return defaults;
        }

        // Multiple Default flags, keep the selected track and clear the rest
        if (defaults.Count > 1)
        {
            TrackProps? keeper = selectKeeper(defaults);
            return defaults.Where(item => !ReferenceEquals(item, keeper));
        }

        // Zero or one Default flag is already correct
        return [];
    }

    public bool RepairMatroskaStructure(ref bool modified)
    {
        // Conditional on Verify, this is a Direct Play verification check
        if (!Program.Config.ProcessOptions.Verify)
        {
            return true;
        }

        // The structural parse looks for a video track, skip audio only files
        if (MkvMergeProps.Video.Count == 0)
        {
            return true;
        }

        // Do not re-attempt on a file that previously failed to converge
        if (_sidecarFile.State.HasFlag(SidecarFile.StatesType.VerifyFailed))
        {
            return true;
        }

        // Read-only structural check
        if (MatroskaStructureValid(out MatroskaStructure.SeekIndexIssue issue))
        {
            // Seek index is usable, do not remux valid files
            return true;
        }

        Log.Warning("Matroska seek index unusable, {Issue} : {FileName}", issue, FileInfo.FullName);

        // Can we repair
        if (!Program.Config.VerifyOptions.AutoRepair || !Program.Config.ProcessOptions.ReMux)
        {
            return SetVerifyFailed("Matroska structure check");
        }

        // Remux to rewrite the container structure
        Log.Information("Remux to repair Matroska structure : {FileName}", FileInfo.FullName);
        if (!Convert.ReMuxToMkv(FileInfo.FullName, out string outputName))
        {
            // Error
            return false;
        }

        // Refresh
        modified = true;
        _sidecarFile.State |= SidecarFile.StatesType.ReMuxed;
        if (!Refresh(outputName))
        {
            return false;
        }

        // Stop if the remux did not fix the structure, else monitor mode loops
        return MatroskaStructureValid(out _) || SetVerifyFailed("Matroska structure remux");
    }

    private bool MatroskaStructureValid(out MatroskaStructure.SeekIndexIssue issue)
    {
        // Read-only logical validation of the Matroska seek index
        issue = MatroskaStructure.GetSeekIndexIssue(FileInfo.FullName);
        return issue == MatroskaStructure.SeekIndexIssue.None;
    }

    public bool AnyTags() =>
        MkvMergeProps.AnyTags || FfProbeProps.AnyTags || MediaInfoProps.AnyTags;

    public bool RemoveTags(ref bool modified, bool ignoreConfig = false)
    {
        // Optional
        if (!ignoreConfig && !Program.Config.ProcessOptions.RemoveTags)
        {
            return true;
        }

        // Remove attachments
        if (!RemoveAttachments(ref modified))
        {
            // Error
            return false;
        }

        // Does the file have tags
        if (!AnyTags())
        {
            // No tags
            return true;
        }

        // Detected: tags present
        Log.Warning(
            "Tags detected : MkvMerge: {MkvMerge}, FfProbe: {FfProbe}, MediaInfo: {MediaInfo} : {FileName}",
            MkvMergeProps.AnyTags,
            FfProbeProps.AnyTags,
            MediaInfoProps.AnyTags,
            FileInfo.FullName
        );

        // Already cleared?
        // Tags can re-appear after running FfMpeg or HandBrake
        if (_sidecarFile.State.HasFlag(SidecarFile.StatesType.ClearedTags))
        {
            Log.Warning("Tags re-detected after clearing : {FileName}", FileInfo.FullName);
        }

        // Remove tags
        Log.Information("Clearing all tags from media file : {FileName}", FileInfo.FullName);

        // Delete the tags
        if (!Tools.MkvPropEdit.ClearTags(FileInfo.FullName, MkvMergeProps))
        {
            // Error
            return false;
        }

        // Refresh
        modified = true;
        _sidecarFile.State |= SidecarFile.StatesType.ClearedTags;
        return Refresh(true);
    }

    public bool RemoveAttachments(ref bool modified)
    {
        // Any attachments, use MkvMergeInfo
        if (MkvMergeProps.Attachments == 0)
        {
            // No attachments
            return true;
        }

        // Detected: attachments present
        Log.Warning(
            "Attachments detected : Attachments: {Attachments} : {FileName}",
            MkvMergeProps.Attachments,
            FileInfo.FullName
        );

        // Already removed?
        if (_sidecarFile.State.HasFlag(SidecarFile.StatesType.RemovedAttachments))
        {
            Log.Warning("Attachments re-detected after clearing : {FileName}", FileInfo.FullName);
        }

        // Remove attachments
        Log.Information("Clearing attachments from media file : {FileName}", FileInfo.FullName);

        // Delete the attachments
        if (!Tools.MkvPropEdit.ClearAttachments(FileInfo.FullName, MkvMergeProps))
        {
            // Error
            return false;
        }

        // Refresh
        modified = true;
        _sidecarFile.State |= SidecarFile.StatesType.RemovedAttachments;
        return Refresh(true);
    }

    public bool RemoveCoverArt(ref bool modified)
    {
        // Any cover art
        if (
            !MkvMergeProps.HasCovertArt()
            && !FfProbeProps.HasCovertArt()
            && !MediaInfoProps.HasCovertArt()
        )
        {
            // Nothing to do
            return true;
        }

        // Conditional
        if (!Program.Config.ProcessOptions.ReMux)
        {
            // Error, cover art interferes with processing and must be removed, enable ReMux option
            Log.Error(
                "Cover Art must be removed, ReMux option not enabled : {FileName}",
                FileInfo.FullName
            );
            return false;
        }

        // Cover art can be detected by MediaInfo or FfMpeg or MkvMerge
        // Process MkvMerge first, sometimes FfProbe detects attachments, and sometimes it detects video streams

        // Any MkvMergeInfo cover art
        if (MkvMergeProps.HasCovertArt() && !RemoveCoverArtMkvMerge(ref modified))
        {
            // Error
            return false;
        }

        // Any FfProbe cover art
        if (FfProbeProps.HasCovertArt() && !RemoveCoverArtFfProbe(ref modified))
        {
            // Error
            return false;
        }

        // Did we get it all?
        Debug.Assert(
            !MkvMergeProps.HasCovertArt()
                && !FfProbeProps.HasCovertArt()
                && !MediaInfoProps.HasCovertArt()
        );

        // Done
        return true;
    }

    public bool RemoveCoverArtFfProbe(ref bool modified)
    {
        // Any FfProbeInfo cover art
        if (!FfProbeProps.HasCovertArt())
        {
            // No cover art
            return true;
        }

        // Remove attachments, if any
        if (!RemoveAttachments(ref modified))
        {
            // Error
            return false;
        }

        // Any FfProbeInfo cover art
        if (!FfProbeProps.HasCovertArt())
        {
            // No cover art
            return true;
        }

        // Remove tags, if any
        if (!RemoveTags(ref modified, true))
        {
            // Error
            return false;
        }

        // Done
        return true;
    }

    public bool RemoveCoverArtMkvMerge(ref bool modified)
    {
        // Any MkvMergeInfo cover art
        if (!MkvMergeProps.HasCovertArt())
        {
            // No cover art
            return true;
        }

        // Detected: cover art present
        Log.Warning(
            "Cover Art detected : Format: {Format} : {FileName}",
            MkvMergeProps.Video.First(item => item.CoverArt).Format,
            FileInfo.FullName
        );

        // Already removed?
        if (_sidecarFile.State.HasFlag(SidecarFile.StatesType.RemovedCoverArt))
        {
            Log.Warning("Cover Art re-detected after removing : {FileName}", FileInfo.FullName);
        }

        // Select all tracks with cover art
        // Use MkvMerge for cover art logic
        // Selected is Keep
        // NotSelected is Remove
        SelectMediaProps selectMediaProps = new(MkvMergeProps, true);
        // First() will throw if not found
        selectMediaProps.Move(MkvMergeProps.Video.First(item => item.CoverArt), false);

        // There must be something left to keep
        Debug.Assert(selectMediaProps.Selected.Count > 0);
        selectMediaProps.SetState(TrackProps.StateType.Keep, TrackProps.StateType.Remove);

        Log.Information("Removing Cover Art from media file : {FileName}", FileInfo.FullName);
        selectMediaProps.WriteLine("Keep", "Remove");

        // ReMux and only keep the selected tracks
        if (!Convert.ReMuxToMkv(FileInfo.FullName, selectMediaProps, out string outputName))
        {
            // Error
            return false;
        }

        // Refresh
        modified = true;
        _sidecarFile.State |= SidecarFile.StatesType.ReMuxed;
        _sidecarFile.State |= SidecarFile.StatesType.RemovedCoverArt;
        return Refresh(outputName);
    }

    public bool SetUnknownLanguageTracks(ref bool modified)
    {
        // Conditional
        if (!Program.Config.ProcessOptions.SetUnknownLanguage)
        {
            return true;
        }

        // Select all tracks with unknown languages
        // Use MkvMerge for IETF language tags
        // Selected is Unknown
        // NotSelected is Known
        SelectMediaProps selectMediaProps = FindUnknownLanguageTracks();
        if (selectMediaProps.Selected.Count == 0)
        {
            // Nothing to do
            return true;
        }

        // Detected: tracks with unknown language
        Log.Warning(
            "Unknown language tracks detected : Tracks: {Tracks} : {FileName}",
            selectMediaProps.Selected.Count,
            FileInfo.FullName
        );

        Log.Information(
            "Setting unknown language tracks to {DefaultLanguage} : {FileName}",
            Program.Config.ProcessOptions.DefaultLanguage,
            FileInfo.FullName
        );
        selectMediaProps.WriteLine("Unknown", "Known");

        // Set the track language to the default language
        selectMediaProps
            .Selected.GetTrackList()
            .ForEach(item => item.LanguageIetf = Program.Config.ProcessOptions.DefaultLanguage);

        // Set languages
        if (!Tools.MkvPropEdit.SetTrackLanguage(FileInfo.FullName, selectMediaProps.Selected))
        {
            // Error
            return false;
        }

        // Refresh
        modified = true;
        _sidecarFile.State |= SidecarFile.StatesType.SetLanguage;
        return Refresh(true);
    }

    public bool RemoveUnwantedLanguageTracks(ref bool modified)
    {
        // Conditional
        if (!Program.Config.ProcessOptions.RemoveUnwantedLanguageTracks)
        {
            return true;
        }

        // Use MkvMerge for IETF language tags
        // Selected is Keep
        // NotSelected is Remove
        SelectMediaProps selectMediaProps = FindUnwantedLanguageTracks();
        if (selectMediaProps.NotSelected.Count == 0)
        {
            // Done
            return true;
        }

        // There must be something left to keep
        Debug.Assert(selectMediaProps.Selected.Count > 0);

        // Detected: tracks with unwanted languages
        Log.Warning(
            "Unwanted language tracks detected : Tracks: {Tracks} : {FileName}",
            selectMediaProps.NotSelected.Count,
            FileInfo.FullName
        );

        Log.Information("Removing unwanted language tracks : {FileName}", FileInfo.FullName);
        selectMediaProps.WriteLine("Keep", "Remove");

        // ReMux and only keep the selected tracks
        if (!Convert.ReMuxToMkv(FileInfo.FullName, selectMediaProps, out string outputName))
        {
            // Error
            return false;
        }

        // Refresh
        modified = true;
        _sidecarFile.State |= SidecarFile.StatesType.ReMuxed;
        return Refresh(outputName);
    }

    public bool RemoveDuplicateTracks(ref bool modified)
    {
        // Conditional
        if (!Program.Config.ProcessOptions.RemoveDuplicateTracks)
        {
            return true;
        }

        // Use MkvMerge logic
        // Selected is Keep
        // NotSelected is Remove
        SelectMediaProps selectMediaProps = FindDuplicateTracks();
        if (selectMediaProps.NotSelected.Count == 0)
        {
            // Done
            return true;
        }

        // There must be something left to keep
        Debug.Assert(selectMediaProps.Selected.Count > 0);

        // Detected: duplicate tracks
        Log.Warning(
            "Duplicate tracks detected : Tracks: {Tracks} : {FileName}",
            selectMediaProps.NotSelected.Count,
            FileInfo.FullName
        );

        Log.Information("Removing duplicate tracks : {FileName}", FileInfo.FullName);
        selectMediaProps.WriteLine("Keep", "Remove");

        // ReMux and only keep the specified tracks
        if (!Convert.ReMuxToMkv(FileInfo.FullName, selectMediaProps, out string outputName))
        {
            // Error
            return false;
        }

        // Refresh
        modified = true;
        _sidecarFile.State |= SidecarFile.StatesType.ReMuxed;
        return Refresh(outputName);
    }

    private bool FindInterlacedTracks(
        bool conditional,
        out VideoProps? videoProps,
        out FfMpegIdetInfo? idetInfo
    )
    {
        // Return false on error
        // Set videoProps if interlaced, idetInfo is set when detected by the idet scan (null for a metadata flag)

        // Any video tracks
        videoProps = null;
        idetInfo = null;
        if (FfProbeProps.Video.Count == 0)
        {
            // No video tracks
            return true;
        }

        // Are any interlaced metadata flags set
        videoProps ??= FfProbeProps.Video.Find(item => item.Interlaced);
        videoProps ??= MediaInfoProps.Video.Find(item => item.Interlaced);
        videoProps ??= MkvMergeProps.Video.Find(item => item.Interlaced);
        if (videoProps != null)
        {
            // Interlaced metadata flag set
            return true;
        }

        // idet on the limited quickscan sample is unreliable, rely on the interlace metadata flags only
        if (Program.Options.QuickScan)
        {
            return true;
        }

        // Running idet is expensive, skip if already verified or already deinterlaced
        if (
            conditional
            && (
                State.HasFlag(SidecarFile.StatesType.Verified)
                || State.HasFlag(SidecarFile.StatesType.VerifyFailed)
                || State.HasFlag(SidecarFile.StatesType.DeInterlaced)
            )
        )
        {
            // Assume not interlaced
            return true;
        }

        // Count the frame types using the idet filter
        if (!GetIdetInfo(out idetInfo) || idetInfo == null)
        {
            // Error, an idet execution or parse failure is unexpected, abort the file so the bug surfaces
            return false;
        }

        // Idet result
        if (!idetInfo.IsInterlaced())
        {
            // Not interlaced, clear the idet info so the caller does not treat it as a detection
            idetInfo = null;
            return true;
        }

        // Use the first video track from FfProbe
        videoProps = FfProbeProps.Video.First();
        videoProps.Interlaced = true;
        return true;
    }

    private bool FindClosedCaptionTracks(bool conditional, out VideoProps? videoProps)
    {
        // Return false on error
        // Set videoProps if contains closed captions

        // Any video tracks
        videoProps = null;
        if (FfProbeProps.Video.Count == 0)
        {
            // No video tracks
            return true;
        }

        // Are any closed caption attributes set
        videoProps ??= FfProbeProps.Video.Find(item => item.ClosedCaptions);
        videoProps ??= MediaInfoProps.Video.Find(item => item.ClosedCaptions);
        videoProps ??= MkvMergeProps.Video.Find(item => item.ClosedCaptions);
        if (videoProps != null)
        {
            // Attribute set
            return true;
        }

        // Running analyze_frames is expensive, skip if already verified or closed captions already removed
        if (
            conditional
            && (
                State.HasFlag(SidecarFile.StatesType.Verified)
                || State.HasFlag(SidecarFile.StatesType.VerifyFailed)
                || State.HasFlag(SidecarFile.StatesType.ClearedCaptions)
            )
        )
        {
            // Assume no closed captions
            return true;
        }

        // Detect closed captions embedded in the video stream
        Log.Information("Finding Closed Captions in video stream : {FileName}", FileInfo.FullName);
        if (!Tools.FfProbe.GetClosedCaptions(FileInfo.FullName, out bool hasClosedCaptions))
        {
            // Error
            Log.Error(
                "Failed to find Closed Captions in video stream : {FileName}",
                FileInfo.FullName
            );
            return false;
        }

        // Mark the first video track when captions are present
        if (hasClosedCaptions)
        {
            // Use the first video track from FfProbe
            videoProps = FfProbeProps.Video.First();
            videoProps.ClosedCaptions = true;
        }

        return true;
    }

    public bool DeInterlace(bool conditional, ref bool modified)
    {
        // Conditional
        if (conditional && !Program.Config.ProcessOptions.DeInterlace)
        {
            return true;
        }

        // Do we have any interlaced video
        if (
            !FindInterlacedTracks(
                conditional,
                out VideoProps? videoProps,
                out FfMpegIdetInfo? idetInfo
            )
        )
        {
            // Error
            return false;
        }
        if (videoProps == null)
        {
            // Not interlaced
            return true;
        }

        // Detected: interlaced video, idetInfo is set when the idet scan detected it, else a metadata flag
        if (idetInfo != null)
        {
            // idetInfo is only set when the idet scan detected interlaced content
            bool interlaced = idetInfo.IsInterlaced(out string reason);
            Debug.Assert(interlaced);
            Log.Warning(
                "Interlaced video detected : Format: {Format}, Detected by: Idet, {Reason} : {FileName}",
                videoProps.Format,
                reason,
                FileInfo.FullName
            );
        }
        else
        {
            Log.Warning(
                "Interlaced video detected : Format: {Format}, Detected by: Metadata flag : {FileName}",
                videoProps.Format,
                FileInfo.FullName
            );
        }

        // Already deinterlaced?
        if (State.HasFlag(SidecarFile.StatesType.DeInterlaced))
        {
            Log.Warning("Deinterlacing already deinterlaced media : {FileName}", FileInfo.FullName);
        }

        Log.Information("Deinterlacing interlaced media : {FileName}", FileInfo.FullName);
        videoProps.State = TrackProps.StateType.DeInterlace;
        videoProps.WriteLine("Interlaced");

        // TODO: HandBrake will convert closed captions and subtitle tracks to ASS format
        // To work around this we will deinterlace without subtitles then add the subtitles back
        // https://github.com/ptr727/PlexCleaner/issues/95

        // Create a temp filename for the deinterlaced output
        string deintName = Path.ChangeExtension(FileInfo.FullName, ".tmp8");
        Debug.Assert(FileInfo.FullName != deintName);

        // DeInterlace using HandBrake and ignore subtitles
        if (!Tools.HandBrake.ConvertToMkv(FileInfo.FullName, deintName, false, true))
        {
            Log.Error("Failed to deinterlace interlaced media : {FileName}", FileInfo.FullName);
            File.Delete(deintName);
            return false;
        }

        // Create a temp filename for the remuxed output
        string remuxName = Path.ChangeExtension(FileInfo.FullName, ".tmp9");
        Debug.Assert(FileInfo.FullName != remuxName);

        // If there are subtitles in the original file merge them back
        if (MkvMergeProps.Subtitle.Count == 0)
        {
            // No subtitles, just remux all content
            Log.Information("Remuxing deinterlaced media : {FileName}", FileInfo.FullName);
            if (!Tools.MkvMerge.ReMuxToMkv(deintName, remuxName))
            {
                Log.Error("Failed to remux deinterlaced media : {FileName}", FileInfo.FullName);
                File.Delete(deintName);
                File.Delete(remuxName);
                return false;
            }
        }
        else
        {
            // Merge the deinterlaced file with the subtitles from the original file
            MediaProps subtitleProps = new(MediaTool.ToolType.MkvMerge, FileInfo.FullName);
            subtitleProps.Subtitle.AddRange(MkvMergeProps.Subtitle);
            Log.Information(
                "Remuxing subtitles and deinterlaced media : {FileName}",
                FileInfo.FullName
            );
            if (!Tools.MkvMerge.MergeToMkv(deintName, FileInfo.FullName, subtitleProps, remuxName))
            {
                Log.Error(
                    "Failed to remux subtitles and deinterlaced media : {FileName}",
                    FileInfo.FullName
                );
                File.Delete(deintName);
                File.Delete(remuxName);
                return false;
            }
        }

        // Delete the temp files and rename the output
        File.Delete(deintName);
        File.Move(remuxName, FileInfo.FullName, true);

        // Clone the original MkvMergeInfo
        MediaProps postMkvMerge = MkvMergeProps.Clone();

        // The remuxed output will be [Video] [Audio] [Subtitles]
        // Reset the track numbers to be in the expected order
        int trackNumber = 1;
        postMkvMerge.Video.Clear();
        postMkvMerge.Video.AddRange(MkvMergeProps.Video);
        postMkvMerge.Video.ForEach(item => item.Number = trackNumber++);
        postMkvMerge.Audio.Clear();
        postMkvMerge.Audio.AddRange(MkvMergeProps.Audio);
        postMkvMerge.Audio.ForEach(item => item.Number = trackNumber++);
        postMkvMerge.Subtitle.Clear();
        postMkvMerge.Subtitle.AddRange(MkvMergeProps.Subtitle);
        postMkvMerge.Subtitle.ForEach(item => item.Number = trackNumber++);

        // FfMpeg and HandBrake discards IETF language tags, restore them after encoding and deinterlacing
        // https://github.com/ptr727/PlexCleaner/issues/148
        if (!Tools.MkvPropEdit.SetTrackLanguage(FileInfo.FullName, postMkvMerge))
        {
            // Error
            return false;
        }

        // Refresh
        modified = true;
        _sidecarFile.State |= SidecarFile.StatesType.DeInterlaced;
        _sidecarFile.State |= SidecarFile.StatesType.ReMuxed;
        if (!Refresh(true))
        {
            return false;
        }

        // Verify that the pre- and post- info is using the same track numbers
        // If this fails then SetTrackLanguage() will have used the wrong tracks
        if (!MkvMergeProps.VerifyTrackOrder(postMkvMerge))
        {
            Log.Error(
                "MkvMerge and HandBrake track metadata does not match : {FileName}",
                FileInfo.FullName
            );
            Debug.Assert(false);
            return false;
        }
        return true;
    }

    public bool RemoveClosedCaptions(bool conditional, ref bool modified)
    {
        // Conditional
        if (conditional && !Program.Config.ProcessOptions.RemoveClosedCaptions)
        {
            return true;
        }

        // Do we have any closed captions
        if (!FindClosedCaptionTracks(conditional, out VideoProps? videoProps))
        {
            // Error
            return false;
        }
        if (videoProps == null)
        {
            // No closed captions
            return true;
        }

        // Detected: closed captions in the video stream
        Log.Warning(
            "Closed Captions detected : Format: {Format} : {FileName}",
            videoProps.Format,
            FileInfo.FullName
        );

        // Already removed?
        if (_sidecarFile.State.HasFlag(SidecarFile.StatesType.ClearedCaptions))
        {
            Log.Warning(
                "Closed Captions re-detected after removing : {FileName}",
                FileInfo.FullName
            );
        }

        Log.Information(
            "Removing Closed Captions from video stream : {FileName}",
            FileInfo.FullName
        );
        videoProps.WriteLine("Closed Captions");

        // Get SEI NAL unit based on video format
        int nalUnit = FfMpeg.GetNalUnit(videoProps.Format);
        if (nalUnit == 0)
        {
            // Error
            Log.Error(
                "Unsupported video format for Closed Captions removal : Format: {Format} : {FileName}",
                videoProps.Format,
                FileInfo.FullName
            );
            return false;
        }

        // https://trac.ffmpeg.org/ticket/5283
        // TODO: HDR10+ information may be removed from H265 content
        // Use MediaInfo tags
        VideoProps mediaInfoVideo = MediaInfoProps.Video.First();
        if (
            Hdr10FormatList.Any(item =>
                mediaInfoVideo.FormatHdr.Contains(item, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            // TODO: Treating this as an error until verified with actual examples
            // Error
            Log.Error(
                "Removing Closed Captions may remove HDR10+ information : {Format} : {FileName}",
                mediaInfoVideo.FormatHdr,
                FileInfo.FullName
            );
            return false;
        }

        // Create a temp output filename
        string tempName = Path.ChangeExtension(FileInfo.FullName, ".tmp10");
        Debug.Assert(FileInfo.FullName != tempName);

        // Remove Closed Captions
        Log.Information("Removing closed captions using FfMpeg : {FileName}", FileInfo.FullName);
        if (!Tools.FfMpeg.RemoveNalUnits(FileInfo.FullName, nalUnit, tempName))
        {
            // Error
            Log.Error(
                "Failed to remove closed captions using FfMpeg : {FileName}",
                FileInfo.FullName
            );
            File.Delete(tempName);
            return false;
        }

        // ReMux using MkvMerge after FfMpeg encoding
        if (!Convert.ReMux(tempName))
        {
            // Error
            File.Delete(tempName);
            return false;
        }

        // Rename the temp file to the original file
        File.Move(tempName, FileInfo.FullName, true);

        // Refresh
        modified = true;
        _sidecarFile.State |= SidecarFile.StatesType.ClearedCaptions;
        _sidecarFile.State |= SidecarFile.StatesType.ReMuxed;
        return Refresh(true);
    }

    public bool RemoveSubtitles(ref bool modified)
    {
        // Only called by MkvProcess, not currently used in Process logic

        // Do we have any subtitles
        if (MkvMergeProps.Subtitle.Count == 0)
        {
            // No subtitles to remove
            return true;
        }

        // Detected: subtitle tracks present
        Log.Warning(
            "Subtitles detected : Subtitle: {Subtitle} : {FileName}",
            MkvMergeProps.Subtitle.Count,
            FileInfo.FullName
        );

        Log.Information("Removing subtitles : {FileName}", FileInfo.FullName);
        MkvMergeProps.Subtitle.ForEach(item => item.WriteLine("Subtitles"));

        // Create a temp output filename
        string tempName = Path.ChangeExtension(FileInfo.FullName, ".tmp6");
        Debug.Assert(FileInfo.FullName != tempName);

        // Remove Subtitles
        if (!Tools.MkvMerge.RemoveSubtitles(FileInfo.FullName, tempName))
        {
            // Error
            Log.Error("Failed to remove subtitles : {FileName}", FileInfo.FullName);
            File.Delete(tempName);
            return false;
        }

        // Rename the temp file to the original file
        File.Move(tempName, FileInfo.FullName, true);

        // Refresh
        modified = true;
        _sidecarFile.State |= SidecarFile.StatesType.ReMuxed;
        return Refresh(true);
    }

    public bool ReEncode(bool conditional, ref bool modified)
    {
        // Conditional
        if (conditional && !Program.Config.ProcessOptions.ReEncode)
        {
            return true;
        }

        // Find all tracks that need re-encoding
        // Use FfProbeInfo for matching logic
        // Selected is ReEncode
        // NotSelected is Keep
        SelectMediaProps selectMediaProps = FindNeedReEncode();
        if (selectMediaProps.Selected.Count == 0)
        {
            // Done
            return true;
        }

        // Detected: tracks requiring re-encoding
        Log.Warning(
            "Tracks requiring re-encode detected : Tracks: {Tracks} : {FileName}",
            selectMediaProps.Selected.Count,
            FileInfo.FullName
        );

        // Already reencoded?
        if (State.HasFlag(SidecarFile.StatesType.ReEncoded))
        {
            Log.Warning("Reencoding already reencoded media : {FileName}", FileInfo.FullName);
        }

        Log.Information("Reencoding required tracks : {FileName}", FileInfo.FullName);
        selectMediaProps.WriteLine("ReEncode", "Passthrough");

        // ReEncode selected tracks
        if (!Convert.ConvertToMkv(FileInfo.FullName, selectMediaProps, out string outputName))
        {
            // Convert will log error
            // Error
            return false;
        }

        // ReMux using MkvMerge after FfMpeg encoding
        if (!Convert.ReMux(outputName))
        {
            // Error
            return false;
        }

        // The FfMpeg map is constructed using the same order as the original file
        // No need to adjust the track numbers
        MediaProps postMkvMerge = MkvMergeProps.Clone();

        // FfMpeg and HandBrake discards IETF language tags, restore them after encoding and deinterlacing
        // https://github.com/ptr727/PlexCleaner/issues/148
        if (!Tools.MkvPropEdit.SetTrackLanguage(outputName, postMkvMerge))
        {
            // Error
            return false;
        }

        // Refresh
        modified = true;
        _sidecarFile.State |= SidecarFile.StatesType.ReEncoded;
        _sidecarFile.State |= SidecarFile.StatesType.ReMuxed;
        if (!Refresh(outputName))
        {
            return false;
        }

        // Verify that the pre- and post- info is using the same track numbers
        // If this fails then SetTrackLanguage() will have used the wrong tracks
        if (!MkvMergeProps.VerifyTrackOrder(postMkvMerge))
        {
            Log.Error(
                "MkvMerge and FfMpeg track metadata does not match : {FileName}",
                FileInfo.FullName
            );
            Debug.Assert(false);
            return false;
        }
        return true;
    }

    public bool VerifyTrackCounts()
    {
        // Use MkvMergeInfo

        // Tooling supports one and only one video track
        if (MkvMergeProps.Video.Count == 0)
        {
            Log.Error("File missing video track : {FileName}", FileInfo.FullName);
            MkvMergeProps.WriteLine("Unsupported");

            // Error
            return false;
        }
        if (MkvMergeProps.Video.Count > 1)
        {
            Log.Error(
                "File has more than one video track : Video: {Video} : {FileName}",
                MkvMergeProps.Video.Count,
                FileInfo.FullName
            );
            MkvMergeProps.WriteLine("Unsupported");

            // Error
            return false;
        }

        // Warn if audio track is missing
        if (MkvMergeProps.Audio.Count == 0)
        {
            Log.Warning("File missing audio : {FileName}", FileInfo.FullName);
            MkvMergeProps.WriteLine("Missing");

            // Warning only
        }

        return true;
    }

    public bool Verify(bool conditional, out bool canRepair)
    {
        // Do not set any state, caller will set state

        // Init
        canRepair = false;

        // Conditional
        if (conditional && !Program.Config.ProcessOptions.Verify)
        {
            // Done
            return true;
        }

        // Skip if Verified
        if (conditional && _sidecarFile.State.HasFlag(SidecarFile.StatesType.Verified))
        {
            Debug.Assert(!_sidecarFile.State.HasFlag(SidecarFile.StatesType.VerifyFailed));
            Debug.Assert(!_sidecarFile.State.HasFlag(SidecarFile.StatesType.RepairFailed));
            return true;
        }

        // Skip if VerifyFailed
        if (conditional && _sidecarFile.State.HasFlag(SidecarFile.StatesType.VerifyFailed))
        {
            Debug.Assert(!_sidecarFile.State.HasFlag(SidecarFile.StatesType.Verified));
            Debug.Assert(!_sidecarFile.State.HasFlag(SidecarFile.StatesType.Repaired));
            return false;
        }

        // Verify track counts
        if (!VerifyTrackCounts())
        {
            // Error, can't repair
            return false;
        }

        // Verify HDR profile
        // Warning only
        _ = VerifyHdrProfile();

        // Verify bitrate
        // Warning only
        // Will update sidecar state if bitrate exceeded
        _ = VerifyBitrate();

        // Verify media streams, both failure kinds are repairable
        canRepair = true;
        _lastVerifyResult = VerifyMediaStreams(FileInfo);

        // Deterministic: pass only when clean, a timestamp-only or decode result is a repairable failure
        return _lastVerifyResult == VerifyResult.Clean;
    }

    public static VerifyResult VerifyMediaStreams(FileInfo fileInfo)
    {
        // Verify
        VerifyResult verifyResult = Tools.FfMpeg.VerifyMedia(fileInfo.FullName);

        // Log the classified outcome so a failure is diagnosable, unless it was a cancellation
        if (!Program.IsCancelledError())
        {
            if (verifyResult == VerifyResult.DecodeError)
            {
                Log.Error("Failed to verify media streams : {FileName}", fileInfo.FullName);
            }
            else if (verifyResult == VerifyResult.TimestampOnly)
            {
                // Correctable failure, the decision Warning is emitted later if the repair runs
                Log.Information(
                    "Verify detected non-monotonic DTS timestamps : {FileName}",
                    fileInfo.FullName
                );
            }
        }
        return verifyResult;
    }

    public bool DeleteFailedFile()
    {
        // Conditional
        if (!Program.Config.VerifyOptions.DeleteInvalidFiles)
        {
            // Return false if not deleted
            return false;
        }

        // Delete the media file and sidecar file
        Log.Warning("Deleting media file that failed processing : {FileName}", FileInfo.FullName);
        File.Delete(FileInfo.FullName);
        File.Delete(SidecarFile.GetSidecarName(FileInfo));

        // Set the sidecar state as deleted
        // Sidecar file no longer exists, but in-memory state does
        _sidecarFile.State |= SidecarFile.StatesType.FileDeleted;

        // Return true if deleted
        return true;
    }

    public bool VerifyAndRepair(ref bool modified)
    {
        // Verify (verify does not set any state, it just tests state)
        if (Verify(true, out bool canRepair))
        {
            // Set Verified state if not already set
            if (!_sidecarFile.State.HasFlag(SidecarFile.StatesType.Verified))
            {
                _sidecarFile.State |= SidecarFile.StatesType.Verified;
                _sidecarFile.State &= ~SidecarFile.StatesType.VerifyFailed;
                Debug.Assert(!_sidecarFile.State.HasFlag(SidecarFile.StatesType.RepairFailed));

                // Update state
                return Refresh(false);
            }

            // Done
            return true;
        }

        // Cancel requested
        if (Program.IsCancelled())
        {
            return false;
        }

        // Verify failed and can't repair
        // Or previous repair failed
        // Or repair not enabled
        if (
            !canRepair
            || _sidecarFile.State.HasFlag(SidecarFile.StatesType.RepairFailed)
            || !Program.Config.VerifyOptions.AutoRepair
        )
        {
            // Set VerifyFailed state if not already set
            if (!_sidecarFile.State.HasFlag(SidecarFile.StatesType.VerifyFailed))
            {
                _sidecarFile.State |= SidecarFile.StatesType.VerifyFailed;
                _sidecarFile.State &= ~SidecarFile.StatesType.Verified;
                Debug.Assert(!_sidecarFile.State.HasFlag(SidecarFile.StatesType.Repaired));

                // Update state
                _ = Refresh(false);
            }

            // Error
            return false;
        }

        // Sanity check
        Debug.Assert(!_sidecarFile.State.HasFlag(SidecarFile.StatesType.Verified));
        Debug.Assert(!_sidecarFile.State.HasFlag(SidecarFile.StatesType.Repaired));

        // Non-monotonic DTS is a repairable failure; try a lossless surgical setts repair first, then fall
        // through to the shared remux and re-encode tiers for a video or post-decode DTS setts cannot fix
        if (_lastVerifyResult == VerifyResult.TimestampOnly)
        {
            return RepairTimestampsAndSetState(ref modified);
        }

        // Attempt repair, if repair fails the original file will not be modified
        bool repaired = RepairAndReVerify();

        // Cancel requested
        if (Program.IsCancelled())
        {
            return false;
        }

        // Repair failed
        if (!repaired)
        {
            // Repair failed and verify failed state
            _sidecarFile.State |= SidecarFile.StatesType.VerifyFailed;
            _sidecarFile.State &= ~SidecarFile.StatesType.Verified;
            _sidecarFile.State |= SidecarFile.StatesType.RepairFailed;
            _sidecarFile.State &= ~SidecarFile.StatesType.Repaired;

            // Update state
            _ = Refresh(false);

            // Error
            return false;
        }

        // Verified and repaired state
        _sidecarFile.State |= SidecarFile.StatesType.Verified;
        _sidecarFile.State &= ~SidecarFile.StatesType.VerifyFailed;
        _sidecarFile.State |= SidecarFile.StatesType.Repaired;
        _sidecarFile.State &= ~SidecarFile.StatesType.RepairFailed;

        // Repaired
        modified = true;
        return Refresh(true);
    }

    private bool VerifyBitrate()
    {
        // Skip if no bitrate limit
        if (Program.Config.VerifyOptions.MaximumBitrate == 0)
        {
            return true;
        }

        // Bitrate over the limited quickscan sample is not representative of the whole file
        if (Program.Options.QuickScan)
        {
            return true;
        }

        // TODO: Verify that bitrate is acceptable for the resolution of the content, i.e. no YIFY, no fake 4K
        // https://4kmedia.org/real-or-fake-4k/
        // https://en.wikipedia.org/wiki/YIFY

        // Calculate bitrate
        if (!GetBitrateInfo(out BitrateInfo? bitrateInfo) || bitrateInfo == null)
        {
            // Error
            Log.Error("Failed to calculate bitrate info : {FileName}", FileInfo.FullName);
            return false;
        }

        // Print bitrate
        bitrateInfo.WriteLine();

        // Combined bitrate exceeded threshold
        if (bitrateInfo.CombinedBitrate.Exceeded > 0)
        {
            Log.Warning(
                "Maximum bitrate exceeded : {CombinedBitrate} > {MaximumBitrate} : {FileName}",
                Bitrate.ToBitsPerSecond(bitrateInfo.CombinedBitrate.Maximum),
                Bitrate.ToBitsPerSecond(Program.Config.VerifyOptions.MaximumBitrate / 8),
                FileInfo.FullName
            );

            // Caller must Refresh()
            _sidecarFile.State |= SidecarFile.StatesType.BitrateExceeded;

            // Warning only
        }

        // Audio bitrate exceeds video bitrate, may indicate an error with the video track
        if (bitrateInfo.AudioBitrate.Average > bitrateInfo.VideoBitrate.Average)
        {
            Log.Warning(
                "Audio bitrate exceeds Video bitrate : {AudioBitrate} > {VideoBitrate} : {FileName}",
                Bitrate.ToBitsPerSecond(bitrateInfo.AudioBitrate.Average),
                Bitrate.ToBitsPerSecond(bitrateInfo.VideoBitrate.Average),
                FileInfo.FullName
            );

            // Warning only
        }

        return true;
    }

    private bool VerifyHdrProfile()
    {
        // Verify that HDR profiles are HDR or Dolby Vision Profile 7+
        // If HDR10 compatibility is not reported the video can't play (without funky colors) on non-DV hardware
        // https://en.wikipedia.org/wiki/High-dynamic-range_video
        // https://en.wikipedia.org/wiki/Dolby_Vision

        // From MediaInfo:

        // HDR:
        /*
        <HDR_Format>SMPTE ST 2086</HDR_Format>
        <HDR_Format_Compatibility>HDR10</HDR_Format_Compatibility>
        */

        // Dolby Vision Profile 5
        /*
        <HDR_Format>Dolby Vision</HDR_Format>
        <HDR_Format_Version>1.0</HDR_Format_Version>
        <HDR_Format_Profile>dvhe.05</HDR_Format_Profile>
        <HDR_Format_Level>06</HDR_Format_Level>
        <HDR_Format_Settings>BL+RPU</HDR_Format_Settings>
        */

        // Dolby Vision Profile 7
        /*
        <HDR_Format>Dolby Vision / SMPTE ST 2086</HDR_Format>
        <HDR_Format_Version>1.0 / </HDR_Format_Version>
        <HDR_Format_Profile>dvhe.07 / </HDR_Format_Profile>
        <HDR_Format_Level>06 / </HDR_Format_Level>
        <HDR_Format_Settings>BL+EL+RPU / </HDR_Format_Settings>
        <HDR_Format_Compatibility>Blu-ray / HDR10</HDR_Format_Compatibility>
        */

        // Dolby Vision Profile 8.1
        /*
        <HDR_Format>Dolby Vision / SMPTE ST 2086</HDR_Format>
        <HDR_Format_Version>1.0 / </HDR_Format_Version>
        <HDR_Format_Profile>dvhe.08 / </HDR_Format_Profile>
        <HDR_Format_Level>09 / </HDR_Format_Level>
        <HDR_Format_Settings>BL+RPU / </HDR_Format_Settings>
        <HDR_Format_Compatibility>Blu-ray / HDR10</HDR_Format_Compatibility>
        */

        // Use MediaInfoInfo and find all HDR tracks
        List<VideoProps> hdrTracks = MediaInfoProps.Video.FindAll(videoItem =>
            !string.IsNullOrEmpty(videoItem.FormatHdr)
        );
        if (hdrTracks.Count == 0)
        {
            // No HDR tracks
            return true;
        }

        // Find tracks that are not HDR10 (SMPTE ST 2086) or HDR10+ (SMPTE ST 2094) compatible
        List<VideoProps> nonHdr10Tracks = hdrTracks.FindAll(videoProps =>
            Hdr10FormatList.All(hdr10Format =>
                !videoProps.FormatHdr.Contains(hdr10Format, StringComparison.OrdinalIgnoreCase)
            )
        );
        nonHdr10Tracks.ForEach(videoItem =>
        {
            Log.Warning(
                "Video is not HDR10 compatible : {Hdr} not in {Hdr10}: {FileName}",
                videoItem.FormatHdr,
                Hdr10FormatList,
                FileInfo.FullName
            );

            // Warning only
        });

        return true;
    }

    private bool RepairAndReVerify()
    {
        // Sanity check
        Debug.Assert(!_sidecarFile.State.HasFlag(SidecarFile.StatesType.Verified));
        Debug.Assert(!_sidecarFile.State.HasFlag(SidecarFile.StatesType.Repaired));

        // Trying again may not succeed unless the tools changed
        if (_sidecarFile.State.HasFlag(SidecarFile.StatesType.RepairFailed))
        {
            Log.Warning("Repairing previously repaired media : {FileName}", FileInfo.FullName);
        }

        // TODO: Analyze the error output and conditionally repair only the audio or video track
        // [aac @ 000001d3c5652440] noise_facs_q 32 is invalid
        // [ac3 @ 000002167861a840] error decoding the audio block
        // [h264 @ 00000270a07f6ac0] Missing reference picture, default is 65514
        // [h264 @ 000001979ebaa7c0] mmco: unref short failure
        // [h264 @ 00000152da828940] number of reference frames (0+5) exceeds max (4; probably corrupt input), discarding one
        // [NULL @ 000002a2115acac0] Invalid NAL unit size (-1148261185 > 8772).
        // [h264 @ 000002a21166bd00] Invalid NAL unit size (-1148261185 > 8772).
        // [matroska,webm @ 0000029a256d9280] Length 7 indicated by an EBML number's first byte 0x02 at pos 1601277 (0x186efd) exceeds max length 4.

        // TODO: HandBrake sometimes fails with what looks like a remux error
        // ERROR: avformatMux: track 1, av_interleaved_write_frame failed with error 'Invalid argument'
        // M[15:35:43] libhb: work result = 4

        // TODO: FfMpeg fails to decode some files
        // https://trac.ffmpeg.org/search?q=%22Invalid+NAL+unit+size%22&noquickjump=1&milestone=on&ticket=on&wiki=on

        // TODO: FfMpeg x265 requires input resolution to be multiple of chroma subsampling
        // https://stackoverflow.com/questions/50371919/ffmpeg-cannot-open-libx265-encoder-error-initializing-output-stream-00-err
        // http://www.ffmpeg-archive.org/scaling-failed-height-must-be-an-integer-td4671624.html
        // E.g. 1280 x 718 will fail, 1280 x 720 will work
        // https://ffmpeg.org/ffmpeg-filters.html#crop
        // -vf crop='iw-mod(iw,4)':'ih-mod(ih,4)'

        // Tier 1: a plain remux rewrites the container and its timestamps, clearing a demux-visible break
        // such as a non-monotonic DTS without re-encoding; it cannot fix decode-level corruption
        if (TryRemuxRepair())
        {
            Log.Information("Repair succeeded : {FileName}", FileInfo.FullName);
            return true;
        }
        if (Program.IsCancelledError())
        {
            return false;
        }

        // Tier 2: re-encode rebuilds the streams, fixing decode corruption and timestamp breaks a remux
        // cannot. Repair to temp file, only if verify is successful replace original file
        string tempName = Path.ChangeExtension(FileInfo.FullName, ".tmp12");
        Debug.Assert(FileInfo.FullName != tempName);

        // Convert using ffmpeg
        Log.Information(
            "Attempting media repair by reencoding using FfMpeg : {FileName}",
            FileInfo.FullName
        );
        if (!Tools.FfMpeg.ConvertToMkv(FileInfo.FullName, tempName))
        {
            // Failed, delete temp file
            File.Delete(tempName);

            // Cancel requested
            if (Program.IsCancelledError())
            {
                return false;
            }

            // Failed
            Log.Error("Failed to reencode using FfMpeg : {FileName}", FileInfo.FullName);

            // Try again using handbrake
            Log.Information(
                "Attempting media repair by reencoding using HandBrake : {FileName}",
                FileInfo.FullName
            );
            if (!Tools.HandBrake.ConvertToMkv(FileInfo.FullName, tempName, true, false))
            {
                // Failed, delete temp file
                File.Delete(tempName);

                // Cancel requested
                if (Program.IsCancelledError())
                {
                    return false;
                }

                // Failed
                Log.Error("Failed to reencode using HandBrake : {FileName}", FileInfo.FullName);

                // Caller will update state
                return false;
            }
        }

        // ReMux using MkvMerge after FfMpeg or HandBrake encoding
        if (!Convert.ReMux(tempName))
        {
            // Failed
            File.Delete(tempName);
            return false;
        }

        // Require a clean re-verify, accepting a timestamp-only result would mark a file Verified that
        // still fails verification, and a future run would skip it as already verified
        if (VerifyMediaStreams(new FileInfo(tempName)) != VerifyResult.Clean)
        {
            // Failed
            File.Delete(tempName);
            return false;
        }

        // Verify succeeded, rename the temp file to the original file
        File.Move(tempName, FileInfo.FullName, true);

        // Repair succeeded
        Log.Information("Repair succeeded : {FileName}", FileInfo.FullName);

        // Caller will update state
        return true;
    }

    private bool TryRemuxRepair()
    {
        // Remux to a temp file, only replace the original if the re-verify is clean
        string tempName = Path.ChangeExtension(FileInfo.FullName, ".tmp11");
        Debug.Assert(FileInfo.FullName != tempName);

        Log.Information(
            "Attempting media repair by remuxing using MkvMerge : {FileName}",
            FileInfo.FullName
        );
        if (!Tools.MkvMerge.ReMuxToMkv(FileInfo.FullName, tempName))
        {
            File.Delete(tempName);
            return false;
        }

        // Require a clean re-verify, a remux that still fails falls through to the re-encode tier
        if (VerifyMediaStreams(new FileInfo(tempName)) != VerifyResult.Clean)
        {
            File.Delete(tempName);
            return false;
        }

        // Verify succeeded, replace the original with the remuxed file
        File.Move(tempName, FileInfo.FullName, true);
        return true;
    }

    public bool RepairTimestamps(ref bool modified)
    {
        // Only process Matroska files, the audio timestamp repair does not require a video stream
        if (!SidecarFile.IsMkvFile(FileInfo.FullName))
        {
            return true;
        }

        // Classify the current verify state
        _lastVerifyResult = VerifyMediaStreams(FileInfo);

        // Cancel requested
        if (Program.IsCancelled())
        {
            return false;
        }

        switch (_lastVerifyResult)
        {
            case VerifyResult.Clean:
                // Nothing to repair, clear any stale failure flags and mark verified
                _sidecarFile.State |= SidecarFile.StatesType.Verified;
                _sidecarFile.State &= ~SidecarFile.StatesType.VerifyFailed;
                _sidecarFile.State &= ~SidecarFile.StatesType.RepairFailed;
                return Refresh(false);
            case VerifyResult.TimestampOnly:
                // Detected non-monotonic DTS, repair losslessly when demux-visible, else stays reported
                return RepairTimestampsAndSetState(ref modified);
            case VerifyResult.DecodeError:
                // Genuine decode corruption, not repairable here, leave the state unchanged
                return true;
            default:
                throw new NotImplementedException();
        }
    }

    private bool RepairTimestampsAndSetState(ref bool modified)
    {
        // Escalate through the repair tiers: a lossless surgical setts repair for a demux-visible audio
        // DTS, then the shared remux and re-encode ladder for a video or post-decode DTS setts cannot fix.
        // The first tier whose re-verify is clean wins
        if (TryLosslessTimestampRepair() || RepairAndReVerify())
        {
            _sidecarFile.State |= SidecarFile.StatesType.Verified;
            _sidecarFile.State &= ~SidecarFile.StatesType.VerifyFailed;
            _sidecarFile.State |= SidecarFile.StatesType.Repaired;
            _sidecarFile.State &= ~SidecarFile.StatesType.RepairFailed;
            modified = true;
            return Refresh(true);
        }

        // Do not touch state on cancellation, the caller retries next run
        if (Program.IsCancelled())
        {
            return false;
        }

        // No tier could repair the detected DTS, it stays reported as a failure, a detected issue is
        // not cleared
        _sidecarFile.State |= SidecarFile.StatesType.VerifyFailed;
        _sidecarFile.State &= ~SidecarFile.StatesType.Verified;
        _sidecarFile.State |= SidecarFile.StatesType.RepairFailed;
        _sidecarFile.State &= ~SidecarFile.StatesType.Repaired;
        _ = Refresh(false);
        return false;
    }

    private bool TryLosslessTimestampRepair()
    {
        // The audio-only setts filter can repair only an audio-stream DTS, so attempt it only when every
        // non-monotonic stream is audio; a video or subtitle DTS, a post-decode-only break with no demux
        // target, or an analysis failure all stay reported failures without a wasted rewrite
        if (
            !GetPacketAnalysis(false, out _, out DtsInfo? dtsInfo)
            || dtsInfo == null
            || !dtsInfo.NonMonotonicIsAudioOnly
        )
        {
            return false;
        }

        // Rewrite timestamps losslessly to a temp file
        string tempName = Path.ChangeExtension(FileInfo.FullName, ".tmp14");
        Debug.Assert(FileInfo.FullName != tempName);

        // Decision to modify the media, log once at Warning before the rewrite so it shows at Warning level
        Log.Warning("Repairing non-monotonic DTS timestamps : {FileName}", FileInfo.FullName);
        if (!Tools.FfMpeg.SetTimestamps(FileInfo.FullName, tempName))
        {
            File.Delete(tempName);
            return false;
        }

        // Reject unless the payload is byte-identical and the result verifies clean
        if (
            !TimestampRepairRegressionGate(FileInfo.FullName, tempName)
            || VerifyMediaStreams(new FileInfo(tempName)) != VerifyResult.Clean
        )
        {
            File.Delete(tempName);
            return false;
        }

        // Replace the original with the repaired file
        File.Move(tempName, FileInfo.FullName, true);
        Log.Information("Timestamp repair succeeded : {FileName}", FileInfo.FullName);
        return true;
    }

    // A timestamp nudge must not shift a stream's start or duration by more than the A/V-sync
    // perceptibility threshold, so the repair never introduces audible drift
    private const double SyncToleranceSeconds = 0.040;

    private static bool TimestampRepairRegressionGate(string original, string repaired)
    {
        // Payload must be byte-identical; the streamhash muxer hashes packet data only, not timestamps,
        // so a matching hash proves only the timestamps changed
        if (
            !Tools.FfMpeg.GetStreamHashes(original, out Dictionary<int, string> beforeHash)
            || !Tools.FfMpeg.GetStreamHashes(repaired, out Dictionary<int, string> afterHash)
            || beforeHash.Count != afterHash.Count
            || !beforeHash.All(kvp =>
                afterHash.TryGetValue(kvp.Key, out string? hash) && hash == kvp.Value
            )
        )
        {
            return false;
        }

        // Timing must be preserved; the streamhash proves the samples are identical but not where they
        // play, so verify no stream's start or duration moved beyond the A/V-sync tolerance
        return TimestampRepairSyncPreserved(original, repaired);
    }

    private static bool TimestampRepairSyncPreserved(string original, string repaired) =>
        Tools.FfProbe.GetStreamTimings(
            original,
            out Dictionary<int, (double Start, double Duration)> before
        )
        && Tools.FfProbe.GetStreamTimings(
            repaired,
            out Dictionary<int, (double Start, double Duration)> after
        )
        && before.Count == after.Count
        && before.All(kvp =>
            after.TryGetValue(kvp.Key, out (double Start, double Duration) other)
            && WithinSyncTolerance(kvp.Value.Start, other.Start)
            && WithinSyncTolerance(kvp.Value.Duration, other.Duration)
        );

    private static bool WithinSyncTolerance(double before, double after) =>
        // A value present on only one side cannot be verified, so fail closed; both sides missing (NaN)
        // is symmetric and uncomparable, treat as unchanged; otherwise the shift must be within tolerance
        double.IsNaN(before) == double.IsNaN(after)
        && (double.IsNaN(before) || Math.Abs(after - before) <= SyncToleranceSeconds);

    public bool SetLastWriteTimeUtc(DateTime lastWriteTimeUtc)
    {
        // Conditional
        if (!Program.Config.ProcessOptions.RestoreFileTimestamp)
        {
            return true;
        }

        // Set modified timestamp
        File.SetLastWriteTimeUtc(FileInfo.FullName, lastWriteTimeUtc);

        // Refresh sidecar info
        return Refresh(true);
    }

    private bool Refresh(string fileName)
    {
        // Media filename changed
        // Compare case sensitive for Linux support
        Debug.Assert(!string.IsNullOrEmpty(fileName));
        if (!FileInfo.FullName.Equals(fileName, StringComparison.Ordinal))
        {
            // Refresh sidecar file info but preserve existing state, mark as renamed
            FileInfo = new FileInfo(fileName);
            SidecarFile.StatesType state = _sidecarFile.State | SidecarFile.StatesType.FileReNamed;
            _sidecarFile = new SidecarFile(FileInfo) { State = state };
        }

        // Refresh sidecar info
        return Refresh(true);
    }

    private bool Refresh(bool modified)
    {
        // Call Refresh() at each processing function exit
        // Set modified to true if the media file has been modified
        // Set modified to false if only the state has been modified

        // Load info from sidecar
        if (Program.Config.ProcessOptions.UseSidecarFiles)
        {
            // Open will read, create, update
            if (!_sidecarFile.Open(modified))
            {
                // Failed to read or create sidecar
                return false;
            }

            // Assign results
            FfProbeProps = _sidecarFile.FfProbeProps;
            MkvMergeProps = _sidecarFile.MkvMergeProps;
            MediaInfoProps = _sidecarFile.MediaInfoProps;

            return true;
        }

        // Get info directly from tools
        if (
            !MediaProps.GetMediaProps(
                FileInfo,
                out MediaProps ffProbeProps,
                out MediaProps mkvMergeProps,
                out MediaProps mediaInfoProps
            )
        )
        {
            return false;
        }

        // Assign results
        MediaInfoProps = mediaInfoProps;
        MkvMergeProps = mkvMergeProps;
        FfProbeProps = ffProbeProps;

        // Print info at Debug; this per-file track dump is diagnostic detail, not a user action
        MediaInfoProps.WriteLine(LogEventLevel.Debug);
        MkvMergeProps.WriteLine(LogEventLevel.Debug);
        FfProbeProps.WriteLine(LogEventLevel.Debug);

        return true;
    }

    public bool VerifyMediaInfo()
    {
        // Comparing track ids generated between media tools are not directly possible
        // MkvMerge and MediaInfo Uid's are the same when reported, Number's and Id's are tool specific

        // Make sure the track counts match
        if (
            FfProbeProps.Audio.Count != MkvMergeProps.Audio.Count
            || MkvMergeProps.Audio.Count != MediaInfoProps.Audio.Count
            || FfProbeProps.Video.Count != MkvMergeProps.Video.Count
            || MkvMergeProps.Video.Count != MediaInfoProps.Video.Count
            || FfProbeProps.Subtitle.Count != MkvMergeProps.Subtitle.Count
            || MkvMergeProps.Subtitle.Count != MediaInfoProps.Subtitle.Count
        )
        {
            // Something is very wrong; bad logic, bad media, bad tools?
            Log.Error("Tool track count discrepancy : {FileName}", FileInfo.FullName);
            MediaInfoProps.WriteLine();
            MkvMergeProps.WriteLine();
            FfProbeProps.WriteLine();

            // Break in debug builds
            Debug.Assert(false);
            return false;
        }

        return true;
    }

    public bool GetMediaProps()
    {
        // Only MKV files in production use
        Debug.Assert(SidecarFile.IsMkvFile(FileInfo));

        // Get media info
        if (!Refresh(false))
        {
            Log.Error("Failed to get media tool info : {FileName}", FileInfo.FullName);
            return false;
        }

        // Verify that all codecs and tracks are supported
        if (MediaInfoProps.Unsupported || FfProbeProps.Unsupported || MkvMergeProps.Unsupported)
        {
            Log.Error("Unsupported media info : {FileName}", FileInfo.FullName);
            if (MediaInfoProps.Unsupported)
            {
                MediaInfoProps.WriteLine("Unsupported");
            }
            if (MkvMergeProps.Unsupported)
            {
                MkvMergeProps.WriteLine("Unsupported");
            }
            if (FfProbeProps.Unsupported)
            {
                FfProbeProps.WriteLine("Unsupported");
            }
            return false;
        }

        // Done
        return true;
    }

    public bool TestMediaProps()
    {
        // Only called from test code to verify behavior of parsing logic

        // Get media info
        if (!Refresh(false))
        {
            Log.Error("Failed to get media tool info : {FileName}", FileInfo.FullName);
            return false;
        }

        // Verify that all codecs and tracks are supported
        if (MediaInfoProps.Unsupported || FfProbeProps.Unsupported || MkvMergeProps.Unsupported)
        {
            Log.Error("Unsupported media info : {FileName}", FileInfo.FullName);
            if (MediaInfoProps.Unsupported)
            {
                MediaInfoProps.WriteLine("Unsupported");
            }
            if (MkvMergeProps.Unsupported)
            {
                MkvMergeProps.WriteLine("Unsupported");
            }
            if (FfProbeProps.Unsupported)
            {
                FfProbeProps.WriteLine("Unsupported");
            }
            return false;
        }

        // Done
        return true;
    }

    public bool GetBitrateInfo(out BitrateInfo? bitrateInfo) =>
        GetPacketAnalysis(Program.Options.QuickScan, out bitrateInfo, out _);

    public bool GetPacketAnalysis(
        bool quickScan,
        out BitrateInfo? bitrateInfo,
        out DtsInfo? dtsInfo
    )
    {
        // Use the default track, else the first track
        VideoProps? videoProps = FfProbeProps.Video.Find(item =>
            item.Flags.HasFlag(TrackProps.FlagsType.Default)
        );
        videoProps ??= FfProbeProps.Video.FirstOrDefault();
        AudioProps? audioProps = FfProbeProps.Audio.Find(item =>
            item.Flags.HasFlag(TrackProps.FlagsType.Default)
        );
        audioProps ??= FfProbeProps.Audio.FirstOrDefault();

        // Read all packets once, computing the bitrate and the DTS monotonicity in a single pass
        bitrateInfo = null;
        dtsInfo = null;
        BitrateInfo packetBitrate = new(
            videoProps?.Id ?? -1,
            audioProps?.Id ?? -1,
            Program.Config.VerifyOptions.MaximumBitrate / 8
        );
        DtsInfo packetDts = new();

        if (
            !Tools.FfProbe.GetAnalysisPackets(
                FileInfo.FullName,
                packet =>
                {
                    packetBitrate.Add(packet);
                    packetDts.Add(packet);
                    return true;
                },
                quickScan
            )
        )
        {
            return false;
        }

        // Calculate bitrate
        packetBitrate.Calculate();
        bitrateInfo = packetBitrate;
        dtsInfo = packetDts;
        return true;
    }

    private bool GetIdetInfo(out FfMpegIdetInfo? idetInfo)
    {
        // Count the frame types using the idet filter
        if (!FfMpegIdetInfo.GetIdetInfo(FileInfo.FullName, out idetInfo) || idetInfo == null)
        {
            // Cancel requested
            if (Program.IsCancelledError())
            {
                return false;
            }

            // Error
            Log.Error("Failed to count interlaced frames : {FileName}", FileInfo.FullName);
            return false;
        }

        return true;
    }

    public SelectMediaProps FindUnknownLanguageTracks()
    {
        // IETF languages will only be set for MkvMerge
        // Select all tracks with undefined languages
        // Selected is Unknown
        // NotSelected is Known
        SelectMediaProps selectMediaProps = new(
            MkvMergeProps,
            item => Language.IsUndefined(item.LanguageIetf)
        );
        return selectMediaProps;
    }

    public SelectMediaProps FindNeedReEncode()
    {
        // Filter logic values are based on FfProbe attributes
        // Start with empty selection
        // Selected is ReEncode
        // NotSelected is Keep
        SelectMediaProps selectMediaProps = new(FfProbeProps);

        // Add audio and video tracks
        // Select tracks matching the reencode lists
        FfProbeProps.Video.ForEach(item =>
            selectMediaProps.Add(
                item,
                Program.Config.ProcessOptions.ReEncodeVideo.Any(item.CompareVideo)
            )
        );
        FfProbeProps.Audio.ForEach(item =>
            selectMediaProps.Add(
                item,
                Program.Config.ProcessOptions.ReEncodeAudioFormats.Contains(item.Format)
            )
        );

        // Keep all subtitles
        selectMediaProps.Add(FfProbeProps.Subtitle, false);

        // If we are encoding audio, the video track may need to be reencoded at the same time
        // [matroska @ 00000195b3585c80] Timestamps are unset in a packet for stream 0.
        // [matroska @ 00000195b3585c80] Can't write packet with unknown timestamp
        // av_interleaved_write_frame(): Invalid argument
        if (selectMediaProps.Selected.Audio.Count > 0 && selectMediaProps.Selected.Video.Count == 0)
        {
            // If the video is not H264, H265 or AV1 (by experimentation), then tag the video to also be reencoded
            List<VideoProps> reEncodeVideo = selectMediaProps.NotSelected.Video.FindAll(item =>
                !ReEncodeVideoOnAudioReEncodeList.Contains(
                    item.Format,
                    StringComparer.OrdinalIgnoreCase
                )
            );
            if (reEncodeVideo.Count > 0)
            {
                selectMediaProps.Move(reEncodeVideo, true);
                Log.Warning(
                    "Audio reencoding requires video reencoding : Audio: {FormatA}, Video: {FormatV} : {FileName}",
                    selectMediaProps.Selected.Audio.Select(item => $"{item.Format}:{item.Codec}"),
                    reEncodeVideo.Select(item => $"{item.Format}:{item.Codec}:{item.Profile}"),
                    FileInfo.FullName
                );
            }
        }

        // Selected is ReEncode
        // NotSelected is Keep
        selectMediaProps.SetState(TrackProps.StateType.ReEncode, TrackProps.StateType.Keep);
        return selectMediaProps;
    }

    public SelectMediaProps FindDuplicateTracks()
    {
        // IETF languages will only be set for MkvMerge
        // Start with all tracks as NotSelected
        // Selected is Keep
        // NotSelected is Remove
        SelectMediaProps selectMediaProps = new(MkvMergeProps, false);

        // Get a track list
        List<TrackProps> trackList = MkvMergeProps.GetTrackList();

        // Get a list of all the IETF track languages
        List<string> languageList = Language.GetLanguageList(trackList);

        // Map each language to its corresponding track list
        List<List<TrackProps>> tracksByLanguage =
        [
            .. languageList.Select(language =>
                trackList.FindAll(item =>
                    language.Equals(item.LanguageIetf, StringComparison.OrdinalIgnoreCase)
                )
            ),
        ];

        foreach (List<TrackProps> trackLanguageList in tracksByLanguage)
        {
            // If multiple audio tracks exist for this language, keep the preferred audio codec track
            List<TrackProps> audioTrackList = trackLanguageList.FindAll(item =>
                item.GetType() == typeof(AudioProps)
            );
            if (audioTrackList.Count > 1)
            {
                AudioProps audioProps = FindPreferredAudio(audioTrackList);
                selectMediaProps.Move(audioProps, true);
            }

            // Keep all tracks with flags
            List<TrackProps> trackFlagList = trackLanguageList.FindAll(item =>
                item.Flags != TrackProps.FlagsType.None
            );
            selectMediaProps.Move(trackFlagList, true);

            // Keep one non-flag track
            // E.g. for subtitles it could be forced, hearing impaired, and one normal
            List<TrackProps> videoNotFlagList = trackLanguageList.FindAll(item =>
                item.Flags == TrackProps.FlagsType.None && item.GetType() == typeof(VideoProps)
            );
            if (videoNotFlagList.Count > 0)
            {
                selectMediaProps.Move(videoNotFlagList.First(), true);
            }
            List<TrackProps> audioNotFlagList = trackLanguageList.FindAll(item =>
                item.Flags == TrackProps.FlagsType.None && item.GetType() == typeof(AudioProps)
            );
            if (audioNotFlagList.Count > 0)
            {
                selectMediaProps.Move(audioNotFlagList.First(), true);
            }
            List<TrackProps> subtitleNotFlagList = trackLanguageList.FindAll(item =>
                item.Flags == TrackProps.FlagsType.None && item.GetType() == typeof(SubtitleProps)
            );
            if (subtitleNotFlagList.Count > 0)
            {
                selectMediaProps.Move(subtitleNotFlagList.First(), true);
            }
        }

        // We should have at least one of each kind of track, if any exists
        Debug.Assert(selectMediaProps.Selected.Video.Count > 0 || MkvMergeProps.Video.Count == 0);
        Debug.Assert(selectMediaProps.Selected.Audio.Count > 0 || MkvMergeProps.Audio.Count == 0);
        Debug.Assert(
            selectMediaProps.Selected.Subtitle.Count > 0 || MkvMergeProps.Subtitle.Count == 0
        );

        // Selected is Keep
        // NotSelected is Remove
        selectMediaProps.SetState(TrackProps.StateType.Keep, TrackProps.StateType.Remove);
        return selectMediaProps;
    }

    public SelectMediaProps FindUnwantedLanguageTracks()
    {
        // Note that zxx, und, and the default language will always be added to Program.Config.ProcessOptions.KeepLanguages

        // IETF language field will only be set for MkvMerge
        // Select tracks with wanted languages, or the original language if set to keep
        // Selected is Keep
        // NotSelected is Remove
        SelectMediaProps selectMediaProps = new(
            MkvMergeProps,
            item =>
                Language.IsMatch(item.LanguageIetf, Program.Config.ProcessOptions.KeepLanguages)
                || (
                    Program.Config.ProcessOptions.KeepOriginalLanguage
                    && item.Flags.HasFlag(TrackProps.FlagsType.Original)
                )
        );

        // Keep at least one video track if any
        if (selectMediaProps.Selected.Video.Count == 0 && MkvMergeProps.Video.Count > 0)
        {
            // Use the first track
            VideoProps videoProps = MkvMergeProps.Video.First();
            selectMediaProps.Move(videoProps, true);
            Log.Warning(
                "No video track matching requested language : {Available} not in {Languages}, selecting {Selected} : {FileName}",
                Language.GetLanguageList(MkvMergeProps.Video),
                Program.Config.ProcessOptions.KeepLanguages,
                videoProps.LanguageIetf,
                FileInfo.FullName
            );
        }

        // Keep at least one audio track if any
        if (selectMediaProps.Selected.Audio.Count == 0 && MkvMergeProps.Audio.Count > 0)
        {
            // Use the preferred audio codec track from the unselected tracks
            AudioProps audioProps = FindPreferredAudio(selectMediaProps.NotSelected.Audio);
            selectMediaProps.Move(audioProps, true);
            Log.Warning(
                "No audio track matching requested language : {Available} not in {Languages}, selecting {Selected} : {FileName}",
                Language.GetLanguageList(MkvMergeProps.Audio),
                Program.Config.ProcessOptions.KeepLanguages,
                audioProps.LanguageIetf,
                FileInfo.FullName
            );
        }

        // No language matching subtitle tracks
        if (selectMediaProps.Selected.Subtitle.Count == 0 && MkvMergeProps.Subtitle.Count > 0)
        {
            Log.Warning(
                "No subtitle track matching requested language : {Available} not in {Languages} : {FileName}",
                Language.GetLanguageList(MkvMergeProps.Subtitle),
                Program.Config.ProcessOptions.KeepLanguages,
                FileInfo.FullName
            );
        }

        // Selected is Keep
        // NotSelected is Remove
        selectMediaProps.SetState(TrackProps.StateType.Keep, TrackProps.StateType.Remove);
        return selectMediaProps;
    }

    private static AudioProps FindPreferredAudio(IEnumerable<TrackProps> trackInfoList)
    {
        // No preferred tracks, or only 1 track, use first track
        List<AudioProps> audioPropsList = [.. trackInfoList.OfType<AudioProps>()];
        Debug.Assert(audioPropsList.Count > 0);
        if (
            Program.Config.ProcessOptions.PreferredAudioFormats.Count == 0
            || audioPropsList.Count == 1
        )
        {
            return audioPropsList.First();
        }

        // Iterate through the preferred codecs in order and return on first match
        AudioProps? audioProps = Program
            .Config.ProcessOptions.PreferredAudioFormats.Select(format =>
                audioPropsList.Find(item =>
                    item.Format.Equals(format, StringComparison.OrdinalIgnoreCase)
                )
            )
            .FirstOrDefault(props => props != null);
        if (audioProps != null)
        {
            Log.Debug(
                "Preferred audio format selected : {Preferred} in {Formats}",
                audioProps.Format,
                audioPropsList.Select(item => item.Format)
            );
            return audioProps;
        }

        // Return first item
        Log.Debug(
            "No audio format matching preferred formats : {Preferred} not in {Formats}, Selecting {Selected}",
            Program.Config.ProcessOptions.PreferredAudioFormats,
            audioPropsList.Select(item => item.Format),
            audioPropsList.First().Format
        );
        return audioPropsList.First();
    }

    public static bool IsTempFile(FileInfo fileInfo) =>
        // All temp files are to be named tmp[x] where x is an incrementing number
        // All uses of temp files must be uniquely named allowing nested use without overlap in temp file names
        fileInfo.Extension.StartsWith(".tmp", StringComparison.OrdinalIgnoreCase);
}
