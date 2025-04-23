using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using InsaneGenius.Utilities;

namespace PlexCleaner;

public class Language
{
    public Language()
    {
        _iso6392 = new Iso6392();
        _ = _iso6392.Create();
        _iso6393 = new Iso6393();
        _ = _iso6393.Create();
        _rfc5646 = new Rfc5646();
        _ = _rfc5646.Create();
    }

    // Get the RFC-5646 tag from an ISO-639-2B tag
    public string GetIetfTag(string language, bool nullOnFailure)
    {
        // Handle defaults
        if (string.IsNullOrEmpty(language))
        {
            return nullOnFailure ? null : Undefined;
        }

        if (language.Equals(Undefined, StringComparison.OrdinalIgnoreCase))
        {
            return Undefined;
        }

        if (language.Equals(None, StringComparison.OrdinalIgnoreCase))
        {
            return None;
        }

        // Handle "chi" as "zho" for Matroska
        // https://gitlab.com/mbunkus/mkvtoolnix/-/wikis/Chinese-not-selectable-as-language
        if (language.Equals("chi", StringComparison.OrdinalIgnoreCase))
        {
            return Chinese;
        }

        // Find a matching RFC 5646 record
        Rfc5646.Record rfc5646 = _rfc5646.Find(language, false);
        if (rfc5646 != null)
        {
            return rfc5646.TagAny;
        }

        // Find a matching ISO-639-3 record
        Iso6393.Record iso6393 = _iso6393.Find(language, false);
        if (iso6393 != null)
        {
            // Find a matching RFC 5646 record from the ISO-639-3 or ISO-639-1 tag
            rfc5646 = _rfc5646.Find(iso6393.Id, false);
            rfc5646 ??= _rfc5646.Find(iso6393.Part1, false);
            if (rfc5646 != null)
            {
                return rfc5646.TagAny;
            }
        }

        // Find a matching ISO-639-2 record
        Iso6392.Record iso6392 = _iso6392.Find(language, false);
        if (iso6392 != null)
        {
            // Find a matching RFC 5646 record from the ISO-639-2 or ISO-639-1 tag
            rfc5646 = _rfc5646.Find(iso6392.Id, false);
            rfc5646 ??= _rfc5646.Find(iso6392.Part1, false);
            if (rfc5646 != null)
            {
                return rfc5646.TagAny;
            }
        }

        // Try CultureInfo
        CultureInfo cultureInfo = CreateCultureInfo(language);
        return cultureInfo == null
            ? nullOnFailure
                ? null
                : Undefined
            : cultureInfo.IetfLanguageTag;
    }

    // Get the ISO-639-2B tag from a RFC-5646 tag
    public string GetIso639Tag(string language, bool nullOnFailure)
    {
        // Handle defaults
        if (string.IsNullOrEmpty(language))
        {
            return nullOnFailure ? null : Undefined;
        }

        if (language.Equals(Undefined, StringComparison.OrdinalIgnoreCase))
        {
            return Undefined;
        }

        if (language.Equals(None, StringComparison.OrdinalIgnoreCase))
        {
            return None;
        }

        // Handle "chi" as "zho" for Matroska
        // https://gitlab.com/mbunkus/mkvtoolnix/-/wikis/Chinese-not-selectable-as-language
        if (language.Equals(Chinese, StringComparison.OrdinalIgnoreCase))
        {
            return "chi";
        }

        // Find a matching RFC-5646 record
        Rfc5646.Record rfc5646 = _rfc5646.Find(language, false);
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
        string[] parts = language.Split('-');
        language = parts[0];

        // Get ISO-639-3 record
        Iso6393.Record iso6393 = _iso6393.Find(language, false);
        if (iso6393 != null)
        {
            // Return the Part 2B code
            return iso6393.Part2B;
        }

        // Get ISO-639-2 record
        Iso6392.Record iso6392 = _iso6392.Find(language, false);
        if (iso6392 != null)
        {
            // Return the Part 2B code
            return iso6392.Part2B;
        }

        // Try cultureInfo
        CultureInfo cultureInfo = CreateCultureInfo(language);
        if (cultureInfo == null)
        {
            return nullOnFailure ? null : Undefined;
        }

        // Get ISO-639-3 record from cultureInfo ISO code
        iso6393 = _iso6393.Find(cultureInfo.ThreeLetterISOLanguageName, false);
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
            CultureInfo cultureInfo = CultureInfo.GetCultureInfo(language, true);

            // Make sure the culture was not custom created
            return
                cultureInfo == null
                || cultureInfo.ThreeLetterWindowsLanguageName.Equals(
                    Missing,
                    StringComparison.OrdinalIgnoreCase
                )
                || (cultureInfo.CultureTypes & CultureTypes.UserCustomCulture)
                    == CultureTypes.UserCustomCulture
                ? null
                : cultureInfo;
        }
        catch (CultureNotFoundException)
        {
            // Not found
        }
        return null;
    }

    public static bool IsUndefined(string language) =>
        string.IsNullOrEmpty(language)
        || language.Equals(Undefined, StringComparison.OrdinalIgnoreCase);

    public bool IsMatch(string prefix, string language)
    {
        Debug.Assert(!string.IsNullOrEmpty(prefix));
        Debug.Assert(!string.IsNullOrEmpty(language));
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
            if (
                language.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && language[prefix.Length..].StartsWith('-')
            )
            {
                return true;
            }

            // Get the extended format of the language
            // E.g. cmn-Hant should be expanded to zh-cmn-Hant else zh will not match

            // Find a matching RFC 5646 record
            Rfc5646.Record rfc5646 = _rfc5646.Find(language, false);
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

    public bool IsMatch(string language, IEnumerable<string> prefixList) =>
        // Match language with any of the prefixes
        prefixList.Any(prefix => IsMatch(prefix, language));

    public static List<string> GetLanguageList(IEnumerable<TrackInfo> tracks)
    {
        // Create case insensitive set
        HashSet<string> languages = new(StringComparer.OrdinalIgnoreCase);
        foreach (TrackInfo item in tracks)
        {
            _ = languages.Add(item.LanguageIetf);
        }
        return [.. languages];
    }

    public const string Undefined = "und";
    public const string Missing = "zzz";
    public const string None = "zxx";
    public const string Chinese = "zh";
    public const string English = "en";

    private readonly Iso6392 _iso6392;
    private readonly Iso6393 _iso6393;
    private readonly Rfc5646 _rfc5646;

    public static readonly Language Singleton = new();
}
