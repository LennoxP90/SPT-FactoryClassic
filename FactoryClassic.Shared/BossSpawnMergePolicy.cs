#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace FactoryClassic.Shared
{
    /// <summary>
    /// Takes only the entries the classic tile owned out of BossLocationSpawn. 4.1 places PMCs
    /// through that array and 3.9.8 did not, so replacing it wholesale deletes every PMC.
    /// </summary>
    public static class BossSpawnMergePolicy
    {
        public static readonly IReadOnlyList<string> ClassicOwned = new[] { "bossTagilla", "sectantPriest" };

        // Generic so Shared stays free of SPT types: it is source-linked into a net472 client.
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
