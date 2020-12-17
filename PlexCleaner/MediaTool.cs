using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using InsaneGenius.Utilities;
using System.Text;
using System.Diagnostics;
using System.Globalization;
using PlexCleaner.FfMpegToolJsonSchema;
using System.IO;
using Newtonsoft.Json;
using System.IO.Compression;
using System.Threading;
using System.Net;
using System.Runtime.InteropServices;

namespace PlexCleaner
{
    public abstract class MediaTool
    {
        // Parser tool versions are set during tool verification
        // Version are used in sidecar tool version update logic
        static public string Version = "";

        public enum ToolFamily
        {
            FfMpeg,
            HandBrake,
            MediaInfo,
            MkvToolNix,
            SevenZip
        }
        public enum ToolType
        {
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
        protected abstract string GetToolNameWindows();
        protected abstract string GetToolNameLinux();
        public abstract bool GetInstalledVersion(out ToolInfo toolInfo);
        public abstract bool GetLatestVersion(out ToolInfo toolInfo);

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
            string tool = GetToolName();

            // System use just tool name
            if (Program.Config.ToolsOptions.UseSystem)
                return tool;
            
            // Append to tools folder using tool family type as folder name
            return Tools.CombineToolPath(GetToolFamily().ToString(), tool);
        }

        public int Command(string parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));
            parameters = parameters.Trim();

            ConsoleEx.WriteLine("");
            ConsoleEx.WriteLineTool($"{GetToolType().ToString()} : {parameters}");

            string path = GetToolPath();
            return ProcessEx.Execute(path, parameters);
        }

        public int Command(string parameters, out string output)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));
            parameters = parameters.Trim();

            ConsoleEx.WriteLine("");
            ConsoleEx.WriteLineTool($"{GetToolType().ToString()} : {parameters}");

            string path = GetToolPath();
            return ProcessEx.Execute(path, parameters, out output);
        }

        public int Command(string parameters, out string output, out string error)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));
            parameters = parameters.Trim();

            ConsoleEx.WriteLine("");
            ConsoleEx.WriteLineTool($"{GetToolType().ToString()} : {parameters}");

            string path = GetToolPath();
            return ProcessEx.Execute(path, parameters, out output, out error);
        }
    }
}
