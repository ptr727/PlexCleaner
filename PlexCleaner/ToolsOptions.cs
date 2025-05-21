using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using Serilog;

namespace PlexCleaner;

// v1
public record ToolsOptions1
{
    protected const int Version = 1;

    [JsonRequired]
    public bool UseSystem { get; set; }

    [JsonRequired]
    public string RootPath { get; set; } = "";

    [JsonRequired]
    public bool RootRelative { get; set; }

    [JsonRequired]
    public bool AutoUpdate { get; set; }

    public void SetDefaults()
    {
        // Set defaults based on OS
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            UseSystem = false;
            RootPath = @".\Tools\";
            RootRelative = true;
            AutoUpdate = true;
        }
        else
        {
            UseSystem = true;
            RootPath = "";
            RootRelative = false;
            AutoUpdate = false;
        }
    }

    public bool VerifyValues()
    {
        // Path must be set if not using system path
        if (!UseSystem && string.IsNullOrEmpty(RootPath))
        {
            Log.Error("ToolsOptions:RootPath must be set if not UseSystem");
            return false;
        }

        return true;
    }
}
