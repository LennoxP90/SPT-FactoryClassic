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

        public static bool IsUnmanaged(string? variant)
            => string.Equals((variant ?? "").Trim(), MapVariantsUnmanaged, StringComparison.OrdinalIgnoreCase);

        public static string FromMapVariants(string? variant)
            => string.Equals((variant ?? "").Trim(), MapVariantsBackport, StringComparison.OrdinalIgnoreCase)
                ? MapVariant.Classic
                : MapVariant.Original;

        public static string ToMapVariants(string? variant)
            => MapVariant.IsClassic(variant) ? MapVariantsBackport : MapVariantsOriginal;
    }
}
