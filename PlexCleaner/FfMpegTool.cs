using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using InsaneGenius.Utilities;
using System.Text;
using System.Diagnostics;
using System.Globalization;
using PlexCleaner.FfMpegToolJsonSchema;
using System.IO;
using Newtonsoft.Json;
using System.IO.Compression;
using System.Threading;
using System.Net;
using System.Runtime.InteropServices;

namespace PlexCleaner
{
    public static class FfMpegTool
    {
        // Tool version
        public static string Version { get; set; } = "";

        public static int FfMpegCli(string parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));
            parameters = parameters.Trim();

            ConsoleEx.WriteLine("");
            ConsoleEx.WriteLineTool($"FFmpeg : {parameters}");

            string path = GetToolPath();
            return ProcessEx.Execute(path, parameters);
        }

        public static int FfMpegCli(string parameters, out string output, out string error)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));
            parameters = parameters.Trim();

            ConsoleEx.WriteLine("");
            ConsoleEx.WriteLineTool($"FFmpeg : {parameters}");

            string path = GetToolPath();
            return ProcessEx.Execute(path, parameters, out output, out error);
        }

        public static int FfProbeCli(string parameters, out string output, out string error)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));
            parameters = parameters.Trim();

            ConsoleEx.WriteLine("");
            ConsoleEx.WriteLineTool($"FFprobe : {parameters}");

            // Windows or Linux path
            string path = FfProbeBinaryLinux;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                path = Tools.CombineToolPath(ToolsOptions.FfMpeg, FfProbeBinaryWindows);

            return ProcessEx.Execute(path, parameters, out output, out error);
        }

        public static string GetToolFolder()
        {
            return Tools.CombineToolPath(ToolsOptions.FfMpeg);
        }

        public static string GetToolPath()
        {
            string path = FfMpegBinaryLinux;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                path = Tools.CombineToolPath(ToolsOptions.FfMpeg, FfMpegBinaryWindows);
            return path;
        }

        public static bool GetLatestVersion(ToolInfo toolinfo)
        {
            if (toolinfo == null)
                throw new ArgumentNullException(nameof(toolinfo));

            toolinfo.Tool = nameof(FfMpegTool);

            try
            {
                // https://www.ffmpeg.org/download.html
                // https://www.videohelp.com/software/ffmpeg
                // https://www.gyan.dev/ffmpeg/builds/packages/

                // Load the release version page
                // https://www.gyan.dev/ffmpeg/builds/release-version
                using WebClient wc = new WebClient();
                toolinfo.Version = wc.DownloadString("https://www.gyan.dev/ffmpeg/builds/release-version");

                // Create download URL and the output filename using the version number
                toolinfo.FileName = $"ffmpeg-{toolinfo.Version}-full_build.7z";
                toolinfo.Url = $"https://www.gyan.dev/ffmpeg/builds/packages/{toolinfo.FileName}";
            }
            catch (Exception e)
            {
                ConsoleEx.WriteLine("");
                ConsoleEx.WriteLineError(e);
                return false;
            }
            return true;
        }

        public static bool GetToolVersion(ToolInfo toolinfo)
        {
            if (toolinfo == null)
                throw new ArgumentNullException(nameof(toolinfo));

            // Type of tool
            toolinfo.Tool = nameof(FfMpegTool);

            // Create the FFmpeg commandline and execute
            // https://ffmpeg.org/ffmpeg.html
            string commandline = $"-version";
            int exitcode = FfMpegCli(commandline, out string output, out string error);
            if (exitcode != 0 || error.Length > 0)
                return false;

            // First line as version
            string[] lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            toolinfo.Version = lines[0];

            // Get tool filename
            toolinfo.FileName = GetToolPath();

            return true;
        }

        public static bool VerifyMedia(string filename, out string error)
        {
            // https://trac.ffmpeg.org/ticket/6375
            // Too many packets buffered for output stream 0:1
            // Set max_muxing_queue_size to large value to work around issue

            // Create the FFmpeg commandline and execute
            // https://ffmpeg.org/ffmpeg.html
            string snippet = Program.Config.VerifyOptions.VerifyDuration == 0 ? "" : $"-ss 0 -t {Program.Config.VerifyOptions.VerifyDuration}";
            string commandline = $"-i \"{filename}\" -max_muxing_queue_size 1024 -nostats -loglevel error -xerror {snippet} -f null -";
            int exitcode = FfMpegCli(commandline, out string _, out error);

            // Wake from sleep during verify operation results in an invalid argument error
            if ((exitcode != 0 || error.Length != 0) &&
                error.EndsWith(": invalid argument\r\n", StringComparison.OrdinalIgnoreCase))
            {
                // Retry
                const int sleepTime = 5;
                ConsoleEx.WriteLine("");
                ConsoleEx.WriteLineError(error);
                ConsoleEx.WriteLine($"Retrying after sleeping {sleepTime}s");
                Thread.Sleep(sleepTime * 1000);
                exitcode = FfMpegCli(commandline, out string _, out error);
            }

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
                // Deserialize
                FfProbe ffprobe = FfProbe.FromJson(json);

                // No tracks
                if (ffprobe.Streams.Count == 0)
                    return false;

                // Tracks
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
                mediainfo.HasErrors = mediainfo.Video.Any(item => item.HasErrors) || 
                                      mediainfo.Audio.Any(item => item.HasErrors) || 
                                      mediainfo.Subtitle.Any(item => item.HasErrors);

                // TODO : Tags
                // TODO : Duration
                // TODO : ContainerType
            }
            catch (Exception e)
            {
                ConsoleEx.WriteLine("");
                ConsoleEx.WriteLineError(e);
                return false;
            }

            return true;
        }

        public static bool ReMuxToMkv(string inputName, MediaInfo keep, string outputName)
        {
            if (keep == null)
                return ReMuxToMkv(inputName, outputName);

            // Delete output file
            FileEx.DeleteFile(outputName);

            // Create an input and output map
            CreateFfMpegMap(keep, out string input, out string output);

            // Create the FFmpeg commandline and execute
            // https://ffmpeg.org/ffmpeg.html
            // https://trac.ffmpeg.org/wiki/Map
            // https://ffmpeg.org/ffmpeg.html#Stream-copy
            string snippet = Program.Options.TestSnippets ? FfmpegSnippet : "";
            string commandline = $"-i \"{inputName}\" {snippet} {input} {output} -f matroska \"{outputName}\"";
            int exitcode = FfMpegCli(commandline);
            return exitcode == 0;
        }

        public static bool ReMuxToMkv(string inputName, string outputName)
        {
            // Delete output file
            FileEx.DeleteFile(outputName);

            // Create the FFmpeg commandline and execute
            // Copy all streams
            // https://ffmpeg.org/ffmpeg.html
            // https://trac.ffmpeg.org/wiki/Map
            // https://ffmpeg.org/ffmpeg.html#Stream-copy
            string snippet = Program.Options.TestSnippets ? FfmpegSnippet : "";
            string commandline = $"-i \"{inputName}\" {snippet} -map 0 -codec copy -f matroska \"{outputName}\"";
            int exitcode = FfMpegCli(commandline);
            return exitcode == 0;
        }

        private static void CreateFfMpegMap(MediaInfo keep, out string input, out string output)
        {
            // Remux only
            MediaInfo reencode = new MediaInfo(MediaInfo.ParserType.FfProbe);
            CreateFfMpegMap("", 0, "", keep, reencode, out input, out output);
        }

        private static void CreateFfMpegMap(string videoCodec, int videoQuality, string audioCodec, MediaInfo keep, MediaInfo reencode, out string input, out string output)
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
            List<TrackInfo> videoList = new List<TrackInfo>();
            videoList.AddRange(keep.Video);
            videoList.AddRange(reencode.Video);
            videoList = videoList.OrderBy(item => item.Id).ToList();
            
            List<TrackInfo> audioList = new List<TrackInfo>();
            audioList.AddRange(keep.Audio);
            videoList.AddRange(reencode.Audio);
            audioList = audioList.OrderBy(item => item.Id).ToList();
            
            List<TrackInfo> subtitleList = new List<TrackInfo>();
            subtitleList.AddRange(keep.Subtitle);
            videoList.AddRange(reencode.Subtitle);
            subtitleList = subtitleList.OrderBy(item => item.Id).ToList();

            // Create a map list of all the input streams we want in the output
            List<TrackInfo> trackList = new List<TrackInfo>();
            trackList.AddRange(videoList);
            trackList.AddRange(audioList);
            trackList.AddRange(subtitleList);
            StringBuilder sb = new StringBuilder();
            foreach (TrackInfo info in trackList)
                sb.Append($"-map 0:{info.Id} ");
            input = sb.ToString();
            input = input.Trim();

            // Set the output stream types for each input map item
            // The order has to match the input order
            sb.Clear();
            int videoIndex = 0;
            int audioIndex = 0;
            int subtitleIndex = 0;
            foreach (TrackInfo info in trackList)
            {
                // Copy or encode
                if (info.GetType() == typeof(VideoInfo))
                    sb.Append(info.State == TrackInfo.StateType.Keep
                        ? $"-c:v:{videoIndex ++} copy "
                        : $"-c:v:{videoIndex ++} {videoCodec} -crf {videoQuality} -preset medium ");
                else if (info.GetType() == typeof(AudioInfo))
                    sb.Append(info.State == TrackInfo.StateType.Keep
                        ? $"-c:a:{audioIndex ++} copy "
                        : $"-c:a:{audioIndex ++} {audioCodec} ");
                else if (info.GetType() == typeof(SubtitleInfo))
                    // No re-encoding of subtitles, just copy
                    sb.Append($"-c:s:{subtitleIndex ++} copy ");
            }
            output = sb.ToString();
            output = output.Trim();
        }

        public static bool ConvertToMkv(string inputName, string videoCodec, int videoQuality, string audioCodec, MediaInfo keep, MediaInfo reencode, string outputName)
        {
            // Simple encoding of audio and video and pasthrough of other tracks
            if (keep == null || reencode == null)
                return ConvertToMkv(inputName, videoCodec, videoQuality, audioCodec, outputName);

            // Delete output file
            FileEx.DeleteFile(outputName);

            // Create an input and output map
            CreateFfMpegMap(videoCodec, videoQuality, audioCodec, keep, reencode, out string input, out string output);

            // TODO : Error with some PGS subtitles
            // https://trac.ffmpeg.org/ticket/2622
            //  [matroska,webm @ 000001d77fb61ca0] Could not find codec parameters for stream 2 (Subtitle: hdmv_pgs_subtitle): unspecified size
            //  Consider increasing the value for the 'analyzeduration' and 'probesize' options

            // Create the FFmpeg commandline and execute
            string snippet = Program.Options.TestSnippets ? FfmpegSnippet : "";
            string commandline = $"-i \"{inputName}\" {snippet} {input} {output} -f matroska \"{outputName}\"";
            int exitcode = FfMpegCli(commandline);
            return exitcode == 0;
        }
        public static bool ConvertToMkv(string inputName, MediaInfo keep, MediaInfo reencode, string outputName)
        {
            // Use defaults
            return ConvertToMkv(inputName,
                                Program.Config.ConvertOptions.EnableH265Encoder ? FfMpegTool.H265Codec : FfMpegTool.H264Codec,
                                Program.Config.ConvertOptions.VideoEncodeQuality,
                                Program.Config.ConvertOptions.AudioEncodeCodec,
                                keep, 
                                reencode, 
                                outputName);
        }

        public static bool ConvertToMkv(string inputName, string videoCodec, int videoQuality, string audioCodec, string outputName)
        {
            // Delete output file
            FileEx.DeleteFile(outputName);

            // Create the FFmpeg commandline and execute
            // Encode video and audio, copy subtitle streams
            // https://trac.ffmpeg.org/wiki/Encode/H.264
            // https://trac.ffmpeg.org/wiki/Encode/H.265
            string snippet = Program.Options.TestSnippets ? FfmpegSnippet : "";
            string commandline = $"-i \"{inputName}\" {snippet} -map 0 -c:v {videoCodec} -crf {videoQuality} -preset medium -c:a {audioCodec} -c:s copy -f matroska \"{outputName}\"";
            int exitcode = FfMpegCli(commandline);
            return exitcode == 0;
        }

        public static bool ConvertToMkv(string inputName, string videoCodec, int videoQuality, string outputName)
        {
            // Delete output file
            FileEx.DeleteFile(outputName);

            // Create the FFmpeg commandline and execute
            // Encode video, copy audio and subtitle streams
            string snippet = Program.Options.TestSnippets ? FfmpegSnippet : "";
            string commandline = $"-i \"{inputName}\" {snippet} -map 0 -c:v {videoCodec} -crf {videoQuality} -preset medium -c:a copy -c:s copy -f matroska \"{outputName}\"";
            int exitcode = FfMpegCli(commandline);
            return exitcode == 0;
        }

        public static bool ConvertToMkv(string inputName, string outputName)
        {
            // Use defaults
            return ConvertToMkv(inputName,
                                Program.Config.ConvertOptions.EnableH265Encoder ? FfMpegTool.H265Codec : FfMpegTool.H264Codec,
                                Program.Config.ConvertOptions.VideoEncodeQuality,
                                Program.Config.ConvertOptions.AudioEncodeCodec,
                                outputName);
        }

        public static bool GetIdetInfo(string filename, out FfMpegIdetInfo idetinfo)
        {
            idetinfo = null;
            return GetIdetInfoText(filename, out string text) &&
                   GetIdetInfoFromText(text, out idetinfo);
        }

        public static bool GetIdetInfoText(string inputName, out string text)
        {
            // Create the FFmpeg commandline and execute
            // Use Idet to get statistics
            // https://ffmpeg.org/ffmpeg-filters.html#idet
            // http://www.aktau.be/2013/09/22/detecting-interlaced-video-with-ffmpeg/
            // https://trac.ffmpeg.org/wiki/Null
            string snippet = Program.Config.VerifyOptions.IdetDuration == 0 ? "" : $"-t 0 -ss {Program.Config.VerifyOptions.IdetDuration}";
            string commandline = $"-i \"{inputName}\" -nostats -xerror -filter:v idet {snippet} -an -f rawvideo -y nul";
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

            // Windows or Linux path
            string path = FfProbeBinaryLinux;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                path = Tools.CombineToolPath(ToolsOptions.FfMpeg, FfProbeBinaryWindows);

            // Write JSON text output to compressed memory stream
            // TODO : Do the packet calculation in ProcessEx.OutputHandler() instead of writing all output to stream then processing the stream
            // Make sure that the various stream processors leave the memory stream open for the duration of operations
            using MemoryStream memoryStream = new MemoryStream();
            using GZipStream compressStream = new GZipStream(memoryStream, CompressionMode.Compress, true);
            using ProcessEx process = new ProcessEx
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

        public const string H264Codec = "libx264";
        public const string H265Codec = "libx265";

        private const string FfMpegBinaryWindows = @"bin\ffmpeg.exe";
        private const string FfProbeBinaryWindows = @"bin\ffprobe.exe";
        private const string FfMpegBinaryLinux = @"ffmpeg";
        private const string FfProbeBinaryLinux = @"ffprobe";
        private const string FfmpegSnippet = "-ss 0 -t 60";
    }
}
