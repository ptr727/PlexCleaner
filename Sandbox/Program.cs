using System.Globalization;
using PlexCleaner;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace Sandbox;

public class Program
{
    private static int Main()
    {
        // Default test initialization
        StaticTestInit();

        int ret = ClosedCaptions.Test();

        Log.CloseAndFlush();

        return ret;
    }

    public static void StaticTestInit()
    {
        // Create default commandline options and config
        PlexCleaner.Program.Options = new CommandLineOptions();
        PlexCleaner.Program.Config = new ConfigFileJsonSchema();
        PlexCleaner.Program.Config.SetDefaults();

        // Create default logger
        Serilog.Debugging.SelfLog.Enable(Console.Error);
        Log.Logger = new LoggerConfiguration()
            .Enrich.WithThreadId()
            .WriteTo.Console(
                theme: AnsiConsoleTheme.Code,
                restrictedToMinimumLevel: LogEventLevel.Debug,
                outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] <{ThreadId}> {Message}{NewLine}{Exception}",
                formatProvider: CultureInfo.InvariantCulture)
            .CreateLogger();
        InsaneGenius.Utilities.LogOptions.Logger = Log.Logger;
    }
}
