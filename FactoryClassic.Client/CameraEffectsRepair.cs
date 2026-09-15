using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace FactoryClassic.Client
{
    // EffectsController.Init pulls sixteen components off the camera's effects prefab and off the
    // camera object itself, every one without a null check. On the classic tile the prefab is missing
    // ChromaticAberration and ThermalVision, and the camera object is missing FrostbiteEffect.
    //
    // Only some of those are survivable. Most are followed immediately by a field read:
    //
    //     cc_FastVignette_0 = go.GetComponent<CC_FastVignette>() ?? go.AddComponentCopy(component3);
    //     cc_FastVignette_0.sharpness = component3.sharpness;      // throws anyway if component3 is null
    //
    // These three are not. ChromaticAberration and ThermalVision are assigned through ?? and never
    // read from again, so putting a bare component on the camera short-circuits the null copy; and
    // FrostbiteEffect is read straight off the camera object as "_frostbiteEffect.enabled = false".
    //
    // The one that actually crashed the raid was FrostbiteEffect. The other two were already present
    // on the camera object, so Init short-circuited on its own - which is only knowable because the
    // repair logs what it observes even when it changes nothing.
    internal static class CameraEffectsRepair
    {
        static readonly string[] SafeToAdd = { "ChromaticAberration", "ThermalVision", "FrostbiteEffect" };

        internal static void Install()
        {
            new Harmony(BuildInfo.Guid + ".cameraeffects").PatchAll(typeof(EffectsInit));
            Plugin.Log.LogInfo("[Camera] armed");
        }

        [HarmonyPatch]
        internal static class EffectsInit
        {
            static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("EffectsController"), "Init");

            static void Prefix(object __instance)
            {
                try
                {
                    if (!(__instance is MonoBehaviour controller) || !PresetSwap.ClassicLoaded()) return;

                    var prefab = AccessTools.Field(controller.GetType(), "_effectsPrefab")?.GetValue(controller) as GameObject;
                    var host = controller.gameObject;

                    foreach (var name in SafeToAdd)
                    {
                        var type = AccessTools.TypeByName(name);
                        if (type == null) { Plugin.Log.LogWarning($"[Camera] type {name} not found"); continue; }

                        // Already on the camera: Init's ?? short-circuits by itself.
                        if (host.GetComponent(type) != null) continue;

                        // On the prefab: Init copies it across correctly.
                        if (prefab != null && prefab.GetComponent(type) != null) continue;

                        host.AddComponent(type);
                        Plugin.Log.LogWarning($"[Camera] added a bare {name}; neither the camera nor its effects prefab had one, " +
                                              "and Init would have copied from null");
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"[Camera] repair failed: {e}");
                }
            }
        }
    }
}
