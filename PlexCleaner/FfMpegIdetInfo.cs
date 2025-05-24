using System;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Serilog;

namespace PlexCleaner;

// http://www.aktau.be/2013/09/22/detecting-interlaced-video-with-ffmpeg/
// https://community.topazlabs.com/t/how-to-know-if-a-video-is-interlaced-or-progressive/67209/2
// https://github.com/mpv-player/mpv/blob/master/TOOLS/idet.sh
// https://forum.videohelp.com/threads/418172-How-to-interpret-ffmpeg-idet-information-to-decide-if-vide-is-interlaced
// https://video.stackexchange.com/questions/38002/how-to-interpret-ffmpeg-idet-information-to-decide-if-video-is-interlaced

// Progressive, 29.97 frames per second:
// ffmpeg -hide_banner -f 'lavfi' -i "testsrc2=size='hd1080':rate='ntsc':duration=300" -codec:v 'libx264' -q:v 2 "testcase_progressive_2997.mkv" -y

// Interlaced, 59.94 fields per second in 29.97 frames per second:
// ffmpeg -hide_banner -f 'lavfi' -i "testsrc2=size='hd1080':rate='(60000/1001)':duration=300,tinterlace=mode='interleave_top',setparams=field_mode='tff'" -codec:v 'libx264' -q:v 2 -flags '+ilme+ildct' "testcase_interlaced_tff.mkv" -y

// Interlaced, 59.94 fields per second in 29.97 frames per second, not tagged as tff:
// ffmpeg -hide_banner -f 'lavfi' -i "testsrc2=size='hd1080':rate='(60000/1001)':duration=300,tinterlace=mode='interleave_top'" -codec:v 'libx264' -q:v 2 "testcase_interlaced_tff_untagged.mkv" -y

// ffmpeg -an -sn -dn -i "testcase_progressive_2997.mkv" -filter:v 'idet' -f null -

// Repeated Fields: Neither:  8992 Top:     0 Bottom:     0
// Single frame detection: TFF:     0 BFF:     0 Progressive:  8992 Undetermined:     0
// Multi frame detection: TFF:     0 BFF:     0 Progressive:  8992 Undetermined:     0

// ffmpeg -an -sn -dn -i "testcase_interlaced_tff.mkv" -filter:v 'idet' -f null -

// Repeated Fields: Neither:  8991 Top:     0 Bottom:     0
// Single frame detection: TFF:  8991 BFF:     0 Progressive:     0 Undetermined:     0
// Multi frame detection: TFF:  8991 BFF:     0 Progressive:     0 Undetermined:     0

// ffmpeg -an -sn -dn -i "testcase_interlaced_tff_untagged.mkv" -filter:v 'idet' -f null -

// Repeated Fields: Neither:  8991 Top:     0 Bottom:     0
// Single frame detection: TFF:  8991 BFF:     0 Progressive:     0 Undetermined:     0
// Multi frame detection: TFF:  8991 BFF:     0 Progressive:     0 Undetermined:     0

// Mixed content:

// Repeated Fields: Neither: 76434 Top:     0 Bottom:     0
// Single frame detection: TFF:   560 BFF:  6353 Progressive: 64750 Undetermined:  4771
// Multi frame detection: TFF:   610 BFF:  6459 Progressive: 69231 Undetermined:   134

public partial class FfMpegIdetInfo
{
    private const string IdetRepeatedFields =
        @"\[Parsed_idet_0\ \@\ (.*?)\]\ Repeated\ Fields:\ Neither:(?<repeated_neither>.*?)Top:(?<repeated_top>.*?)Bottom:(?<repeated_bottom>.*?)$";

    private const string IdetSingleFrame =
        @"\[Parsed_idet_0\ \@\ (.*?)\]\ Single\ frame\ detection:\ TFF:(?<single_tff>.*?)BFF:(?<single_bff>.*?)Progressive:(?<single_prog>.*?)Undetermined:(?<single_und>.*?)$";

    private const string IdetMultiFrame =
        @"\[Parsed_idet_0\ \@\ (.*?)\]\ Multi\ frame\ detection:\ TFF:(?<multi_tff>.*?)BFF:(?<multi_bff>.*?)Progressive:(?<multi_prog>.*?)Undetermined:(?<multi_und>.*?)$";

    public Repeated RepeatedFields { get; set; } = new();

    public Frames SingleFrame { get; set; } = new();
    public Frames MultiFrame { get; set; } = new();

    public bool IsInterlaced() => IsInterlaced(out _);

    public bool IsInterlaced(out double percentage) =>
        MultiFrame.IsInterlaced(out percentage) || SingleFrame.IsInterlaced(out percentage);

    public static bool GetIdetInfo(string fileName, out FfMpegIdetInfo idetInfo, out string error)
    {
        // Get idet output from ffmpeg
        idetInfo = null;
        error = string.Empty;
        if (!Tools.FfMpeg.GetIdetText(fileName, out string text))
        {
            error = text;
            return false;
        }

        // Parse the text
        idetInfo = new FfMpegIdetInfo();
        return idetInfo.Parse(text);
    }

    public void WriteLine()
    {
        RepeatedFields.WriteLine(nameof(RepeatedFields));
        SingleFrame.WriteLine(nameof(SingleFrame));
        MultiFrame.WriteLine(nameof(MultiFrame));
    }

    internal bool Parse(string text)
    {
        // Example output:

        // Stream mapping:
        //   Stream #0:0 -> #0:0 (h264 (native) -> wrapped_avframe (native))
        // Press [q] to stop, [?] for help
        // Output #0, null, to 'pipe:':
        //   Metadata:
        //     encoder         : Lavf61.7.100
        //   Stream #0:0(eng): Video: wrapped_avframe, yuv420p(tv, bt709, progressive), 1920x1080 [SAR 1:1 DAR 16:9], q=2-31, 200 kb/s, 29.97 fps, 29.97 tbn (default)
        //       Metadata:
        //         BPS             : 4969575
        //         DURATION        : 00:42:30.648000000
        //         NUMBER_OF_FRAMES: 76434
        //         NUMBER_OF_BYTES : 1584454580
        //         _STATISTICS_WRITING_APP: mkvmerge v61.0.0 ('So') 64-bit
        //         _STATISTICS_WRITING_DATE_UTC: 2022-03-10 12:55:01
        //         _STATISTICS_TAGS: BPS DURATION NUMBER_OF_FRAMES NUMBER_OF_BYTES
        //         encoder         : Lavc61.19.101 wrapped_avframe
        // [Parsed_idet_0 @ 000001c11e8aef00] Repeated Fields: Neither: 76434 Top:     0 Bottom:     0
        // [Parsed_idet_0 @ 000001c11e8aef00] Single frame detection: TFF:   560 BFF:  6353 Progressive: 64750 Undetermined:  4771
        // [Parsed_idet_0 @ 000001c11e8aef00] Multi frame detection: TFF:   610 BFF:  6459 Progressive: 69231 Undetermined:   134
        // [out#0/null @ 000001c11d401040] video:32843KiB audio:0KiB subtitle:0KiB other streams:0KiB global headers:0KiB muxing overhead: unknown
        // frame=76434 fps=1114 q=-0.0 Lsize=N/A time=00:42:30.68 bitrate=N/A speed=37.2x

        // Match (regex construction uses \n for new line)
        Match match = IdetRegex().Match(text.Replace("\r\n", "\n", StringComparison.Ordinal));
        if (!match.Success)
        {
            Log.Error("Failed to parse idet output");
            return false;
        }

        // Get the frame counts
        RepeatedFields.Neither = ParseGroupInt(match, "repeated_neither");
        RepeatedFields.Top = ParseGroupInt(match, "repeated_top");
        RepeatedFields.Bottom = ParseGroupInt(match, "repeated_bottom");

        SingleFrame.Tff = ParseGroupInt(match, "single_tff");
        SingleFrame.Bff = ParseGroupInt(match, "single_bff");
        SingleFrame.Progressive = ParseGroupInt(match, "single_prog");
        SingleFrame.Undetermined = ParseGroupInt(match, "single_und");

        MultiFrame.Tff = ParseGroupInt(match, "multi_tff");
        MultiFrame.Bff = ParseGroupInt(match, "multi_bff");
        MultiFrame.Progressive = ParseGroupInt(match, "multi_prog");
        MultiFrame.Undetermined = ParseGroupInt(match, "multi_und");
        return true;
    }

    internal static int ParseGroupInt(Match match, string groupName) =>
        int.Parse(match.Groups[groupName].Value.Trim(), CultureInfo.InvariantCulture);

    [GeneratedRegex(
        $"{IdetRepeatedFields}\n{IdetSingleFrame}\n{IdetMultiFrame}",
        RegexOptions.IgnoreCase | RegexOptions.Multiline
    )]
    public static partial Regex IdetRegex();

    public class Repeated
    {
        public int Neither { get; set; }
        public int Top { get; set; }
        public int Bottom { get; set; }
        public int Total => Neither + Top + Bottom;

        public void WriteLine(string prefix) =>
            Log.Information(
                "{Prefix} : Neither: {Neither}, Top: {Top}, Bottom: {Bottom}",
                prefix,
                Neither,
                Top,
                Bottom
            );
    }

    public class Frames
    {
        public int Tff { get; set; }
        public int Bff { get; set; }
        public int Progressive { get; set; }
        public int Undetermined { get; set; }
        public int Interlaced => Tff + Bff;
        public int Determined => Interlaced + Progressive;
        public int Total => Tff + Bff + Progressive + Undetermined;

        public bool IsInterlaced(out double percentage)
        {
            // Interlaced to total ratio
            percentage =
                Total == 0
                    ? 0.0
                    : System.Convert.ToDouble(Interlaced) / System.Convert.ToDouble(Total) * 100.0;
            Debug.Assert(percentage is >= 0.0 and <= 100.0);

            // Less than 50% are determined
            if (Undetermined >= Determined)
            {
                // Assume not interlaced
                return false;
            }

            // TODO: What is a reasonable ratio?
            // Even after deinterlacing the interlaced frame count can still be > 0
            // 5% or more are interlaced vs. progressive
            if (Interlaced * 20 >= Progressive)
            {
                // Assume interlaced
                return true;
            }

            // Assume progressive
            return false;
        }

        public void WriteLine(string prefix)
        {
            bool interlaced = IsInterlaced(out double percentage);
            Log.Information(
                "{Prefix} : Interlaced: {Interlaced} ({Percentage:F2}%), TFF: {TFF}, BFF: {BFF}, Progressive: {Progressive}, Undetermined: {Undetermined}",
                prefix,
                interlaced,
                percentage,
                Tff,
                Bff,
                Progressive,
                Undetermined
            );
        }
    }
}
