using System;
using System.Linq;
using System.Reflection;
using EFT.UI.Matchmaker;
using FactoryClassic.Shared;
using HarmonyLib;

namespace FactoryClassic.Client
{
    // With the prompt turned off there is no other moment that tells a player which Factory is
    // loading, so the last screen before the raid says so.
    internal static class RaidLoadingLabel
    {
        internal static void Install()
        {
            // Show is overloaded; the three-argument one carries the RaidSettings we need.
            var target = typeof(MatchmakerTimeHasCome)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name == nameof(MatchmakerTimeHasCome.Show) && m.GetParameters().Length == 3);

            if (target == null)
            {
                Plugin.Log.LogWarning("[Label] MatchmakerTimeHasCome.Show(session, settings, matchmaker) not found; the loading screen stays unlabelled");
                return;
            }

            new Harmony(BuildInfo.Guid + ".loadinglabel")
                .Patch(target, postfix: new HarmonyMethod(AccessTools.Method(typeof(RaidLoadingLabel), nameof(AfterShow))));
            Plugin.Log.LogInfo("[Label] armed");
        }

        // Postfix: the game writes _locationName inside Show, so a prefix would be overwritten.
        static void AfterShow(MatchmakerTimeHasCome __instance, object[] __args)
        {
            try
            {
                var label = __instance._locationName;
                if (label == null) return;

                var settings = __args.OfType<EFT.RaidSettings>().FirstOrDefault();
                var locationId = settings?.SelectedLocation?.Id ?? "";
                if (!FactoryScenes.ServerNames.Any(id => string.Equals(id, locationId, StringComparison.OrdinalIgnoreCase))) return;

                // Both variants are named, not just ours. Labelling only the added tile made the
                // shipped one look like an unmodded raid, so a player who picked the wrong one had
                // nothing to tell them.
                // Asked of the server when nobody chose on a map screen, which is every transit. The
                // configured default is only a last resort: using it as the fallback labelled every
                // transit with this player's preference rather than with what the raid is loading.
                var variant = PresetSwap.SessionChoice ?? VariantSync.Ask(locationId) ?? Plugin.DefaultVariant;
                var suffix = MapVariant.IsClassic(variant) ? ClassicSuffix : VanillaSuffix;

                var text = label.text ?? "";
                // Both suffixes are checked, so a second Show cannot stack one on top of the other.
                if (text.EndsWith(ClassicSuffix, StringComparison.Ordinal) || text.EndsWith(VanillaSuffix, StringComparison.Ordinal)) return;

                label.text = text + suffix;
                Plugin.Log.LogDebug($"[Label] loading screen marked '{label.text}'");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[Label] {e.GetType().Name}: {e.Message}");
            }
        }

        // Derived from the display labels rather than written twice, so the prompt and the loading
        // screen cannot drift apart.
        static readonly string ClassicSuffix = Suffix(VariantDisplay.ClassicLabel);
        static readonly string VanillaSuffix = Suffix(VariantDisplay.VanillaLabel);

        // "Factory - Classic" -> " - Classic": the game has already written the map's own name.
        static string Suffix(string label)
        {
            var dash = label.IndexOf(" - ", StringComparison.Ordinal);
            return dash < 0 ? " - " + label : label.Substring(dash);
        }
    }
}
