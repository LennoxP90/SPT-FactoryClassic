#nullable enable
using System;

namespace FactoryClassic.Shared
{
    /// <summary>
    /// How the classic tile's loose loot is built, and the classic tile only. Classic serves the
    /// 3.9.8 tables untouched; hybrid adds 4.1's items alongside; modern replaces with them.
    /// </summary>
    public static class LootMode
    {
        public const string Classic = "classic";
        public const string Hybrid = "hybrid";
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
