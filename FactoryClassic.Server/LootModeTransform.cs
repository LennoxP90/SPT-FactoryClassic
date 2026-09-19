using FactoryClassic.Shared;
using SPTarkov.Server.Core.Models.Eft.Common;

namespace FactoryClassic.Server;

/// <summary>
/// Folds the items 4.1 spawns on Factory into the classic tile's loose loot, at load and on the
/// classic dataset only. The classic tables are frozen at November 2024, so 464 templates on day
/// and 476 at night reach the tile no other way. Each lands at the NEAREST classic point.
/// </summary>
public static class LootModeTransform
{
    public readonly record struct Report(string Mode, int Added, int Replaced, int Unplaceable)
    {
        public override string ToString() => Mode == LootMode.Classic
            ? "loot: classic tables, untouched"
            : $"loot: {Mode}, {Added} item(s) added, {Replaced} classic item(s) replaced, {Unplaceable} unplaceable";
    }

    // The donor pool comes through a LazyLoad and so can be absent, which leaves the classic
    // tables served as authored.
    public static Report Apply(LooseLoot classic, LooseLoot? modern, string mode)
    {
        var normalised = LootMode.Normalise(mode);
        if (!LootMode.AddsItems(normalised)) return new Report(normalised, 0, 0, 0);

        var targets = Placeable(classic);
        if (targets.Count == 0) return new Report(normalised, 0, 0, 0);

        var positions = targets.ConvertAll(target => target.Position);

        // Modern rebuilds each point's offering; hybrid keeps the classic items and adds alongside.
        var replaced = normalised == LootMode.Modern ? ClearAll(targets) : 0;

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

    private static int ClearAll(List<SpawnPointView> targets)
    {
        var removed = 0;
        foreach (var target in targets) removed += target.Clear();
        return removed;
    }

    // A point whose position cannot be parsed is left exactly as it is rather than guessed at: a
    // mis-parsed coordinate would move loot silently.
    private static List<SpawnPointView> Placeable(LooseLoot? loot)
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
