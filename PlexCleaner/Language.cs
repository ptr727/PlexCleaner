using InsaneGenius.Utilities;
using System.Collections.Generic;

namespace PlexCleaner
{
    public static class Language
    {
        public static Iso6393 GetIso6393(string language)
        {
            // Match by string
            return Iso6393.FromString(language, Iso6393List);
        }

        private static readonly List<Iso6393> Iso6393List = Iso6393.Create();
    }
}
