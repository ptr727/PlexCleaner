#region

using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using InsaneGenius.Utilities;
using PlexCleaner;
using Serilog;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

#endregion

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
    private const string JsonConfigFile = "Sandbox.json";

    private static readonly JsonSerializerOptions s_jsonReadOptions = new()
    {
        AllowTrailingCommas = true,
        IncludeFields = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
        ReadCommentHandling = JsonCommentHandling.Skip,
        PropertyNameCaseInsensitive = true,
    };

    private readonly Dictionary<string, JsonElement>? _settings;

    private Program(Dictionary<string, JsonElement>? settings) => _settings = settings;

    public static async Task<int> Main(string[] args)
    {
        // Create default commandline options and config
        PlexCleaner.Program.Options = new CommandLineOptions();
        PlexCleaner.Program.Config = new ConfigFileJsonSchema();
        PlexCleaner.Program.Config.SetDefaults();

        // Set runtime options
        SetRuntimeOptions();

        // Create logger
        SelfLog.Enable(Console.Error);
        Log.Logger = new LoggerConfiguration()
            .Enrich.WithThreadId()
            .WriteTo.Console(
                theme: AnsiConsoleTheme.Code,
                restrictedToMinimumLevel: LogEventLevel.Debug,
                outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] <{ThreadId}> {Message}{NewLine}{Exception}",
                formatProvider: CultureInfo.InvariantCulture
            )
            .CreateLogger();
        LogOptions.Logger = Log.Logger;

        // Get settings
        Dictionary<string, JsonElement>? settings = null;
        if (GetSettingsFilePath(JsonConfigFile) is { } settingsPath)
        {
            await using FileStream jsonStream = File.OpenRead(settingsPath);
            settings = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                jsonStream,
                s_jsonReadOptions
            );
            Log.Information("Settings loaded : {FilePath}", settingsPath);
        }

        // Derive from Program and implement Sandbox()
        Program program = new(settings);
        int ret = await program.Sandbox(args);

        // Done
        await Log.CloseAndFlushAsync();
        return ret;
    }

    protected virtual Task<int> Sandbox(string[] args) => Task.FromResult(0);

    public static void SetRuntimeOptions()
    {
        FileEx.Options.RetryCount = PlexCleaner.Program.Config.MonitorOptions.FileRetryCount;
        FileEx.Options.RetryWaitTime = PlexCleaner.Program.Config.MonitorOptions.FileRetryWaitTime;

        PlexCleaner.Program.Options.ThreadCount = PlexCleaner.Program.Options.Parallel
            ? PlexCleaner.Program.Options.ThreadCount == 0
                ? Math.Clamp(Environment.ProcessorCount / 2, 1, 4)
                : Math.Clamp(PlexCleaner.Program.Options.ThreadCount, 1, Environment.ProcessorCount)
            : 1;
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

    public JsonElement? GetSettingsObject(string key) =>
        _settings?.TryGetValue(key, out JsonElement value) == true ? value : null;

    public Dictionary<string, string> GetSettingsDictionary(string key) =>
        new(
            GetSettingsObject(key)?.Deserialize<Dictionary<string, string>>() ?? [],
            StringComparer.OrdinalIgnoreCase
        );

    public T? GetSettings<T>(string key)
        where T : class => GetSettingsObject(key)?.Deserialize<T>();
}
