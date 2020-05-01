using InsaneGenius.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PlexCleaner
{
    public static class SidecarFile
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

            return SidecarExtensions.Contains(extension);
        }

        public static bool CreateSidecarFiles(FileInfo fileinfo)
        {
            return GetMediaInfo(fileinfo, true, out MediaInfo _, out MediaInfo _, out MediaInfo _);
        }

        public static bool GetMediaInfo(FileInfo fileinfo, bool forceupdate, out MediaInfo ffprobe, out MediaInfo mkvmerge, out MediaInfo mediainfo)
        {
            // Init
            ffprobe = null;
            mkvmerge = null;
            mediainfo = null;

            return GetMediaInfo(fileinfo, forceupdate, MediaInfo.ParserType.FfProbe, out ffprobe) &&
                   GetMediaInfo(fileinfo, forceupdate, MediaInfo.ParserType.MkvMerge, out mkvmerge) &&
                   GetMediaInfo(fileinfo, forceupdate, MediaInfo.ParserType.MediaInfo, out mediainfo);
        }

        public static bool GetMediaInfo(FileInfo fileinfo, bool forceupdate, MediaInfo.ParserType parser, out MediaInfo mediainfo)
        {
            if (fileinfo == null)
                throw new ArgumentNullException(nameof(fileinfo));
            
            // Init
            mediainfo = null;

            // TODO : Chance of race condition between reading media, external write to same media, and writing sidecar

            // Create the sidecar file name
            string sidecarfile = Path.ChangeExtension(fileinfo.FullName, $".{parser}");

            // Get the parser current version number
            string parserversion = parser switch
            {
                MediaInfo.ParserType.MediaInfo => MediaInfoTool.Version,
                MediaInfo.ParserType.MkvMerge => MkvTool.Version,
                MediaInfo.ParserType.FfProbe => FfMpegTool.Version,
                _ => throw new NotImplementedException()
            };

            // Create or read the sidecar file
            bool createsidecar = false;
            string sidecartext = "";
            for (;;)
            {
                // If forceupdate is set we always create a fresh sidecar
                if (forceupdate)
                {
                    createsidecar = true;
                    break;
                }

                // If the sidecar does not exist create it
                if (!File.Exists(sidecarfile))
                {
                    createsidecar = true;
                    break;
                }

                // Read the sidecar file
                ConsoleEx.WriteLine($"Reading media info from sidecar file : \"{sidecarfile}\"");
                sidecartext = File.ReadAllText(sidecarfile);

                // Extract the header from the sidecar text
                // If we fail to extract the header we recreate the sidecar file
                if (!ExtractHeader(out SidecarFileJsonSchema header, ref sidecartext))
                {
                    createsidecar = true;
                    break;
                }

                // Compare the tool version number
                if (!parserversion.Equals(header.ToolVersion, StringComparison.OrdinalIgnoreCase))
                {
                    createsidecar = true;
                    break;
                }

                // Compare the media modified time and file size
                fileinfo.Refresh();
                if (fileinfo.LastWriteTimeUtc != header.MediaLastWriteTimeUtc ||
                    fileinfo.Length != header.MediaLength)
                {
                    createsidecar = true;
                    break;
                }

                // Done
                break;
            }

            // Create the sidecar
            if (createsidecar)
            {
                // Use the specified stream parser tool to get the sidecar text
                switch (parser)
                {
                    case MediaInfo.ParserType.MediaInfo:
                        // Get the stream info from MediaInfo
                        if (!MediaInfoTool.GetMediaInfoXml(fileinfo.FullName, out sidecartext) ||
                            !MediaInfoTool.GetMediaInfoFromXml(sidecartext, out mediainfo))
                            return false;
                        break;
                    case MediaInfo.ParserType.MkvMerge:
                        // Get the stream info from MKVMerge
                        if (!MkvTool.GetMkvInfoJson(fileinfo.FullName, out sidecartext) ||
                            !MkvTool.GetMkvInfoFromJson(sidecartext, out mediainfo))
                            return false;
                        break;
                    case MediaInfo.ParserType.FfProbe:
                        // Get the stream info from FFprobe
                        if (!FfMpegTool.GetFfProbeInfoJson(fileinfo.FullName, out sidecartext) ||
                            !FfMpegTool.GetFfProbeInfoFromJson(sidecartext, out mediainfo))
                            return false;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(parser));
                }

                // Create the header
                fileinfo.Refresh();
                SidecarFileJsonSchema header = new SidecarFileJsonSchema
                {
                    // Save the tool version
                    ToolVersion = parserversion,

                    // Save the media file modified time and file size
                    MediaLastWriteTimeUtc = fileinfo.LastWriteTimeUtc,
                    MediaLength = fileinfo.Length
                };
                InjectHeader(header, ref sidecartext);

                // Write the text to the sidecar file
                ConsoleEx.WriteLine($"Writing media info to sidecar file : \"{sidecarfile}\"");
                File.WriteAllText(sidecarfile, sidecartext);

                // Done
            }
            else
            {
                // Use the specified stream parser tool to convert the text
                switch (parser)
                {
                    case MediaInfo.ParserType.MediaInfo:
                        // Convert the stream info using MediaInfo
                        if (!MediaInfoTool.GetMediaInfoFromXml(sidecartext, out mediainfo))
                            return false;
                        break;
                    case MediaInfo.ParserType.MkvMerge:
                        // Convert the stream info using MKVMerge
                        if (!MkvTool.GetMkvInfoFromJson(sidecartext, out mediainfo))
                            return false;
                        break;
                    case MediaInfo.ParserType.FfProbe:
                        // Convert the stream info using FFprobe
                        if (!FfMpegTool.GetFfProbeInfoFromJson(sidecartext, out mediainfo))
                            return false;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(parser));
                }
            }

            return true;
        }

        private static void InjectHeader(SidecarFileJsonSchema header, ref string sidecartext)
        {
            // TODO : An alternate method to storing headers in every file would be to use a dedicated sidecar file, plus a tool specific sidecar

            // Add the header to the front of the sidecar text
            StringBuilder sb = new StringBuilder();
            sb.Append(SidecarFileJsonSchema.ToJson(header));

            // Add a blank line
            sb.AppendLine();

            // Add rest of sidecar text
            sb.Append(sidecartext);

            // Return full text
            sidecartext = sb.ToString();
        }

        private static bool ExtractHeader(out SidecarFileJsonSchema header, ref string sidecartext)
        {
            header = null;

            try 
            { 
                // Create a string reader from the full text
                using StringReader stringReader = new StringReader(sidecartext);

                // Read the schema header in asingle line
                // The JSON stream reader is overly greedy and does not allow us to read JSON and text from the same reader
                // https://github.com/JamesNK/Newtonsoft.Json/issues/803
                string jsonLine = stringReader.ReadLine();
                header = SidecarFileJsonSchema.FromJson(jsonLine);

                // Read the remainder of text
                sidecartext = stringReader.ReadToEnd();
            }
            catch (Exception e)
            {
                ConsoleEx.WriteLineError(e);
                return false;
            }

            return header != null;
        }

        public static readonly HashSet<string> SidecarExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                $".{nameof(MediaInfo.ParserType.MediaInfo)}",
                $".{nameof(MediaInfo.ParserType.MkvMerge)}",
                $".{nameof(MediaInfo.ParserType.FfProbe)}"
            };
    }
}
