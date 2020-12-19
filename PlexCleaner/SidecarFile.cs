using InsaneGenius.Utilities;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace PlexCleaner
{
    public class SidecarFile
    {
        public static bool IsSidecarFile(string filename)
        {
            return IsSidecarExtension(Path.GetExtension(filename));
        }

        public static bool IsSidecarFile(FileInfo fileinfo)
        {
            if (fileinfo == null)
                throw new ArgumentNullException(nameof(fileinfo));

            return IsSidecarExtension(fileinfo.Extension);
        }

        public static bool IsSidecarExtension(string extension)
        {
            if (extension == null)
                throw new ArgumentNullException(nameof(extension));

            return extension.Equals(SidecarExtension, StringComparison.OrdinalIgnoreCase);
        }

        public static bool CreateSidecarFile(FileInfo mediaFile)
        {
            SidecarFile sidecarfile = new SidecarFile();
            return sidecarfile.CreateSidecar(mediaFile);
        }

        public static bool DoesSidecarExist(FileInfo mediaFile)
        {
            if (mediaFile == null)
                throw new ArgumentNullException(nameof(mediaFile));

            // Does the sidecar file exist for this media file
            string sidecarName = Path.ChangeExtension(mediaFile.FullName, SidecarExtension);
            return File.Exists(sidecarName);
        }

        public static string GetSidecarName(FileInfo mediaFile)
        {
            if (mediaFile == null)
                throw new ArgumentNullException(nameof(mediaFile));

            return Path.ChangeExtension(mediaFile.FullName, SidecarExtension);
        }

        public static bool GetMediaInfo(FileInfo mediaFile, out MediaInfo ffprobeInfo, out MediaInfo mkvmergeInfo, out MediaInfo mediainfoInfo)
        {
            // Init
            ffprobeInfo = null;
            mkvmergeInfo = null;
            mediainfoInfo = null;

            // Read or create
            SidecarFile sidecarFile = new SidecarFile();
            if (!sidecarFile.GetMediaInfo(mediaFile))
                return false;

            // Assign
            ffprobeInfo = sidecarFile.FfProbeInfo;
            mkvmergeInfo = sidecarFile.MkvMergeInfo;
            mediainfoInfo = sidecarFile.MediaInfoInfo;

            return true;
        }

        public bool ReadSidecar(FileInfo mediaFile)
        {
            if (mediaFile == null)
                throw new ArgumentNullException(nameof(mediaFile));

            // Init
            Verified = false;
            FfProbeInfo = null;
            MkvMergeInfo = null;
            MediaInfoInfo = null;

            // Does the sidecar file exist
            string sidecarName = GetSidecarName(mediaFile);
            if (!File.Exists(sidecarName))
                return false;

            // Read the JSON from disk
            FileInfo sidecarFile = new FileInfo(sidecarName);
            if (!ReadSidecarJson(sidecarFile))
                return false;

            // Compare the schema version
            if (SidecarJson.SchemaVersion != SidecarFileJsonSchema.CurrentSchemaVersion)
            {
                ConsoleEx.WriteLine("");
                ConsoleEx.WriteLine($"Warning : Sidecar schema version mismatch : \"{sidecarFile.Name}\"");
                return false;
            }

            // Compare the media modified time and file size
            mediaFile.Refresh();
            if (mediaFile.LastWriteTimeUtc != SidecarJson.MediaLastWriteTimeUtc ||
                mediaFile.Length != SidecarJson.MediaLength)
            {
                ConsoleEx.WriteLine("");
                ConsoleEx.WriteLine($"Warning : Sidecar out of sync with media file : \"{sidecarFile.Name}\"");
                return false;
            }

            // Compare the tool versions
            if (SidecarJson.FfProbeToolVersion != Tools.FfProbe.Info.Version ||
                SidecarJson.MkvMergeToolVersion != Tools.MkvMerge.Info.Version ||
                SidecarJson.MediaInfoToolVersion != Tools.MediaInfo.Info.Version)
            {
                ConsoleEx.WriteLine("");
                ConsoleEx.WriteLine($"Warning : Sidecar tool versions out of date : \"{sidecarFile.Name}\"");
                if (Program.Config.ProcessOptions.SidecarUpdateOnToolChange)
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
                ConsoleEx.WriteLine("");
                ConsoleEx.WriteLineError($"Error : Failed to de-serialize tool data : \"{sidecarFile.Name}\"");
                return false;
            }

            // Assign verified
            Verified = SidecarJson.Verified;

            // Assign mediainfo data
            FfProbeInfo = ffProbeInfo;
            MkvMergeInfo = mkvMergeInfo;
            MediaInfoInfo = mediaInfoInfo;

            return true;
        }

        public bool ReadSidecarJson(FileInfo sidecarFile)
        {
            if (sidecarFile == null)
                throw new ArgumentNullException(nameof(sidecarFile));

            try
            {
                // Read the sidecar file
                ConsoleEx.WriteLine("");
                ConsoleEx.WriteLine($"Reading media info from sidecar file : \"{sidecarFile.Name}\"");
                SidecarJson = SidecarFileJsonSchema.FromJson(File.ReadAllText(sidecarFile.FullName));

                // Decompress the tool data
                FfProbeInfoJson = StringCompression.Decompress(SidecarJson.FfProbeInfoData);
                MkvMergeInfoJson = StringCompression.Decompress(SidecarJson.MkvMergeInfoData);
                MediaInfoXml = StringCompression.Decompress(SidecarJson.MediaInfoData);
            }
            catch (Exception e)
            {
                ConsoleEx.WriteLine("");
                ConsoleEx.WriteLineError(e);
                return false;
            }
            return true;
        }

        public bool CreateSidecar(FileInfo mediaFile)
        {
            if (mediaFile == null)
                throw new ArgumentNullException(nameof(mediaFile));

            // Read the tool data text
            ConsoleEx.WriteLine("");
            ConsoleEx.WriteLine($"Reading media info : \"{mediaFile.Name}\"");
            if (!Tools.MediaInfo.GetMediaInfoXml(mediaFile.FullName, out MediaInfoXml) ||
                !Tools.MkvMerge.GetMkvInfoJson(mediaFile.FullName, out MkvMergeInfoJson) ||
                !Tools.FfProbe.GetFfProbeInfoJson(mediaFile.FullName, out FfProbeInfoJson))
            {
                ConsoleEx.WriteLine("");
                ConsoleEx.WriteLineError($"Error : Failed to read media info : \"{mediaFile.Name}\"");
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
                ConsoleEx.WriteLine("");
                ConsoleEx.WriteLineError($"Error : Failed to de-serialize tool data : \"{mediaFile.Name}\"");
                return false;
            }

            // Assign the mediainfo data
            FfProbeInfo = ffProbeInfo;
            MkvMergeInfo = mkvMergeInfo;
            MediaInfoInfo = mediaInfoInfo;

            // Verify is externally assigned

            // Write the sidecar
            return WriteSidecarJson(mediaFile);
        }

        public bool WriteSidecarJson(FileInfo mediaFile)
        {
            if (mediaFile == null)
                throw new ArgumentNullException(nameof(mediaFile));

            // Delete the sidecar if it exists
            // string sidecarName = GetSidecarName(mediaFile);
            string sidecarName = Path.ChangeExtension(mediaFile.Name, SidecarExtension);
            string sidecarFullName = Path.ChangeExtension(mediaFile.FullName, SidecarExtension);
            if (File.Exists(sidecarFullName))
                File.Delete(sidecarFullName);

            // Refresh the media file info
            mediaFile.Refresh();

            // Create the sidecar json object
            SidecarJson = new SidecarFileJsonSchema
            {
                // Schema version
                SchemaVersion = SidecarFileJsonSchema.CurrentSchemaVersion,

                // Media file info
                MediaLastWriteTimeUtc = mediaFile.LastWriteTimeUtc,
                MediaLength = mediaFile.Length,

                // Tool version info
                FfProbeToolVersion = Tools.FfProbe.Info.Version,
                MkvMergeToolVersion = Tools.MkvMerge.Info.Version,
                MediaInfoToolVersion = Tools.MediaInfo.Info.Version,

                // Compressed tool info
                FfProbeInfoData = StringCompression.Compress(FfProbeInfoJson),
                MkvMergeInfoData = StringCompression.Compress(MkvMergeInfoJson),
                MediaInfoData = StringCompression.Compress(MediaInfoXml),

                // Verified flag
                Verified = Verified
            };

            try
            {
                // Write the json text to the sidecar file
                ConsoleEx.WriteLine("");
                ConsoleEx.WriteLine($"Writing media info to sidecar file : \"{sidecarName}\"");
                File.WriteAllText(sidecarFullName, SidecarFileJsonSchema.ToJson(SidecarJson));
            }
            catch (Exception e)
            {
                ConsoleEx.WriteLine("");
                ConsoleEx.WriteLineError(e);
                return false;
            }
            return true;
        }

        public bool GetMediaInfo(FileInfo mediaFile)
        {
            return GetMediaInfo(mediaFile, false);
        }

        public bool GetMediaInfo(FileInfo mediaFile, bool refresh)
        {
            // Create a new sidecar
            if (refresh)
                return CreateSidecar(mediaFile);

            // Try to read the sidecar, else create a new sidecar
            return ReadSidecar(mediaFile) || CreateSidecar(mediaFile);
        }

        public MediaInfo GetMediaInfo(MediaTool.ToolType parser)
        {
            Debug.Assert(IsValid());

            return parser switch
            {
                MediaTool.ToolType.MediaInfo => MediaInfoInfo,
                MediaTool.ToolType.MkvMerge => MkvMergeInfo,
                MediaTool.ToolType.FfProbe => FfProbeInfo,
                _ => throw new NotImplementedException()
            };
        }

        public bool IsValid()
        {
            return FfProbeInfo != null &&
                   MkvMergeInfo != null &&
                   MediaInfoInfo != null;
        }

        public override string ToString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("MediaInfoXml :");
            stringBuilder.AppendLine(MediaInfoXml);
            stringBuilder.AppendLine("MkvMergeInfoJson :");
            stringBuilder.AppendLine(MkvMergeInfoJson);
            stringBuilder.AppendLine("FfProbeInfoJson :");
            stringBuilder.AppendLine(FfProbeInfoJson);
            stringBuilder.AppendLine($"Verified : {SidecarJson.Verified}");
            stringBuilder.AppendLine($"MediaLastWriteTimeUtc : {SidecarJson.MediaLastWriteTimeUtc}");
            stringBuilder.AppendLine($"MediaLength : {SidecarJson.MediaLength}");
            stringBuilder.AppendLine($"MediaInfoToolVersion : {SidecarJson.MediaInfoToolVersion}");
            stringBuilder.AppendLine($"MkvMergeToolVersion : {SidecarJson.MkvMergeToolVersion}");
            stringBuilder.AppendLine($"FfProbeToolVersion : {SidecarJson.FfProbeToolVersion}");

            return stringBuilder.ToString();
        }

        public MediaInfo FfProbeInfo { get; set; }
        public MediaInfo MkvMergeInfo { get; set; }
        public MediaInfo MediaInfoInfo { get; set; }
        public bool Verified { get; set; }

        private string MediaInfoXml;
        private string MkvMergeInfoJson;
        private string FfProbeInfoJson;
        private SidecarFileJsonSchema SidecarJson;

        public const string SidecarExtension = @".PlexCleaner";
    }
}
