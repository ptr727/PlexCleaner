using System.Diagnostics;
using InsaneGenius.Utilities;
using Newtonsoft.Json.Linq;
using Serilog;

namespace PlexCleaner;

public class GitHubRelease
{
    public static string GetLatestRelease(string repo)
    {
        // Get the latest release version number from github releases
        // https://api.github.com/repos/ptr727/PlexCleaner/releases/latest
        string uri = $"https://api.github.com/repos/{repo}/releases/latest";
        Log.Logger.Information("Getting latest GitHub Release version from : {Uri}", uri);
        var json = Download.GetHttpClient().GetStringAsync(uri).Result;
        Debug.Assert(json != null);

        // Parse latest version from "tag_name"
        var releases = JObject.Parse(json);
        Debug.Assert(releases != null);
        var versionTag = releases["tag_name"];
        Debug.Assert(versionTag != null);
        return versionTag.ToString();
    }

    public static string GetDownloadUri(string repo, string tag, string file)
    {
        // Create download URL from the repo, tag, and filename
        // https://github.com/ptr727/PlexCleaner/releases/download/3.3.2/PlexCleaner.7z
        return $"https://github.com/{repo}/releases/download/{tag}/{file}";
    }
}
