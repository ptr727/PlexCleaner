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
            return sidecarfile.WriteSidecar(mediaFile);
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
            string sidecarName = Path.ChangeExtension(mediaFile.FullName, SidecarExtension);
            if (!File.Exists(sidecarName))
                return false;
            FileInfo sidecarInfo = new FileInfo(sidecarName);

            // Init
            FfProbeInfo = null;
            MkvMergeInfo = null;
            MediaInfoInfo = null;
            FfMpegIdetInfo = null;

            try
            { 
                // Read the sidecar file
                ConsoleEx.WriteLine($"Reading media info from sidecar file : \"{sidecarInfo.Name}\"");
                SidecarFileJsonSchema sidecarJson = SidecarFileJsonSchema.FromJson(File.ReadAllText(sidecarName));

                // Compare the schema version
                if (sidecarJson.SchemaVersion != SidecarFileJsonSchema.CurrentSchemaVersion)
                {
                    ConsoleEx.WriteLine($"Sidecar version mismatch : {sidecarJson.SchemaVersion} != {SidecarFileJsonSchema.CurrentSchemaVersion} : \"{sidecarInfo.Name}\"");
                    return false;
                }

                // Compare the media modified time and file size
                mediaFile.Refresh();
                if (mediaFile.LastWriteTimeUtc != sidecarJson.MediaLastWriteTimeUtc ||
                    mediaFile.Length != sidecarJson.MediaLength)
                {
                    ConsoleEx.WriteLine($"Sidecar out of sync with media file : {mediaFile.LastWriteTimeUtc} != {sidecarJson.MediaLastWriteTimeUtc}, {mediaFile.Length} != {sidecarJson.MediaLength}, \"{sidecarInfo.Name}\"");
                    return false;
                }

                // Compare the tool versions
                if (sidecarJson.FfMpegToolVersion != FfMpegTool.Version)
                {
                    ConsoleEx.WriteLine($"Sidecar FFmpeg tool version out of date : {sidecarJson.FfMpegToolVersion} != {FfMpegTool.Version}, \"{sidecarInfo.Name}\"");
                    return false;
                }
                if (sidecarJson.MkvToolVersion != MkvTool.Version)
                {
                    ConsoleEx.WriteLine($"Sidecar MKVMerge tool version out of date : {sidecarJson.MkvToolVersion} != {MkvTool.Version}, \"{sidecarInfo.Name}\"");
                    return false;
                }
                if (sidecarJson.MediaInfoToolVersion != MediaInfoTool.Version)
                {
                    ConsoleEx.WriteLine($"Sidecar MediaInfo tool version out of date : {sidecarJson.MediaInfoToolVersion} != {MediaInfoTool.Version}, \"{sidecarInfo.Name}\"");
                    return false;
                }

                // Decompress the tool data
                string ffProbeInfoJson = StringCompression.Decompress(sidecarJson.FfProbeInfoData);
                string ffIdetInfoText = StringCompression.Decompress(sidecarJson.FfIdetInfoData);
                string mkvMergeInfoJson = StringCompression.Decompress(sidecarJson.MkvMergeInfoData);
                string mediaInfoXml = StringCompression.Decompress(sidecarJson.MediaInfoData);

                // Deserialize the tool data
                MediaInfo mediaInfoInfo = null;
                MediaInfo mkvMergeInfo = null;
                MediaInfo ffProbeInfo = null;
                FfMpegIdetInfo ffMpegIdetInfo = null;
                if (!MediaInfoTool.GetMediaInfoFromXml(mediaInfoXml, out mediaInfoInfo) ||
                    !MkvTool.GetMkvInfoFromJson(mkvMergeInfoJson, out mkvMergeInfo) ||
                    !FfMpegTool.GetFfProbeInfoFromJson(ffProbeInfoJson, out ffProbeInfo) ||
                    !FfMpegTool.GetIdetInfoFromText(ffIdetInfoText, out ffMpegIdetInfo))
                {
                    ConsoleEx.WriteLineError($"Failed to de-serialize tool data : \"{sidecarInfo.Name}\"");
                    return false;
                }

                // Assign the data
                FfProbeInfo = ffProbeInfo;
                MkvMergeInfo = mkvMergeInfo;
                MediaInfoInfo = mediaInfoInfo;
                FfMpegIdetInfo = ffMpegIdetInfo;
            }
            catch (Exception e)
            {
                ConsoleEx.WriteLineError(e);
                return false;
            }
            return true;
        }

        public bool WriteSidecar(FileInfo mediaFile)
        {
            if (mediaFile == null)
                throw new ArgumentNullException(nameof(mediaFile));

            try
            {
                // Delete the sidecar if it exists
                string sidecarName = Path.ChangeExtension(mediaFile.FullName, SidecarExtension);
                if (File.Exists(sidecarName))
                    File.Delete(sidecarName);
                FileInfo sidecarInfo = new FileInfo(sidecarName);

                // Serialize the tool data
                ConsoleEx.WriteLine($"Reading media info : \"{mediaFile.Name}\"");
                if (!MediaInfoTool.GetMediaInfoXml(mediaFile.FullName, out string mediaInfoXml) ||
                    !MkvTool.GetMkvInfoJson(mediaFile.FullName, out string mkvMergeInfoJson) ||
                    !FfMpegTool.GetFfProbeInfoJson(mediaFile.FullName, out string ffProbeInfoJson) ||
                    !FfMpegTool.GetIdetInfoText(mediaFile.FullName, out string ffIdetInfoText))
                {
                    ConsoleEx.WriteLineError($"Failed to read media info : \"{mediaFile.Name}\"");
                    return false;
                }

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
                sidecarJson.FfProbeInfoData = StringCompression.Compress(ffProbeInfoJson);
                sidecarJson.FfIdetInfoData = StringCompression.Compress(ffIdetInfoText);
                sidecarJson.MkvMergeInfoData = StringCompression.Compress(mkvMergeInfoJson);
                sidecarJson.MediaInfoData = StringCompression.Compress(mediaInfoXml);

                // Convert the JSON to text
                string json = SidecarFileJsonSchema.ToJson(sidecarJson);

                // Write the json text to the sidecar file
                ConsoleEx.WriteLine($"Writing media info to sidecar file : \"{sidecarInfo.Name}\"");
                File.WriteAllText(sidecarName, json);

                // Deserialize the tool data
                MediaInfo mediaInfoInfo = null;
                MediaInfo mkvMergeInfo = null;
                MediaInfo ffProbeInfo = null;
                FfMpegIdetInfo ffMpegIdetInfo = null;
                if (!MediaInfoTool.GetMediaInfoFromXml(mediaInfoXml, out mediaInfoInfo) ||
                    !MkvTool.GetMkvInfoFromJson(mkvMergeInfoJson, out mkvMergeInfo) ||
                    !FfMpegTool.GetFfProbeInfoFromJson(ffProbeInfoJson, out ffProbeInfo) ||
                    !FfMpegTool.GetIdetInfoFromText(ffIdetInfoText, out ffMpegIdetInfo))
                {
                    ConsoleEx.WriteLineError($"Failed to de-serialize tool data : \"{sidecarInfo.Name}\"");
                    return false;
                }

                // Assign the data
                FfProbeInfo = ffProbeInfo;
                MkvMergeInfo = mkvMergeInfo;
                MediaInfoInfo = mediaInfoInfo;
                FfMpegIdetInfo = ffMpegIdetInfo;
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
                   WriteSidecar(mediaFile);
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
                   MediaInfoInfo != null &&
                   FfMpegIdetInfo != null;
        }

        public MediaInfo FfProbeInfo { get; set; }
        public MediaInfo MkvMergeInfo { get; set; }
        public MediaInfo MediaInfoInfo { get; set; }
        public FfMpegIdetInfo FfMpegIdetInfo { get; set; }

        public const string SidecarExtension = @".PlexCleaner";
    }
}
