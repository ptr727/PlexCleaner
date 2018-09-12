using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Utilities;
using Settings = PlexCleaner.Properties.Settings;

namespace PlexCleaner
{
    internal static class Convert
    {
        // ReEncode to MKV H264
        public static bool ConvertToMkv(string inputname, out string outputname)
        {
            // Convert all tracks
            return ConvertToMkv(inputname, 20, "ac3", null, null, out outputname);
        }

        // ReEncode to MKV H264
        // Re-encode only the specified tracks, and copy the passthrough tracks
        public static bool ConvertToMkv(string inputname, int quality, string audiocodec, Info.MediaInfo keep, Info.MediaInfo reencode, out string outputname)
        {
            if (inputname == null) throw new ArgumentNullException(nameof(inputname));

            // Match the logic in ReMuxToMKV()

            // Test
            if (Settings.Default.TestNoModify)
            {
                outputname = inputname;
                return true;
            }

            // Create a temp filename based on the input name
            outputname = Path.ChangeExtension(inputname, ".mkv");
            string tempname = Path.ChangeExtension(inputname, ".tmp");

            // Convert using ffmpeg
            if (!ConvertToMkvFfMpeg(inputname, quality, audiocodec, keep, reencode, tempname))
            {
                FileEx.DeleteFile(tempname);
                return false;
            }

            // Rename the temp file to the output file
            if (!FileEx.RenameFile(tempname, outputname))
                return false;

            // If the input and output names are not the same, delete the input
            return inputname.Equals(outputname, StringComparison.OrdinalIgnoreCase) || FileEx.DeleteFile(inputname);
        }

        // ReMux the file to MKV
        public static bool ReMuxToMkv(string inputname, out string outputname)
        {
            // Remux all tracks
            return ReMuxToMkv(inputname, null, out outputname);
        }

        // ReMux the file to MKV
        // Include only the tracks from the filter
        public static bool ReMuxToMkv(string inputname, Info.MediaInfo keep, out string outputname)
        {
            if (inputname == null) throw new ArgumentNullException(nameof(inputname));

            // Match the logic in ConvertToMKV()

            // Test
            if (Settings.Default.TestNoModify)
            {
                outputname = inputname;
                return true;
            }

            // Create a temp filename based on the input name
            outputname = Path.ChangeExtension(inputname, ".mkv");
            string tempname = Path.ChangeExtension(inputname, ".tmp");

            // MKVToolNix and FFMpeg both have problems dealing some with AVI files, so we will try both
            // MKVToolNix does not support WMV or ASF files, or maybe just the WMAPro codec
            // E.g. https://github.com/FFmpeg/FFmpeg/commit/8de1ee9f725aa3c550f425bd3120bcd95d5b2ea8
            // E.g. https://github.com/mbunkus/mkvtoolnix/issues/2123
            bool result;
            if (Tools.IsMkvFile(inputname))
            {
                // MKV files always try MKVMerge first
                result = RemuxToMkvMkvToolNix(inputname, keep, tempname);
                if (!result)
                    // Retry using FFMpeg
                    result = RemuxToMkvFfMpeg(inputname, keep, tempname);
            }
            else
            {
                // Non-MKV files always try FFMpeg first
                result = RemuxToMkvFfMpeg(inputname, keep, tempname);
                if (!result)
                    // Retry using MKVMerge
                    result = RemuxToMkvMkvToolNix(inputname, keep, tempname);
            }
            if (!result)
            {
                FileEx.DeleteFile(tempname);
                return false;
            }

            // Rename the temp file to the output file
            if (!FileEx.RenameFile(tempname, outputname))
                return false;

            // If the input and output names are not the same, delete the input
            return inputname.Equals(outputname, StringComparison.OrdinalIgnoreCase) || FileEx.DeleteFile(inputname);
        }

        // ReMux the file to MKV using mkvmerge
        // Include only tracks with an unknown language or the specified language
/*
        private static bool RemuxToMkvMkvToolNix(string inputname, string language, string outputname)
        {
            if (String.IsNullOrEmpty(language))
                return RemuxToMkvMkvToolNix(inputname, outputname);

            // Delete output file
            Tools.DeleteFile(outputname);

            // Cut part of file
            // --split parts:00:00:30-00:01:00

            // Create the MKVMerge commandline and execute
            // https://en.wikipedia.org/wiki/List_of_ISO_639-2_codes
            // https://mkvtoolnix.download/doc/mkvmerge.html
            string commandline = $"--default-language {language} --output \"{outputname}\" --audio-tracks {language} --subtitle-tracks {language} \"{inputname}\"";
            Tools.WriteLine("");
            int exitcode = MkvTool.MkvMerge(commandline);
            Tools.WriteLine("");
            if (exitcode != 0 && exitcode != 1)
                return false;
            return true;
        }
*/

        // ReMux the file to MKV using mkvmerge
        // Include only the tracks from the filter
        private static bool RemuxToMkvMkvToolNix(string inputname, Info.MediaInfo keep, string outputname)
        {
            if (keep == null)
                return RemuxToMkvMkvToolNix(inputname, outputname);

            // Delete output file
            FileEx.DeleteFile(outputname);

            // Create the track number filters
            // The track numbers are reported by MKVMerge --identify, use the track.id values
            string videotracks = keep.Video.Count > 0 ? $"--video-tracks {String.Join(",", keep.Video.Select(info => info.Id.ToString()))} " : "--no-video ";
            string audiotracks = keep.Audio.Count > 0 ? $"--audio-tracks {String.Join(",", keep.Audio.Select(info => info.Id.ToString()))} " : "--no-audio ";
            string subtitletracks = keep.Subtitle.Count > 0 ? $"--subtitle-tracks {String.Join(",", keep.Subtitle.Select(info => info.Id.ToString()))} " : "--no-subtitles ";

            // Create the MKVMerge commandline and execute
            // https://mkvtoolnix.download/doc/mkvmerge.html
            string snippets = Settings.Default.TestSnippets ? MkvMergeSnippet : "";
            string commandline = $"{snippets} --output \"{outputname}\" {videotracks}{audiotracks}{subtitletracks} \"{inputname}\"";
            ConsoleEx.WriteLine("");
            int exitcode = MkvTool.MkvMerge(commandline);
            ConsoleEx.WriteLine("");
            return exitcode == 0 || exitcode == 1;
        }

        // ReMux the file to MKV using mkvmerge
        private static bool RemuxToMkvMkvToolNix(string inputname, string outputname)
        {
            // Delete output file
            FileEx.DeleteFile(outputname);

            // Create the MKVMerge commandline and execute
            // https://mkvtoolnix.download/doc/mkvmerge.html
            string snippets = Settings.Default.TestSnippets ? MkvMergeSnippet : "";
            string commandline = $"{snippets} --output \"{outputname}\" \"{inputname}\"";
            ConsoleEx.WriteLine("");
            int exitcode = MkvTool.MkvMerge(commandline);
            ConsoleEx.WriteLine("");
            return exitcode == 0 || exitcode == 1;
        }


        // ReMux the file to MKV using mkvmerge
        // Include only the tracks from the filter
        private static bool RemuxToMkvFfMpeg(string inputname, Info.MediaInfo keep, string outputname)
        {
            if (keep == null)
                return RemuxToMkvFfMpeg(inputname, outputname);

            // Delete output file
            FileEx.DeleteFile(outputname);

            // Create an input and output map
            CreateFfMpegMap(keep, out string input, out string output);

            // Create the FFmpeg commandline and execute
            // https://ffmpeg.org/ffmpeg.html
            // https://trac.ffmpeg.org/wiki/Map
            // https://ffmpeg.org/ffmpeg.html#Stream-copy
            string snippets = Settings.Default.TestSnippets ? FfMpegSnippet : "";
            string commandline = $"-i \"{inputname}\" {snippets} {input} {output} -f matroska \"{outputname}\"";
            ConsoleEx.WriteLine("");
            int exitcode = FfMpegTool.FfMpeg(commandline);
            ConsoleEx.WriteLine("");
            return exitcode == 0;
        }

        // ReMux the file to MKV using ffmpeg
        private static bool RemuxToMkvFfMpeg(string inputname, string outputname)
        {
            // Delete output file
            FileEx.DeleteFile(outputname);

            // Create the FFmpeg commandline and execute
            // Copy all streams
            // https://ffmpeg.org/ffmpeg.html
            // https://trac.ffmpeg.org/wiki/Map
            // https://ffmpeg.org/ffmpeg.html#Stream-copy
            string snippets = Settings.Default.TestSnippets ? FfMpegSnippet : "";
            string commandline = $"-i \"{inputname}\" {snippets} -map 0 -codec copy -f matroska \"{outputname}\"";
            ConsoleEx.WriteLine("");
            int exitcode = FfMpegTool.FfMpeg(commandline);
            ConsoleEx.WriteLine("");
            return exitcode == 0;
        }

        // ReEncode to MKV H264
        // Video is always re-encoded, audio is copied if supported, else AC3
        /*
                private static bool ConvertToMkvHandbrake(string inputname, int quality, string outputname)
                {
                    // Delete output file
                    Tools.DeleteFile(outputname);

                    // Create the HandbrakeCLI commandline and execute
                    // https://handbrake.fr/docs/en/latest/cli/cli-guide.html
                    string commandline = $"--input \"{inputname}\" --output \"{outputname}\" --format av_mkv --encoder x264 " +
                                         $"--encoder-preset medium --quality {quality} --subtitle 1,2,3,4 --audio 1,2,3,4 " +
                                         $"--aencoder copy --audio-fallback ac3";
                    Tools.WriteLine("");
                    int exitcode = Tools.Handbrake(commandline);
                    Tools.WriteLine("");
                    if (exitcode != 0)
                        return false;
                    return true;
                }
        */

        private static void CreateFfMpegMap(Info.MediaInfo keep, out string input, out string output)
        {
            Info.MediaInfo reencode = new Info.MediaInfo();
            CreateFfMpegMap(0, "", keep, reencode, out input, out output);
        }

        private static void CreateFfMpegMap(int quality, string audiocodec, Info.MediaInfo keep, Info.MediaInfo reencode, out string input, out string output)
        {
            // Create an input and output map
            // http://ffmpeg.org/ffmpeg.html#Advanced-options
            // http://ffmpeg.org/ffmpeg.html#Stream-specifiers
            // https://trac.ffmpeg.org/wiki/Map

            // Order by video, audio, and subtitle
            // Each ordered by id, to keep the original order
            List<Info.TrackInfo> videolist = new List<Info.TrackInfo>();
            videolist.AddRange(keep.Video);
            videolist.AddRange(reencode.Video);
            videolist = videolist.OrderBy(item => item.Id).ToList();
            List<Info.TrackInfo> audiolist = new List<Info.TrackInfo>();
            audiolist.AddRange(keep.Audio);
            videolist.AddRange(reencode.Audio);
            audiolist = audiolist.OrderBy(item => item.Id).ToList();
            List<Info.TrackInfo> subtitlelist = new List<Info.TrackInfo>();
            subtitlelist.AddRange(keep.Subtitle);
            videolist.AddRange(reencode.Subtitle);
            subtitlelist = subtitlelist.OrderBy(item => item.Id).ToList();

            // Create a map list of all the input streams we want in the output
            List<Info.TrackInfo> tracklist = new List<Info.TrackInfo>();
            tracklist.AddRange(videolist);
            tracklist.AddRange(audiolist);
            tracklist.AddRange(subtitlelist);
            StringBuilder sb = new StringBuilder();
            foreach (Info.TrackInfo info in tracklist)
                sb.Append($"-map 0:{info.Id} ");
            input = sb.ToString();
            input = input.Trim();

            // Set the output stream types for each input map item
            // The order has to match the input order
            sb.Clear();
            int video = 0;
            int audio = 0;
            int subtitle = 0;
            foreach (Info.TrackInfo info in tracklist)
            {
                // Copy or encode
                if (info.GetType() == typeof(Info.VideoInfo))
                    sb.Append(info.State == Info.TrackInfo.StateType.Keep
                        ? $"-c:v:{video++} copy "
                        : $"-c:v:{video++} libx264 -crf {quality} -preset medium ");
                else if (info.GetType() == typeof(Info.AudioInfo))
                    sb.Append(info.State == Info.TrackInfo.StateType.Keep
                        ? $"-c:a:{audio++} copy "
                        : $"-c:a:{audio++} {audiocodec} ");
                else if (info.GetType() == typeof(Info.SubtitleInfo))
                    // No re-encoding of subtitles, just copy
                    sb.Append($"-c:s:{subtitle++} copy ");
            }
            output = sb.ToString();
            output = output.Trim();
        }

        // ReEncode to MKV H264
        // Re-encode only the specified tracks, and copy the passthrough tracks
        private static bool ConvertToMkvFfMpeg(string inputname, int quality, string audiocodec, Info.MediaInfo keep, Info.MediaInfo reencode, string outputname)
        {
            if (keep == null || reencode == null)
                return ConvertToMkvFfMpeg(inputname, quality, outputname);

            // Delete output file
            FileEx.DeleteFile(outputname);

            // Create an input and output map
            CreateFfMpegMap(quality, audiocodec, keep, reencode, out string input, out string output);

            // TODO : Error with some PGS subtitles
            // https://trac.ffmpeg.org/ticket/2622
            //  [matroska,webm @ 000001d77fb61ca0] Could not find codec parameters for stream 2 (Subtitle: hdmv_pgs_subtitle): unspecified size
            //  Consider increasing the value for the 'analyzeduration' and 'probesize' options

            // Create the FFmpeg commandline and execute
            // https://trac.ffmpeg.org/wiki/Encode/H.264
            string snippets = Settings.Default.TestSnippets ? FfMpegSnippet : "";
            string commandline = $"-v warning -i \"{inputname}\" {snippets} {input} {output} -f matroska \"{outputname}\"";
            ConsoleEx.WriteLine("");
            int exitcode = FfMpegTool.FfMpeg(commandline);
            ConsoleEx.WriteLine("");
            return exitcode == 0;
        }

        // ReEncode to MKV H264
        // Video is always re-encoded, audio is copied
        private static bool ConvertToMkvFfMpeg(string inputname, int quality, string outputname)
        {
            // Delete output file
            FileEx.DeleteFile(outputname);

            // Create the FFmpeg commandline and execute
            // Copy all streams
            // https://trac.ffmpeg.org/wiki/Encode/H.264
            string snippets = Settings.Default.TestSnippets ? FfMpegSnippet : "";
            string commandline = $"-v warning -i \"{inputname}\" {snippets} -map 0 -c:v libx264 -crf {quality} -preset medium -c:a copy -f matroska \"{outputname}\"";
            ConsoleEx.WriteLine("");
            int exitcode = FfMpegTool.FfMpeg(commandline);
            ConsoleEx.WriteLine("");
            return exitcode == 0;
        }

        // File split options
        private const string FfMpegSnippet = "-ss 0 -t 60";
        private const string MkvMergeSnippet = "--split parts:00:00:00-00:01:00";
    }
}
