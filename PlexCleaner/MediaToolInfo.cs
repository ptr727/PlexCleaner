using System;
using Serilog;

namespace PlexCleaner;

public class MediaToolInfo
{
    public MediaToolInfo()
    {
    }

    public MediaToolInfo(MediaTool mediaTool)
    {
        ToolType = mediaTool.GetToolType();
        ToolFamily = mediaTool.GetToolFamily();
    }

    public MediaTool.ToolType ToolType { get; set; }
    public MediaTool.ToolFamily ToolFamily { get; set; }
    public string FileName { get; set; }
    public DateTime ModifiedTime { get; set; }
    public long Size { get; set; }
    public string Url { get; set; }
    public string Version { get; set; }

    public void WriteLine(string prefix) =>
        Log.Logger.Information("{Prefix} : {ToolType}, {Version}, {FileName}, {Size}, {ModifiedTime}, {Url}",
            prefix,
            ToolType,
            Version,
            FileName,
            Size,
            ModifiedTime, Url);

    public int CompareTo(MediaToolInfo toolInfo)
    {
        int result = string.Compare(FileName, toolInfo.FileName, StringComparison.OrdinalIgnoreCase);
        if (result != 0)
        {
            return result;
        }

        result = ModifiedTime.CompareTo(toolInfo.ModifiedTime);
        if (result != 0)
        {
            return result;
        }

        result = Size.CompareTo(toolInfo.Size);
        return result != 0 ? result : string.Compare(Version, toolInfo.Version, StringComparison.OrdinalIgnoreCase);
    }

    public void Copy(MediaToolInfo toolInfo)
    {
        // Do not assign to self
        if (Equals(toolInfo))
        {
            return;
        }

        // Copy values
        ToolType = toolInfo.ToolType;
        ToolFamily = toolInfo.ToolFamily;
        FileName = toolInfo.FileName;
        ModifiedTime = toolInfo.ModifiedTime;
        Size = toolInfo.Size;
        Url = toolInfo.Url;
        Version = toolInfo.Version;
    }
}
