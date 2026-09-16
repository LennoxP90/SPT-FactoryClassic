using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FactoryClassic.Server;
using FactoryClassic.Shared;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Utils.Json;
using Xunit;

namespace FactoryClassic.Tests;

// Runs the real transform over the real tables. The unit tests around LootMergePolicy cover the
// arithmetic; this covers the thing that actually breaks - producing loose loot the generator cannot
// read, which fails silently as a map with no loot rather than as an error.
public class LootModeTransformTests
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

    static LooseLoot Classic(string map) => JsonSerializer.Deserialize<LooseLoot>(
        File.ReadAllText(Path.Combine(RepoRoot(), "FactoryClassic.Server", "db", "classic", map, "looseLoot.json")), SptJson)!;

    // The shipped tile's loose loot is the donor. Read from the clean install, so this test is skipped
    // rather than failed where that install is absent.
    static LooseLoot? Shipped(string map)
    {
        var path = SptInstall.Locations(map, "looseLoot.json");
        return File.Exists(path) ? JsonSerializer.Deserialize<LooseLoot>(File.ReadAllText(path), SptJson) : null;
    }

    static int Points(LooseLoot loot) => loot.Spawnpoints?.Count() ?? 0;

    static IEnumerable<MongoId> Templates(LooseLoot loot) =>
        (loot.Spawnpoints ?? []).SelectMany(s => s.Template?.Items ?? []).Select(i => i.Template);

    [Fact]
    public void ClassicModeChangesNothing()
    {
        var loot = Classic("factory4_day");
        var before = Templates(loot).Count();

        var report = LootModeTransform.Apply(loot, Shipped("factory4_day"), LootMode.Classic);

        Assert.Equal(0, report.Added);
        Assert.Equal(0, report.Replaced);
        Assert.Equal(before, Templates(loot).Count());
    }

    [Fact]
    public void HybridAddsTheItemsTheClassicTablesNeverHad()
    {
        var shipped = Shipped("factory4_day");
        if (shipped is null) return;   // no clean install on this machine

        var loot = Classic("factory4_day");
        var classicTemplates = Templates(loot).ToHashSet();
        var shippedOnly = Templates(shipped).ToHashSet().Except(classicTemplates).ToHashSet();
        Assert.NotEmpty(shippedOnly);

        var pointsBefore = Points(loot);
        var report = LootModeTransform.Apply(loot, shipped, LootMode.Hybrid);

        Assert.True(report.Added > 0, "hybrid added nothing");
        Assert.Equal(0, report.Replaced);

        // Positions are classic; only what they offer changes.
        Assert.Equal(pointsBefore, Points(loot));

        // Every classic item survives, and most of what only the shipped tile had is now reachable.
        var after = Templates(loot).ToHashSet();
        Assert.True(classicTemplates.IsSubsetOf(after), "hybrid dropped classic items");
        Assert.True(shippedOnly.Intersect(after).Count() > shippedOnly.Count / 2,
            "hybrid reached fewer than half the shipped-only templates");
    }

    [Fact]
    public void ModernReplacesRatherThanAdds()
    {
        var shipped = Shipped("factory4_day");
        if (shipped is null) return;

        var loot = Classic("factory4_day");
        var pointsBefore = Points(loot);

        var report = LootModeTransform.Apply(loot, shipped, LootMode.Modern);

        Assert.True(report.Replaced > 0, "modern replaced nothing");
        Assert.True(report.Added > 0, "modern added nothing");
        Assert.Equal(pointsBefore, Points(loot));
    }

    // The defect that matters. LocationLootGenerator matches itemDistribution against each item's
    // ComposedKey and SKIPS THE WHOLE SPAWN POINT when it cannot, with nothing in the log. So every
    // key must be present, unique within its point, and resolvable.
    [Theory]
    [InlineData(LootMode.Hybrid)]
    [InlineData(LootMode.Modern)]
    public void EveryDistributionKeyStillResolves(string mode)
    {
        var shipped = Shipped("factory4_day");
        if (shipped is null) return;

        var loot = Classic("factory4_day");
        LootModeTransform.Apply(loot, shipped, mode);

        foreach (var spawn in loot.Spawnpoints ?? [])
        {
            var items = (spawn.Template?.Items ?? []).ToList();
            var keys = items.Select(i => i.ComposedKey).Where(k => !string.IsNullOrEmpty(k)).ToList();

            Assert.Equal(keys.Count, keys.Distinct().Count());

            var available = keys.ToHashSet();
            foreach (var entry in spawn.ItemDistribution ?? [])
            {
                var key = entry.ComposedKey?.Key;
                Assert.False(string.IsNullOrEmpty(key), $"distribution entry with no key at {spawn.LocationId}");
                Assert.Contains(key!, available);
                Assert.True((entry.RelativeProbability ?? 0d) > 0d, $"zero weight at {spawn.LocationId}");
            }
        }
    }

    // Ids have to stay ObjectIds or the server refuses the file on the next load.
    [Fact]
    public void AddedItemsGetRealObjectIds()
    {
        var shipped = Shipped("factory4_day");
        if (shipped is null) return;

        var loot = Classic("factory4_day");
        LootModeTransform.Apply(loot, shipped, LootMode.Hybrid);

        var ids = (loot.Spawnpoints ?? []).SelectMany(s => s.Template?.Items ?? []).Select(i => i.Id.ToString()).ToList();
        Assert.All(ids, id => Assert.Matches("^[0-9a-fA-F]{24}$", id));
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }
}
