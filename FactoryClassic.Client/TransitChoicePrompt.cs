using System;
using System.Reflection;
using EFT;
using FactoryClassic.Shared;
using HarmonyLib;

namespace FactoryClassic.Client
{
    // The variant choice, on the item-transfer screen the transit already opens.
    //
    // That screen is the only interactive UI a raid has: it is where holding the interact key takes
    // you, it already owns the cursor, and closing it is what commits the transit
    // (OpenTransferItemsScreen wires OnClose to TransitInteraction). So the choice sits exactly where
    // the decision is made, with no cursor of ours to take and no EventSystem to wrestle.
    //
    // Shown only to the player holding the claim. Two people cannot both be choosing: entering the
    // zone claims, the server settles that with a single TryAdd, and whoever lost is not the owner
    // and gets no window. The server would refuse their choice even if one were shown.
    internal static class TransitChoicePrompt
    {
        internal static void Install()
        {
            new Harmony(BuildInfo.Guid + ".transitprompt").PatchAll(typeof(OnScreen));
            Plugin.Log.LogInfo("[Variant] transit prompt armed");
        }

        [HarmonyPatch]
        internal static class OnScreen
        {
            static MethodBase TargetMethod() =>
                AccessTools.Method(typeof(ClientTransitController), nameof(ClientTransitController.OpenTransferItemsScreen));

            // pointId, not our remembered claim: the claim survives the transit into Factory, so
            // asking it which way we are going answered "to Factory" while standing on Factory about
            // to leave, and the prompt appeared on the way out.
            static void Postfix(ClientTransitController __instance, int pointId)
            {
                try
                {
                    if (__instance == null || !__instance.pointsById.TryGetValue(pointId, out var point) || point == null)
                        return;

                    var destination = point.parameters.location;
                    if (!TransitClaimPolicy.IsFactoryBound(destination)) return;

                    // Not the claimant: somebody else already decided, so there is nothing to ask.
                    if (!TransitVariantClaim.Owns(destination))
                    {
                        Plugin.Log.LogDebug($"[Variant] transit to '{destination}' was claimed by someone else; no prompt");
                        return;
                    }

                    MapChoiceWindow.Show(
                        MapVariantPrompt.TileImage("factory_vanilla.png"),
                        MapVariantPrompt.TileImage("factory_classic.png"),
                        QuestGateSync.AcceptedBlocked(),
                        choice =>
                        {
                            // Cancelled: keep what entering the zone already claimed.
                            if (choice == null) return;
                            TransitVariantClaim.Choose(destination, choice.Value ? MapVariant.Classic : MapVariant.Original);
                        });
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"[Variant] transit prompt failed: {e}");
                }
            }
        }
    }
}
