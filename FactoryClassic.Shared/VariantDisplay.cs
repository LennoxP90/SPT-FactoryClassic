#nullable enable
namespace FactoryClassic.Shared
{
    // One place owns the two strings a player ever sees, so the choice prompt and the loading screen
    // cannot drift apart. BSG's folder is named Factory_Rework and it is the CURRENT map, so the word
    // "rework" must never reach a player here: it would mean the opposite of what it means in
    // InterchangeRework.
    public static class VariantDisplay
    {
        public const string ClassicLabel = "Factory - Classic";
        public const string VanillaLabel = "Factory - Vanilla";

        public static string For(string? variant)
            => MapVariant.IsClassic(variant) ? ClassicLabel : VanillaLabel;
    }
}
