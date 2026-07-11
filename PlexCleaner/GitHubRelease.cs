using System.Text.Json.Nodes;
using ptr727.Utilities;
using Serilog;

namespace PlexCleaner;

// TODO: Convert to JSON Schema class
public class GitHubRelease
{
    public static bool GetLatestRelease(string repo, out string version)
    {
        version = string.Empty;

        // Get the latest release version number from github releases
        // https://api.github.com/repos/ptr727/PlexCleaner/releases/latest
        string uri = $"https://api.github.com/repos/{repo}/releases/latest";
        Log.Debug("Getting latest GitHub Release version from : {Uri}", uri);
        if (!Download.DownloadString(new Uri(uri), out string json))
        {
            return false;
        }

        // Parse latest version from "tag_name"; malformed or unexpected JSON returns false, not throws
        try
        {
            string? versionTag = JsonNode.Parse(json)?["tag_name"]?.ToString();
            if (string.IsNullOrEmpty(versionTag))
            {
                Log.Error("Failed to read tag_name from GitHub release : {Uri}", uri);
                return false;
            }
            version = versionTag;
            return true;
        }
        catch (Exception e) when (Log.Logger.LogAndHandle(e))
        {
            return false;
        }
    }

    public static string GetDownloadUri(string repo, string tag, string file) =>
        // Create download URL from the repo, tag, and filename
        // https://github.com/ptr727/PlexCleaner/releases/download/3.3.2/PlexCleaner.7z
        $"https://github.com/{repo}/releases/download/{tag}/{file}";
}
