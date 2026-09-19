using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace FactoryClassic.Client
{
    /// <summary>
    /// EffectsController.Init pulls sixteen components off the camera and its effects prefab with no
    /// null check, and the classic tile is missing three of them. See docs/BUGS.md.
    /// </summary>
    internal static class CameraEffectsRepair
    {
        // Only these three. The other thirteen are read from one line later, so a bare component
        // does not save them.
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
