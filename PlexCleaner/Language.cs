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
            // Match by string
            return Iso6393.FromString(language, iso6393List);
        }

        private static List<Iso6393> iso6393List = Iso6393.Create();
    }
}
