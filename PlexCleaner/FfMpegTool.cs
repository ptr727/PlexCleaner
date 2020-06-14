using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using InsaneGenius.Utilities;
using System.Text;
using System.Diagnostics;
using System.Globalization;
using PlexCleaner.FfMpegToolJsonSchema;
using System.IO;
using Newtonsoft.Json;
using System.IO.Compression;

// TODO : FFmpeg de-interlacing
// https://www.reddit.com/r/ffmpeg/comments/d3cwxp/what_is_the_difference_between_bwdif_and_yadif1/
// https://video.stackexchange.com/questions/14874/faster-deinterlacing-with-ffmpeg-and-yadif
// https://askubuntu.com/questions/866186/how-to-get-good-quality-when-converting-digital-video
// http://avisynth.nl/index.php/QTGMC
// https://forum.videohelp.com/threads/392565-How-to-get-ffmpeg-deinterlace-to-work-like-handbrake-decomb
// https://offset.skew.org/wiki/User:Mjb/FFmpeg#Basic_deinterlace

// TODO : Hardware acceleration
// https://trac.ffmpeg.org/wiki/HWAccelIntro

// TODO : Add x265 support
// https://trac.ffmpeg.org/wiki/Encode/H.265

namespace PlexCleaner
{
    public static class FfMpegTool
    {
        // Tool version, read from Tools.json
        public static string Version { get; set; } = "";

        public static int FfMpegCli(string parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));
            parameters = parameters.Trim();

            ConsoleEx.WriteLine("");
            ConsoleEx.WriteLineTool($"FFmpeg : {parameters}");
            string path = Tools.CombineToolPath(ToolsOptions.FfMpeg, FfMpegBinary);
            return ProcessEx.Execute(path, parameters);
        }

        public static int FfMpegCli(string parameters, out string output, out string error)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));
            parameters = parameters.Trim();

            ConsoleEx.WriteLine("");
            ConsoleEx.WriteLineTool($"FFmpeg : {parameters}");
            string path = Tools.CombineToolPath(ToolsOptions.FfMpeg, FfMpegBinary);
            return ProcessEx.Execute(path, parameters, out output, out error);
        }

        public static int FfProbeCli(string parameters, out string output, out string error)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));
            parameters = parameters.Trim();

            ConsoleEx.WriteLine("");
            ConsoleEx.WriteLineTool($"FFprobe : {parameters}");
            string path = Tools.CombineToolPath(ToolsOptions.FfMpeg, FfProbeBinary);
            return ProcessEx.Execute(path, parameters, out output, out error);
        }

        public static string GetToolFolder()
        {
            return Tools.CombineToolPath(ToolsOptions.FfMpeg);
        }

        public static string GetToolPath()
        {
            return Tools.CombineToolPath(ToolsOptions.FfMpeg, FfMpegBinary);
        }

        public static bool GetLatestVersion(ToolInfo toolinfo)
        {
            if (toolinfo == null)
                throw new ArgumentNullException(nameof(toolinfo));

            toolinfo.Tool = nameof(FfMpegTool);

            try
            {
                // Load the download page
                // TODO : Find a more reliable way of getting the last released version number
                // https://www.ffmpeg.org/download.html
                // https://ffmpeg.zeranoe.com/builds/
                HtmlWeb web = new HtmlWeb();
                HtmlDocument doc = web.Load(new Uri(@"https://www.ffmpeg.org/download.html"));

                // Get the download element and the download button
                HtmlNode download = doc.GetElementbyId("download");
                HtmlNodeCollection divs = download.SelectNodes("//div[contains(@class, 'btn-download-wrapper')]");
                if (divs.Count != 1) throw new ArgumentException($"Expecting only one node : {divs.Count}");

                // Get the current version URL from the first href
                HtmlNodeCollection anchors = divs.First().SelectNodes("a");
                if (anchors.Count != 2) throw new ArgumentException($"Expecting two nodes : {anchors.Count}");
                HtmlAttribute attr = anchors.First().Attributes["href"];
                string sourceurl = attr.Value;

                // Extract the version number from the URL
                // E.g. https://ffmpeg.org/releases/ffmpeg-3.4.tar.bz2
                const string pattern = @"ffmpeg\.org\/releases\/ffmpeg-(?<version>.*?)\.tar\.bz2";
                Regex regex = new Regex(pattern);
                Match match = regex.Match(sourceurl);
                Debug.Assert(match.Success);
                toolinfo.Version = match.Groups["version"].Value;

                // Create download URL and the output filename using the version number
                // E.g. https://ffmpeg.zeranoe.com/builds/win64/static/ffmpeg-3.4-win64-static.zip
                toolinfo.FileName = $"ffmpeg-{toolinfo.Version}-win64-static.zip";
                toolinfo.Url = $"https://ffmpeg.zeranoe.com/builds/win64/static/{toolinfo.FileName}";
            }
            catch (Exception e)
            {
                ConsoleEx.WriteLine("");
                ConsoleEx.WriteLineError(e);
                return false;
            }
            return true;
        }

        public static bool VerifyMedia(string filename, out string error)
        {
            // https://trac.ffmpeg.org/ticket/6375
            // Too many packets buffered for output stream 0:1

            // Create the FFmpeg commandline and execute
            // https://ffmpeg.org/ffmpeg.html
            string snippet = Program.Config.VerifyOptions.VerifyDuration == 0 ? "" : $"-t 0 -ss {Program.Config.VerifyOptions.VerifyDuration}";
            string commandline = $"-i \"{filename}\" -max_muxing_queue_size 512 -nostats -loglevel error -xerror {snippet} -f null -";
            int exitcode = FfMpegCli(commandline, out string _, out error);
            return exitcode == 0 && error.Length == 0;
        }

        public static bool GetFfProbeInfo(string filename, out MediaInfo mediainfo)
        {
            mediainfo = null;
            return GetFfProbeInfoJson(filename, out string json) && 
                   GetFfProbeInfoFromJson(json, out mediainfo);
        }

        public static bool GetFfProbeInfoJson(string filename, out string json)
        {
            // Create the FFprobe commandline and execute
            // https://ffmpeg.org/ffprobe.html
            string commandline = $"-loglevel quiet -show_streams -print_format json \"{filename}\"";
            int exitcode = FfProbeCli(commandline, out json, out string error);
            return exitcode == 0 && error.Length == 0;
        }

        public static bool GetFfProbeInfoFromJson(string json, out MediaInfo mediainfo)
        {
            // Parser type is FfProbe
            mediainfo = new MediaInfo(MediaInfo.ParserType.FfProbe);

            // Populate the MediaInfo object from the JSON string
            try
            {
                FfMpegToolJsonSchema.FfProbe ffprobe = FfMpegToolJsonSchema.FfProbe.FromJson(json);
                if (ffprobe.Streams.Count == 0)
                {
                    // No tracks
                    return false;
                }

                foreach (FfMpegToolJsonSchema.Stream stream in ffprobe.Streams)
                {
                    if (stream.CodecType.Equals("video", StringComparison.OrdinalIgnoreCase))
                    {
                        // We need to exclude cover art
                        if (stream.CodecName.Equals("mjpeg", StringComparison.OrdinalIgnoreCase) ||
                            stream.CodecName.Equals("png", StringComparison.OrdinalIgnoreCase))
                            continue;

                        VideoInfo info = new VideoInfo(stream);
                        mediainfo.Video.Add(info);
                    }
                    else if (stream.CodecType.Equals("audio", StringComparison.OrdinalIgnoreCase))
                    {
                        AudioInfo info = new AudioInfo(stream);
                        mediainfo.Audio.Add(info);
                    }
                    else if (stream.CodecType.Equals("subtitle", StringComparison.OrdinalIgnoreCase))
                    {
                        SubtitleInfo info = new SubtitleInfo(stream);
                        mediainfo.Subtitle.Add(info);
                    }
                }

                // Errors
                mediainfo.HasErrors = mediainfo.Video.Any(item => item.HasErrors) || mediainfo.Audio.Any(item => item.HasErrors) || mediainfo.Subtitle.Any(item => item.HasErrors);

                // TODO : Tags
                // TODO : Duration
            }
            catch (Exception e)
            {
                ConsoleEx.WriteLine("");
                ConsoleEx.WriteLineError(e);
                return false;
            }

            return true;
        }

        public static bool ReMuxToMkv(string inputname, MediaInfo keep, string outputname)
        {
            if (keep == null)
                return ReMuxToMkv(inputname, outputname);

            // Delete output file
            FileEx.DeleteFile(outputname);

            // Create an input and output map
            CreateFfMpegMap(keep, out string input, out string output);

            // Create the FFmpeg commandline and execute
            // https://ffmpeg.org/ffmpeg.html
            // https://trac.ffmpeg.org/wiki/Map
            // https://ffmpeg.org/ffmpeg.html#Stream-copy
            string snippet = Program.Options.TestSnippets ? FfmpegSnippet : "";
            string commandline = $"-i \"{inputname}\" {snippet} {input} {output} -f matroska \"{outputname}\"";
            int exitcode = FfMpegCli(commandline);
            return exitcode == 0;
        }

        public static bool ReMuxToMkv(string inputname, string outputname)
        {
            // Delete output file
            FileEx.DeleteFile(outputname);

            // Create the FFmpeg commandline and execute
            // Copy all streams
            // https://ffmpeg.org/ffmpeg.html
            // https://trac.ffmpeg.org/wiki/Map
            // https://ffmpeg.org/ffmpeg.html#Stream-copy
            string snippet = Program.Options.TestSnippets ? FfmpegSnippet : "";
            string commandline = $"-i \"{inputname}\" {snippet} -map 0 -codec copy -f matroska \"{outputname}\"";
            int exitcode = FfMpegCli(commandline);
            return exitcode == 0;
        }

        private static void CreateFfMpegMap(MediaInfo keep, out string input, out string output)
        {
            MediaInfo reencode = new MediaInfo(MediaInfo.ParserType.FfProbe);
            CreateFfMpegMap(0, "", keep, reencode, out input, out output);
        }

        private static void CreateFfMpegMap(int quality, string audiocodec, MediaInfo keep, MediaInfo reencode, out string input, out string output)
        {
            // Verify correct data type
            Debug.Assert(keep.Parser == MediaInfo.ParserType.FfProbe);
            Debug.Assert(reencode.Parser == MediaInfo.ParserType.FfProbe);


            // Create an input and output map
            // http://ffmpeg.org/ffmpeg.html#Advanced-options
            // http://ffmpeg.org/ffmpeg.html#Stream-specifiers
            // https://trac.ffmpeg.org/wiki/Map

            // Order by video, audio, and subtitle
            // Each ordered by id, to keep the original order
            List<TrackInfo> videolist = new List<TrackInfo>();
            videolist.AddRange(keep.Video);
            videolist.AddRange(reencode.Video);
            videolist = videolist.OrderBy(item => item.Id).ToList();
            List<TrackInfo> audiolist = new List<TrackInfo>();
            audiolist.AddRange(keep.Audio);
            videolist.AddRange(reencode.Audio);
            audiolist = audiolist.OrderBy(item => item.Id).ToList();
            List<TrackInfo> subtitlelist = new List<TrackInfo>();
            subtitlelist.AddRange(keep.Subtitle);
            videolist.AddRange(reencode.Subtitle);
            subtitlelist = subtitlelist.OrderBy(item => item.Id).ToList();

            // Create a map list of all the input streams we want in the output
            List<TrackInfo> tracklist = new List<TrackInfo>();
            tracklist.AddRange(videolist);
            tracklist.AddRange(audiolist);
            tracklist.AddRange(subtitlelist);
            StringBuilder sb = new StringBuilder();
            foreach (TrackInfo info in tracklist)
                sb.Append($"-map 0:{info.Id} ");
            input = sb.ToString();
            input = input.Trim();

            // Set the output stream types for each input map item
            // The order has to match the input order
            sb.Clear();
            int video = 0;
            int audio = 0;
            int subtitle = 0;
            foreach (TrackInfo info in tracklist)
            {
                // Copy or encode
                if (info.GetType() == typeof(VideoInfo))
                    sb.Append(info.State == TrackInfo.StateType.Keep
                        ? $"-c:v:{video++} copy "
                        : $"-c:v:{video++} libx264 -crf {quality} -preset medium ");
                else if (info.GetType() == typeof(AudioInfo))
                    sb.Append(info.State == TrackInfo.StateType.Keep
                        ? $"-c:a:{audio++} copy "
                        : $"-c:a:{audio++} {audiocodec} ");
                else if (info.GetType() == typeof(SubtitleInfo))
                    // No re-encoding of subtitles, just copy
                    sb.Append($"-c:s:{subtitle++} copy ");
            }
            output = sb.ToString();
            output = output.Trim();
        }

        public static bool ConvertToMkv(string inputname, int quality, string audiocodec, MediaInfo keep, MediaInfo reencode, string outputname)
        {
            // Simple encoding of audio and video and pasthrough of otehr tracks
            if (keep == null || reencode == null)
                return ConvertToMkv(inputname, quality, audiocodec, outputname);

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
            string snippet = Program.Options.TestSnippets ? FfmpegSnippet : "";
            string commandline = $"-i \"{inputname}\" {snippet} {input} {output} -f matroska \"{outputname}\"";
            int exitcode = FfMpegCli(commandline);
            return exitcode == 0;
        }

        public static bool ConvertToMkv(string inputname, int quality, string audiocodec, string outputname)
        {
            // Delete output file
            FileEx.DeleteFile(outputname);

            // Create the FFmpeg commandline and execute
            // Copy subtitle streams
            // https://trac.ffmpeg.org/wiki/Encode/H.264
            string snippet = Program.Options.TestSnippets ? FfmpegSnippet : "";
            string commandline = $"-i \"{inputname}\" {snippet} -map 0 -c:v libx264 -crf {quality} -preset medium -c:a {audiocodec} -c:s copy -f matroska \"{outputname}\"";
            int exitcode = FfMpegCli(commandline);
            return exitcode == 0;
        }

        public static bool GetIdetInfo(string filename, out FfMpegIdetInfo idetinfo)
        {
            idetinfo = null;
            return GetIdetInfoText(filename, out string text) &&
                   GetIdetInfoFromText(text, out idetinfo);
        }

        public static bool GetIdetInfoText(string inputname, out string text)
        {
            // Create the FFmpeg commandline and execute
            // Use Idet to get statistics
            // https://ffmpeg.org/ffmpeg-filters.html#idet
            // http://www.aktau.be/2013/09/22/detecting-interlaced-video-with-ffmpeg/
            // https://trac.ffmpeg.org/wiki/Null
            string snippet = Program.Config.VerifyOptions.IdetDuration == 0 ? "" : $"-t 0 -ss {Program.Config.VerifyOptions.IdetDuration}";
            string commandline = $"-i \"{inputname}\" -nostats -xerror -filter:v idet {snippet} -an -f rawvideo -y nul";
            // FFMpeg logs output to stderror
            int exitcode = FfMpegCli(commandline, out string _, out text);
            return exitcode == 0;
        }

        public static bool GetIdetInfoFromText(string text, out FfMpegIdetInfo idetinfo)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));
            
            // Init
            idetinfo = new FfMpegIdetInfo();

            // Parse the text
            try
            {
                // Example:
                // frame= 2048 fps=294 q=-0.0 Lsize= 6220800kB time=00:01:21.92 bitrate=622080.0kbits/s speed=11.8x
                // video:6220800kB audio:0kB subtitle:0kB other streams:0kB global headers:0kB muxing overhead: 0.000000%
                // [Parsed_idet_0 @ 00000234e42d0440] Repeated Fields: Neither:  2049 Top:     0 Bottom:     0
                // [Parsed_idet_0 @ 00000234e42d0440] Single frame detection: TFF:     0 BFF:     0 Progressive:  1745 Undetermined:   304
                // [Parsed_idet_0 @ 00000234e42d0440] Multi frame detection: TFF:     0 BFF:     0 Progressive:  2021 Undetermined:    28
                
                // Pattern
                const string repeatedfields = @"\[Parsed_idet_0 \@ (.*?)\] Repeated Fields: Neither: (?<repeated_neither>.*?)Top: (?<repeated_top>.*?)Bottom: (?<repeated_bottom>.*?)$";
                const string singleframe = @"\[Parsed_idet_0 \@ (.*?)\] Single frame detection: TFF: (?<single_tff>.*?)BFF: (?<single_bff>.*?)Progressive: (?<single_prog>.*?)Undetermined: (?<single_und>.*?)$";
                const string multiframe = @"\[Parsed_idet_0 \@ (.*?)\] Multi frame detection: TFF: (?<multi_tff>.*?)BFF: (?<multi_bff>.*?)Progressive: (?<multi_prog>.*?)Undetermined: (?<multi_und>.*?)$";

                // We need to match in LF not CRLF mode else $ does not work as expected
                string pattern = $"{repeatedfields}\n{singleframe}\n{multiframe}";
                string textlf = text.Replace("\r\n", "\n", StringComparison.Ordinal);

                // Match
                Regex regex = new Regex(pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase);
                Match match = regex.Match(textlf);
                Debug.Assert(match.Success);

                // Get the frame counts
                idetinfo.RepeatedFields.Neither = int.Parse(match.Groups["repeated_neither"].Value.Trim(), CultureInfo.InvariantCulture);
                idetinfo.RepeatedFields.Top = int.Parse(match.Groups["repeated_top"].Value.Trim(), CultureInfo.InvariantCulture);
                idetinfo.RepeatedFields.Bottom = int.Parse(match.Groups["repeated_bottom"].Value.Trim(), CultureInfo.InvariantCulture);

                idetinfo.SingleFrame.Tff = int.Parse(match.Groups["single_tff"].Value.Trim(), CultureInfo.InvariantCulture);
                idetinfo.SingleFrame.Bff  = int.Parse(match.Groups["single_bff"].Value.Trim(), CultureInfo.InvariantCulture);
                idetinfo.SingleFrame.Progressive = int.Parse(match.Groups["single_prog"].Value.Trim(), CultureInfo.InvariantCulture);
                idetinfo.SingleFrame.Undetermined = int.Parse(match.Groups["single_und"].Value.Trim(), CultureInfo.InvariantCulture);

                idetinfo.MultiFrame.Tff = int.Parse(match.Groups["multi_tff"].Value.Trim(), CultureInfo.InvariantCulture);
                idetinfo.MultiFrame.Bff = int.Parse(match.Groups["multi_bff"].Value.Trim(), CultureInfo.InvariantCulture);
                idetinfo.MultiFrame.Progressive = int.Parse(match.Groups["multi_prog"].Value.Trim(), CultureInfo.InvariantCulture);
                idetinfo.MultiFrame.Undetermined = int.Parse(match.Groups["multi_und"].Value.Trim(), CultureInfo.InvariantCulture);
            }
            catch (Exception e)
            {
                ConsoleEx.WriteLine("");
                ConsoleEx.WriteLineError(e);
                return false;
            }
            return true;
        }

        public static bool GetPacketInfo(string filename, out List<Packet> packets)
        {
            // Init
            packets = null;

            // Create the FFprobe commandline
            // https://ffmpeg.org/ffprobe.html
            string commandline = $"-loglevel error -show_packets -show_entries packet=codec_type,stream_index,pts_time,dts_time,duration_time,size -print_format json \"{filename}\"";
            string path = Tools.CombineToolPath(ToolsOptions.FfMpeg, FfProbeBinary);

            // Write JSON text output to compressed memory stream
            // TODO : Do the packet calculation in ProcessEx.OutputHandler() instead of writing all output to stream then processing the stream
            // Make sure that the various stream processors leave the memory stream open for the duration of operations
            using MemoryStream memoryStream = new MemoryStream();
            using GZipStream compressStream = new GZipStream(memoryStream, CompressionMode.Compress, true);
            using ProcessEx process = new ProcessEx()
            {
                RedirectOutput = true,
                OutputStream = new StreamWriter(compressStream)
            };

            // Execute
            ConsoleEx.WriteLine("");
            ConsoleEx.WriteLineTool($"FFprobe : {commandline}");
            int exitcode = process.ExecuteEx(path, commandline);
            if (exitcode != 0)
                return false;

            // Read JSON from stream
            process.OutputStream.Flush();
            memoryStream.Seek(0, SeekOrigin.Begin);
            using GZipStream decompressStream = new GZipStream(memoryStream, CompressionMode.Decompress, true);
            using StreamReader streamReader = new StreamReader(decompressStream);
            using JsonTextReader jsonReader = new JsonTextReader(streamReader);
            JsonSerializer serializer = new JsonSerializer();
            PacketInfo packetInfo = serializer.Deserialize<PacketInfo>(jsonReader);
            packets = packetInfo.Packets;

            return true;
        }


        private const string FfMpegBinary = @"bin\ffmpeg.exe";
        private const string FfProbeBinary = @"bin\ffprobe.exe";
        private const string FfmpegSnippet = "-ss 0 -t 60";
    }
}
