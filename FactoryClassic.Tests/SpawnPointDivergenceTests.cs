using System.IO;
using System.Linq;
using System.Text.Json;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Utils.Json;
using Xunit;

namespace FactoryClassic.Tests;

// Why the timing of the variant swap matters, expressed as data.
//
// A player fell through the classic tile on the first raid after the quest gate went in. The server
// had served spawn point 91c08464-492f-443d-8632-54e01d6bdd20, which exists only in the shipped
// tables: SPT and Fika build a raid's location from whatever the location table holds when the raid
// is CREATED, which is earlier than any raid-start route, so the swap landed after the raid was
// already built.
//
// These assertions are the standing reminder of why the two datasets can never be used
// interchangeably. They cannot catch the ordering bug itself - that is live request sequencing - but
// they fail loudly if the datasets ever converge, which is the other way this stops being dangerous.
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

    // The far easier trap to miss: the ids the two share are the SAME ids at DIFFERENT places. BSG
    // kept the identifiers and moved the points when they rebuilt the map, so a spawn point can look
    // perfectly valid, match by id, and still be tens of metres from where the classic geometry is.
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
