using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Json;
using SysPath = System.IO.Path;

namespace FactoryClassic.Server;

// Builds the classic Location as a NEW object rather than mutating the shipped one. That is what
// makes the standing invariant structural instead of a promise: the vanilla Location is never written
// to, so a player who never picks classic cannot be affected by anything here.
//
// The LazyLoad members are constructed fresh rather than cloned. LazyLoad holds a deserializer and its
// transformers are additive with no way to remove one, so a cloned instance carrying the vanilla
// loader plus our transformer would be a second, subtly different object. A new LazyLoad that simply
// returns our data has no such ambiguity.
public static class ClassicDataset
{
    public static Location Build(Location vanilla, string dir, string lootMode, JsonUtil jsonUtil, out ClassicCounts counts)
    {
        // Start from a copy of the shipped base so the 4.1-only keys survive, then overlay only what
        // is bound to the geometry.
        var merged = Reshape(vanilla.Base, Read<LocationBase>(jsonUtil, dir, "base.classic.json"), jsonUtil);

        var looseLoot = Read<LooseLoot>(jsonUtil, dir, "looseLoot.json");

        // The shipped tile's own loose loot is the donor pool: it is where the items added to EFT
        // since November 2024 live, and where on the map they sit.
        var loot = LootModeTransform.Apply(looseLoot, vanilla.LooseLoot?.Value, lootMode);
        var staticContainers = Read<StaticContainerDetails>(jsonUtil, dir, "staticContainers.json");
        var staticLoot = Read<Dictionary<MongoId, StaticLootDetails>>(jsonUtil, dir, "staticLoot.json");

        counts = new ClassicCounts(
            merged.SpawnPointParams?.Count() ?? -1,
            merged.Exits?.Count() ?? -1,
            merged.BossLocationSpawn?.Count ?? -1,
            looseLoot.Spawnpoints?.Count() ?? -1,
            staticContainers.StaticContainers?.Count() ?? -1,
            loot);

        return new Location
        {
            Base = merged,
            LooseLoot = new LazyLoad<LooseLoot>(() => looseLoot),
            StaticContainers = new LazyLoad<StaticContainerDetails>(() => staticContainers),
            StaticLoot = new LazyLoad<Dictionary<MongoId, StaticLootDetails>>(() => staticLoot),
            StaticAmmo = Read<Dictionary<string, IEnumerable<StaticAmmoDetails>>>(jsonUtil, dir, "staticAmmo.json"),
            Statics = Read<StaticContainer>(jsonUtil, dir, "statics.json"),
            AllExtracts = Read<IEnumerable<AllExtractsExit>>(jsonUtil, dir, "allExtracts.json"),
        };
    }

    // A round trip through JSON is the cheapest faithful copy of the shipped base: it keeps every
    // 4.1-only key without naming them, which a hand-written copy would have to and would then drift
    // from as SPT adds more.
    private static LocationBase Reshape(LocationBase vanilla, LocationBase classic, JsonUtil jsonUtil)
    {
        var copy = jsonUtil.Deserialize<LocationBase>(jsonUtil.Serialize(vanilla)!)!;
        BaseMerge.Apply(copy, classic);
        return copy;
    }

    private static T Read<T>(JsonUtil jsonUtil, string dir, string file) =>
        jsonUtil.Deserialize<T>(File.ReadAllText(SysPath.Combine(dir, file)))!;
}

public readonly record struct ClassicCounts(
    int SpawnPoints, int Exits, int BossEntries, int LooseLootPoints, int Containers,
    LootModeTransform.Report Loot)
{
    // Six boss entries is the number that proves the PMC merge held: 4.1 places PMCs through
    // BossLocationSpawn and 3.9.8 did not, so two would mean they were deleted.
    public override string ToString() =>
        $"{SpawnPoints} spawn point(s), {Exits} exit(s), {BossEntries} boss entr(ies), " +
        $"{LooseLootPoints} loose loot point(s), {Containers} container(s); {Loot}";
}
