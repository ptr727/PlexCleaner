using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using InsaneGenius.Utilities;

namespace PlexCleaner
{
    internal class Process
    {
        public static ProcessOptions Options { get; set; } = new ProcessOptions();

        public Process()
        {
            // TODO : Add cleanup for extra empty entry when string is empty
            // extensionlist = extensionlist.Where(s => !String.IsNullOrWhiteSpace(s)).Distinct().ToList();

            // Sidecar extension, use the enum names
            SidecarExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                $".{MediaInfo.ParserType.MediaInfo.ToString()}",
                $".{MediaInfo.ParserType.MkvMerge.ToString()}",
                $".{MediaInfo.ParserType.FfProbe.ToString()}"
            };

            // Wanted extensions, always keep .mkv and sidecar files
            List<string> stringlist = Options.KeepExtensions.Split(',').ToList();
            KeepExtensions = new HashSet<string>(stringlist, StringComparer.OrdinalIgnoreCase)
            {
                ".mkv"
            };
            foreach (string extension in SidecarExtensions)
                KeepExtensions.Add(extension);

            // Containers types that can be remuxed to MKV
            stringlist = Options.ReMuxExtensions.Split(',').ToList();
            RemuxExtensions = new HashSet<string>(stringlist, StringComparer.OrdinalIgnoreCase);

            // Languages are in short form using ISO 639-2 notation
            // https://www.loc.gov/standards/iso639-2/php/code_list.php
            // zxx = no linguistic content, und = undetermined
            // Default language
            if (string.IsNullOrEmpty(Options.DefaultLanguage))
                Options.DefaultLanguage = "eng";

            // Languages to keep, always keep no linguistic content and the default language
            // The languages must be in ISO 639-2 form
            stringlist = Options.KeepLanguages.Split(',').ToList();
            KeepLanguages = new HashSet<string>(stringlist, StringComparer.OrdinalIgnoreCase)
            {
                "zxx",
                Options.DefaultLanguage
            };

            // Re-encode any video track that match the list
            // We use ffmpeg to re-encode, so we use ffprobe formats
            // All other formats will be encoded to h264
            List<string> codeclist = Options.ReEncodeVideoCodecs.Split(',').ToList();
            List<string> profilelist = Options.ReEncodeVideoProfiles.Split(',').ToList();
            ReencodeVideoCodecs = new List<VideoInfo>();
            for (int i = 0; i < codeclist.Count; i++)
            {
                // We match against the format and profile
                // Match the logic in VideoInfo.CompareVideo
                VideoInfo videoinfo = new VideoInfo
                {
                    Codec = "*",
                    Format = codeclist.ElementAt(i),
                    Profile = profilelist.ElementAt(i)
                };
                ReencodeVideoCodecs.Add(videoinfo);
            }

            // Re-encode any audio track that match the list
            // We use ffmpeg to re-encode, so we use ffprobe formats
            // All other formats will be encoded to the default codec, e.g. ac3
            stringlist = Options.ReEncodeAudioCodecs.Split(',').ToList();
            ReencodeAudioCodecs = new HashSet<string>(stringlist, StringComparer.OrdinalIgnoreCase);
        }

        public bool ProcessFiles(List<FileInfo> fileList)
        {
            ConsoleEx.WriteLine("");
            ConsoleEx.WriteLine("Processing files ...");
            ConsoleEx.WriteLine("");

            // Start the stopwatch
            Stopwatch timer = new Stopwatch();
            timer.Start();

            // Process all files
            int errorcount = 0;
            int modifiedcount = 0;
            foreach (FileInfo fileinfo in fileList)
            {
                // Process the file
                ConsoleEx.WriteLine($"Processing \"{fileinfo.FullName}\"");
                if (!ProcessFile(fileinfo, out bool modified))
                    errorcount++;
                else if (modified)
                    modifiedcount++;

                // Cancel handler
                if (Program.Cancel.State)
                    return false;

                // Next file
            }

            // Stop the timer
            timer.Stop();
            ConsoleEx.WriteLine("");
            ConsoleEx.WriteLine($"Total files : {fileList.Count}");
            ConsoleEx.WriteLine($"Modified files : {modifiedcount}");
            ConsoleEx.WriteLine($"Error files : {errorcount}");
            ConsoleEx.WriteLine($"Processing time : {timer.Elapsed}");
            ConsoleEx.WriteLine("");

            return true;
        }

        public bool ProcessFolders(List<string> folderList)
        {
            // Create the file and directory list
            // Process the files
            return FileEx.EnumerateDirectories(folderList, out List<FileInfo> fileList, out _) && ProcessFiles(fileList);
        }

        public static bool DeleteEmptyFolders(List<string> folderList)
        {
            if (!Options.DeleteEmptyFolders)
                return true;

            ConsoleEx.WriteLine("");
            ConsoleEx.WriteLine("Deleting empty folders ...");
            ConsoleEx.WriteLine("");

            // Delete all empty folders
            int deleted = 0;
            foreach (string folder in folderList)
            {
                ConsoleEx.WriteLine($"Looking for empty folders in \"{folder}\"");
                FileEx.DeleteEmptyDirectories(folder, ref deleted);
            }

            ConsoleEx.WriteLine("");
            ConsoleEx.WriteLine($"Deleted folders : {deleted}");
            ConsoleEx.WriteLine("");

            return true;
        }

        public bool ReMuxFiles(List<FileInfo> fileList)
        {
            ConsoleEx.WriteLine("");
            ConsoleEx.WriteLine("ReMuxing files ...");
            ConsoleEx.WriteLine("");

            // Start the stopwatch
            Stopwatch timer = new Stopwatch();
            timer.Start();

            // Process all files
            int errorcount = 0;
            int modifiedcount = 0;
            foreach (FileInfo fileinfo in fileList)
            {
                // Cancel handler
                if (Program.Cancel.State)
                    return false;

                // Handle only MKV files, and files in the remux extension list
                if (!Tools.IsMkvFile(fileinfo) &&
                    !RemuxExtensions.Contains(fileinfo.Extension))
                    continue;

                // ReMux file
                ConsoleEx.WriteLine($"ReMuxing \"{fileinfo.FullName}\"");
                if (!ReMuxFile(fileinfo, out bool modified))
                    errorcount++;
                else if (modified)
                    modifiedcount++;

                // Next file
            }

            // Stop the timer
            timer.Stop();
            ConsoleEx.WriteLine("");
            ConsoleEx.WriteLine($"Total files : {fileList.Count}");
            ConsoleEx.WriteLine($"Modified files : {modifiedcount}");
            ConsoleEx.WriteLine($"Error files : {errorcount}");
            ConsoleEx.WriteLine($"Processing time : {timer.Elapsed}");
            ConsoleEx.WriteLine("");

            return true;
        }

        public bool ReEncodeFiles(List<FileInfo> fileList)
        {
            ConsoleEx.WriteLine("");
            ConsoleEx.WriteLine("ReEncoding files ...");
            ConsoleEx.WriteLine("");

            // Start the stopwatch
            Stopwatch timer = new Stopwatch();
            timer.Start();

            // Process all files
            int errorcount = 0;
            int modifiedcount = 0;
            foreach (FileInfo fileinfo in fileList)
            {
                // Cancel handler
                if (Program.Cancel.State)
                    return false;

                // Handle only MKV files
                // ReMux before re-encode, so the track attribute logic works as expected
                if (!Tools.IsMkvFile(fileinfo))
                    continue;

                // Process the file
                ConsoleEx.WriteLine($"ReEncoding \"{fileinfo.FullName}\"");
                if (!ReEncodeFile(fileinfo, out bool modified))
                    errorcount++;
                else if (modified)
                    modifiedcount++;

                // Next file
            }

            // Stop the timer
            timer.Stop();
            ConsoleEx.WriteLine("");
            ConsoleEx.WriteLine($"Total files : {fileList.Count}");
            ConsoleEx.WriteLine($"Modified files : {modifiedcount}");
            ConsoleEx.WriteLine($"Error files : {errorcount}");
            ConsoleEx.WriteLine($"Processing time : {timer.Elapsed}");
            ConsoleEx.WriteLine("");

            return true;
        }

        public bool CreateTagMapFiles(List<FileInfo> fileList)
        {
            ConsoleEx.WriteLine("");
            ConsoleEx.WriteLine($"Creating tag map for {fileList.Count} files ...");
            ConsoleEx.WriteLine("");

            // Start the stopwatch
            Stopwatch timer = new Stopwatch();
            timer.Start();

            // We want to create a dictionary of ffprobe to mkvmerge and mediainfo tag strings
            // And how they map to each other for the same media file
            TagMapDictionary fftags = new TagMapDictionary();
            TagMapDictionary mktags = new TagMapDictionary();
            TagMapDictionary mitags = new TagMapDictionary();

            // Process all files
            int errorcount = 0;
            int modifiedcount = 0;
            foreach (FileInfo fileinfo in fileList)
            {
                // Cancel handler
                if (Program.Cancel.State)
                    return false;

                // Handle only MKV files
                if (!Tools.IsMkvFile(fileinfo))
                    continue;

                // Write all the different sidecar file types
                MediaInfo mi = null, mk = null, ff = null;
                foreach (MediaInfo.ParserType parser in Enum.GetValues(typeof(MediaInfo.ParserType)))
                { 
                    if (parser == MediaInfo.ParserType.None)
                        continue;

                    if (GetMediaInfoSidecar(fileinfo, false, parser, out bool modified, out MediaInfo info))
                    {
                        switch (parser)
                        {
                            case MediaInfo.ParserType.MediaInfo:
                                mi = info;
                                break;
                            case MediaInfo.ParserType.MkvMerge:
                                mk = info;
                                break;
                            case MediaInfo.ParserType.FfProbe:
                                ff = info;
                                break;
                            default:
                                throw new ArgumentOutOfRangeException(nameof(fileList));
                        }

                        if (modified)
                            modifiedcount++;
                    }
                    else
                        errorcount++;
                }

                // Compare to make sure we can map tracks between tools
                if (!MediaInfo.MatchTracks(mi, mk, ff))
                    continue;

                // Add all the tags
                fftags.Add(ff, mk, mi);
                mktags.Add(mk, ff, mi);
                mitags.Add(mi, ff, mk);

                // Next file
            }

            // Print the results
            ConsoleEx.WriteLine("");
            ConsoleEx.WriteLine("FFProbe:");
            fftags.WriteLine();
            ConsoleEx.WriteLine("");
            ConsoleEx.WriteLine("MKVMerge:");
            mktags.WriteLine();
            ConsoleEx.WriteLine("");
            ConsoleEx.WriteLine("MediaInfo:");
            mitags.WriteLine();
            ConsoleEx.WriteLine("");

            // Stop the timer
            timer.Stop();
            ConsoleEx.WriteLine("");
            ConsoleEx.WriteLine($"Total files : {fileList.Count}");
            ConsoleEx.WriteLine($"Modified files : {modifiedcount}");
            ConsoleEx.WriteLine($"Error files : {errorcount}");
            ConsoleEx.WriteLine($"Processing time : {timer.Elapsed}");
            ConsoleEx.WriteLine("");

            return true;
        }

        public bool WriteSidecarFiles(List<FileInfo> fileList)
        {
            ConsoleEx.WriteLine("");
            ConsoleEx.WriteLine("Writing sidecar files ...");
            ConsoleEx.WriteLine("");

            // Start the stopwatch
            Stopwatch timer = new Stopwatch();
            timer.Start();

            // Process all files
            int errorcount = 0;
            int modifiedcount = 0;
            foreach (FileInfo fileinfo in fileList)
            {
                // Cancel handler
                if (Program.Cancel.State)
                    return false;

                // Handle only MKV files
                if (!Tools.IsMkvFile(fileinfo))
                    continue;

                // Write all the different sidecar file types
                foreach (MediaInfo.ParserType parser in Enum.GetValues(typeof(MediaInfo.ParserType)))
                {
                    if (parser == MediaInfo.ParserType.None) 
                        continue;

                    if (!GetMediaInfoSidecar(fileinfo, true, parser, out bool modified, out MediaInfo _))
                        errorcount++;
                    else if (modified)
                        modifiedcount++;
                }

                // Next file
            }

            // Stop the timer
            timer.Stop();
            ConsoleEx.WriteLine("");
            ConsoleEx.WriteLine($"Total files : {fileList.Count}");
            ConsoleEx.WriteLine($"Modified files : {modifiedcount}");
            ConsoleEx.WriteLine($"Error files : {errorcount}");
            ConsoleEx.WriteLine($"Processing time : {timer.Elapsed}");
            ConsoleEx.WriteLine("");

            return true;
        }

        private bool ProcessFile(FileInfo fileinfo, out bool modified)
        {
            // Init
            modified = false;

            // Delete unwanted files, anything not in our extensions lists
            if (Options.DeleteUnwantedExtensions &&
                !KeepExtensions.Contains(fileinfo.Extension)  &&
                !RemuxExtensions.Contains(fileinfo.Extension))
            {
                // Delete the file
                ConsoleEx.WriteLine($"Deleting file with undesired extension : {fileinfo.Extension}");
                if (!FileEx.DeleteFile(fileinfo.FullName))
                    return false;

                // File deleted, do not continue processing
                modified = true;
                return true;
            }

            // Sidecar files must have a matching MKV media file
            if (SidecarExtensions.Contains(fileinfo.Extension))
            {
                // Get the matching MKV file
                string mediafile = Path.ChangeExtension(fileinfo.FullName, ".mkv");

                // If the media file does not exists, delete the sidecar file
                if (!File.Exists(mediafile))
                {
                    ConsoleEx.WriteLine("Deleting sidecar file with no matching MKV file");
                    if (!FileEx.DeleteFile(fileinfo.FullName))
                        return false;

                    // File deleted, do not continue processing
                    modified = true;
                    return true;
                }
            }

            // ReMux undesirable containers matched by extension
            if (Options.ReMux &&
                RemuxExtensions.Contains(fileinfo.Extension))
            {
                // ReMux the file
                ConsoleEx.WriteLine($"ReMux file matched by extension : {fileinfo.Extension}");
                if (!Convert.ReMuxToMkv(fileinfo.FullName, out string outputname))
                    return false;

                // Continue processing with the new file name
                modified = true;
                fileinfo = new FileInfo(outputname);
            }

            // By now all the media files we are processing should be MKV files
            if (!Tools.IsMkvFile(fileinfo))
                // Skip non-MKV files
                return true;

            // Get the file media info
            // Force a sidecar refresh only if the file had been modified, else read the existing sidecar values
            if (!GetMediaInfo(fileinfo, modified, out MediaInfo ffprobe, out MediaInfo mkvmerge, out MediaInfo mediainfo))
                return false;

            // Re-Encode interlaced content
            if (Options.DeInterlace &&
                mediainfo.IsVideoInterlaced())
            {
                ConsoleEx.WriteLine("Found interlaced video");

                // Convert using HandBrakeCLI, it produces the best de-interlacing results
                if (!Convert.DeInterlaceToMkv(fileinfo.FullName, out string outputname))
                    return false;

                // Continue processing with the new file name and new media info
                modified = true;
                fileinfo = new FileInfo(outputname);
                if (!GetMediaInfo(fileinfo, true, out ffprobe, out mkvmerge, out mediainfo))
                    return false;
            }

            // Re-Encode formats that cannot be direct-played, e.g. MPEG2, WMAPro
            // Logic uses FFProbe data
            if (Options.ReEncode &&
                ffprobe.FindNeedReEncode(ReencodeVideoCodecs, ReencodeAudioCodecs, out MediaInfo keep, out MediaInfo reencode))
            {
                ConsoleEx.WriteLine("Found tracks that need to be re-encoded:");
                keep.WriteLine("Passthrough");
                reencode.WriteLine("ReEncode");

                // Convert streams that need re-encoding, copy the rest of the streams as is
                if (!Convert.ConvertToMkv(fileinfo.FullName, keep, reencode, out string outputname))
                    return false;

                // Continue processing with the new file name and new media info
                modified = true;
                fileinfo = new FileInfo(outputname);
                if (!GetMediaInfo(fileinfo, true, out ffprobe, out mkvmerge, out mediainfo))
                    return false;
            }

            // Change all tracks with an unknown language to the default language
            // Logic uses MKVMerge data
            if (Options.SetUnknownLanguage &&
                mkvmerge.FindUnknownLanguage(out MediaInfo known, out MediaInfo unknown))
            {
                ConsoleEx.WriteLine($"Found tracks with an unknown language, setting to \"{Options.DefaultLanguage}\":");
                known.WriteLine("Known");
                unknown.WriteLine("Unknown");

                // Set the track language to the default language
                // MKVPropEdit uses track numbers, not track id's
                if (!Options.TestNoModify &&
                    unknown.GetTrackList().Any(info => !MediaInfo.SetMkvTrackLanguage(fileinfo.FullName, info.Number, Options.DefaultLanguage)))
                {
                    return false;
                }

                // Continue processing with new media info
                modified = true;
                fileinfo.Refresh();
                if (!GetMediaInfo(fileinfo, true, out ffprobe, out mkvmerge, out mediainfo))
                    return false;
            }

            // Filter out all the undesired tracks
            if (Options.RemoveUnwantedTracks &&
                mkvmerge.FindNeedRemove(KeepLanguages, out keep, out MediaInfo remove))
            {
                ConsoleEx.WriteLine("Found tracks that need to be removed:");
                keep.WriteLine("Keep");
                remove.WriteLine("Remove");

                // ReMux and only keep the specified tracks
                // MKVMerge uses track id's, not track numbers
                if (!Convert.ReMuxToMkv(fileinfo.FullName, keep, out string outputname))
                    return false;

                // Continue processing with new media info
                modified = true;
                fileinfo = new FileInfo(outputname);
                if (!GetMediaInfo(fileinfo, true, out ffprobe, out mkvmerge, out mediainfo))
                    return false;
            }

            // Do we need to remux any tracks
            if (Options.ReMux &&
                mediainfo.FindNeedReMux(out keep, out MediaInfo remux))
            {
                ConsoleEx.WriteLine("Found tracks that need to be re-muxed:");
                keep.WriteLine("Keep");
                remux.WriteLine("ReMux");

                // ReMux the file in-place, we ignore the track details
                if (!Convert.ReMuxToMkv(fileinfo.FullName, out string outputname))
                    return false;

                // Continue processing with new media info
                modified = true;
                fileinfo = new FileInfo(outputname);
                if (!GetMediaInfo(fileinfo, true, out ffprobe, out mkvmerge, out mediainfo))
                    return false;
            }

            // TODO : Verify the integrity of the media file and the streams in the file

            // Stream check, at least one video and audio track
            // There can be multiple video tracks, where one track is a V_MJPEG embedded image
            if (mediainfo.Video.Count == 0 || mediainfo.Audio.Count == 0)
            {
                // File is missing required streams
                ConsoleEx.WriteLineError($"File missing required tracks : Video Count : {mediainfo.Video.Count} : Audio Count {mediainfo.Audio.Count} : Subtitle Count {mediainfo.Subtitle.Count}");

                // Delete the file
                if (Options.DeleteFailedFiles)
                    FileEx.DeleteFile(fileinfo.FullName);

                return false;
            }

            // Done
            return true;
        }

        private static bool ReMuxFile(FileInfo fileinfo, out bool modified)
        {
            // Init
            modified = false;

            // ReMux the file
            if (!Convert.ReMuxToMkv(fileinfo.FullName, out string _))
                return false;

            // Modified
            modified = true;
            return true;
        }

        private static bool ReEncodeFile(FileInfo fileinfo, out bool modified)
        {
            // Init
            modified = false;

            // ReEncode the file
            if (!Convert.ConvertToMkv(fileinfo.FullName, out string _))
                return false;

            // Modified
            modified = true;
            return true;
        }

        private static bool GetMediaInfoSidecar(FileInfo fileinfo, bool update, MediaInfo.ParserType parser, out bool modified, out MediaInfo mediainfo)
        {
            // Init
            modified = false;
            mediainfo = null;

            // MKV tools only work on MKV files
            if (parser == MediaInfo.ParserType.MkvMerge &&
                !Tools.IsMkvFile(fileinfo))
            {
                return true;
            }

            // TODO : Chance of race condition between reading media, external write to same media, and writing sidecar
            // TODO : On non-NTFS filesystems the timestamp granularity is insufficient to use as a reliable method of change detection

            // Create or read the sidecar file
            bool createsidecar = false;
            string sidecarfile = Path.ChangeExtension(fileinfo.FullName, $".{parser.ToString()}");
            if (File.Exists(sidecarfile))
            {
                // We are explicitly setting the sidecar modified time to match the media modified time
                // We look for changes to the media file by comparing the modified times
                FileInfo sidecarinfo = new FileInfo(sidecarfile);
                fileinfo = new FileInfo(fileinfo.FullName);
                if (fileinfo.LastWriteTimeUtc != sidecarinfo.LastWriteTimeUtc)
                    createsidecar = true;
            }
            else
                // Create the file
                createsidecar = true;

            // If update is set we always create a fresh sidecar
            if (update)
                createsidecar = true;

            // Create the sidecar
            string sidecartext;
            if (createsidecar)
            {
                // Use the specified stream parser tool
                switch (parser)
                {
                    case MediaInfo.ParserType.MediaInfo:
                        // Get the stream info from MediaInfo
                        if (!MediaInfo.GetMediaInfoXml(fileinfo.FullName, out sidecartext) ||
                            !MediaInfo.GetMediaInfoFromXml(sidecartext, out mediainfo))
                            return false;
                        break;
                    case MediaInfo.ParserType.MkvMerge:
                        // Get the stream info from MKVMerge
                        if (!MediaInfo.GetMkvInfoJson(fileinfo.FullName, out sidecartext) ||
                            !MediaInfo.GetMkvInfoFromJson(sidecartext, out mediainfo))
                            return false;
                        break;
                    case MediaInfo.ParserType.FfProbe:
                        // Get the stream info from FFProbe
                        if (!MediaInfo.GetFfProbeInfoJson(fileinfo.FullName, out sidecartext) ||
                            !MediaInfo.GetFfProbeInfoFromJson(sidecartext, out mediainfo))
                            return false;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(parser), parser, null);
                }

                try
                {
                    // Write the text to the sidecar file
                    ConsoleEx.WriteLine($"Writing stream info to sidecar file : \"{sidecarfile}\"");
                    File.WriteAllText(sidecarfile, sidecartext);

                    // Set the sidecar modified time to match the media modified time
                    File.SetLastWriteTimeUtc(sidecarfile, fileinfo.LastWriteTimeUtc);
                }
                catch (Exception e)
                {
                    ConsoleEx.WriteLineError(e);
                    return false;
                }
            }
            else
            {
                // Read the sidecar file
                try
                {
                    ConsoleEx.WriteLine($"Reading stream info from sidecar file : \"{sidecarfile}\"");
                    sidecartext = File.ReadAllText(sidecarfile);
                }
                catch (Exception e)
                {
                    ConsoleEx.WriteLineError(e);
                    return false;
                }

                // Use the specified stream parser tool to convert the text
                switch (parser)
                {
                    case MediaInfo.ParserType.MediaInfo:
                        // Convert the stream info using MediaInfo
                        if (!MediaInfo.GetMediaInfoFromXml(sidecartext, out mediainfo))
                            return false;
                        break;
                    case MediaInfo.ParserType.MkvMerge:
                        // Convert the stream info using MKVMerge
                        if (!MediaInfo.GetMkvInfoFromJson(sidecartext, out mediainfo))
                            return false;
                        break;
                    case MediaInfo.ParserType.FfProbe:
                        // Convert the stream info using FFProbe
                        if (!MediaInfo.GetFfProbeInfoFromJson(sidecartext, out mediainfo))
                            return false;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(parser), parser, null);
                }
            }

            return true;
        }

/*
        bool GetMediaInfo(string filename, out MediaInfo ffprobe, out MediaInfo mkvmerge, out MediaInfo mediainfo)
        {
            FileInfo fileinfo = new FileInfo(filename);
            return GetMediaInfo(fileinfo, out ffprobe, out mkvmerge, out mediainfo);
        }
*/

        private static bool GetMediaInfo(FileInfo fileinfo, bool update, out MediaInfo ffprobe, out MediaInfo mkvmerge, out MediaInfo mediainfo)
        {
            ffprobe = null;
            mkvmerge = null;
            mediainfo = null;

            return GetMediaInfo(fileinfo, update, MediaInfo.ParserType.FfProbe, out ffprobe) && 
                   GetMediaInfo(fileinfo, update, MediaInfo.ParserType.MkvMerge, out mkvmerge) && 
                   GetMediaInfo(fileinfo, update, MediaInfo.ParserType.MediaInfo, out mediainfo);
        }

/*
        bool GetMediaInfo(string filename, MediaInfo.ParserType parser, out MediaInfo mediainfo)
        {
            FileInfo fileinfo = new FileInfo(filename);
            return GetMediaInfo(fileinfo, parser, out mediainfo);
        }
*/

        private static bool GetMediaInfo(FileInfo fileinfo, bool update, MediaInfo.ParserType parser, out MediaInfo mediainfo)
        {
            // Read or create sidecar file
            mediainfo = null;
            if (Options.UseSidecarFiles)
                return GetMediaInfoSidecar(fileinfo, update, parser, out bool _, out mediainfo);
            
            // Use the specified stream parser tool
            return parser switch
            {
                MediaInfo.ParserType.MediaInfo => MediaInfo.GetMediaInfo(fileinfo.FullName, out mediainfo),
                MediaInfo.ParserType.MkvMerge => MediaInfo.GetMkvInfo(fileinfo.FullName, out mediainfo),
                MediaInfo.ParserType.FfProbe => MediaInfo.GetFfProbeInfo(fileinfo.FullName, out mediainfo),
                _ => throw new ArgumentOutOfRangeException(nameof(parser))
            };
        }

        private readonly HashSet<string> SidecarExtensions;
        private readonly HashSet<string> KeepExtensions;
        private readonly HashSet<string> RemuxExtensions;
        private readonly HashSet<string> ReencodeAudioCodecs;
        private readonly HashSet<string> KeepLanguages;
        private readonly List<VideoInfo> ReencodeVideoCodecs;
    }
}
