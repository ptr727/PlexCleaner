using System.Text.Json;

namespace Sandbox;

internal sealed class TestSomething(Dictionary<string, JsonElement> settings) : Program(settings)
{
    protected override Task<int> Sandbox(string[] args) => Task.FromResult(0);
}
