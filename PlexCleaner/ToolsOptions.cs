using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;

namespace PlexCleaner;

public class ToolsOptions
{
    [Required]
    public bool UseSystem { get; set; }
    [Required]
    public string RootPath { get; set; } = "";
    [Required]
    public bool RootRelative { get; set; }
    [Required]
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
}
