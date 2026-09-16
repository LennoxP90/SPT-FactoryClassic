using System.Collections.Generic;
using System.Linq;
using FactoryClassic.Shared;
using Xunit;

namespace FactoryClassic.Tests;

// Fixtures are the real BossLocationSpawn lists, read on 2026-09-14 from SPT 4.1's factory4_day and
// factory4_night and from the SPT 3.9.8 server in the 0.14.9 archive.
public class BossSpawnMergePolicyTests
{
    sealed record Spawn(string Name, int Chance);

    static readonly Spawn[] ShippedDay =
    {
        new("bossTagilla", 30), new("pmcUSEC", 50), new("pmcUSEC", 50),
        new("pmcBEAR", 50), new("pmcBEAR", 50), new("gifter", 0),
    };

    static readonly Spawn[] ShippedNight =
    {
        new("bossTagilla", 30), new("sectantPriest", 20),
        new("pmcBEAR", 50), new("pmcBEAR", 50), new("pmcUSEC", 50), new("pmcUSEC", 50),
    };

    static readonly Spawn[] ClassicDay = { new("bossTagilla", 35) };
    static readonly Spawn[] ClassicNight = { new("bossTagilla", 35), new("sectantPriest", 2) };

    static List<Spawn> Merge(IReadOnlyList<Spawn> shipped, IReadOnlyList<Spawn> classic)
        => BossSpawnMergePolicy.Merge(shipped, classic, s => s.Name);

    // The defect this whole policy exists to prevent: replacing the list wholesale deletes every PMC
    // and yields a raid of scavs and Tagilla, with nothing in the log.
    [Fact]
    public void KeepsEveryPmcEntryOnDay()
    {
        var merged = Merge(ShippedDay, ClassicDay);
        Assert.Equal(2, merged.Count(s => s.Name == "pmcUSEC"));
        Assert.Equal(2, merged.Count(s => s.Name == "pmcBEAR"));
    }

    [Fact]
    public void KeepsEveryPmcEntryOnNight()
    {
        var merged = Merge(ShippedNight, ClassicNight);
        Assert.Equal(2, merged.Count(s => s.Name == "pmcUSEC"));
        Assert.Equal(2, merged.Count(s => s.Name == "pmcBEAR"));
    }

    [Fact]
    public void TakesTheClassicTagillaChance()
    {
        Assert.Equal(35, Merge(ShippedDay, ClassicDay).Single(s => s.Name == "bossTagilla").Chance);
    }

    // Cultists were on classic Factory at night only, and at 2% against 4.1's 20%.
    [Fact]
    public void TakesTheClassicCultistChance()
    {
        Assert.Equal(2, Merge(ShippedNight, ClassicNight).Single(s => s.Name == "sectantPriest").Chance);
    }

    [Fact]
    public void ClassicDayHasNoCultists()
    {
        Assert.DoesNotContain(Merge(ShippedDay, ClassicDay), s => s.Name == "sectantPriest");
    }

    [Fact]
    public void KeepsTheGifter()
    {
        Assert.Contains(Merge(ShippedDay, ClassicDay), s => s.Name == "gifter");
    }

    [Fact]
    public void AddsAClassicOwnedEntryMissingFromShipped()
    {
        var merged = Merge(new[] { new Spawn("pmcBEAR", 50) }, ClassicNight);
        Assert.Contains(merged, s => s.Name == "bossTagilla" && s.Chance == 35);
        Assert.Contains(merged, s => s.Name == "sectantPriest" && s.Chance == 2);
        Assert.Contains(merged, s => s.Name == "pmcBEAR");
    }

    [Fact]
    public void NoEntryIsDuplicated()
    {
        var merged = Merge(ShippedNight, ClassicNight);
        Assert.Single(merged, s => s.Name == "bossTagilla");
        Assert.Single(merged, s => s.Name == "sectantPriest");
    }

    [Fact]
    public void NullListsAreTreatedAsEmpty()
    {
        Assert.Empty(BossSpawnMergePolicy.Merge<Spawn>(null, null, s => s.Name));
        Assert.Single(BossSpawnMergePolicy.Merge(null, ClassicDay, s => s.Name));
    }

    [Fact]
    public void OnlyTagillaAndCultistsAreClassicOwned()
    {
        Assert.Equal(new[] { "bossTagilla", "sectantPriest" }, BossSpawnMergePolicy.ClassicOwned);
    }
}
