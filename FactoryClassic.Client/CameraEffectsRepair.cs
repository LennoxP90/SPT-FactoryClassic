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
            var harmony = new Harmony(BuildInfo.Guid + ".cameraeffects");
            harmony.PatchAll(typeof(EffectsInit));
            harmony.PatchAll(typeof(Fsr3Init));
            Plugin.Log.LogInfo("[Camera] armed");
        }

        // Cam2_nightfactory was saved before FSR3 existed, so its SSAAPropagator's eight FSR3 slots
        // are empty. SSAAImpl checks only the FSR1 shaders before building the FSR3 wrapper, which
        // then throws every frame: a black screen with the HUD and sound still working. The day
        // prefab, Resources "cam2", has all eight. Not gated to classic: it only fills empty slots.
        static readonly string[] Fsr3Shaders =
        {
            "FSR3AutogenReactiveShader", "FSR3TCRAutogenShader", "FSR3ComputeLuminancePyramidShader",
            "FSR3ReconstructPreviousDepthShader", "FSR3DepthClipShader", "FSR3LockShader",
            "FSR3AccumulateShader", "FSR3RCAS2Shader",
        };

        [HarmonyPatch]
        internal static class Fsr3Init
        {
            static Type _impl;
            static FieldInfo _wrapper, _propagator;

            static bool Prepare()
            {
                _impl = AccessTools.TypeByName("SSAAImpl");
                _wrapper = _impl == null ? null : AccessTools.Field(_impl, "_fsr3Wrapper");
                _propagator = _impl == null ? null : AccessTools.Field(_impl, "_ssaaPropagator");
                if (_wrapper != null && _propagator != null) return true;
                Plugin.Log.LogWarning("[Camera] SSAAImpl FSR3 fields not found; FSR3 repair off");
                return false;
            }

            static MethodBase TargetMethod() => AccessTools.Method(_impl, "TryRenderFSR3");

            // Runs every FSR3 frame, but the wrapper exists from the first one on, so the fill is once per camera.
            // A mod that renders the frame itself (FSR4 Bridge) skips the original; the fill waits for one it does not.
            static void Prefix(object __instance, bool __runOriginal)
            {
                try
                {
                    if (!__runOriginal || _wrapper.GetValue(__instance) != null) return;
                    if (_propagator.GetValue(__instance) is Component propagator && propagator != null) FillFsr3(propagator);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"[Camera] FSR3 repair failed: {e}");
                }
            }
        }

        static void FillFsr3(Component propagator)
        {
            var type = propagator.GetType();
            Component donor = null;
            int filled = 0;
            foreach (var name in Fsr3Shaders)
            {
                var field = AccessTools.Field(type, name);
                if (field == null || field.GetValue(propagator) as UnityEngine.Object != null) continue;

                if (donor == null) donor = Resources.Load<GameObject>("cam2")?.GetComponent(type);
                if (donor == null)
                {
                    Plugin.Log.LogError($"[Camera] {propagator.name} has no FSR3 shaders and the cam2 donor is missing; FSR3 will throw");
                    return;
                }
                field.SetValue(propagator, field.GetValue(donor));
                filled++;
            }
            if (filled > 0)
                Plugin.Log.LogWarning($"[Camera] {propagator.name}: {filled} empty FSR3 shader slot(s) filled from cam2");
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

                    AddUltimateBloom(host);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"[Camera] repair failed: {e}");
                }
            }
        }

        // Cam2 has none, and a bare one keeps its arrays null until its first render, so a mod
        // configuring it at raid start (HollywoodGraphics) throws. CreateMaterials fills them now.
        static void AddUltimateBloom(GameObject host)
        {
            if (host.GetComponent<UltimateBloom>() != null) return;

            host.AddComponent<UltimateBloom>().CreateMaterials();
            Plugin.Log.LogWarning("[Camera] added an UltimateBloom; the classic camera has none");
        }
    }
}
