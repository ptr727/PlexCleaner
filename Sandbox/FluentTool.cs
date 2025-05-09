using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.Buffered;
using CliWrap.Builders;
using CliWrap.EventStream;
using CliWrap.Exceptions;
using PlexCleaner;


namespace Sandbox;

// Settings:
/*
{
    "FluentTool": {
        "CCExtractor": "ccextractor.exe",
        "FilePath": "C:\\Temp\\Test\\ClosedCaptions",
        "ProcessExtensions": ".ts,.mp4,.mkv"
    }
}
*/

public class FluentTool
{
    public FluentTool(Program program)
    {
        // Get settings
        Dictionary<string, string>? settings = program.GetSettingsDictionary(
            nameof(FluentTool)
        );
        Debug.Assert(settings is not null);
    }

    public async Task<int> TestAsync()
    {
        // Get tools
        if (!Tools.VerifyTools() && !Tools.CheckForNewTools())
        {
            return -1;
        }

        try
        {
            using CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));
            string fileName = Tools.FfProbe.GetToolPath();
            Command command = Cli.Wrap(fileName)
                .WithValidation(CommandResultValidation.None)
                .WithArguments(args => args
                    .Add("--version")
                    .AddOption("--foo", "bar"));


            BufferedCommandResult result = await command.ExecuteBufferedAsync(cts.Token);
            Console.WriteLine(result.StandardOutput);
            Console.WriteLine(result.StandardError);

            //await foreach (CommandEvent commandEvent in command.ListenAsync())
            //{
            //    switch (commandEvent)
            //    {
            //        case StartedCommandEvent started:
            //            Console.WriteLine($"Command started: {started.ProcessId}");
            //            break;
            //        case StandardOutputCommandEvent stdOut:
            //            Console.ForegroundColor = ConsoleColor.White;
            //            Console.WriteLine("OUT> " + stdOut.Text);
            //            Console.ResetColor();
            //            break;
            //        case StandardErrorCommandEvent stdErr:
            //            Console.ForegroundColor = ConsoleColor.Red;
            //            Console.WriteLine("ERR >" + stdErr.Text);
            //            Console.ResetColor();
            //            break;
            //    }
            //}
        }
        catch (CommandExecutionException e)
        {
            Console.WriteLine($"Command failed: {e.Message}");
            Console.WriteLine($"Arguments: {e.Command.Arguments}");
            Console.WriteLine($"Exit code: {e.ExitCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }

        return 0;
    }
}

public static class  CliExtensions
{
    public static ArgumentsBuilder AddOption(
        this ArgumentsBuilder args,
        string name,
        string value
    ) => string.IsNullOrEmpty(value) || string.IsNullOrWhiteSpace(value) ? args : args.Add(name).Add(value);
}

public class FluentMediaTools
{
    // public static Command FfProbe(string targetFilePath) => new(targetFilePath);

    public class FfProbe
    {
        public interface IFfProbe
        {
            public IGlobalOptions GlobalOptions();
            public IInputOptions InputOptions();
            public IOutputOptions OutputOptions();
            public ICommands Commands();
        }

        public interface IGlobalOptions : IInputOptions, IOutputOptions
        {
            public IGlobalOptions HideBanner();
            public IGlobalOptions LogLevel(string level);
            public IGlobalOptions LogLevelError();
        }
        public interface IInputOptions : IOutputOptions
        {
            public IInputOptions FilePath(string filePath);
        }
        public interface IOutputOptions
        {
            ICommands FilePath(string filePath);
            ICommands StdOut();
        }
        public interface ICommands
        {
            public ICreateCommand GetVersion();
            public ICreateCommand GetFormat();
            public ICreateCommand GetFrames();
            public ICreateCommand GetPackets();
        }
        public interface ICreateCommand
        {
            Command GetCommand();
        }
        public static FfProbe CreateBuilder(string targetFilePath) => new(targetFilePath);
        private FfProbe(string targetFilePath)
        {
            TargetFilePath = targetFilePath;
        }

        public string TargetFilePath { get; }
        public string InputFilePath { get; private set; }
        public bool ShowVersion { get; private set; }
        public bool ShowFormat { get; private set; }
        public bool ShowFrames { get; private set; }
        public bool ShowPackets { get; private set; }
        public bool HideBanner { get; private set; }
        public TimeSpan TimeStart { get; private set; }
        public TimeSpan TimeStop { get; private set; }

        public FfProbe WithInputFilePath(string inputFilePath)
        {
            InputFilePath = inputFilePath;
            return this;
        }
        public FfProbe WithShowVersion()
        {
            ShowVersion = true;
            return this;
        }
        public FfProbe WithHideBanner()
        {
            HideBanner = true;
            return this;
        }
        public FfProbe WithShowFormat()
        {
            ShowFormat = true;
            return this;
        }
        public FfProbe WithShowFrames()
        {
            ShowFrames = true;
            return this;
        }
        public FfProbe WithShowPackets()
        {
            ShowPackets = true;
            return this;
        }
        public FfProbe WithTimeStart(TimeSpan timeStart)
        {
            TimeStart = timeStart;
            return this;
        }
        public FfProbe WithTimeStop(TimeSpan timeStop)
        {
            TimeStart = timeStop;
            return this;
        }
        public FfProbe WithTimeStartStop(TimeSpan timeStart, TimeSpan timeStop)
        {
            TimeStart = timeStart;
            TimeStop = timeStop;
            return this;
        }
        // Analyze
        // GetFrames
        // GetPackets
        public Command GetCommand()
        {
            return Cli.Wrap("ffprobe")
                .WithArguments(args => args
                    .Add("--version"));
        }
    }
}
