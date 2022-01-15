using InsaneGenius.Utilities;
using Serilog;
using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace PlexCleaner;

public class SidecarFile
{
    [Flags]
    public enum States
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
        ReNamed = 1 << 10,
        Deleted = 1 << 11,
        Modified = 1 << 12
    }

    public SidecarFile(FileInfo mediaFileInfo)
    {
        MediaFileInfo = mediaFileInfo ?? throw new ArgumentNullException(nameof(mediaFileInfo));
        SidecarFileInfo = new FileInfo(GetSidecarName(mediaFileInfo));
    }

    public bool Create()
    {
        // Get tool info
        if (!GetToolInfo())
            return false;

        // Reset state
        State = States.None;

        // Set the JSON info
        if (!SetJsonInfo())
            return false;

        // Write the JSON to file
        if (!WriteJson())
            return false;

        Log.Logger.Information("Sidecar created : State: {State} : {FileName}", State, SidecarFileInfo.Name);

        return true;
    }

    public bool Read()
    {
        // Read the JSON from file
        if (!ReadJson())
            return false;

        // Get the info from JSON
        if (!GetInfoFromJson())
            return false;

        // Log warnings
        // Do not read new tool data, will be read in Update()

        // Verify the media file matches the json info
        if (!IsMediaCurrent(true))
        { 
            // The media file has been changed
            Log.Logger.Warning("Sidecar out of sync with media file, clearing state : {FileName}", SidecarFileInfo.Name);
            State = States.Modified;
        }

        // Verify the tools matches the json info
        // Ignore changes if SidecarUpdateOnToolChange is not set
        if (!IsToolsCurrent(true) && 
            Program.Config.ProcessOptions.SidecarUpdateOnToolChange)
        {
            // Remove the verified state flag if set
            if (State.HasFlag(States.Verified))
            { 
                Log.Logger.Warning("Sidecar out of sync with tools, clearing Verified flag : {FileName}", SidecarFileInfo.Name);
                State &= ~States.Verified;
            }
        }

        Log.Logger.Information("Sidecar read : State: {State} : {FileName}", State, SidecarFileInfo.Name);

        return true;
    }

    public bool Update()
    {
        return Update(false);
    }

    public bool Update(bool modified)
    {
        // Create or Read must be called before update
        if (!IsValid())
            return false;

        // Did the media file or tools change
        // Do not log if not current, updates are intentional
        if (modified ||
            !IsMediaAndToolsCurrent(false))
        {
            // Get updated tool info
            if (!GetToolInfo())
                return false;
        }

        // Set the JSON info from tool info
        if (!SetJsonInfo())
            return false;

        // Write the JSON to file
        if (!WriteJson())
            return false;

        Log.Logger.Information("Sidecar updated : State: {State} : {FileName}", State, SidecarFileInfo.Name);

        return true;
    }

    public bool Delete()
    {
        try
        {
            if (SidecarFileInfo.Exists)
                SidecarFileInfo.Delete();
        }
        catch (Exception e) when (Log.Logger.LogAndHandle(e, MethodBase.GetCurrentMethod()?.Name))
        {
            return false;
        }

        // Reset
        SidecarJson = null;
        State = States.None;
        FfProbeInfo = null;
        MkvMergeInfo = null;
        MediaInfoInfo = null;

        Log.Logger.Information("Sidecar deleted : {FileName}", SidecarFileInfo.Name);

        return true;
    }

    public bool Open()
    {
        return Open(false);
    }

    public bool Open(bool modified)
    {
        // Make sure the sidecar file has been read or created
        if (!IsValid())
        {
            // If the sidecar does not exist, or can't be read, create it
            if (!SidecarFileInfo.Exists ||
                !Read())
                return Create();
        }

        // Update if not current
        // Do not log if not current, updates are intentional
        if (modified ||
            !IsStateCurrent() ||
            !IsMediaAndToolsCurrent(false))
            return Update(modified);

        // Already up to date
        return true;
    }

    public bool Upgrade()
    {
        // Do the files exist
        if (!SidecarFileInfo.Exists ||
            !MediaFileInfo.Exists)
        { 
            Log.Logger.Error("File not found : {File}", SidecarFileInfo.FullName);
            return false;
        }

        // Read the JSON from file
        // Get the info from JSON
        if (!ReadJson() ||
            !GetInfoFromJson())
            return false;

        // Check one by one to log all the mismatches
        // ReSharper disable once ReplaceWithSingleAssignment.False
        bool update = false;
        if (!IsSchemaCurrent())
            update = true;
        if (!IsStateCurrent())
            update = true;
        if (!IsMediaCurrent(true))
            update = true;
        if (!IsToolsCurrent(true))
            update = true;
        if (!update)
        { 
            Log.Logger.Information("Sidecar up to date : State: {State} : {FileName}", State, SidecarFileInfo.Name);
            return true;
        }

        // Set the JSON info from tool info
        // Write the JSON to file
        if (!SetJsonInfo() ||
            !WriteJson())
            return false;

        Log.Logger.Information("Sidecar upgraded : State: {State} : {FileName}", State, SidecarFileInfo.Name);

        return true;
    }

    public bool IsCurrent()
    {
        return IsMediaAndToolsCurrent(true);
    }

    private bool IsMediaAndToolsCurrent(bool log)
    {
        // Verify the media file matches the json info
        bool mismatch = !IsMediaCurrent(log);

        // Verify the tools matches the json info
        // Ignore changes if SidecarUpdateOnToolChange is not set
        if (!IsToolsCurrent(log) && Program.Config.ProcessOptions.SidecarUpdateOnToolChange)
            mismatch = true;

        return !mismatch;
    }

    public bool IsStateCurrent()
    {
        return State == SidecarJson.State;
    }

    public bool IsSchemaCurrent()
    {
        return SidecarJson.SchemaVersion == SidecarFileJsonSchema.CurrentSchemaVersion;
    }

    public bool IsValid()
    {
        return SidecarJson != null;
    }

    public bool IsWriteable()
    {
        // File must exist and be writeable
        return SidecarFileInfo.Exists && FileEx.IsFileReadWriteable(SidecarFileInfo);
    }

    public bool Exists()
    {
        return SidecarFileInfo.Exists;
    }

    private bool GetInfoFromJson()
    {
        Log.Logger.Information("Reading media info from sidecar : {FileName}", SidecarFileInfo.Name);

        // Decompress the tool data
        FfProbeInfoJson = StringCompression.Decompress(SidecarJson.FfProbeInfoData);
        MkvMergeInfoJson = StringCompression.Decompress(SidecarJson.MkvMergeInfoData);
        MediaInfoXml = StringCompression.Decompress(SidecarJson.MediaInfoData);

        // Deserialize the tool data
        MediaInfo mediaInfoInfo = null;
        MediaInfo mkvMergeInfo = null;
        MediaInfo ffProbeInfo = null;
        if (!Tools.MediaInfo.GetMediaInfoFromXml(MediaInfoXml, out mediaInfoInfo) ||
            !Tools.MkvMerge.GetMkvInfoFromJson(MkvMergeInfoJson, out mkvMergeInfo) ||
            !Tools.FfProbe.GetFfProbeInfoFromJson(FfProbeInfoJson, out ffProbeInfo))
        {
            Log.Logger.Error("Failed to de-serialize tool data : {FileName}", SidecarFileInfo.Name);
            return false;
        }

        // Assign mediainfo data
        FfProbeInfo = ffProbeInfo;
        MkvMergeInfo = mkvMergeInfo;
        MediaInfoInfo = mediaInfoInfo;

        // Assign state
        State = SidecarJson.State;

        return true;
    }

    private bool IsMediaCurrent(bool log)
    {
        // Refresh file info
        MediaFileInfo.Refresh();

        // Compare media attributes
        bool mismatch = false;
        if (MediaFileInfo.LastWriteTimeUtc != SidecarJson.MediaLastWriteTimeUtc)
        {
            // Ignore LastWriteTimeUtc, it is unreliable over SMB
            // mismatch = true;
            if (log)
                Log.Logger.Warning("Sidecar LastWriteTimeUtc out of sync with media file : {SidecarJsonMediaLastWriteTimeUtc} != {MediaFileLastWriteTimeUtc} : {FileName}",
                    SidecarJson.MediaLastWriteTimeUtc,
                    MediaFileInfo.LastWriteTimeUtc,
                    SidecarFileInfo.Name);
        }
        if (MediaFileInfo.Length != SidecarJson.MediaLength)
        {
            mismatch = true;
            if (log)
                Log.Logger.Warning("Sidecar FileLength out of sync with media file : {SidecarJsonMediaLength} != {MediaFileLength} : {FileName}",
                    SidecarJson.MediaLength,
                    MediaFileInfo.Length,
                    SidecarFileInfo.Name);
        }
        string hash = ComputeHash();
        if (string.Compare(hash, SidecarJson.MediaHash, StringComparison.OrdinalIgnoreCase) != 0)
        {
            mismatch = true;
            if (log)
                Log.Logger.Warning("Sidecar SHA256 out of sync with media file : {SidecarJsonHash} != {MediaFileHash} : {FileName}",
                    SidecarJson.MediaHash,
                    hash,
                    SidecarFileInfo.Name);
        }

        return !mismatch;
    }

    private bool IsToolsCurrent(bool log)
    {
        // Compare tool versions
        bool mismatch = false;
        if (!SidecarJson.FfProbeToolVersion.Equals(Tools.FfProbe.Info.Version, StringComparison.OrdinalIgnoreCase))
        {
            mismatch = true;
            if (log)
                Log.Logger.Warning("Sidecar FfProbe tool version out of date : {SidecarJsonFfProbeToolVersion} != {ToolsFfProbeInfoVersion} : {FileName}",
                    SidecarJson.FfProbeToolVersion,
                    Tools.FfProbe.Info.Version,
                    SidecarFileInfo.Name);
        }
        if (!SidecarJson.MkvMergeToolVersion.Equals(Tools.MkvMerge.Info.Version, StringComparison.OrdinalIgnoreCase))
        {
            mismatch = true;
            if (log)
                Log.Logger.Warning("Sidecar MkvMerge tool version out of date : {SidecarJsonMkvMergeToolVersion} != {ToolsMkvMergeInfoVersion} : {FileName}",
                    SidecarJson.MkvMergeToolVersion,
                    Tools.MkvMerge.Info.Version,
                    SidecarFileInfo.Name);
        }
        if (!SidecarJson.MediaInfoToolVersion.Equals(Tools.MediaInfo.Info.Version, StringComparison.OrdinalIgnoreCase))
        {
            mismatch = true;
            if (log)
                Log.Logger.Warning("Sidecar MediaInfo tool version out of date : {SidecarJsonMediaInfoToolVersion} != {ToolsMediaInfoVersion} : {FileName}",
                    SidecarJson.MediaInfoToolVersion,
                    Tools.MediaInfo.Info.Version,
                    SidecarFileInfo.Name);
        }

        return !mismatch;
    }

    private bool ReadJson()
    {
        try
        {
            // Read the text from the sidecar file
            using StreamReader streamReader = SidecarFileInfo.OpenText();
            string json = streamReader.ReadToEnd();
            streamReader.Close();

            // Create object from text
            SidecarJson = SidecarFileJsonSchema.FromJson(json);
        }
        catch (Exception e) when (Log.Logger.LogAndHandle(e, MethodBase.GetCurrentMethod()?.Name))
        {
            return false;
        }

        // Compare the schema version
        if (SidecarJson.SchemaVersion != SidecarFileJsonSchema.CurrentSchemaVersion)
        {
            Log.Logger.Warning("Sidecar JSON schema mismatch : {JsonSchemaVersion} != {CurrentSchemaVersion}, {FileName}",
                SidecarJson.SchemaVersion,
                SidecarFileJsonSchema.CurrentSchemaVersion,
                SidecarFileInfo.Name);

            // Upgrade schema
            if (!SidecarFileJsonSchema.Upgrade(SidecarJson))
                return false;
        }

        return true;
    }

    private bool WriteJson()
    {
        try
        {
            // Get json text from object
            string json = SidecarFileJsonSchema.ToJson(SidecarJson);

            // Write the text to the sidecar file
            using StreamWriter streamWriter = SidecarFileInfo.CreateText();
            streamWriter.Write(json);
            streamWriter.Flush();
            streamWriter.Close();
        }
        catch (Exception e) when (Log.Logger.LogAndHandle(e, MethodBase.GetCurrentMethod()?.Name))
        {
            return false;
        }
        return true;
    }

    private bool SetJsonInfo()
    {
        // Create the sidecar json object
        SidecarJson ??= new SidecarFileJsonSchema();

        // Schema version
        SidecarJson.SchemaVersion = SidecarFileJsonSchema.CurrentSchemaVersion;

        // Media file info
        MediaFileInfo.Refresh();
        SidecarJson.MediaLastWriteTimeUtc = MediaFileInfo.LastWriteTimeUtc;
        SidecarJson.MediaLength = MediaFileInfo.Length;
        SidecarJson.MediaHash = ComputeHash();

        // Tool version info
        SidecarJson.FfProbeToolVersion = Tools.FfProbe.Info.Version;
        SidecarJson.MkvMergeToolVersion = Tools.MkvMerge.Info.Version;
        SidecarJson.MediaInfoToolVersion = Tools.MediaInfo.Info.Version;

        // Compressed tool info
        SidecarJson.FfProbeInfoData = StringCompression.Compress(FfProbeInfoJson);
        SidecarJson.MkvMergeInfoData = StringCompression.Compress(MkvMergeInfoJson);
        SidecarJson.MediaInfoData = StringCompression.Compress(MediaInfoXml);

        // State
        SidecarJson.State = State;

        return true;
    }

    private bool GetToolInfo()
    {
        Log.Logger.Information("Reading media info from tools : {FileName}", MediaFileInfo.Name);

        // Read the tool data text
        if (!Tools.MediaInfo.GetMediaInfoXml(MediaFileInfo.FullName, out MediaInfoXml) ||
            !Tools.MkvMerge.GetMkvInfoJson(MediaFileInfo.FullName, out MkvMergeInfoJson) ||
            !Tools.FfProbe.GetFfProbeInfoJson(MediaFileInfo.FullName, out FfProbeInfoJson))
        {
            Log.Logger.Error("Failed to read media info : {FileName}", MediaFileInfo.Name);
            return false;
        }

        // Deserialize the tool data
        MediaInfo mediaInfoInfo = null;
        MediaInfo mkvMergeInfo = null;
        MediaInfo ffProbeInfo = null;
        if (!Tools.MediaInfo.GetMediaInfoFromXml(MediaInfoXml, out mediaInfoInfo) ||
            !Tools.MkvMerge.GetMkvInfoFromJson(MkvMergeInfoJson, out mkvMergeInfo) ||
            !Tools.FfProbe.GetFfProbeInfoFromJson(FfProbeInfoJson, out ffProbeInfo))
        {
            Log.Logger.Error("Failed to de-serialize tool data : {FileName}", MediaFileInfo.Name);
            return false;
        }

        // Assign the mediainfo data
        MediaInfoInfo = mediaInfoInfo;
        MkvMergeInfo = mkvMergeInfo;
        FfProbeInfo = ffProbeInfo;

        // Print info
        MediaInfoInfo.WriteLine("MediaInfo");
        MkvMergeInfo.WriteLine("MKVMerge");
        FfProbeInfo.WriteLine("FFprobe");

        return true;
    }

    private string ComputeHash()
    {
        try
        {
            // Create SHA256 hash calculator
            using SHA256 hashCalculator = SHA256.Create();

            // Create a buffer to hold the file data being hashed
            byte[] buffer = new byte[2 * HashWindowLength];

            // Open file
            using FileStream fileStream = MediaFileInfo.Open(FileMode.Open);

            // Small files read entire file, big files read front and back
            if (MediaFileInfo.Length <= buffer.Length)
            {
                // Read the entire file
                fileStream.Seek(0, SeekOrigin.Begin);
                if (fileStream.Read(buffer, 0, (int)MediaFileInfo.Length) != MediaFileInfo.Length)
                {
                    Log.Logger.Error("Error reading from media file : {FileName}", MediaFileInfo.Name);
                    return null;
                }
            }
            else 
            { 
                // Read the beginning of the file
                fileStream.Seek(0, SeekOrigin.Begin);
                if (fileStream.Read(buffer, 0, HashWindowLength) != HashWindowLength)
                {
                    Log.Logger.Error("Error reading from media file : {FileName}", MediaFileInfo.Name);
                    return null;
                }

                // Read the end of the file
                fileStream.Seek(-HashWindowLength, SeekOrigin.End);
                if (fileStream.Read(buffer, HashWindowLength, HashWindowLength) != HashWindowLength)
                {
                    Log.Logger.Error("Error reading from media file : {FileName}", MediaFileInfo.Name);
                    return null;
                }
            }

            // Close stream
            fileStream.Close();

            // Calculate the hash 
            byte[] hash = hashCalculator.ComputeHash(buffer);

            // Convert to string
            return System.Convert.ToBase64String(hash);
        }
        catch (Exception e) when (Log.Logger.LogAndHandle(e, MethodBase.GetCurrentMethod()?.Name))
        {
            return null;
        }
    }

    public static bool IsSidecarFile(FileInfo sidecarFileInfo)
    {
        if (sidecarFileInfo == null)
            throw new ArgumentNullException(nameof(sidecarFileInfo));

        // Compare extension
        return sidecarFileInfo.Extension.Equals(SidecarExtension, StringComparison.OrdinalIgnoreCase);
    }
    public static string GetSidecarName(FileInfo mediaFileInfo)
    {
        if (mediaFileInfo == null)
            throw new ArgumentNullException(nameof(mediaFileInfo));

        // Change extension of media file
        return Path.ChangeExtension(mediaFileInfo.FullName, SidecarExtension);
    }

    public static bool IsMediaFileName(FileInfo mediaFileInfo)
    {
        if (mediaFileInfo == null)
            throw new ArgumentNullException(nameof(mediaFileInfo));

        // Compare extension
        return mediaFileInfo.Extension.Equals(MkvExtension, StringComparison.OrdinalIgnoreCase);
    }

    public static string GetMediaName(FileInfo sidecarFileInfo)
    {
        if (sidecarFileInfo == null)
            throw new ArgumentNullException(nameof(sidecarFileInfo));

        // Change extension of media file
        return Path.ChangeExtension(sidecarFileInfo.FullName, MkvExtension);
    }

    public static bool CreateSidecarFile(FileInfo mediaFileInfo)
    {
        if (mediaFileInfo == null)
            throw new ArgumentNullException(nameof(mediaFileInfo));

        // Create new sidecar for media file
        SidecarFile sidecarfile = new(mediaFileInfo);
        return sidecarfile.Create();
    }

    public void WriteLine()
    {
        Log.Logger.Information("State: {State}", State);
        Log.Logger.Information("MediaInfoXml: {MediaInfoXml}", MediaInfoXml);
        Log.Logger.Information("MkvMergeInfoJson: {MkvMergeInfoJson}", MkvMergeInfoJson);
        Log.Logger.Information("FfProbeInfoJson: {FfProbeInfoJson}", FfProbeInfoJson);
        Log.Logger.Information("MediaLastWriteTimeUtc: {MediaLastWriteTimeUtc}", SidecarJson.MediaLastWriteTimeUtc);
        Log.Logger.Information("MediaLength: {MediaLength}", SidecarJson.MediaLength);
        Log.Logger.Information("MediaInfoToolVersion: {MediaInfoToolVersion}", SidecarJson.MediaInfoToolVersion);
        Log.Logger.Information("MkvMergeToolVersion: {MkvMergeToolVersion}", SidecarJson.MkvMergeToolVersion);
        Log.Logger.Information("FfProbeToolVersion: {FfProbeToolVersion}", SidecarJson.FfProbeToolVersion);
    }

    public MediaInfo FfProbeInfo { get; set; }
    public MediaInfo MkvMergeInfo { get; set; }
    public MediaInfo MediaInfoInfo { get; set; }
    public States State { get; set; }

    private FileInfo MediaFileInfo;
    private FileInfo SidecarFileInfo;

    private string MediaInfoXml;
    private string MkvMergeInfoJson;
    private string FfProbeInfoJson;

    private SidecarFileJsonSchema SidecarJson;

    public const string SidecarExtension = @".PlexCleaner";
    public const string MkvExtension = @".mkv";

    private const int HashWindowLength = 64 * Format.KiB;
}