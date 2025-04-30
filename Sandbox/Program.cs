using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using PlexCleaner;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace Sandbox;

// Settings:
/*
{
    "class": {
        "key": "value",
    }
}
*/

public class Program
{
    private static int Main()
    {
        // Create default commandline options and config
        PlexCleaner.Program.Options = new CommandLineOptions();
        PlexCleaner.Program.Config = new ConfigFileJsonSchema();
        PlexCleaner.Program.Config.SetDefaults();

        // Set runtime options
        SetRuntimeOptions();

        // Create logger
        Serilog.Debugging.SelfLog.Enable(Console.Error);
        Log.Logger = new LoggerConfiguration()
            .Enrich.WithThreadId()
            .WriteTo.Console(
                theme: AnsiConsoleTheme.Code,
                restrictedToMinimumLevel: LogEventLevel.Debug,
                outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] <{ThreadId}> {Message}{NewLine}{Exception}",
                formatProvider: CultureInfo.InvariantCulture
            )
            .CreateLogger();
        InsaneGenius.Utilities.LogOptions.Logger = Log.Logger;

        // Sandbox tests
        int ret = 0;
        // Program program = new();

        // ClosedCaptions closedCaptions = new(program);
        // int ret = closedCaptions.Test();

        // ProcessFiles processFiles = new(program);
        // int ret = processFiles.Test();

        // Done
        Log.CloseAndFlush();
        return ret;
    }

    public static void SetRuntimeOptions()
    {
        InsaneGenius.Utilities.FileEx.Options.RetryCount = PlexCleaner
            .Program
            .Config
            .MonitorOptions
            .FileRetryCount;
        InsaneGenius.Utilities.FileEx.Options.RetryWaitTime = PlexCleaner
            .Program
            .Config
            .MonitorOptions
            .FileRetryWaitTime;

        PlexCleaner.Program.Options.ThreadCount = PlexCleaner.Program.Options.Parallel
            ? PlexCleaner.Program.Options.ThreadCount == 0
                ? Math.Clamp(Environment.ProcessorCount / 2, 1, 4)
                : Math.Clamp(PlexCleaner.Program.Options.ThreadCount, 1, Environment.ProcessorCount)
            : 1;
    }

    private Program()
    {
        // Get settings
        if (GetSettingsFilePath(JsonConfigFile) is string settingsPath)
        {
            using FileStream jsonStream = File.OpenRead(settingsPath);
            _settings = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                jsonStream,
                s_jsonReadOptions
            );
            Log.Information("Settings loaded : {FilePath}", settingsPath);
        }
    }

    public static string? GetSettingsFilePath(string fileName)
    {
        // Load settings file from current working directory
        string settingsPath = Path.GetFullPath(fileName);
        if (!File.Exists(settingsPath))
        {
            // Try to load settings file from assembly directory
            if (
                Assembly.GetEntryAssembly() is { } entryAssembly
                && Path.GetDirectoryName(entryAssembly.Location) is { } assemblyDirectory
            )
            {
                settingsPath = Path.GetFullPath(Path.Combine(assemblyDirectory, fileName));
            }
        }
        if (!File.Exists(settingsPath))
        {
            Log.Error("File not found : {FilePath}", fileName);
            return null;
        }
        return settingsPath;
    }

    private static readonly JsonSerializerOptions s_jsonReadOptions = new()
    {
        AllowTrailingCommas = true,
        IncludeFields = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
        ReadCommentHandling = JsonCommentHandling.Skip,
        PropertyNameCaseInsensitive = true,
    };

    public JsonElement? GetSettingsObject(string key) =>
        _settings?.TryGetValue(key, out JsonElement value) == true ? value : null;

    public Dictionary<string, string>? GetSettingsDictionary(string key) =>
        new(
            GetSettingsObject(key)?.Deserialize<Dictionary<string, string>>() ?? [],
            StringComparer.OrdinalIgnoreCase
        );

    public T? GetSettings<T>(string key)
        where T : class => GetSettingsObject(key)?.Deserialize<T>();

    private readonly Dictionary<string, JsonElement>? _settings;

    private const string JsonConfigFile = "Sandbox.json";
}
