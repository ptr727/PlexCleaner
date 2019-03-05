using System;
using System.Linq;
using System.Globalization;
using InsaneGenius.Utilities;
using System.Collections.Generic;

namespace PlexCleaner
{
    public static class Language
    {
        public static Iso6393 GetIso6393(string language)
        {
            // Create list if it does not exist
            if (iso6393List == null)
            {
                iso6393List = Iso6393.Create();
            }

            // TODO : Call Iso6393.FromString() instead

            // Match the input string type
            Iso6393 lang;
            if (language.Length > 3 && language.ElementAt(2) == '-')
            {
                // Treat the language as a culture form, e.g. en-us
                CultureInfo cix = new CultureInfo(language);

                // Recursively call using the ISO 639-2 code
                return GetIso6393(cix.ThreeLetterISOLanguageName);
            }
            if (language.Length > 3)
            {
                // Try long form
                lang = iso6393List.FirstOrDefault(item => item.RefName.Equals(language, StringComparison.OrdinalIgnoreCase));
                if (lang != null)
                    return lang;
            }
            if (language.Length == 3)
            {
                // Try 639-3
                lang = iso6393List.FirstOrDefault(item =>
                    item.Id.Equals(language, StringComparison.OrdinalIgnoreCase));
                if (lang != null)
                    return lang;

                // Try the 639-2/B
                lang = iso6393List.FirstOrDefault(item =>
                    item.Part2B.Equals(language, StringComparison.OrdinalIgnoreCase));
                if (lang != null)
                    return lang;

                // Try the 639-2/T
                lang = iso6393List.FirstOrDefault(item =>
                    item.Part2T.Equals(language, StringComparison.OrdinalIgnoreCase));
                if (lang != null)
                    return lang;
            }
            if (language.Length == 2)
            {
                // Try 639-1
                lang = iso6393List.FirstOrDefault(item =>
                    item.Part1.Equals(language, StringComparison.OrdinalIgnoreCase));
                if (lang != null)
                    return lang;
            }

            // Not found
            return null;
        }

        private static List<Iso6393> iso6393List = Iso6393.Create();
    }
}
