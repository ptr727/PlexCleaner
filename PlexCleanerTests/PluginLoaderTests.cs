using AwesomeAssertions;
using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

public class PluginLoaderTests
{
    private static string ExamplePluginPath =>
        Path.Combine(AppContext.BaseDirectory, "MatroskaHeaderCleanup.dll");

    [Fact]
    public void Load_ExamplePlugin_ReturnsInitializedPlugin()
    {
        IProcessPlugin? plugin = PluginLoader.Load(ExamplePluginPath);

        // A non-null typed return proves the loaded type satisfies the host's IProcessPlugin
        // (single shared type identity) and that Initialize accepted the host
        _ = plugin.Should().NotBeNull();
        IProcessPlugin loaded = plugin;
        _ = loaded.Name.Should().Be("MatroskaHeaderCleanup");
        _ = loaded.GetType().Assembly.GetName().Name.Should().Be("MatroskaHeaderCleanup");
    }

    [Fact]
    public void Load_MissingAssembly_ReturnsNull()
    {
        IProcessPlugin? plugin = PluginLoader.Load(
            Path.Combine(AppContext.BaseDirectory, "DoesNotExist.dll")
        );

        _ = plugin.Should().BeNull();
    }
}
