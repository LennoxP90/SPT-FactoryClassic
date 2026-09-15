using FactoryClassic.Shared;
using SPTarkov.Server.Core.Models.Eft.Common;

namespace FactoryClassic.Server;

// Folds the items 4.1 spawns on Factory into the classic tile's loose loot.
//
// The classic tables are frozen at November 2024, so nothing added to EFT since can appear on the
// tile: measured, 464 templates on day and 476 at night exist in 4.1's pool and in no classic spawn
// point at all. LootMode.Classic leaves that alone as the faithful option; Hybrid and Modern do not.
//
// Every item is placed at the classic spawn point NEAREST to where 4.1 spawns it, using the
// coordinates each point already carries in its locationId. That keeps the map's character: an item
// 4.1 puts by the offices lands by the offices rather than in a random corner.
//
// This runs at load, on the classic dataset only. The shipped Factory is never touched.
public static class LootModeTransform
{
    public readonly record struct Report(string Mode, int Added, int Replaced, int Unplaceable)
    {
        public override string ToString() => Mode == LootMode.Classic
            ? "loot: classic tables, untouched"
            : $"loot: {Mode}, {Added} item(s) added, {Replaced} classic item(s) replaced, {Unplaceable} unplaceable";
    }

    public static Report Apply(LooseLoot classic, LooseLoot modern, string mode)
    {
        var normalised = LootMode.Normalise(mode);
        if (!LootMode.AddsItems(normalised)) return new Report(normalised, 0, 0, 0);

        var targets = Placeable(classic);
        if (targets.Count == 0) return new Report(normalised, 0, 0, 0);

        var positions = targets.ConvertAll(target => target.Position);

        // Modern rebuilds each point's offering, so the classic items go first and everything is then
        // added back from 4.1's pool. Hybrid keeps them and adds alongside.
        var replaced = 0;
        if (normalised == LootMode.Modern)
        {
            foreach (var target in targets) replaced += target.Clear();
        }

        var added = 0;
        var unplaceable = 0;

        foreach (var source in Placeable(modern))
        {
            var nearest = LootMergePolicy.NearestIndex(source.Position, positions);
            if (nearest < 0) { unplaceable++; continue; }

            var target = targets[nearest];
            var sourceTotal = source.TotalWeight();
            var targetTotal = target.TotalWeight();

            foreach (var (item, weight) in source.Offerings())
            {
                // Hybrid only fills gaps: an item the classic point already offers keeps its own
                // authored weight rather than being averaged with 4.1's.
                if (normalised == LootMode.Hybrid && target.Offers(item.Template)) continue;

                var scaled = LootMergePolicy.RoundWeight(
                    LootMergePolicy.ScaledWeight(weight, sourceTotal, targetTotal));
                if (scaled <= 0) continue;

                target.Add(item, scaled);
                added++;
            }
        }

        return new Report(normalised, added, replaced, unplaceable);
    }

    // Spawn points whose position can be read. One that cannot be parsed is left exactly as it is
    // rather than guessed at: a mis-parsed coordinate would move loot silently.
    private static List<SpawnPointView> Placeable(LooseLoot loot)
    {
        var views = new List<SpawnPointView>();
        foreach (var spawn in loot?.Spawnpoints ?? [])
        {
            if (!LootMergePolicy.TryParsePosition(spawn.LocationId, out var position)) continue;
            views.Add(new SpawnPointView(spawn, position));
        }
        return views;
    }
}
