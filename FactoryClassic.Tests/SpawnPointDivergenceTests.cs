using System.IO;
using System.Linq;
using System.Text.Json;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Utils.Json;
using Xunit;

namespace FactoryClassic.Tests;

/// <summary>
/// Why the timing of the variant swap matters, expressed as data. These cannot catch the ordering
/// bug itself, which is live request sequencing, but they fail loudly if the two datasets ever
/// converge, which is the other way it stops being dangerous.
/// </summary>
public class SpawnPointDivergenceTests
{
    static readonly JsonSerializerOptions SptJson = Build();

    static JsonSerializerOptions Build()
    {
        var options = new JsonSerializerOptions();
        foreach (var converter in new SptJsonConverterRegistrator().GetJsonConverters())
            options.Converters.Add(converter);
        return options;
    }

    static string RepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SPT-FactoryClassic.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    static LocationBase Classic(string map) => JsonSerializer.Deserialize<LocationBase>(
        File.ReadAllText(Path.Combine(RepoRoot(), "FactoryClassic.Server", "db", "classic", map, "base.classic.json")), SptJson)!;

    static LocationBase? Vanilla(string map)
    {
        var path = SptInstall.Locations(map, "base.json");
        return File.Exists(path) ? JsonSerializer.Deserialize<LocationBase>(File.ReadAllText(path), SptJson) : null;
    }

    // A bot spawn point that names no zone gives its bot no patrol graph, and the 3.9.8 tables name
    // none at all. Observed as a whole raid of bots standing where they spawned, Tagilla excepted:
    // his BossLocationSpawn is the one entry that names BotZone.
    [Theory]
    [InlineData("factory4_day")]
    [InlineData("factory4_night")]
    public void EveryBotSpawnPointNamesTheZoneTheSceneCarries(string map)
    {
        var bot = Classic(map).SpawnPointParams!
            .Where(s => s.Categories != null && s.Categories.Count() == 1 && s.Categories.First() == "Bot")
            .ToList();

        Assert.Equal(19, bot.Count);
        Assert.All(bot, s => Assert.Equal("BotZone", s.BotZoneName));

        var vanilla = Vanilla(map);
        if (vanilla is null) return;   // no clean install on this machine

        // The shipped tile is the authority on what the zone is called.
        var shipped = vanilla.SpawnPointParams!
            .Where(s => s.Categories != null && s.Categories.Contains("Bot"))
            .Select(s => s.BotZoneName)
            .Distinct()
            .ToList();
        Assert.Equal(new[] { "BotZone" }, shipped);
    }
    [Theory]
    [InlineData("factory4_day")]
    [InlineData("factory4_night")]
    public void TheShippedTablesCarrySpawnPointsTheClassicTileHasNowhereToPut(string map)
    {
        var vanilla = Vanilla(map);
        if (vanilla is null) return;   // no clean install on this machine

        var classicIds = Classic(map).SpawnPointParams!.Select(s => s.Id).ToHashSet();
        var vanillaOnly = vanilla.SpawnPointParams!.Where(s => !classicIds.Contains(s.Id)).ToList();

        Assert.NotEmpty(vanillaOnly);

        // Every one of them is a player spawn, which is why serving the wrong table drops a player
        // rather than merely misplacing a bot.
        Assert.All(vanillaOnly, spawn => Assert.Contains("Player", spawn.Categories!));
    }

    // The trap that is easier to miss: the ids the two share are the SAME ids at DIFFERENT places,
    // so a spawn point can match by id and still be tens of metres from any classic geometry.
    [Theory]
    [InlineData("factory4_day")]
    [InlineData("factory4_night")]
    public void SharedSpawnPointIdsSitAtDifferentPlacesOnTheTwoTiles(string map)
    {
        var vanilla = Vanilla(map);
        if (vanilla is null) return;

        var classic = Classic(map).SpawnPointParams!.ToDictionary(s => s.Id);
        var moved = vanilla.SpawnPointParams!
            .Where(s => classic.ContainsKey(s.Id))
            .Count(s =>
            {
                var here = s.Position!.Value;
                var there = classic[s.Id].Position!.Value;
                var dx = here.X - there.X;
                var dy = here.Y - there.Y;
                var dz = here.Z - there.Z;
                return (dx * dx) + (dy * dy) + (dz * dz) > 0.25d;
            });

        Assert.True(moved > 0,
            "the two tiles' shared spawn points are in the same places, which they should not be");
    }
}
