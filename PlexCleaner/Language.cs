using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InsaneGenius.Utilities;

namespace PlexCleaner;

public class Language
{
    public Language()
    {
        Iso6392 = new Iso6392();
        Iso6392.Create();
        Iso6393 = new Iso6393();
        Iso6393.Create();
        Rfc5646 = new Rfc5646();
        Rfc5646.Create();
    }

    // Get the RFC-5646 tag from an ISO-639-2B tag
    public string GetIetfTag(string language, bool nullOnFailure)
    {
        // Handle defaults
        if (string.IsNullOrEmpty(language)) return nullOnFailure ? null : Undefined;
        if (language.Equals(Undefined, StringComparison.OrdinalIgnoreCase)) return Undefined;
        if (language.Equals(None, StringComparison.OrdinalIgnoreCase)) return None;

        // Handle "chi" as "zho" for Matroska
        // https://gitlab.com/mbunkus/mkvtoolnix/-/wikis/Chinese-not-selectable-as-language
        if (language.Equals("chi", StringComparison.OrdinalIgnoreCase)) 
        {
            return Chinese;
        }

        // Find a matching RFC 5646 record
        var rfc5646 = Rfc5646.Find(language, false);
        if (rfc5646 != null)
        {
            return rfc5646.TagAny;
        }

        // Find a matching ISO-639-3 record
        var iso6393 = Iso6393.Find(language, false);
        if (iso6393 != null)
        {
            // Find a matching RFC 5646 record from the ISO-639-3 or ISO-639-1 tag
            rfc5646 = Rfc5646.Find(iso6393.Id, false);
            rfc5646 ??= Rfc5646.Find(iso6393.Part1, false);
            if (rfc5646 != null)
            {
                return rfc5646.TagAny;
            }
        }

        // Find a matching ISO-639-2 record
        var iso6392 = Iso6392.Find(language, false);
        if (iso6392 != null)
        {
            // Find a matching RFC 5646 record from the ISO-639-2 or ISO-639-1 tag
            rfc5646 = Rfc5646.Find(iso6392.Id, false);
            rfc5646 ??= Rfc5646.Find(iso6392.Part1, false);
            if (rfc5646 != null)
            {
                return rfc5646.TagAny;
            }
        }

        // Try CultureInfo
        var cultureInfo = CreateCultureInfo(language);
        if (cultureInfo == null)
        {
            return nullOnFailure ? null : Undefined;
        }
        return cultureInfo.IetfLanguageTag;
    }

    // Get the ISO-639-2B tag from a RFC-5646 tag
    public string GetIso639Tag(string language, bool nullOnFailure)
    {
        // Handle defaults
        if (string.IsNullOrEmpty(language)) return nullOnFailure ? null : Undefined;
        if (language.Equals(Undefined, StringComparison.OrdinalIgnoreCase)) return Undefined;
        if (language.Equals(None, StringComparison.OrdinalIgnoreCase)) return None;

        // Handle "chi" as "zho" for Matroska
        // https://gitlab.com/mbunkus/mkvtoolnix/-/wikis/Chinese-not-selectable-as-language
        if (language.Equals(Chinese, StringComparison.OrdinalIgnoreCase))
        {
            return "chi";
        }

        // Find a matching RFC-5646 record
        var rfc5646 = Rfc5646.Find(language, false);
        if (rfc5646 != null)
        {
            // Use expanded form if Redundant, or just use TagAny
            // E.g. cmn-Hant -> zh-cmn-Hant
            language = rfc5646.TagAny;
        }

        // TODO: Split complex tags and resolve in parts
        // language-extlang-script-region-variant-extension-privateuse-...
        // zh-cmn-Hant-Foo-Bar -> zh-cmn-Hant-Foo -> zh-cmn-Hant -> zh-cmn -> zh
        // Private tags -x- is not expected to resolve
        // E.g. zh-cmn-Hans-CN, sr-Latn, zh-yue-HK, sl-IT-nedis, hy-Latn-IT-arevela, az-Arab-x-AZE-derbend

        // Split the parts and use the first part
        // zh-cmn-Hant -> zh
        var parts = language.Split('-');
        language = parts[0];

        // Get ISO-639-3 record
        var iso6393 = Iso6393.Find(language, false);
        if (iso6393 != null)
        {
            // Return the Part 2B code
            return iso6393.Part2B;
        }

        // Get ISO-639-2 record
        var iso6392 = Iso6392.Find(language, false);
        if (iso6392 != null)
        {
            // Return the Part 2B code
            return iso6392.Part2B;
        }

        // Try cultureInfo
        var cultureInfo = CreateCultureInfo(language);
        if (cultureInfo == null)
        {
            return nullOnFailure ? null : Undefined;
        }

        // Get ISO-639-3 record from cultureInfo ISO code
        iso6393 = Iso6393.Find(cultureInfo.ThreeLetterISOLanguageName, false);
        if (iso6393 != null)
        {
            // Return the Part 2B code
            return iso6393.Part2B;
        }

        // Not found
        return nullOnFailure ? null : Undefined;
    }

    public static CultureInfo CreateCultureInfo(string language)
    {
        // Get a CultureInfo representation
        try
        {
            // Cultures are created on the fly, we can't rely on an exception
            // https://stackoverflow.com/questions/35074033/invalid-cultureinfo-no-longer-throws-culturenotfoundexception/
            var cultureInfo = CultureInfo.GetCultureInfo(language, true);

            // Make sure the culture was not custom created
            if (cultureInfo == null ||
                cultureInfo.ThreeLetterWindowsLanguageName.Equals(Missing, StringComparison.OrdinalIgnoreCase) ||
                (cultureInfo.CultureTypes & CultureTypes.UserCustomCulture) == CultureTypes.UserCustomCulture)
            {
                return null;
            }
            return cultureInfo;
        }
        catch (CultureNotFoundException)
        {
            // Not found
        }
        return null;
    }

    public static bool IsUndefined(string language)
    {
        return string.IsNullOrEmpty(language) || language.Equals(Undefined, StringComparison.OrdinalIgnoreCase);
    }

    public bool IsMatch(string prefix, string language)
    {
        while (true)
        {
            // https://r12a.github.io/app-subtags/

            // zh match: zh: zh, zh-Hant, zh-Hans, zh-cmn-Hant
            // zho not: zh
            // zho match: zho
            // zh-Hant match: zh-Hant, zh-Hant-foo

            // The language matches the prefix exactly
            if (language.Equals(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // The language start with the prefix, and the the next character is a -
            if (language.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && language[prefix.Length..].StartsWith('-'))
            {
                return true;
            }

            // Get the extended format of the language
            // E.g. cmn-Hant should be expanded to zh-cmn-Hant else zh will not match

            // Find a matching RFC 5646 record
            var rfc5646 = Rfc5646.Find(language, false);
            if (rfc5646 != null)
            {
                // If the lookup is different then rematch
                if (!string.Equals(language, rfc5646.TagAny, StringComparison.OrdinalIgnoreCase))
                {
                    // Reiterate
                    language = rfc5646.TagAny;
                    continue;
                }
            }

            // No match
            return false;
        }
    }

    public bool IsMatch(string language, IEnumerable<string> prefixList)
    {
        // Match language with any of the prefixes
        return prefixList.Any(prefix => IsMatch(prefix, language));
    }

    public static List<string> GetLanguageList(IEnumerable<TrackInfo> tracks)
    {
        // Create case insensitive set
        HashSet<string> languages = new(StringComparer.OrdinalIgnoreCase);
        foreach (var item in tracks) 
        {
            languages.Add(item.LanguageIetf);
        }
        return languages.ToList();
    }

    public const string Undefined = "und";
    public const string Missing = "zzz";
    public const string None = "zxx";
    public const string Chinese = "zh";
    public const string English = "en";

    private Iso6392 Iso6392;
    private Iso6393 Iso6393;
    private Rfc5646 Rfc5646;

    public static readonly Language Singleton = new();
}
