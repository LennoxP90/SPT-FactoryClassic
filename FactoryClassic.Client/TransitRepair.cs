using System;
using System.Collections.Generic;
using System.Reflection;
using EFT;
using EFT.Interactive;
using HarmonyLib;
using UnityEngine;

namespace FactoryClassic.Client
{
    // Road_to_woods sits on exactly the same coordinate as Road_to_Labs, to three decimal places:
    // BSG duplicated one to make the other and never moved it. The reworked tile has all three at
    // distinct positions, so nothing caught it in a scene the game never loads.
    //
    // It can only be fixed here. TransitController.InitTransitPoints collects
    // LocationScene.GetAllObjects<TransitPoint>() and merges the server's parameters onto whatever
    // it finds, so a transit's position is the scene object's transform and base.json carries none.
    internal static class TransitRepair
    {
        // Chosen by standing on each spot in raid and reading the position out of the screenshot
        // filename, so these are measured rather than estimated. Each is about ten metres inboard of
        // its gate, which leaves the extract its own ground.
        static readonly Dictionary<string, Vector3> Placements = new Dictionary<string, Vector3>
        {
            { "Road_to_woods", new Vector3(57.98f, 1.77f, 49.81f) },      // inboard of exit (1)
            { "Road_to_customs", new Vector3(-49.51f, 2.82f, 55.75f) },   // inboard of exit (2)
            { "Road_to_Labs", new Vector3(-18.16f, 1.75f, -44.76f) },     // inboard of exit_m
        };

        internal static void Install()
        {
            new Harmony(BuildInfo.Guid + ".transits").PatchAll(typeof(Placement));
            Plugin.Log.LogInfo("[Transits] armed");
        }

        [HarmonyPatch]
        internal static class Placement
        {
            static MethodBase TargetMethod() =>
                AccessTools.Method(typeof(TransitController), nameof(TransitController.InitTransitPoints));

            // After, not before: the points are known to exist and be registered by then, and moving
            // one afterwards changes nothing the controller has already read.
            static void Postfix()
            {
                try
                {
                    if (!PresetSwap.ClassicLoaded()) return;

                    foreach (var point in UnityEngine.Object.FindObjectsOfType<TransitPoint>())
                    {
                        if (point == null || !Placements.TryGetValue(point.name, out var wanted)) continue;

                        var was = point.transform.position;
                        if ((was - wanted).sqrMagnitude < 0.01f) continue;

                        point.transform.position = wanted;
                        Plugin.Log.LogInfo($"[Transits] moved '{point.name}' from {was} to {wanted}");
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"[Transits] placement failed: {e}");
                }
            }
        }
    }
}
