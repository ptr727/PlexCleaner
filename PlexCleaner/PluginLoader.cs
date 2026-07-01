#if PLUGINS
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Serilog;

namespace PlexCleaner;

// Loads a user plugin assembly, sharing the host's already-loaded assemblies so the IProcessPlugin
// type identity and the static Program.Config / Tools are the same instance the plugin binds to
internal sealed class PluginLoadContext(string pluginPath)
    : AssemblyLoadContext(isCollectible: false)
{
    private readonly AssemblyDependencyResolver _resolver = new(pluginPath);

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Defer to the default context for host-provided assemblies (PlexCleaner, Serilog, framework)
        // so shared types keep a single identity. Match the full identity, not just the simple name, so
        // a plugin's private dependency that shares a simple name with a host assembly but differs in
        // version or strong name still loads its own compatible copy in isolation.
        if (
            Default.Assemblies.Any(item =>
                string.Equals(
                    item.GetName().FullName,
                    assemblyName.FullName,
                    StringComparison.Ordinal
                )
            )
        )
        {
            return null;
        }
        string? path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path != null ? LoadFromAssemblyPath(path) : null;
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        // Probe the plugin directory for native dependencies the plugin bundles
        string? path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path != null ? LoadUnmanagedDllFromPath(path) : nint.Zero;
    }
}

internal sealed class PluginHost(ILogger logger) : IPluginHost
{
    public int PluginApiVersion => PluginApi.Version;
    public string ApplicationVersion => AssemblyVersion.GetReleaseVersion();
    public string OperatingSystem => RuntimeInformation.OSDescription;
    public string Runtime => AssemblyVersion.GetRuntimeVersion();
    public ILogger Logger => logger;
}

public static class PluginLoader
{
    [RequiresUnreferencedCode(
        "Loads a plugin assembly and discovers IProcessPlugin via reflection"
    )]
    [RequiresDynamicCode("Loads a plugin assembly at runtime")]
    public static IProcessPlugin? Load(string assemblyPath)
    {
        // An empty path would resolve to the current directory and report a misleading error
        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            Log.Error("Plugin assembly path is empty");
            return null;
        }

        // Resolve inside the try so a malformed path (GetFullPath throws) fails cleanly with a log
        string fullPath = assemblyPath;
        try
        {
            fullPath = Path.GetFullPath(assemblyPath);
            if (!File.Exists(fullPath))
            {
                Log.Error("Plugin assembly not found : {AssemblyPath}", fullPath);
                return null;
            }

            PluginLoadContext context = new(fullPath);
            Assembly assembly = context.LoadFromAssemblyPath(fullPath);

            // Require exactly one concrete IProcessPlugin implementation
            List<Type> pluginTypes =
            [
                .. assembly
                    .GetTypes()
                    .Where(type =>
                        typeof(IProcessPlugin).IsAssignableFrom(type)
                        && type is { IsInterface: false, IsAbstract: false }
                    ),
            ];
            if (pluginTypes.Count != 1)
            {
                Log.Error(
                    "Plugin assembly must contain exactly one IProcessPlugin implementation, found {Count} : {AssemblyPath}",
                    pluginTypes.Count,
                    fullPath
                );
                return null;
            }

            if (Activator.CreateInstance(pluginTypes[0]) is not IProcessPlugin plugin)
            {
                Log.Error("Failed to create plugin instance : {Type}", pluginTypes[0].FullName);
                return null;
            }

            // The plugin validates host compatibility against PluginApi.Version
            if (!plugin.Initialize(new PluginHost(Log.Logger)))
            {
                Log.Error("Plugin reported incompatible with the host : {Name}", plugin.Name);
                return null;
            }

            Log.Information("Loaded plugin : {Name} : {AssemblyPath}", plugin.Name, fullPath);
            return plugin;
        }
        catch (Exception e) when (Log.Logger.LogAndHandle(e))
        {
            // Include the assembly path since LogAndHandle only reports the caller member
            Log.Error("Failed to load plugin : {AssemblyPath}", fullPath);
            return null;
        }
    }
}
#endif
