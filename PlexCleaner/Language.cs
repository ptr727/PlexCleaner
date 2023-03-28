using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InsaneGenius.Utilities;
using Serilog;

namespace PlexCleaner;

public static class Language
{
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

        // Handle "chi" as "zho"
        // https://gitlab.com/mbunkus/mkvtoolnix/-/issues/1149
        if (language.Equals("chi", StringComparison.OrdinalIgnoreCase)) 
        {
            return Chinese;
        }

        // Get ISO639-3 record
        var iso6393 = GetIso6393(language);
        if (iso6393 == null)
        {
            Log.Logger.Error("ISO639-3 language match not found : {Language}", language);
            return nullOnFailure ? null : Undefined;
        }

        // Get a CultureInfo from the 639-3 3 letter code
        // E.g. afr -> afr
        // E.g. ger -> deu
        // E.g. fre -> fra
        // If not found try the ISO639-1 2 letter code
        // E.g. afr -> af
        var cultureInfo = CreateCultureInfo(iso6393.Id) ?? CreateCultureInfo(iso6393.Part1);
        if (cultureInfo == null)
        {
            Log.Logger.Error("CultureInfo match not found : {Language}", language);
            return nullOnFailure ? null : Undefined;
        }

        // Return the IETF tag
        return cultureInfo.IetfLanguageTag;
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

        // zh match: zh: zh, zh-Hant, zh-Hans
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

    public static bool IsEqual(string source, string target)
    {
        // Case insensitive compare
        return source.Equals(target, StringComparison.OrdinalIgnoreCase);
    }

    public static Iso6393 GetIso6393(string language)
    {
        return Iso6393.FromString(language, Iso6393List);
    }

    public const string Undefined = "und";
    public const string Missing = "zzz";
    public const string None = "zxx";
    public const string Chinese = "zh";
    public const string English = "en";

    private static readonly List<Iso6393> Iso6393List = Iso6393.Create();
}
