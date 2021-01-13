using InsaneGenius.Utilities;
using System;

namespace PlexCleaner
{
    public class MediaToolInfo : IComparable
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

        public void WriteLine(string prefix)
        {
            ConsoleEx.WriteLine($"{prefix} : {ToolType}, {Version}, \"{FileName}\", {Size}, {ModifiedTime}, \"{Url}\"");
        }

        public int CompareTo(object obj)
        {
            return CompareTo(obj as MediaToolInfo);
        }

        public int CompareTo(MediaToolInfo toolInfo)
        {
            if (toolInfo == null)
                throw new ArgumentNullException(nameof(toolInfo));

            int result = FileName.CompareTo(toolInfo.FileName);
            if (result != 0)
                return result;

            result = ModifiedTime.CompareTo(toolInfo.ModifiedTime);
            if (result != 0)
                return result;

            result = Size.CompareTo(toolInfo.Size);
            if (result != 0)
                return result;

            return Version.CompareTo(toolInfo.Version);
        }

        public void Copy(MediaToolInfo toolInfo)
        {
            // Do not assign to self
            if (Equals(toolInfo))
                return;

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
}
