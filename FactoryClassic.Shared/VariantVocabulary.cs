#nullable enable
using System;

namespace FactoryClassic.Shared
{
    /// <summary>
    /// Translates between MapVariants' words and ours, and every crossing goes through here: passing
    /// a value straight across yields the SHIPPED map, silently, in either direction.
    /// </summary>
    public static class VariantVocabulary
    {
        public const string MapVariantsOriginal = "original";
        public const string MapVariantsBackport = "backport";

        /// <summary>
        /// MapVariants' answer for a location it does not manage. NOT a variant: check IsUnmanaged
        /// before reading FromMapVariants, which folds it into "original".
        /// </summary>
        public const string MapVariantsUnmanaged = "unmanaged";

        /// <summary>
        /// The Halloween 2026 variant, declared to MapVariants on both halves. It is Classic's data and scenes
        /// with the event layer on top, so FromMapVariants folds it to Classic. Stable: other mods key on it.
        /// </summary>
        public const string MapVariantsHalloween2026 = "halloween2026";

        public static bool IsHalloween(string? variant)
            => string.Equals((variant ?? "").Trim(), MapVariantsHalloween2026, StringComparison.OrdinalIgnoreCase);

        public static bool IsUnmanaged(string? variant)
            => string.Equals((variant ?? "").Trim(), MapVariantsUnmanaged, StringComparison.OrdinalIgnoreCase);

        public static string FromMapVariants(string? variant)
            => string.Equals((variant ?? "").Trim(), MapVariantsBackport, StringComparison.OrdinalIgnoreCase) || IsHalloween(variant)
                ? MapVariant.Classic
                : MapVariant.Original;

        public static string ToMapVariants(string? variant)
            => MapVariant.IsClassic(variant) ? MapVariantsBackport : MapVariantsOriginal;
    }
}
