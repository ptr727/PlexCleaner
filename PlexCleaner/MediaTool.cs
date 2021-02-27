using System;
using InsaneGenius.Utilities;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Serilog;

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
            LogOptions.Logger.LogInformation("Extracting {UpdateFile} ...", updateFile);
            return Tools.SevenZip.UnZip(updateFile, toolPath);
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
            // TODO: Mac may work the same as Linux, but untested
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? GetToolNameWindows() : GetToolNameLinux();
        }

        public string GetToolPath()
        {
            // Tool binary name
            string toolName = GetToolName();

            // System use just tool name
            // Append to tools folder using tool family type and sub folder as folder name
            return Program.Config.ToolsOptions.UseSystem ? toolName : Tools.CombineToolPath(GetToolFamily().ToString(), GetSubFolder(), toolName);
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
            // TODO: Mac may work the same as Linux, but untested
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? GetLatestVersionWindows(out mediaToolInfo) : GetLatestVersionLinux(out mediaToolInfo);
        }

        public int Command(string parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));
            parameters = parameters.Trim();

            Log.Logger.Information("Executing {ToolType} : {Parameters}", GetToolType(), parameters);

            string path = GetToolPath();
            int exitcode = ProcessEx.Execute(path, parameters);
            if (exitcode != 0)
                Log.Logger.Warning("Executing {ToolType} : ExitCode: {ExitCode}", GetToolType(), exitcode);
            return exitcode;
        }

        public int Command(string parameters, bool console, bool limit, out string output)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));
            parameters = parameters.Trim();

            Log.Logger.Information("Executing {ToolType} : {Parameters}", GetToolType(), parameters);

            string path = GetToolPath();
            int exitcode = ProcessEx.Execute(path, 
                                             parameters, 
                                             console, limit ? MaxConsoleLines : 0, 
                                             out output);
            if (exitcode != 0)
                Log.Logger.Warning("Executing {ToolType} : ExitCode: {ExitCode}", GetToolType(), exitcode);
            return exitcode;
        }

        public int Command(string parameters, bool console, bool limit, out string output, out string error)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));
            parameters = parameters.Trim();

            Log.Logger.Information("Executing {ToolType} : {Parameters}", GetToolType(), parameters);

            string path = GetToolPath();
            int exitcode = ProcessEx.Execute(path, 
                                             parameters, 
                                             console, 
                                             limit ? MaxConsoleLines : 0, 
                                             out output, 
                                             out error);
            if (exitcode != 0)
                Log.Logger.Warning("Executing {ToolType} : ExitCode: {ExitCode}", GetToolType(), exitcode);
            return exitcode;
        }

        private const int MaxConsoleLines = 5;
    }
}
