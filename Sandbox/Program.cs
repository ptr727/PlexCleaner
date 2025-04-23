using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using PlexCleaner;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace Sandbox;

public class Program
{
    private static int Main()
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
                formatProvider: CultureInfo.InvariantCulture
            )
            .CreateLogger();
        InsaneGenius.Utilities.LogOptions.Logger = Log.Logger;

        // Dynamic init
        Program program = new();

        // Sandbox tests
        ClosedCaptions closedCaptions = new(program);
        int ret = closedCaptions.Test();

        Log.CloseAndFlush();
        return ret;
    }

    private Program()
    {
        // Get full path to JSON settings file
        string? settingsPath = GetSettingsFilePath(JsonConfigFile);
        if (settingsPath is not null)
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
            Assembly? entryAssembly = Assembly.GetEntryAssembly();
            Debug.Assert(entryAssembly != null);
            string? assemblyDirectory = Path.GetDirectoryName(entryAssembly.Location);
            Debug.Assert(assemblyDirectory != null);
            settingsPath = Path.GetFullPath(Path.Combine(assemblyDirectory, fileName));
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
        _settings is null ? null
        : _settings.TryGetValue(key, out JsonElement value) ? value
        : null;

    public Dictionary<string, string>? GetSettingsDictionary(string key)
    {
        JsonElement? jsonElement = GetSettingsObject(key);
        return jsonElement == null ? null : jsonElement?.Deserialize<Dictionary<string, string>>();
    }

    private readonly Dictionary<string, JsonElement>? _settings;

    private const string JsonConfigFile = "Sandbox.json";
}
