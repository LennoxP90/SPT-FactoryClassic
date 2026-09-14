#nullable enable
using System;

namespace FactoryClassic.Shared
{
    // Which dataset the server serves and which scenes the client loads. The location id is
    // factory4_day or factory4_night either way, and anything that is not "classic" is the map SPT
    // ships. The value "original" matches InterchangeRework's, so a mod reading either API sees one
    // vocabulary; the player-facing word is "Vanilla" and lives in VariantDisplay.
    public static class MapVariant
    {
        public const string Original = "original";
        public const string Classic = "classic";

        public static string Normalise(string? variant)
            => string.Equals(variant, Classic, StringComparison.OrdinalIgnoreCase) ? Classic : Original;

        public static bool IsClassic(string? variant) => Normalise(variant) == Classic;
    }
}
