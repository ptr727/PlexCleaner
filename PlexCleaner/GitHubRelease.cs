using System.Diagnostics;
using System.Text.Json.Nodes;
using InsaneGenius.Utilities;
using Serilog;

namespace PlexCleaner;

public class GitHubRelease
{
    public static string GetLatestRelease(string repo)
    {
        // Get the latest release version number from github releases
        // https://api.github.com/repos/ptr727/PlexCleaner/releases/latest
        string uri = $"https://api.github.com/repos/{repo}/releases/latest";
        Log.Information("Getting latest GitHub Release version from : {Uri}", uri);
        string json = Download.GetHttpClient().GetStringAsync(uri).GetAwaiter().GetResult();
        Debug.Assert(json != null);

        // Parse latest version from "tag_name"
        JsonNode releases = JsonNode.Parse(json);
        Debug.Assert(releases != null);
        JsonNode versionTag = releases["tag_name"];
        Debug.Assert(versionTag != null);
        return versionTag.ToString();
    }

    public static string GetDownloadUri(string repo, string tag, string file) =>
        // Create download URL from the repo, tag, and filename
        // https://github.com/ptr727/PlexCleaner/releases/download/3.3.2/PlexCleaner.7z
        $"https://github.com/{repo}/releases/download/{tag}/{file}";
}
