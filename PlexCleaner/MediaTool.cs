using System;
using InsaneGenius.Utilities;
using System.Runtime.InteropServices;

namespace PlexCleaner
{
    public abstract class MediaTool
    {
        public enum ToolFamily
        {
            None,
            FfMpeg,
            HandBrake,
            MediaInfo,
            MkvToolNix,
            SevenZip
        }
        public enum ToolType
        {
            None,
            FfMpeg,
            FfProbe,
            HandBrake,
            MediaInfo,
            MkvMerge,
            MkvPropEdit,
            SevenZip
        }
        public abstract ToolFamily GetToolFamily();
        public abstract ToolType GetToolType();
        // Tool binary name
        protected abstract string GetToolNameWindows();
        protected abstract string GetToolNameLinux();
        // Installed version information retrieved from the tool commandline
        public abstract bool GetInstalledVersion(out MediaToolInfo mediaToolInfo);
        // Latest downloadable version
        public abstract bool GetLatestVersionWindows(out MediaToolInfo mediaToolInfo);
        public abstract bool GetLatestVersionLinux(out MediaToolInfo mediaToolInfo);

        // Tools can override the default behavior as needed
        public virtual bool Update(string updateFile)
        {
            // Make sure the tool folder exists and is empty
            string toolPath = GetToolFolder();
            if (!FileEx.CreateDirectory(toolPath) ||
                !FileEx.DeleteInsideDirectory(toolPath))
                return false;

            // Extract the update file
            ConsoleEx.WriteLine($"Extracting \"{updateFile}\" ...");
            if (!Tools.SevenZip.UnZip(updateFile, toolPath))
                return false;

            // Done
            return true;
        }

        // Tool subfolder, e.g. /x64, /bin
        // Used in GetToolPath()
        public virtual string GetSubFolder()
        {
            return "";
        }

        // The tool info must be set during initialization
        // Version information is used in the sidecar tool logic
        public MediaToolInfo Info { get; set; }

        public string GetToolName()
        {
            // Windows or Linux
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return GetToolNameWindows();

            // TODO: Mac may work the same as Linux, but untested
            return GetToolNameLinux();
        }

        public string GetToolPath()
        {
            // Tool binary name
            string toolName = GetToolName();

            // System use just tool name
            if (Program.Config.ToolsOptions.UseSystem)
                return toolName;
            
            // Append to tools folder using tool family type and sub folder as folder name
            return Tools.CombineToolPath(GetToolFamily().ToString(), GetSubFolder(), toolName);
        }

        public string GetToolFolder()
        {
            // Append to tools folder using tool family type as folder name
            // Sub folders are not included in the tool folder
            return Tools.CombineToolPath(GetToolFamily().ToString());
        }

        public bool GetLatestVersion(out MediaToolInfo mediaToolInfo)
        {
            // Windows or Linux
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return GetLatestVersionWindows(out mediaToolInfo);

            // TODO: Mac may work the same as Linux, but untested
            return GetLatestVersionLinux(out mediaToolInfo);
        }

        public int Command(string parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));
            parameters = parameters.Trim();

            ConsoleEx.WriteLine("");
            ConsoleEx.WriteLineTool($"{GetToolType()} : {parameters}");

            string path = GetToolPath();
            return ProcessEx.Execute(path, parameters);
        }

        public int Command(string parameters, out string output)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));
            parameters = parameters.Trim();

            ConsoleEx.WriteLine("");
            ConsoleEx.WriteLineTool($"{GetToolType()} : {parameters}");

            string path = GetToolPath();
            return ProcessEx.Execute(path, parameters, out output);
        }

        public int Command(string parameters, out string output, out string error)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));
            parameters = parameters.Trim();

            ConsoleEx.WriteLine("");
            ConsoleEx.WriteLineTool($"{GetToolType()} : {parameters}");

            string path = GetToolPath();
            return ProcessEx.Execute(path, parameters, out output, out error);
        }
    }
}
