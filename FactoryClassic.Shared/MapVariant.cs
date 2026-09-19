#nullable enable
using System;

namespace FactoryClassic.Shared
{
    /// <summary>
    /// Which dataset the server serves and which scenes the client loads. "original" matches
    /// InterchangeRework; the word a player sees is "Vanilla", in VariantDisplay.
    /// </summary>
    public static class MapVariant
    {
        public const string Original = "original";
        public const string Classic = "classic";

        public static string Normalise(string? variant)
            => string.Equals(variant, Classic, StringComparison.OrdinalIgnoreCase) ? Classic : Original;

        public static bool IsClassic(string? variant) => Normalise(variant) == Classic;
    }
}
