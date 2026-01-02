using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using InsaneGenius.Utilities;
using Serilog;

namespace PlexCleaner;

public class SidecarFile
{
    [Flags]
    public enum StatesType
    {
        None = 0,
        SetLanguage = 1,
        ReMuxed = 1 << 1,
        ReEncoded = 1 << 2,
        DeInterlaced = 1 << 3,
        Repaired = 1 << 4,
        RepairFailed = 1 << 5,
        Verified = 1 << 6,
        VerifyFailed = 1 << 7,
        BitrateExceeded = 1 << 8,
        ClearedTags = 1 << 9,
        FileReNamed = 1 << 10,
        FileDeleted = 1 << 11,
        FileModified = 1 << 12,
        ClearedCaptions = 1 << 13,
        RemovedAttachments = 1 << 14,
        SetFlags = 1 << 15,
        RemovedCoverArt = 1 << 16,
    }

    private const string SidecarExtension = ".PlexCleaner";
    private const string MkvExtension = ".mkv";
    private const int HashWindowLength = 64 * 1024;

    private readonly FileInfo _mediaFileInfo;
    private readonly FileInfo _sidecarFileInfo;

    private string _ffProbeJson;
    private string _mediaInfoJson;
    private string _mkvMergeJson;

    private SidecarFileJsonSchema _sidecarJson;

    public SidecarFile(FileInfo mediaFileInfo)
    {
        _mediaFileInfo = mediaFileInfo;
        _sidecarFileInfo = new FileInfo(GetSidecarName(_mediaFileInfo));
    }

    public SidecarFile(string mediaFileName)
    {
        _mediaFileInfo = new FileInfo(mediaFileName);
        _sidecarFileInfo = new FileInfo(GetSidecarName(_mediaFileInfo));
    }

    public MediaProps FfProbeProps { get; private set; }
    public MediaProps MkvMergeProps { get; private set; }
    public MediaProps MediaInfoProps { get; private set; }
    public StatesType State { get; set; }

    public bool Create()
    {
        // Do not modify the state, it is managed external to the create path

        // Get tool info
        if (!GetToolInfo())
        {
            return false;
        }

        // Set the JSON info
        if (!SetJsonInfo())
        {
            return false;
        }

        // Write the JSON to file
        if (!WriteJson())
        {
            return false;
        }

        Log.Information(
            "Sidecar created : State: {State} : {FileName}",
            State,
            _sidecarFileInfo.Name
        );

        return true;
    }

    public bool Read() => Read(out _);

    public bool Read(out bool current, bool verify = true)
    {
        current = true;

        // Read the JSON from file
        if (!ReadJson())
        {
            return false;
        }

        // Get the info from JSON
        if (!GetMediaPropsFromJson())
        {
            return false;
        }

        // Stop here if not verifying
        if (!verify)
        {
            return true;
        }

        // Log warnings
        // Do not get new tool data, do not check state, will be set in Update()

        // Verify the media file matches the json info
        if (!IsMediaCurrent(true))
        {
            // The media file has been changed
            current = false;
            Log.Warning(
                "Sidecar out of sync with media file, clearing state : {FileName}",
                _sidecarFileInfo.Name
            );
            State = StatesType.FileModified;
        }

        // Verify the tools matches the json info
        // Ignore changes if SidecarUpdateOnToolChange is not set
        if (
            !IsToolsCurrent(Program.Config.ProcessOptions.SidecarUpdateOnToolChange)
            && Program.Config.ProcessOptions.SidecarUpdateOnToolChange
        )
        {
            // Remove the verified state flag if set
            current = false;
            if (State.HasFlag(StatesType.Verified))
            {
                Log.Warning(
                    "Sidecar out of sync with tools, clearing Verified flag : {FileName}",
                    _sidecarFileInfo.Name
                );
                State &= ~StatesType.Verified;
            }
        }

        Log.Information("Sidecar read : State: {State} : {FileName}", State, _sidecarFileInfo.Name);

        return true;
    }

    private bool Update(bool modified = false)
    {
        // Create or Read must be called before update
        Debug.Assert(_sidecarJson != null);

        // Did the media file or tools change
        // Do not log if not current, updates are intentional
        if (modified || !IsMediaAndToolsCurrent(false))
        {
            // Get updated tool info
            if (!GetToolInfo())
            {
                return false;
            }
        }

        // Set the JSON info from tool info
        if (!SetJsonInfo())
        {
            return false;
        }

        // Write the JSON to file
        if (!WriteJson())
        {
            return false;
        }

        Log.Information(
            "Sidecar updated : State: {State} : {FileName}",
            State,
            _sidecarFileInfo.Name
        );

        return true;
    }

    public bool Open(bool modified = false)
    {
        // Open will Read, Create, Update

        // Make sure the sidecar file has been read or created
        // If the sidecar file exists, read it
        // If we can't read it, re-create it
        // If it does not exist, create it
        // If it no longer matches, update it
        if (_sidecarJson == null)
        {
            if (_sidecarFileInfo.Exists)
            {
                // Sidecar file exists, read and verify it matches media file
                if (!Read(out bool current))
                {
                    // Failed to read, create it
                    return Create();
                }
                // Media file changed, force an update
                if (!current)
                {
                    modified = true;
                }
            }
            else
            {
                // Sidecar file does not exist, create it
                return Create();
            }
        }
        Debug.Assert(_sidecarJson != null);

        // Update info if media file or tools changed, or if state is not current
        if (modified || !IsStateCurrent())
        {
            // Update will write the JSON including state, but only update tool and hash info if modified set
            return Update(modified);
        }

        // Already up to date
        return true;
    }

    private bool IsMediaAndToolsCurrent(bool log)
    {
        // Follow all steps to log all mismatches, do not jump out early

        // Verify the media file matches the json info
        bool mismatch = !IsMediaCurrent(log);

        // Verify the tools matches the json info
        // Ignore changes if SidecarUpdateOnToolChange is not set
        if (!IsToolsCurrent(log) && Program.Config.ProcessOptions.SidecarUpdateOnToolChange)
        {
            mismatch = true;
        }

        return !mismatch;
    }

    private bool IsStateCurrent() => State == _sidecarJson.State;

    public bool IsWriteable() => _sidecarFileInfo.Exists && !_sidecarFileInfo.IsReadOnly;

    public bool Exists() => _sidecarFileInfo.Exists;

    private bool GetMediaPropsFromJson()
    {
        Log.Information("Reading media info from sidecar : {FileName}", _sidecarFileInfo.Name);

        // Decompress the tool data
        _mediaInfoJson = StringCompression.Decompress(_sidecarJson.MediaInfoData);
        _mkvMergeJson = StringCompression.Decompress(_sidecarJson.MkvMergeData);
        _ffProbeJson = StringCompression.Decompress(_sidecarJson.FfProbeData);

        // Must have data to deserialize
        if (
            string.IsNullOrEmpty(_mediaInfoJson)
            || string.IsNullOrEmpty(_mkvMergeJson)
            || string.IsNullOrEmpty(_ffProbeJson)
        )
        {
            Log.Error("Media info tool data is missing : {FileName}", _sidecarFileInfo.Name);
            return false;
        }

        // Deserialize the tool data
        if (
            !MediaInfo.Tool.GetMediaPropsFromJson(
                _mediaInfoJson,
                _mediaFileInfo.Name,
                out MediaProps mediaInfoProps
            )
            || !MkvMerge.Tool.GetMediaPropsFromJson(
                _mkvMergeJson,
                _mediaFileInfo.Name,
                out MediaProps mkvMergeProps
            )
            || !FfProbe.Tool.GetMediaPropsFromJson(
                _ffProbeJson,
                _mediaFileInfo.Name,
                out MediaProps ffProbeProps
            )
        )
        {
            Log.Error("Failed to de-serialize tool data : {FileName}", _sidecarFileInfo.Name);
            return false;
        }

        // Assign MediaProps data
        MediaInfoProps = mediaInfoProps;
        MkvMergeProps = mkvMergeProps;
        FfProbeProps = ffProbeProps;

        // Assign state
        State = _sidecarJson.State;

        return true;
    }

    private bool IsMediaCurrent(bool log)
    {
        // Refresh file info
        _mediaFileInfo.Refresh();

        // Compare media attributes
        bool mismatch = false;
        if (_mediaFileInfo.LastWriteTimeUtc != _sidecarJson.MediaLastWriteTimeUtc)
        {
            // Ignore LastWriteTimeUtc, it is unreliable over SMB
            // mismatch = true;
            if (log)
            {
                Log.Warning(
                    "Sidecar LastWriteTimeUtc out of sync with media file : {SidecarJsonMediaLastWriteTimeUtc} != {MediaFileLastWriteTimeUtc} : {FileName}",
                    _sidecarJson.MediaLastWriteTimeUtc,
                    _mediaFileInfo.LastWriteTimeUtc,
                    _sidecarFileInfo.Name
                );
            }
        }
        if (_mediaFileInfo.Length != _sidecarJson.MediaLength)
        {
            mismatch = true;
            if (log)
            {
                Log.Warning(
                    "Sidecar FileLength out of sync with media file : {SidecarJsonMediaLength} != {MediaFileLength} : {FileName}",
                    _sidecarJson.MediaLength,
                    _mediaFileInfo.Length,
                    _sidecarFileInfo.Name
                );
            }
        }
        string hash = ComputeHash();
        Debug.Assert(!string.IsNullOrEmpty(hash));
        if (!string.Equals(hash, _sidecarJson.MediaHash, StringComparison.OrdinalIgnoreCase))
        {
            mismatch = true;
            if (log)
            {
                Log.Warning(
                    "Sidecar SHA256 out of sync with media file : {SidecarJsonHash} != {MediaFileHash} : {FileName}",
                    _sidecarJson.MediaHash,
                    hash,
                    _sidecarFileInfo.Name
                );
            }
        }

        return !mismatch;
    }

    private bool IsToolsCurrent(bool log)
    {
        // Compare tool versions
        bool mismatch = false;
        if (
            !_sidecarJson.FfProbeToolVersion.Equals(
                Tools.FfProbe.Info.Version,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            mismatch = true;
            if (log)
            {
                Log.Warning(
                    "Sidecar FfProbe tool version mismatch : {SidecarJsonFfProbeToolVersion} != {ToolsFfProbeInfoVersion} : {FileName}",
                    _sidecarJson.FfProbeToolVersion,
                    Tools.FfProbe.Info.Version,
                    _sidecarFileInfo.Name
                );
            }
        }
        if (
            !_sidecarJson.MkvMergeToolVersion.Equals(
                Tools.MkvMerge.Info.Version,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            mismatch = true;
            if (log)
            {
                Log.Warning(
                    "Sidecar MkvMerge tool version mismatch : {SidecarJsonMkvMergeToolVersion} != {ToolsMkvMergeInfoVersion} : {FileName}",
                    _sidecarJson.MkvMergeToolVersion,
                    Tools.MkvMerge.Info.Version,
                    _sidecarFileInfo.Name
                );
            }
        }
        if (
            !_sidecarJson.MediaInfoToolVersion.Equals(
                Tools.MediaInfo.Info.Version,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            mismatch = true;
            if (log)
            {
                Log.Warning(
                    "Sidecar MediaInfo tool version mismatch : {SidecarJsonMediaInfoToolVersion} != {ToolsMediaInfoVersion} : {FileName}",
                    _sidecarJson.MediaInfoToolVersion,
                    Tools.MediaInfo.Info.Version,
                    _sidecarFileInfo.Name
                );
            }
        }

        return !mismatch;
    }

    private bool ReadJson()
    {
        try
        {
            // Create the object from the sidecar file
            _sidecarJson = SidecarFileJsonSchema.FromFile(_sidecarFileInfo.FullName);
            if (_sidecarJson == null)
            {
                Log.Error("Failed to read JSON from file : {FileName}", _sidecarFileInfo.Name);
                return false;
            }
        }
        catch (Exception e) when (Log.Logger.LogAndHandle(e))
        {
            return false;
        }
        return true;
    }

    private bool WriteJson()
    {
        try
        {
            // Write the object to the sidecar file
            SidecarFileJsonSchema.ToFile(_sidecarFileInfo.FullName, _sidecarJson);
        }
        catch (Exception e) when (Log.Logger.LogAndHandle(e))
        {
            return false;
        }
        return true;
    }

    private bool SetJsonInfo()
    {
        // Create the sidecar json object
        _sidecarJson ??= new SidecarFileJsonSchema();

        // Schema version
        _sidecarJson.SchemaVersion = SidecarFileJsonSchema.Version;

        // Media file info
        _mediaFileInfo.Refresh();
        _sidecarJson.MediaLastWriteTimeUtc = _mediaFileInfo.LastWriteTimeUtc;
        _sidecarJson.MediaLength = _mediaFileInfo.Length;
        _sidecarJson.MediaHash = ComputeHash();
        Debug.Assert(!string.IsNullOrEmpty(_sidecarJson.MediaHash));

        // Tool version info
        _sidecarJson.FfProbeToolVersion = Tools.FfProbe.Info.Version;
        _sidecarJson.MkvMergeToolVersion = Tools.MkvMerge.Info.Version;
        _sidecarJson.MediaInfoToolVersion = Tools.MediaInfo.Info.Version;

        // Compressed tool info
        _sidecarJson.FfProbeData = StringCompression.Compress(_ffProbeJson);
        _sidecarJson.MkvMergeData = StringCompression.Compress(_mkvMergeJson);
        _sidecarJson.MediaInfoData = StringCompression.Compress(_mediaInfoJson);

        // State
        _sidecarJson.State = State;

        return true;
    }

    private bool GetToolInfo()
    {
        Log.Information("Reading media info from tools : {FileName}", _mediaFileInfo.Name);

        // Read the tool data text
        if (
            !Tools.MediaInfo.GetMediaPropsJson(_mediaFileInfo.FullName, out _mediaInfoJson)
            || !Tools.MkvMerge.GetMediaPropsJson(_mediaFileInfo.FullName, out _mkvMergeJson)
            || !Tools.FfProbe.GetMediaPropsJson(_mediaFileInfo.FullName, out _ffProbeJson)
        )
        {
            Log.Error("Failed to read media properties : {FileName}", _mediaFileInfo.Name);
            return false;
        }

        // Deserialize the tool data
        if (
            !MediaInfo.Tool.GetMediaPropsFromJson(
                _mediaInfoJson,
                _mediaFileInfo.Name,
                out MediaProps mediaInfoProps
            )
            || !MkvMerge.Tool.GetMediaPropsFromJson(
                _mkvMergeJson,
                _mediaFileInfo.Name,
                out MediaProps mkvMergeProps
            )
            || !FfProbe.Tool.GetMediaPropsFromJson(
                _ffProbeJson,
                _mediaFileInfo.Name,
                out MediaProps ffProbeProps
            )
        )
        {
            Log.Error("Failed to de-serialize tool data : {FileName}", _mediaFileInfo.Name);
            return false;
        }

        // Assign the mediainfo data
        MediaInfoProps = mediaInfoProps;
        MkvMergeProps = mkvMergeProps;
        FfProbeProps = ffProbeProps;

        // Print info
        MediaInfoProps.WriteLine();
        MkvMergeProps.WriteLine();
        FfProbeProps.WriteLine();

        return true;
    }

    private string ComputeHash()
    {
        byte[] hashBuffer = null;
        try
        {
            // Rent buffer from shared pool for efficient memory reuse
            hashBuffer = ArrayPool<byte>.Shared.Rent(2 * HashWindowLength);

            // Clear the rented buffer
            Array.Clear(hashBuffer, 0, 2 * HashWindowLength);

            // Open file
            using FileStream fileStream = _mediaFileInfo.Open(
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read
            );

            // Small files read entire file, big files read front and back
            if (_mediaFileInfo.Length <= 2 * HashWindowLength)
            {
                // Read the entire file, buffer is already zeroed
                _ = fileStream.Seek(0, SeekOrigin.Begin);
                if (
                    fileStream.Read(hashBuffer, 0, (int)_mediaFileInfo.Length)
                    != _mediaFileInfo.Length
                )
                {
                    Log.Error("Error reading from media file : {FileName}", _mediaFileInfo.Name);
                    return null;
                }
            }
            else
            {
                // Read the beginning of the file
                _ = fileStream.Seek(0, SeekOrigin.Begin);
                if (fileStream.Read(hashBuffer, 0, HashWindowLength) != HashWindowLength)
                {
                    Log.Error("Error reading from media file : {FileName}", _mediaFileInfo.Name);
                    return null;
                }

                // Read the end of the file
                _ = fileStream.Seek(-HashWindowLength, SeekOrigin.End);
                if (
                    fileStream.Read(hashBuffer, HashWindowLength, HashWindowLength)
                    != HashWindowLength
                )
                {
                    Log.Error("Error reading from media file : {FileName}", _mediaFileInfo.Name);
                    return null;
                }
            }

            // Close stream
            fileStream.Close();

            // Calculate the hash and convert to string
            // Only hash the relevant portion (2 * HashWindowLength)
            return System.Convert.ToBase64String(
                SHA256.HashData(hashBuffer.AsSpan(0, 2 * HashWindowLength))
            );
        }
        catch (Exception e) when (Log.Logger.LogAndHandle(e))
        {
            return null;
        }
        finally
        {
            // Always return buffer to pool
            if (hashBuffer != null)
            {
                ArrayPool<byte>.Shared.Return(hashBuffer);
            }
        }
    }

    public static bool IsSidecarFile(string sidecarName) =>
        IsSidecarExtension(Path.GetExtension(sidecarName));

    public static bool IsSidecarFile(FileInfo sidecarFileInfo) =>
        IsSidecarExtension(sidecarFileInfo.Extension);

    public static bool IsSidecarExtension(string extension) =>
        extension.Equals(SidecarExtension, StringComparison.OrdinalIgnoreCase);

    public static bool IsMkvFile(string mkvName) => IsMkvExtension(Path.GetExtension(mkvName));

    public static bool IsMkvFile(FileInfo mkvFileInfo) => IsMkvExtension(mkvFileInfo.Extension);

    public static bool IsMkvExtension(string extension) =>
        extension.Equals(MkvExtension, StringComparison.OrdinalIgnoreCase);

    public static string GetSidecarName(string mkvName) =>
        Path.ChangeExtension(mkvName, SidecarExtension);

    public static string GetSidecarName(FileInfo mkvFileInfo) =>
        Path.ChangeExtension(mkvFileInfo.FullName, SidecarExtension);

    public static string GetMkvName(string sidecarName) =>
        Path.ChangeExtension(sidecarName, MkvExtension);

    public static string GetMkvName(FileInfo sidecarFileInfo) =>
        Path.ChangeExtension(sidecarFileInfo.FullName, MkvExtension);

    public void WriteLine()
    {
        Log.Information("State: {State}", State);
        Log.Information("MediaInfoJson: {MediaInfoJson}", _mediaInfoJson);
        Log.Information("MkvMergeJson: {MkvMergeJson}", _mkvMergeJson);
        Log.Information("FfProbeJson: {FfProbeJson}", _ffProbeJson);
        Log.Information("SchemaVersion: {SchemaVersion}", _sidecarJson.SchemaVersion);
        Log.Information(
            "MediaLastWriteTimeUtc: {MediaLastWriteTimeUtc}",
            _sidecarJson.MediaLastWriteTimeUtc
        );
        Log.Information("MediaLength: {MediaLength}", _sidecarJson.MediaLength);
        Log.Information("MediaHash: {MediaHash}", _sidecarJson.MediaHash);
        Log.Information(
            "MediaInfoToolVersion: {MediaInfoToolVersion}",
            _sidecarJson.MediaInfoToolVersion
        );
        Log.Information(
            "MkvMergeToolVersion: {MkvMergeToolVersion}",
            _sidecarJson.MkvMergeToolVersion
        );
        Log.Information(
            "FfProbeToolVersion: {FfProbeToolVersion}",
            _sidecarJson.FfProbeToolVersion
        );
    }

    public static bool GetInformation(string fileName)
    {
        // Must be a MKV file
        Debug.Assert(IsMkvFile(fileName));

        // Does a sidecar exist
        if (!File.Exists(GetSidecarName(fileName)))
        {
            // Skip if no sidecar for MKV file
            return true;
        }

        // Read sidecar information
        SidecarFile sidecarFile = new(fileName);
        if (!sidecarFile.Read())
        {
            return false;
        }

        // Print info
        sidecarFile.WriteLine();

        return true;
    }

    public static bool Create(string fileName)
    {
        // Must be a MKV file
        Debug.Assert(IsMkvFile(fileName));

        // Create new or overwrite existing sidecar file
        SidecarFile sidecarFile = new(fileName);
        return sidecarFile.Create();
    }

    public static bool Update(string fileName)
    {
        // Must be a MKV file
        Debug.Assert(IsMkvFile(fileName));

        // Create new or update existing sidecar file
        SidecarFile sidecarFile = new(fileName);
        return sidecarFile.Open(true);
    }
}
