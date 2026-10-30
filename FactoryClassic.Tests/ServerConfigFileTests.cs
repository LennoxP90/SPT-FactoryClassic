using System.Text.Json.Nodes;
using FactoryClassic.Server;
using Xunit;

namespace FactoryClassic.Tests;

public class ServerConfigFileTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"fc-config-{Guid.NewGuid():N}.json");

    public void Dispose() => File.Delete(_path);

    [Fact]
    public void WritePreservesOtherKeys()
    {
        File.WriteAllText(_path, """{ "lootMode": "hybrid", "questGate": "hide", "extra": 7, "halloweenChance": 50 }""");
        var config = ServerConfigFile.Read(_path);
        config.HalloweenChance = 30;
        ServerConfigFile.Write(_path, config);

        var json = JsonNode.Parse(File.ReadAllText(_path))!;
        Assert.Equal("hybrid", (string?)json["lootMode"]);
        Assert.Equal("hide", (string?)json["questGate"]);
        Assert.Equal(7, (int?)json["extra"]);
        Assert.Equal(30, (int?)json["halloweenChance"]);
    }

    [Fact]
    public void ReadRoundTrips()
    {
        ServerConfigFile.Write(_path, new ServerConfig { HalloweenChance = 30 });
        Assert.Equal(30, ServerConfigFile.Read(_path).HalloweenChance);
    }

    [Fact]
    public void ReadMalformedReturnsDefaults()
    {
        File.WriteAllText(_path, "{ not json");
        Assert.Equal(50, ServerConfigFile.Read(_path).HalloweenChance);
    }
}
