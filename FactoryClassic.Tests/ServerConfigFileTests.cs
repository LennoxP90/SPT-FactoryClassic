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
        File.WriteAllText(_path, """{ "lootMode": "hybrid", "questGate": "hide", "extra": 7 }""");
        var config = ServerConfigFile.Read(_path);
        config.QuestGate = "off";
        ServerConfigFile.Write(_path, config);

        var json = JsonNode.Parse(File.ReadAllText(_path))!;
        Assert.Equal("hybrid", (string?)json["lootMode"]);
        Assert.Equal("off", (string?)json["questGate"]);
        Assert.Equal(7, (int?)json["extra"]);
    }

    // Halloween is a variant the player picks now, so a chance left over from 1.1.0 is dropped on write.
    [Fact]
    public void WriteDropsTheRetiredHalloweenChance()
    {
        File.WriteAllText(_path, """{ "lootMode": "classic", "halloweenChance": 50 }""");
        ServerConfigFile.Write(_path, ServerConfigFile.Read(_path));
        Assert.Null(JsonNode.Parse(File.ReadAllText(_path))!["halloweenChance"]);
    }

    [Fact]
    public void ReadRoundTrips()
    {
        ServerConfigFile.Write(_path, new ServerConfig { QuestGate = "hide" });
        Assert.Equal("hide", ServerConfigFile.Read(_path).QuestGate);
    }

    [Fact]
    public void ReadMalformedReturnsDefaults()
    {
        File.WriteAllText(_path, "{ not json");
        Assert.Equal("warn", ServerConfigFile.Read(_path).QuestGate);
    }
}
