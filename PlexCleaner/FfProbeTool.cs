using System;
using System.Collections.Generic;
using System.Linq;
using InsaneGenius.Utilities;
using PlexCleaner.FfMpegToolJsonSchema;
using System.IO;
using Newtonsoft.Json;
using System.IO.Compression;
using System.Reflection;
using Serilog;

// https://ffmpeg.org/ffprobe.html

namespace PlexCleaner
{
    // Use FfMpeg family
    public class FfProbeTool : FfMpegTool
    {
        public override ToolType GetToolType()
        {
            return ToolType.FfProbe;
        }

        protected override string GetToolNameWindows()
        {
            return "ffprobe.exe";
        }

        protected override string GetToolNameLinux()
        {
            return "ffprobe";
        }

        public bool GetPacketInfo(string filename, out List<Packet> packets)
        {
            // Init
            packets = null;

            // Write JSON text output to compressed memory stream to save memory
            // TODO : Do the packet calculation in ProcessEx.OutputHandler() instead of writing all output to stream then processing the stream
            // Make sure that the various stream processors leave the memory stream open for the duration of operations
            using MemoryStream memoryStream = new MemoryStream();
            using GZipStream compressStream = new GZipStream(memoryStream, CompressionMode.Compress, true);
            using ProcessEx process = new ProcessEx
            {
                RedirectOutput = true,
                OutputStream = new StreamWriter(compressStream)
            };

            // Get packet info
            string commandline = $"-loglevel error -show_packets -show_entries packet=codec_type,stream_index,pts_time,dts_time,duration_time,size -print_format json \"{filename}\"";
            string path = GetToolPath();
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

        public bool GetFfProbeInfo(string filename, out MediaInfo mediainfo)
        {
            mediainfo = null;
            return GetFfProbeInfoJson(filename, out string json) && 
                   GetFfProbeInfoFromJson(json, out mediainfo);
        }

        public bool GetFfProbeInfoJson(string filename, out string json)
        {
            // Get media info as JSON
            string commandline = $"-loglevel quiet -show_streams -print_format json \"{filename}\"";
            int exitcode = Command(commandline, out json, out string error);
            return exitcode == 0 && error.Length == 0;
        }

        public bool GetFfProbeInfoFromJson(string json, out MediaInfo mediaInfo)
        {
            // Parser type is FfProbe
            mediaInfo = new MediaInfo(ToolType.FfProbe);

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
                        mediaInfo.Video.Add(info);
                    }
                    else if (stream.CodecType.Equals("audio", StringComparison.OrdinalIgnoreCase))
                    {
                        AudioInfo info = new AudioInfo(stream);
                        mediaInfo.Audio.Add(info);
                    }
                    else if (stream.CodecType.Equals("subtitle", StringComparison.OrdinalIgnoreCase))
                    {
                        SubtitleInfo info = new SubtitleInfo(stream);
                        mediaInfo.Subtitle.Add(info);
                    }
                }

                // Errors
                mediaInfo.HasErrors = mediaInfo.Video.Any(item => item.HasErrors) || 
                                      mediaInfo.Audio.Any(item => item.HasErrors) || 
                                      mediaInfo.Subtitle.Any(item => item.HasErrors);

                // TODO : Tags
                // TODO : Duration
                // TODO : ContainerType
            }
            catch (Exception e) when (Log.Logger.LogAndHandle(e, MethodBase.GetCurrentMethod().Name))
            {
                return false;
            }
            return true;
        }
    }
}
