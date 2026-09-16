using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FactoryClassic.Shared;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Utils.Json;
using Xunit;

namespace FactoryClassic.Tests;

// Exercises the committed 3.9.8 tables themselves rather than synthetic rows. If an extraction ever
// pulls the wrong file, or BSG's data moves under us, these fail rather than a raid doing it.
public class ClassicTableTests
{
    static string RepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SPT-FactoryClassic.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    // SPT's own converters, not plain System.Text.Json. Without StringToMongoIdConverter the very
    // first "_Id" fails to bind, which is a property of the reader rather than of the data: the value
    // is identical in the 3.9.8 and 4.1 files. The server reads these through JsonUtil, which
    // registers the same set.
    static readonly JsonSerializerOptions SptJson = BuildOptions();

    static JsonSerializerOptions BuildOptions()
    {
        var options = new JsonSerializerOptions();
        foreach (var converter in new SptJsonConverterRegistrator().GetJsonConverters())
            options.Converters.Add(converter);
        return options;
    }

    static LocationBase ClassicBase(string map)
    {
        var path = Path.Combine(RepoRoot(), "FactoryClassic.Server", "db", "classic", map, "base.classic.json");
        Assert.True(File.Exists(path), $"missing {path}; run scripts/Extract-ClassicTables.ps1");
        return JsonSerializer.Deserialize<LocationBase>(File.ReadAllText(path), SptJson)!;
    }

    [Fact]
    public void ClassicDayIsThePreReworkTile()
    {
        var day = ClassicBase("factory4_day");
        Assert.Equal(120, day.SpawnPointParams!.Count());
        Assert.Equal(new[] { "Cellars", "Gate 3", "Gate 0", "Gate m" }, day.Exits.Select(e => e.Name));
        Assert.Single(day.BossLocationSpawn);
        Assert.Equal("bossTagilla", day.BossLocationSpawn[0].BossName);
    }

    // Cultists were on classic Factory at night only, which is the difference between the two files.
    [Fact]
    public void ClassicNightAddsCultists()
    {
        var night = ClassicBase("factory4_night");
        Assert.Equal(2, night.BossLocationSpawn.Count);
        Assert.Contains(night.BossLocationSpawn, b => b.BossName == "sectantPriest");
    }

    // Gate_o exists only on the reworked tile, so the classic exit list must not mention it.
    [Fact]
    public void NoReworkOnlyExitSurvives()
    {
        foreach (var map in FactoryScenes.ServerNames)
            Assert.DoesNotContain(ClassicBase(map).Exits, e => e.Name == "Gate_o");
    }

    // The whole point of the per-name merge, run against the real classic file and a shipped list
    // shaped like 4.1's: one Tagilla, four PMC entries and a gifter.
    [Fact]
    public void MergeKeepsThePmcEntriesFromTheRealFile()
    {
        var shipped = new List<BossLocationSpawn>
        {
            new() { BossName = "bossTagilla", BossChance = 30 },
            new() { BossName = "pmcUSEC", BossChance = 50 },
            new() { BossName = "pmcUSEC", BossChance = 50 },
            new() { BossName = "pmcBEAR", BossChance = 50 },
            new() { BossName = "pmcBEAR", BossChance = 50 },
            new() { BossName = "gifter", BossChance = 0 },
        };

        var merged = BossSpawnMergePolicy.Merge(
            shipped, ClassicBase("factory4_day").BossLocationSpawn, b => b.BossName);

        Assert.Equal(4, merged.Count(b => b.BossName!.StartsWith("pmc")));
        Assert.Contains(merged, b => b.BossName == "gifter");
        Assert.Equal(35, merged.Single(b => b.BossName == "bossTagilla").BossChance);
    }

    [Fact]
    public void EveryTableFileIsPresentForBothMaps()
    {
        string[] files =
        {
            "allExtracts.json", "base.classic.json", "looseLoot.json",
            "staticAmmo.json", "staticContainers.json", "staticLoot.json", "statics.json",
        };

        foreach (var map in FactoryScenes.ServerNames)
        foreach (var file in files)
        {
            var path = Path.Combine(RepoRoot(), "FactoryClassic.Server", "db", "classic", map, file);
            Assert.True(File.Exists(path), $"missing {file} for {map}");
            Assert.True(new FileInfo(path).Length > 0, $"empty {file} for {map}");
        }
    }
}
