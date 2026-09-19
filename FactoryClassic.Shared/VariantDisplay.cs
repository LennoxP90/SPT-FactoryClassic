#nullable enable
namespace FactoryClassic.Shared
{
    /// <summary>
    /// The two strings a player sees, built from three pieces that MapVariants is registered with
    /// separately, so its wording and ours match by construction rather than by agreement.
    /// </summary>
    public static class VariantDisplay
    {
        public const string MapName = "Factory";
        public const string ClassicName = "Classic";
        public const string VanillaName = "Vanilla";

        public const string Separator = " - ";

        public const string ClassicLabel = MapName + Separator + ClassicName;
        public const string VanillaLabel = MapName + Separator + VanillaName;

        public static string For(string? variant)
            => MapVariant.IsClassic(variant) ? ClassicLabel : VanillaLabel;
    }
}
