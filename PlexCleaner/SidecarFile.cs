using InsaneGenius.Utilities;
using System;
using System.Diagnostics;
using System.IO;

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

            // Does the sidecar file exist
            string sidecarName = GetSidecarName(mediaFile);
            if (!File.Exists(sidecarName))
                return false;
            FileInfo sidecarInfo = new FileInfo(sidecarName);

            // Init
            FfProbeInfo = null;
            MkvMergeInfo = null;
            MediaInfoInfo = null;
            Verified = false;

            try
            { 
                // Read the sidecar file
                ConsoleEx.WriteLine($"Reading media info from sidecar file : \"{sidecarInfo.Name}\"");
                SidecarFileJsonSchema sidecarJson = SidecarFileJsonSchema.FromJson(File.ReadAllText(sidecarName));

                // Compare the schema version
                if (sidecarJson.SchemaVersion != SidecarFileJsonSchema.CurrentSchemaVersion)
                {
                    ConsoleEx.WriteLine($"Sidecar version mismatch : \"{sidecarInfo.Name}\"");
                    return false;
                }

                // Compare the media modified time and file size
                mediaFile.Refresh();
                if (mediaFile.LastWriteTimeUtc != sidecarJson.MediaLastWriteTimeUtc ||
                    mediaFile.Length != sidecarJson.MediaLength)
                {
                    ConsoleEx.WriteLine($"Sidecar out of sync with media file : \"{sidecarInfo.Name}\"");
                    return false;
                }

                // Compare the tool versions
                if (sidecarJson.FfMpegToolVersion != FfMpegTool.Version ||
                    sidecarJson.MkvToolVersion != MkvTool.Version ||
                    sidecarJson.MediaInfoToolVersion != MediaInfoTool.Version)
                {
                    ConsoleEx.WriteLine($"Sidecar tool version out of date : \"{sidecarInfo.Name}\"");
                    return false;
                }

                // Verified
                Verified = sidecarJson.Verified;

                // Decompress the tool data
                string ffProbeInfoJson = StringCompression.Decompress(sidecarJson.FfProbeInfoData);
                string mkvMergeInfoJson = StringCompression.Decompress(sidecarJson.MkvMergeInfoData);
                string mediaInfoXml = StringCompression.Decompress(sidecarJson.MediaInfoData);

                // Deserialize the tool data
                MediaInfo mediaInfoInfo = null;
                MediaInfo mkvMergeInfo = null;
                MediaInfo ffProbeInfo = null;
                if (!MediaInfoTool.GetMediaInfoFromXml(mediaInfoXml, out mediaInfoInfo) ||
                    !MkvTool.GetMkvInfoFromJson(mkvMergeInfoJson, out mkvMergeInfo) ||
                    !FfMpegTool.GetFfProbeInfoFromJson(ffProbeInfoJson, out ffProbeInfo))
                {
                    ConsoleEx.WriteLineError($"Failed to de-serialize tool data : \"{sidecarInfo.Name}\"");
                    return false;
                }

                // Assign mediainfo data
                FfProbeInfo = ffProbeInfo;
                MkvMergeInfo = mkvMergeInfo;
                MediaInfoInfo = mediaInfoInfo;
            }
            catch (Exception e)
            {
                ConsoleEx.WriteLineError(e);
                return false;
            }
            return true;
        }

        public bool CreateSidecar(FileInfo mediaFile)
        {
            if (mediaFile == null)
                throw new ArgumentNullException(nameof(mediaFile));

            try
            {
                // Read the tool data text
                ConsoleEx.WriteLine($"Reading media info : \"{mediaFile.Name}\"");
                if (!MediaInfoTool.GetMediaInfoXml(mediaFile.FullName, out MediaInfoXml) ||
                    !MkvTool.GetMkvInfoJson(mediaFile.FullName, out MkvMergeInfoJson) ||
                    !FfMpegTool.GetFfProbeInfoJson(mediaFile.FullName, out FfProbeInfoJson))
                {
                    ConsoleEx.WriteLineError($"Failed to read media info : \"{mediaFile.Name}\"");
                    return false;
                }

                // Deserialize the tool data
                MediaInfo mediaInfoInfo = null;
                MediaInfo mkvMergeInfo = null;
                MediaInfo ffProbeInfo = null;
                if (!MediaInfoTool.GetMediaInfoFromXml(MediaInfoXml, out mediaInfoInfo) ||
                    !MkvTool.GetMkvInfoFromJson(MkvMergeInfoJson, out mkvMergeInfo) ||
                    !FfMpegTool.GetFfProbeInfoFromJson(FfProbeInfoJson, out ffProbeInfo))
                {
                    ConsoleEx.WriteLineError($"Failed to de-serialize tool data : \"{mediaFile.Name}\"");
                    return false;
                }

                // Assign the mediainfo data
                FfProbeInfo = ffProbeInfo;
                MkvMergeInfo = mkvMergeInfo;
                MediaInfoInfo = mediaInfoInfo;

                // Verify is externally assigned
            }
            catch (Exception e)
            {
                ConsoleEx.WriteLineError(e);
                return false;
            }
            return WriteSidecar(mediaFile);
        }

        public bool WriteSidecar(FileInfo mediaFile)
        {
            if (mediaFile == null)
                throw new ArgumentNullException(nameof(mediaFile));

            try
            {
                // Delete the sidecar if it exists
                string sidecarName = GetSidecarName(mediaFile);
                if (File.Exists(sidecarName))
                    File.Delete(sidecarName);
                FileInfo sidecarInfo = new FileInfo(sidecarName);

                // Create sidecar JSON
                SidecarFileJsonSchema sidecarJson = new SidecarFileJsonSchema();

                // Set the media modified time and file size
                mediaFile.Refresh();
                sidecarJson.MediaLastWriteTimeUtc = mediaFile.LastWriteTimeUtc;
                sidecarJson.MediaLength = mediaFile.Length;

                // Set the tool versions
                sidecarJson.SchemaVersion = SidecarFileJsonSchema.CurrentSchemaVersion;
                sidecarJson.FfMpegToolVersion = FfMpegTool.Version;
                sidecarJson.MkvToolVersion = MkvTool.Version;
                sidecarJson.MediaInfoToolVersion = MediaInfoTool.Version;

                // Compress the tool data
                sidecarJson.FfProbeInfoData = StringCompression.Compress(FfProbeInfoJson);
                sidecarJson.MkvMergeInfoData = StringCompression.Compress(MkvMergeInfoJson);
                sidecarJson.MediaInfoData = StringCompression.Compress(MediaInfoXml);

                // Verify flag
                sidecarJson.Verified = Verified;

                // Convert the JSON to text
                string json = SidecarFileJsonSchema.ToJson(sidecarJson);

                // Write the json text to the sidecar file
                ConsoleEx.WriteLine($"Writing media info to sidecar file : \"{sidecarInfo.Name}\"");
                File.WriteAllText(sidecarName, json);
            }
            catch (Exception e)
            {
                ConsoleEx.WriteLineError(e);
                return false;
            }
            return true;
        }

        public bool GetMediaInfo(FileInfo mediaFile)
        {
            // Try to read the sidecar file
            // Else try to write a new sidecar
            return ReadSidecar(mediaFile) || 
                   CreateSidecar(mediaFile);
        }

        public MediaInfo GetMediaInfo(MediaInfo.ParserType parser)
        {
            Debug.Assert(IsValid());

            return parser switch
            {
                MediaInfo.ParserType.MediaInfo => MediaInfoInfo,
                MediaInfo.ParserType.MkvMerge => MkvMergeInfo,
                MediaInfo.ParserType.FfProbe => MediaInfoInfo,
                _ => throw new NotImplementedException(),
            };
        }

        public bool IsValid()
        {
            return FfProbeInfo != null &&
                   MkvMergeInfo != null &&
                   MediaInfoInfo != null;
        }

        public static string GetSidecarName(FileInfo mediaFile)
        {
            if (mediaFile == null)
                throw new ArgumentNullException(nameof(mediaFile));

            return Path.ChangeExtension(mediaFile.FullName, SidecarExtension);
        }

        public MediaInfo FfProbeInfo { get; set; }
        public MediaInfo MkvMergeInfo { get; set; }
        public MediaInfo MediaInfoInfo { get; set; }
        public bool Verified { get; set; }

        private string MediaInfoXml;
        private string MkvMergeInfoJson;
        private string FfProbeInfoJson;

        public const string SidecarExtension = @".PlexCleaner";
    }
}
