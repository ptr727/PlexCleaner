using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using Serilog;

namespace PlexCleaner;

// v1
public record ToolsOptions1
{
    protected const int Version = 1;

    [Required]
    public bool UseSystem { get; internal set; } // Changed at runtime

    [Required]
    public string RootPath { get; protected set; } = "";

    [Required]
    public bool RootRelative { get; protected set; }

    [Required]
    public bool AutoUpdate { get; internal set; } // Changed at runtime

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
            Log.Logger.Error("ToolsOptions:RootPath must be set if not UseSystem");
            return false;
        }

        return true;
    }
}
