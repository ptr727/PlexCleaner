using System.Globalization;
using Serilog;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace PlexCleaner;

public static class LoggerFactory
{
    // Serilog File sink defaults, made explicit so the size cap is self-documenting.
    // On reaching the limit the sink rolls to a numbered file: <base>_001.<ext>, <base>_002.<ext>, etc.
    private const long FileSizeLimitBytes = 1_073_741_824; // 1 GiB
    private const int RetainedFileCountLimit = 31;

    public sealed class Options
    {
        public LogEventLevel Level { get; init; } = LogEventLevel.Information;
        public FileInfo? File { get; init; }
        public bool FileClear { get; init; }
        public bool Elevate { get; init; }
    }

    // Map bound commandline options to logger options, applying deprecated flag semantics
    public static Options FromCommandLine(CommandLineOptions options) =>
        new()
        {
            // Deprecated --logwarning maps to the Warning level only, it does not enable elevation
            Level = options.LogWarning ? LogEventLevel.Warning : options.LogLevel,
            File = options.LogFile,
            // Deprecated --logappend is a no-op; appending is the default, --logclear opts into clearing
            FileClear = options.LogClear,
            Elevate = options.LogElevate,
        };

    public static ILogger Create(Options? options = null)
    {
        LogEventLevel userLevel = options?.Level ?? LogEventLevel.Information;
        bool elevate = options?.Elevate ?? false;

        // When elevation is enabled the per-file filter must be able to reveal Information events even
        // when the user floor is higher (e.g. Warning), so the pipeline gate is lowered to the more
        // verbose of the user level and Information. Serilog orders Verbose(0) < ... < Fatal(5), so the
        // smaller enum value is the more verbose level and Math.Min selects it. When elevation is
        // disabled the gate is simply the user level and no filter is installed.
        LogEventLevel gate = elevate
            ? (LogEventLevel)Math.Min((int)userLevel, (int)LogEventLevel.Information)
            : userLevel;

        // Enable Serilog debug output to the console
        SelfLog.Enable(Console.Error);

        // Logger configuration
        LoggerConfiguration loggerConfiguration = new LoggerConfiguration()
            .MinimumLevel.Is(gate)
            // LogOverride context always emits regardless of the configured level (summary and banner lines)
            .MinimumLevel.Override(typeof(Extensions.LogOverride).FullName!, LogEventLevel.Verbose)
            .Enrich.WithThreadId();

        // The per-file filter enforces the user level as the per-file floor and self-elevates to
        // Information after a warning or error; only installed when elevation is enabled
        if (elevate)
        {
            _ = loggerConfiguration.Filter.With(new PerFileLogLevel.Filter(userLevel));
        }

        // Log to the console
        _ = loggerConfiguration.WriteTo.Console(
            theme: AnsiConsoleTheme.Code,
            // Remove lj from default to quote strings
            outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] <{ThreadId}> {Message}{NewLine}{Exception}",
            formatProvider: CultureInfo.InvariantCulture
        );

        // Log to file
        if (options?.File is { } file)
        {
            // Clear is opt-in; the default is to append to any existing log file
            if (options.FileClear && file.Exists)
            {
                file.Delete();
            }

            // Roll to a numbered file (<base>_001.<ext>) when the 1 GiB size limit is reached
            _ = loggerConfiguration.WriteTo.Async(action =>
                action.File(
                    file.FullName,
                    rollOnFileSizeLimit: true,
                    fileSizeLimitBytes: FileSizeLimitBytes,
                    retainedFileCountLimit: RetainedFileCountLimit,
                    // Remove lj from default to quote strings
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] <{ThreadId}> {Message}{NewLine}{Exception}",
                    formatProvider: CultureInfo.InvariantCulture
                )
            );
        }

        // Create logger
        return loggerConfiguration.CreateLogger();
    }

    // Bridge the Serilog logger to a Microsoft.Extensions.Logging factory, so libraries that log
    // through ILoggerFactory (e.g. ptr727.LanguageTags) share the same sinks and configuration
    public static Microsoft.Extensions.Logging.ILoggerFactory CreateLoggerFactory(ILogger logger) =>
        new Serilog.Extensions.Logging.SerilogLoggerFactory(logger);
}
