using System;
using System.Globalization;
using System.Linq;
using InsaneGenius.Utilities;

namespace PlexCleaner
{
    public class TrackInfo
    {
        public TrackInfo()
        {
            State = StateType.None;
        }

        internal TrackInfo(MkvTool.TrackJson track)
        {
            if (track == null)
                throw new ArgumentNullException(nameof(track));

            Format = track.Codec;
            Codec = track.Properties.CodecId;

            // Set language
            if (string.IsNullOrEmpty(track.Properties.Language))
                Language = "und";
            else
            {
                // MKVMerge sets the language to always be und or 3 letter ISO 639-2 code
                // TODO : Make sure it is correct anyway
                Iso6393 lang = PlexCleaner.Language.GetIso6393(track.Properties.Language);
                Language = lang != null ? lang.Part2B : "und";
            }
            
            // Take care to use id and number correctly in MKVMerge and MKVPropEdit
            Id = track.Id;
            Number = track.Properties.Number;
        }

        internal TrackInfo(FfMpegTool.StreamJson stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (!string.IsNullOrEmpty(stream.CodecName))
                Format = stream.CodecName;
            else
                Format = "null";
            if (!string.IsNullOrEmpty(stream.CodecLongName))
                Codec = stream.CodecLongName;
            else
                Codec = "null";
            if (!string.IsNullOrEmpty(stream.Profile))
                Profile = stream.Profile;
            else
                Profile = "null";

            // TODO : FFProbe interprets the language tag instead of tag_language
            // Result is MediaInfo and MKVMerge say language is "eng", FFProbe says language is "und"
            // https://github.com/MediaArea/MediaAreaXml/issues/34

            // Set language
            // TODO : Language is supposed to be 3 characters, but some sample files are "???" or "null", set to und
            Language = stream.Tags?.Language;
            if (string.IsNullOrEmpty(Language) || Language.Equals("???", StringComparison.OrdinalIgnoreCase) || Language.Equals("null", StringComparison.OrdinalIgnoreCase))
                Language = "und";
            else
            {
                // FFProbe normally sets a 3 letter ISO 639-2 code, but some samples have 2 letter codes
                Iso6393 lang = PlexCleaner.Language.GetIso6393(Language);
                Language = lang != null ? lang.Part2B : "und";
            }

            // Use index for number
            Id = stream.Index;
            Number = stream.Index;
        }

        internal TrackInfo(MediaInfoTool.TrackXml track)
        {
            if (track == null)
                throw new ArgumentNullException(nameof(track));

            Format = track.Format;
            Codec = track.CodecId;
            if (!string.IsNullOrEmpty(track.FormatProfile))
                Profile = track.FormatProfile;
            else
                Profile = "null";

            // Set language
            Language = track.Language;
            if (string.IsNullOrEmpty(track.Language))
                Language = "und";
            else
            {
                // MediaInfo uses ab or abc or ab-cd tags, we need to convert to ISO 639-2
                // https://github.com/MediaArea/MediaAreaXml/issues/33
                Iso6393 lang = PlexCleaner.Language.GetIso6393(track.Language);
                Language = lang != null ? lang.Part2B : "und";
            }

            // FFProbe and Matroksa use chi not zho
            // https://github.com/mbunkus/mkvtoolnix/issues/1149
            if (Language.Equals("zho", StringComparison.OrdinalIgnoreCase))
                Language = "chi";

            // ID can be an integer or an integer-type, e.g. 3-CC1
            // https://github.com/MediaArea/MediaInfo/issues/201
            Id = int.Parse(track.Id.All(char.IsDigit) ? track.Id : track.Id.Substring(0, track.Id.IndexOf('-', StringComparison.OrdinalIgnoreCase)), CultureInfo.InvariantCulture);

            // Use streamorder for number
            // StreamOrder is not always present
            if (!string.IsNullOrEmpty(track.StreamOrder))
                Number = int.Parse(track.StreamOrder, CultureInfo.InvariantCulture);
        }
        public string Format { get; set; }
        public string Codec { get; set; }
        public string Profile { get; set; }
        public string Language { get; set; }
        public int Id { get; set; }
        public int Number { get; set; }
        public string ScanType {  get; set; }
        public enum StateType { None, Keep, Remove, ReMux, ReEncode }
        public StateType State { get; set; }
        public bool IsLanguageUnknown()
        {
            // Test for empty or "und" field values
            return string.IsNullOrEmpty(Language) ||
                   Language.Equals("und", StringComparison.OrdinalIgnoreCase);
        }
        public bool IsInterlaced()
        {
            // TODO : Find a better way to do this
            // Test for MBAFF or not Progressive
            if (string.IsNullOrEmpty(ScanType))
                return false;
            return string.Compare(ScanType, "Progressive", StringComparison.OrdinalIgnoreCase) != 0;
        }
    }
}