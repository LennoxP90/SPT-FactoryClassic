using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Utils;
using SysPath = System.IO.Path;
using Location = SPTarkov.Server.Core.Models.Eft.Common.Location;

namespace FactoryClassic.Server.Halloween;

public sealed class HalloweenCorpse
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("tpl")] public MongoId Tpl { get; set; }
}

/// <summary>
/// The event's lootable corpses join the classic container list only on a Halloween raid. Their pool is 4.1
/// Factory's own PMC_dead pool, since the classic tables have none; civilians and the lab assistants (whose
/// template 4.1 lacks, so they ship as dead_civilian) use the same one.
/// </summary>
public static class HalloweenLoot
{
    public static readonly MongoId PmcDead = new("6582e6d7b14c3f72eb071420");
    public static readonly MongoId DeadCivilian = new("658420d8085fea07e674cdb6");

    public static int Apply(Location classic, Location vanilla, string root, string locationId, JsonUtil jsonUtil, Func<string, bool> isHalloween)
    {
        var corpses = jsonUtil.Deserialize<List<HalloweenCorpse>>(File.ReadAllText(SysPath.Combine(root, "db", "halloween", "corpses.json")))!;
        var pool = vanilla.StaticLoot!.Value![PmcDead];
        var loot = classic.StaticLoot!.Value!;
        loot[PmcDead] = pool;
        loot[DeadCivilian] = pool;

        var details = classic.StaticContainers!.Value!;
        details.StaticContainers = With(details.StaticContainers.ToList(), corpses.Select(Entry).ToList(), () => isHalloween(locationId));
        return corpses.Count;
    }

    // Lazy on purpose: the generator reads the list at each raid start, and that read is what rolls the raid.
    public static IEnumerable<StaticContainerData> With(List<StaticContainerData> plain, List<StaticContainerData> corpses, Func<bool> halloween)
    {
        foreach (var c in plain) yield return c;
        if (!halloween()) yield break;
        foreach (var c in corpses) yield return c;
    }

    public static StaticContainerData Entry(HalloweenCorpse corpse)
    {
        var root = new MongoId();
        return new StaticContainerData
        {
            Probability = 1,
            Template = new SpawnpointTemplate
            {
                Id = corpse.Id,
                IsContainer = true,
                IsAlwaysSpawn = true,
                Position = new Vector3(0, 0, 0),
                Rotation = new Vector3(0, 0, 0),
                GroupPositions = [],
                Root = root.ToString(),
                Items = [new SptLootItem { Id = root, Template = corpse.Tpl, Upd = new Upd { StackObjectsCount = 1 } }],
            },
        };
    }
}
