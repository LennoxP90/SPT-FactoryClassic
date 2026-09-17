using System;
using System.Reflection;
using Comfort.Common;
using EFT;
using EFT.Communications;
using EFT.Interactive;
using FactoryClassic.Shared;
using HarmonyLib;
using UnityEngine;

namespace FactoryClassic.Client
{
    // A transit has no map screen, so nobody is prompted for a variant. The first player to step into
    // a Factory-bound transit zone claims one for the whole group, and everyone who joins - a Fika
    // client and the headless that hosts the destination raid - reads that claim back.
    //
    // Claimed on entering the zone rather than on the transit firing, so the server has it while the
    // countdown is still running, long before the destination raid is built.
    //
    // The claimant can still change their mind on the item-transfer screen, which is where holding
    // the interact key leads and which already owns the cursor - see TransitChoicePrompt.
    internal static class TransitVariantClaim
    {
        static string _claimed;
        static bool _owned;

        // Set on the first claim and cleared only at raid end. Zone presence is not enough to gate
        // on: a player has several colliders and the transit is inactive for the first minute, so
        // waiting by it means entering and leaving repeatedly, and each re-entry would claim again.
        static string _claimedFor;

        internal static void Install()
        {
            new Harmony(BuildInfo.Guid + ".transitclaim").PatchAll(typeof(ZoneWatch));
            Plugin.Log.LogInfo("[Variant] transit claim armed");
        }

        internal static void Forget()
        {
            _claimed = null;
            _claimedFor = null;
            _owned = false;
        }

        // What the transit is currently set to, and where it is going.
        internal static string Chosen => _claimed;
        internal static string ClaimedDestination => _claimedFor;

        // Only the claimant is offered a choice: anyone else joined a transit somebody already set,
        // and the server would refuse to change it anyway.
        internal static bool Owns(string destination) => _owned && _claimedFor == destination;

        // Picked from the transit's own interaction menu.
        internal static void Choose(string destination, string variant)
        {
            if (MapVariant.Normalise(_claimed) == MapVariant.Normalise(variant)) return;
            Claim(destination, variant);
        }

        // What this player would pick if asked. The prompt is for the map screen; a transit has none,
        // so the configured default is their standing preference.
        static string Preference() => Plugin.DefaultVariant;

        static bool IsOurs(Collider col)
        {
            var main = Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance.MainPlayer : null;
            return main != null && col != null && col.GetComponentInParent<Player>() == main;
        }

        static void Claim(string destination, string offered)
        {
            var winner = VariantSync.Claim(destination, offered, out var outcome);
            if (winner == null) return;

            _claimed = winner;
            _owned = outcome != "Joined";
            var label = VariantDisplay.For(winner);

            // The choice itself is on the transfer screen, which holding the interact key opens. This
            // only reports what the transit is set to.
            var message = outcome == "Joined"
                ? $"Transit is going to {label}"
                : $"Transit set to {label}";
            Notify(message);
            Plugin.Log.LogInfo($"[Variant] transit to '{destination}': {outcome} -> {winner}");
        }

        static void Notify(string message)
        {
            try
            {
                NotificationManager.DisplayMessageNotification(message);
            }
            catch (Exception e)
            {
                Plugin.Log.LogDebug($"[Variant] could not show a notification: {e.GetType().Name}");
            }
        }

        [HarmonyPatch]
        internal static class ZoneWatch
        {
            [HarmonyPatch(typeof(TransitPoint), nameof(TransitPoint.OnTriggerEnter))]
            [HarmonyPostfix]
            static void Entered(TransitPoint __instance, Collider col)
            {
                try
                {
                    if (__instance == null || !TransitClaimPolicy.IsFactoryBound(__instance.parameters.location)) return;
                    if (!IsOurs(col)) return;

                    var destination = __instance.parameters.location;
                    if (_claimedFor == destination) return;
                    _claimedFor = destination;

                    Claim(destination, Preference());
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"[Variant] transit claim failed: {e}");
                }
            }

        }
    }
}
