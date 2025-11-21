using System;
using System.Collections.Generic;
using System.Linq;
using ptr727.LanguageTags;

namespace PlexCleaner;

public class Language
{
    public const string Undefined = "und";
    public const string None = "zxx";
    public const string English = "en";

    public static readonly LanguageLookup Lookup = new();

    public static bool IsUndefined(string language) =>
        string.IsNullOrEmpty(language)
        || language.Equals(Undefined, StringComparison.OrdinalIgnoreCase);

    public static bool IsMatch(string language, IEnumerable<string> prefixList) =>
        // Match language with any of the prefixes
        prefixList.Any(prefix => Lookup.IsMatch(prefix, language));

    public static List<string> GetLanguageList(IEnumerable<TrackProps> tracks)
    {
        // Create case insensitive set
        HashSet<string> languages = new(StringComparer.OrdinalIgnoreCase);
        foreach (TrackProps item in tracks)
        {
            _ = languages.Add(item.LanguageIetf);
        }
        return [.. languages];
    }
}
