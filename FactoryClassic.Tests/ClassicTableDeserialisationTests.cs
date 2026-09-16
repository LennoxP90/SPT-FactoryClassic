using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using FactoryClassic.Shared;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Utils.Json;
using Xunit;

namespace FactoryClassic.Tests;

// Deserialises EVERY classic table into the exact type ClassicLocationMod reads it as, through SPT's
// own converters.
//
// This exists because the first server start crashed on looseLoot.json and the existing tests could
// not have caught it: they read base.classic.json and nothing else, so the one file under test was
// the one file that worked. Comparing top-level keys between versions is not a schema check either -
// the keys matched exactly while the TYPE of the values inside had changed, 3.9.8 holding item ids as
// integer hashes where 4.1 requires a 24-character ObjectId.
public class ClassicTableDeserialisationTests
{
    static readonly JsonSerializerOptions SptJson = BuildOptions();
    static readonly Regex ObjectId = new("^[0-9a-fA-F]{24}$");

    static JsonSerializerOptions BuildOptions()
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

    static T Read<T>(string map, string file)
    {
        var path = Path.Combine(RepoRoot(), "FactoryClassic.Server", "db", "classic", map, file);
        Assert.True(File.Exists(path), $"missing {path}");
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), SptJson)!;
    }

    public static IEnumerable<object[]> Maps() => FactoryScenes.ServerNames.Select(m => new object[] { m });

    // One test per table, each into the type the server actually asks for, so a failure names the file.
    [Theory, MemberData(nameof(Maps))]
    public void BaseDeserialises(string map) => Assert.NotNull(Read<LocationBase>(map, "base.classic.json"));

    [Theory, MemberData(nameof(Maps))]
    public void LooseLootDeserialises(string map)
    {
        var loot = Read<LooseLoot>(map, "looseLoot.json");
        Assert.NotNull(loot.Spawnpoints);
        Assert.NotEmpty(loot.Spawnpoints!);
    }

    [Theory, MemberData(nameof(Maps))]
    public void StaticContainersDeserialise(string map) => Assert.NotNull(Read<StaticContainerDetails>(map, "staticContainers.json"));

    [Theory, MemberData(nameof(Maps))]
    public void StaticLootDeserialises(string map) => Assert.NotEmpty(Read<Dictionary<MongoId, StaticLootDetails>>(map, "staticLoot.json"));

    [Theory, MemberData(nameof(Maps))]
    public void StaticAmmoDeserialises(string map) => Assert.NotEmpty(Read<Dictionary<string, IEnumerable<StaticAmmoDetails>>>(map, "staticAmmo.json"));

    [Theory, MemberData(nameof(Maps))]
    public void StaticsDeserialise(string map) => Assert.NotNull(Read<StaticContainer>(map, "statics.json"));

    [Theory, MemberData(nameof(Maps))]
    public void AllExtractsDeserialise(string map) => Assert.NotEmpty(Read<IEnumerable<AllExtractsExit>>(map, "allExtracts.json"));

    // The migration's own invariants, asserted on the committed files rather than on its output.
    [Theory, MemberData(nameof(Maps))]
    public void EveryLooseLootIdIsAnObjectId(string map)
    {
        var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(RepoRoot(), "FactoryClassic.Server", "db", "classic", map, "looseLoot.json")));

        foreach (var section in new[] { "spawnpoints", "spawnpointsForced" })
        foreach (var spawn in document.RootElement.GetProperty(section).EnumerateArray())
        foreach (var item in spawn.GetProperty("template").GetProperty("Items").EnumerateArray())
        {
            Assert.Matches(ObjectId, item.GetProperty("_id").GetString()!);
            if (item.TryGetProperty("parentId", out var parent) && parent.ValueKind == JsonValueKind.String)
                Assert.Matches(ObjectId, parent.GetString()!);
        }
    }

    // LocationLootGenerator matches itemDistribution against each item's composedKey, not its _id.
    // An unmatched key makes it skip the spawn point, so this failing means a map with no loose loot
    // and nothing in the log to say why.
    [Theory, MemberData(nameof(Maps))]
    public void EveryDistributionKeyResolvesToAnItem(string map)
    {
        var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(RepoRoot(), "FactoryClassic.Server", "db", "classic", map, "looseLoot.json")));

        var checkedKeys = 0;
        foreach (var section in new[] { "spawnpoints", "spawnpointsForced" })
        foreach (var spawn in document.RootElement.GetProperty(section).EnumerateArray())
        {
            if (!spawn.TryGetProperty("itemDistribution", out var distribution)) continue;

            var keys = new HashSet<string>();
            foreach (var item in spawn.GetProperty("template").GetProperty("Items").EnumerateArray())
            {
                keys.Add(item.GetProperty("_id").GetString()!);
                if (item.TryGetProperty("composedKey", out var composed) && composed.ValueKind == JsonValueKind.String)
                    keys.Add(composed.GetString()!);
            }

            foreach (var entry in distribution.EnumerateArray())
            {
                var key = entry.GetProperty("composedKey").GetProperty("key").GetString()!;
                Assert.Contains(key, keys);
                checkedKeys++;
            }
        }

        Assert.True(checkedKeys > 1000, $"only {checkedKeys} distribution keys checked for {map}");
    }
}
