using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using FactoryClassic.Server.Quests;
using Xunit;

namespace FactoryClassic.Tests;

// db/quest-gate.json is generated from the client's scene files by
// analysis/find_original_only_quests.py and then committed, so the list is reviewable rather than
// re-derived on every boot. The cost of committing it is that it can go stale, and these are the
// checks that make going stale loud.
public class QuestGateTableTests
{
    static string RepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SPT-FactoryClassic.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    static QuestGateDocument Document() => JsonSerializer.Deserialize<QuestGateDocument>(
        File.ReadAllText(Path.Combine(RepoRoot(), "FactoryClassic.Server", "db", "quest-gate.json")),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = false })!;

    // The shipped quest table. Read from the clean install, so this is skipped rather than failed
    // where that install is absent.
    static JsonObject? LiveQuests()
    {
        var path = SptInstall.Template("quests.json");
        return File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path)) as JsonObject : null;
    }

    [Fact]
    public void TheFileParsesAndHoldsTheMeasuredResult()
    {
        var document = Document();
        Assert.Equal(6, document.Quests.Count);
        Assert.Equal(18, document.Zones.Count);

        // Every zone belongs to exactly one quest, and every quest names at least one.
        Assert.All(document.Quests.Values, quest => Assert.NotEmpty(quest.Zones));
        var claimed = document.Quests.Values.SelectMany(q => q.Zones).ToList();
        Assert.Equal(claimed.Count, claimed.Distinct().Count());
        Assert.Equal(document.Zones.OrderBy(z => z), claimed.OrderBy(z => z));
    }

    [Fact]
    public void QuestIdsAreObjectIds()
    {
        Assert.All(Document().Quests.Keys, id => Assert.Matches("^[0-9a-fA-F]{24}$", id));
    }

    // The failure this guards against: BSG renames or replaces a quest, the gate silently stops
    // covering it, and a player takes an impossible objective with nothing said.
    [Fact]
    public void EveryGatedQuestStillExistsUnderTheSameId()
    {
        var live = LiveQuests();
        if (live is null) return;   // no clean install on this machine

        foreach (var (questId, entry) in Document().Quests)
            Assert.True(live.ContainsKey(questId), $"{entry.Name} ({questId}) is no longer in the quest database");
    }

    [Fact]
    public void EveryGatedQuestStillCarriesTheNameWeDisplay()
    {
        var live = LiveQuests();
        if (live is null) return;

        foreach (var (questId, entry) in Document().Quests)
            Assert.Equal(entry.Name, live[questId]?["QuestName"]?.GetValue<string>());
    }

    // The other half of staleness: the quest still exists but no longer sends the player to that
    // zone, so gating it punishes a quest that is now fine.
    [Fact]
    public void EveryGatedQuestStillNamesEveryZoneWeGateItOn()
    {
        var live = LiveQuests();
        if (live is null) return;

        foreach (var (questId, entry) in Document().Quests)
        {
            var named = new HashSet<string>();
            Collect(live[questId]?["conditions"], named);
            foreach (var zone in entry.Zones)
                Assert.True(named.Contains(zone), $"{entry.Name} no longer names zone '{zone}'");
        }
    }

    static void Collect(JsonNode? node, HashSet<string> into)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj)
                {
                    if (key == "zoneId" && value is JsonValue leaf && leaf.TryGetValue<string>(out var zone) && !string.IsNullOrEmpty(zone))
                        into.Add(zone);
                    else
                        Collect(value, into);
                }
                break;
            case JsonArray array:
                foreach (var value in array) Collect(value, into);
                break;
        }
    }
}
