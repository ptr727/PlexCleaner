using InsaneGenius.Utilities;
using System;
using System.Globalization;
using System.IO;

namespace PlexCleaner
{
    internal class LogFile
    {
        public bool Clear()
        {
            // Is filename set
            if (string.IsNullOrEmpty(FileName))
                return false;

            try
            {
                // Create empty file
                using FileStream fileStream = File.Create(FileName);
            }
            catch (Exception e)
            {
                ConsoleEx.WriteLineError(e);
                return false;
            }
            return true;
        }

        public bool Log(string value)
        {
            // Skip if no filename set
            if (string.IsNullOrEmpty(FileName))
                return true;

            try
            {
                File.AppendAllText(FileName, GetLogLine(value));
            }
            catch (Exception e)
            {
                ConsoleEx.WriteLineError(e);
                return false;
            }
            return true;
        }

        public bool LogConsole(string value)
        {
            // Log to console and file
            ConsoleEx.WriteLine(value);
            return Log(value);
        }

        public bool LogConsoleError(string value)
        {
            // Log to console and file
            ConsoleEx.WriteLineError(value);
            return Log(value);
        }

        private static string GetLogLine(string value)
        {
            // Match ConsoleEx.WriteLine() formatting
            if (string.IsNullOrEmpty(value))
                return Environment.NewLine;
            return $"{DateTime.Now.ToString(CultureInfo.CurrentCulture)} : {value}" + Environment.NewLine;
        }

        public string FileName { get; set; }
    }
}
