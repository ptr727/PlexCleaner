using System;
using System.IO;
using InsaneGenius.Utilities;

namespace PlexCleaner
{
    public static class Convert
    {
        public static ConvertOptions Options { get; set; } = new ConvertOptions();

        public static bool ConvertToMkv(string inputname, out string outputname)
        {
            // Convert all tracks
            return ConvertToMkv(inputname, null, null, out outputname);
        }

        public static bool ConvertToMkv(string inputname, MediaInfo keep, MediaInfo reencode, out string outputname)
        {
            if (inputname == null)
                throw new ArgumentNullException(nameof(inputname));

            // Match the logic in ReMuxToMKV()

            // Test
            if (Process.Options.TestNoModify)
            {
                outputname = inputname;
                return true;
            }

            // Create a temp filename based on the input name
            outputname = Path.ChangeExtension(inputname, ".mkv");
            string tempname = Path.ChangeExtension(inputname, ".tmp");

            // Convert using ffmpeg
            if (!FfMpegTool.ConvertToMkv(inputname, Options.VideoEncodeQuality, Options.AudioEncodeCodec, keep, reencode, tempname))
            {
                FileEx.DeleteFile(tempname);
                return false;
            }

            // Rename the temp file to the output file
            if (!FileEx.RenameFile(tempname, outputname))
                return false;

            // If the input and output names are not the same, delete the input
            return inputname.Equals(outputname, StringComparison.OrdinalIgnoreCase) || 
                   FileEx.DeleteFile(inputname);
        }

        public static bool ReMuxToMkv(string inputname, out string outputname)
        {
            // Remux all tracks
            return ReMuxToMkv(inputname, null, out outputname);
        }

        public static bool ReMuxToMkv(string inputname, MediaInfo keep, out string outputname)
        {
            if (inputname == null)
                throw new ArgumentNullException(nameof(inputname));

            // Match the logic in ConvertToMKV()

            // Test
            if (Process.Options.TestNoModify)
            {
                outputname = inputname;
                return true;
            }

            // Create a temp filename based on the input name
            outputname = Path.ChangeExtension(inputname, ".mkv");
            string tempname = Path.ChangeExtension(inputname, ".tmp");

            // MKVToolNix and FFmpeg both have problems dealing with some AVI files, so we will try both
            // MKVToolNix does not support WMV or ASF files, or maybe just the WMAPro codec
            // E.g. https://github.com/FFmpeg/FFmpeg/commit/8de1ee9f725aa3c550f425bd3120bcd95d5b2ea8
            // E.g. https://github.com/mbunkus/mkvtoolnix/issues/2123
            bool result;
            if (MkvTool.IsMkvFile(inputname))
            {
                // MKV files always try MKVMerge first
                result = MkvTool.ReMuxToMkv(inputname, keep, tempname);
                if (!result && !Program.Cancel.State)
                    // Retry using FFmpeg
                    result = FfMpegTool.ReMuxToMkv(inputname, keep, tempname);
            }
            else
            {
                // Non-MKV files always try FFmpeg first
                result = FfMpegTool.ReMuxToMkv(inputname, keep, tempname);
                if (!result && !Program.Cancel.State)
                    // Retry using MKVMerge
                    result = MkvTool.ReMuxToMkv(inputname, keep, tempname);
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
            return inputname.Equals(outputname, StringComparison.OrdinalIgnoreCase) || 
                   FileEx.DeleteFile(inputname);
        }

        public static bool DeInterlaceToMkv(string inputname, out string outputname)
        {
            if (inputname == null)
                throw new ArgumentNullException(nameof(inputname));

            // Match the logic in ConvertToMKV()

            // Test
            if (Process.Options.TestNoModify)
            {
                outputname = inputname;
                return true;
            }

            // Create a temp filename based on the input name
            outputname = Path.ChangeExtension(inputname, ".mkv");
            string tempname = Path.ChangeExtension(inputname, ".tmp");

            // HandBrake produces the best de-interlacing results
            if (!HandBrakeTool.ConvertToMkv(inputname, Options.VideoEncodeQuality, tempname))
            {
                FileEx.DeleteFile(tempname);
                return false;
            }

            // Rename the temp file to the output file
            if (!FileEx.RenameFile(tempname, outputname))
                return false;

            // If the input and output names are not the same, delete the input
            return inputname.Equals(outputname, StringComparison.OrdinalIgnoreCase) || 
                   FileEx.DeleteFile(inputname);
        }
    }
}
