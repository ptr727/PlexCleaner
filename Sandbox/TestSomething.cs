using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace Sandbox;

internal sealed class TestSomething(Dictionary<string, JsonElement> settings) : Program(settings)
{
    protected override Task<int> Sandbox(string[] args) => Task.FromResult(0);
}
