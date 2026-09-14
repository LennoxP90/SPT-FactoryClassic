#nullable enable
using System;

namespace FactoryClassic.Shared
{
    // How the classic tile's loose loot is built. Server-driven and read at load, so a change takes
    // effect on a server restart, and it applies to the classic variant only - the shipped Factory is
    // never touched whatever this is set to.
    public static class LootMode
    {
        // The 3.9.8 tables exactly as they were. Faithful, but frozen at November 2024: nothing added
        // to EFT since can spawn loose on the tile.
        public const string Classic = "classic";

        // Classic positions and containers, with the items 4.1 spawns on Factory folded in alongside
        // the classic ones.
        public const string Hybrid = "hybrid";

        // Classic positions, with each point's items taken from 4.1's pool instead of the classic one.
        public const string Modern = "modern";

        public static string Normalise(string? mode)
        {
            if (string.Equals(mode, Hybrid, StringComparison.OrdinalIgnoreCase)) return Hybrid;
            if (string.Equals(mode, Modern, StringComparison.OrdinalIgnoreCase)) return Modern;
            return Classic;
        }

        public static bool AddsItems(string? mode) => Normalise(mode) != Classic;
    }
}
