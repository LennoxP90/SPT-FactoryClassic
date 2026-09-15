using FactoryClassic.Shared;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;

namespace FactoryClassic.Server;

// A loose loot spawn point, made writable.
//
// The shipped records expose Items and ItemDistribution as IEnumerable, so both are materialised into
// lists once and assigned back. Doing it per edit would re-enumerate the original each time and throw
// the previous edit away.
//
// The two collections are joined by composedKey, NOT by item id: LocationLootGenerator matches
// itemDistribution against each item's ComposedKey, and anything it cannot match makes it skip the
// whole spawn point silently. So every item added here gets a key that is unique within this point
// and a distribution entry carrying the same one.
public sealed class SpawnPointView
{
    private readonly Spawnpoint _spawn;
    private readonly List<SptLootItem> _items;
    private readonly List<LooseLootItemDistribution> _distribution;
    private readonly HashSet<string> _keys = new(StringComparer.Ordinal);
    private int _minted;

    public LootMergePolicy.Position Position { get; }

    public SpawnPointView(Spawnpoint spawn, LootMergePolicy.Position position)
    {
        _spawn = spawn;
        Position = position;

        _items = spawn.Template?.Items?.ToList() ?? [];
        _distribution = spawn.ItemDistribution?.ToList() ?? [];

        foreach (var item in _items)
            if (!string.IsNullOrEmpty(item.ComposedKey)) _keys.Add(item.ComposedKey!);

        Flush();
    }

    public double TotalWeight()
    {
        var total = 0d;
        foreach (var entry in _distribution) total += entry.RelativeProbability ?? 0d;
        return total;
    }

    public bool Offers(MongoId template)
    {
        foreach (var item in _items)
            if (item.Template == template) return true;
        return false;
    }

    // Each offering paired with the weight its own distribution entry gives it. An item with no
    // entry is not on offer at all, so it is skipped rather than treated as weightless.
    public IEnumerable<(SptLootItem Item, double Weight)> Offerings()
    {
        var weights = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var entry in _distribution)
        {
            var key = entry.ComposedKey?.Key;
            if (string.IsNullOrEmpty(key)) continue;
            weights[key!] = entry.RelativeProbability ?? 0d;
        }

        foreach (var item in _items)
        {
            var key = item.ComposedKey;
            if (string.IsNullOrEmpty(key)) continue;
            if (weights.TryGetValue(key!, out var weight) && weight > 0d) yield return (item, weight);
        }
    }

    // Copies an offering in from another point. The item keeps its template and its upd - stack sizes
    // and durability are part of what the item IS - but takes a fresh id and a key unique here.
    public void Add(SptLootItem source, int weight)
    {
        var key = UniqueKey(source.ComposedKey);

        _items.Add(new SptLootItem
        {
            Id = new MongoId(),
            Template = source.Template,
            Upd = source.Upd,
            ComposedKey = key,
        });

        _distribution.Add(new LooseLootItemDistribution
        {
            ComposedKey = new ComposedKey { Key = key },
            RelativeProbability = weight,
        });

        Flush();
    }

    public int Clear()
    {
        var removed = _items.Count;
        _items.Clear();
        _distribution.Clear();
        _keys.Clear();
        Flush();
        return removed;
    }

    // The source key where it is free, so a merged point still reads like the tables it came from,
    // and a suffixed one where it is not. Uniqueness is within this spawn point only, which is the
    // scope the generator matches in.
    private string UniqueKey(string? preferred)
    {
        if (!string.IsNullOrEmpty(preferred) && _keys.Add(preferred!)) return preferred!;

        while (true)
        {
            var candidate = $"fc{_minted++}_{preferred ?? "item"}";
            if (_keys.Add(candidate)) return candidate;
        }
    }

    private void Flush()
    {
        if (_spawn.Template is not null) _spawn.Template.Items = _items;
        _spawn.ItemDistribution = _distribution;
    }
}
