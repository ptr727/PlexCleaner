using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InsaneGenius.Utilities;
using Serilog;

namespace PlexCleaner;

public static class Language
{
    // Get the IETF/RFC-5646/BCP-47 tag from a ISO-639-2B or similar tag
    public static string GetIetfTag(string language, bool nullOnFailure)
    {
        if (string.IsNullOrEmpty(language))
        {
            return nullOnFailure ? null : Undefined;
        }

        // Undefined "und"
        if (language.Equals(Undefined, StringComparison.OrdinalIgnoreCase))
        { 
            return Undefined;
        }

        // No linguistic content "zxx"
        if (language.Equals(None, StringComparison.OrdinalIgnoreCase))
        {
            return None;
        }

        // Handle "chi" as "zho" for Matroska
        // https://gitlab.com/mbunkus/mkvtoolnix/-/issues/1149
        if (language.Equals("chi", StringComparison.OrdinalIgnoreCase)) 
        {
            return Chinese;
        }

        // Get ISO639 record
        var iso6393 = GetIso639(language);
        if (iso6393 == null)
        {
            Log.Logger.Error("ISO639 language match not found : {Language}", language);
            return nullOnFailure ? null : Undefined;
        }

        // Get a CultureInfo from the ISO639-3 3 letter code
        // E.g. afr -> afr
        // E.g. ger -> deu
        // E.g. fre -> fra
        // If not found try the ISO639-1 2 letter code
        // E.g. afr -> af
        var cultureInfo = CreateCultureInfo(iso6393.Id) ?? CreateCultureInfo(iso6393.Part1);
        if (cultureInfo == null)
        {
            Log.Logger.Warning("CultureInfo not found : {Language}", language);
            return nullOnFailure ? null : Undefined;
        }

        // Return the IETF
        return cultureInfo.IetfLanguageTag;
    }

    // Get the ISO-639-2B tag from a IETF/RFC-5646/BCP-47 tag
    public static string GetIso639Tag(string language, bool nullOnFailure)
    {
        if (string.IsNullOrEmpty(language))
        {
            return nullOnFailure ? null : Undefined;
        }

        // Undefined "und"
        if (language.Equals(Undefined, StringComparison.OrdinalIgnoreCase))
        {
            return Undefined;
        }

        // No linguistic content "zxx"
        if (language.Equals(None, StringComparison.OrdinalIgnoreCase))
        {
            return None;
        }

        // Split the parts and use the first part
        // zh-cmn-Hant -> zh
        var parts = language.Split('-');
        language = parts[0];

        // Handle "chi" as "zho" for Matroska
        // https://gitlab.com/mbunkus/mkvtoolnix/-/issues/1149
        if (language.Equals(Chinese, StringComparison.OrdinalIgnoreCase))
        {
            return "chi";
        }

        // Get ISO639 record
        var iso639 = GetIso639(language);
        if (iso639 != null)
        {
            // Return the Part 2B code
            return iso639.Part2B;
        }

        var cultureInfo = CreateCultureInfo(language);
        if (cultureInfo == null)
        {
            Log.Logger.Warning("CultureInfo not found : {Language}", language);
            return nullOnFailure ? null : Undefined;
        }

        // Get ISO639 record from cultureInfo ISO code
        iso639 = GetIso639(cultureInfo.ThreeLetterISOLanguageName);
        if (iso639 != null)
        {
            // Return the Part 2B code
            return iso639.Part2B;
        }

        // Not found
        Log.Logger.Warning("ISO639 not found : {Language}", cultureInfo.ThreeLetterISOLanguageName);
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

    public static bool IsMatch(string prefix, string language)
    {
        // TODO: Is there an easy to use C# BCP 47 matcher?
        // https://r12a.github.io/app-subtags/
        // https://www.loc.gov/standards/iso639-2/php/langcodes-search.php

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
        if (language.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            language[prefix.Length..].StartsWith('-'))
        {
            return true;
        }

        // No match
        return false;
    }

    public static bool IsMatch(string language, IEnumerable<string> prefixList)
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

    public static Iso6393 GetIso639(string language)
    {
        // Match with any record
        return Iso6393.FromString(language, Iso6393List);
    }

    public const string Undefined = "und";
    public const string Missing = "zzz";
    public const string None = "zxx";
    public const string Chinese = "zh";
    public const string English = "en";

    private static readonly List<Iso6393> Iso6393List = Iso6393.Create();
}
