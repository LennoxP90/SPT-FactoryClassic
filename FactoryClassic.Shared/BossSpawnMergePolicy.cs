#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace FactoryClassic.Shared
{
    // BossLocationSpawn is not only bosses. In SPT 3.9.8 it held one entry, Tagilla, and PMCs came
    // from the server's own bot generation. In 4.1 the same array is also how PMCs are placed, so
    // replacing it with the classic list deletes every PMC and yields a raid of scavs and Tagilla,
    // silently. Only the entries the classic tile actually owned are taken from it.
    public static class BossSpawnMergePolicy
    {
        public static readonly IReadOnlyList<string> ClassicOwned = new[] { "bossTagilla", "sectantPriest" };

        // Generic over the entry type so this stays free of SPT types: Shared is source-linked into a
        // net472 client that cannot reference SPTarkov.Server.Core.
        public static List<T> Merge<T>(IReadOnlyList<T>? shipped, IReadOnlyList<T>? classic, Func<T, string?> bossName)
        {
            var kept = (shipped ?? new List<T>())
                .Where(entry => !IsClassicOwned(bossName(entry)))
                .ToList();

            var taken = (classic ?? new List<T>())
                .Where(entry => IsClassicOwned(bossName(entry)))
                .ToList();

            kept.AddRange(taken);
            return kept;
        }

        static bool IsClassicOwned(string? name)
            => name != null && ClassicOwned.Contains(name, StringComparer.OrdinalIgnoreCase);
    }
}
