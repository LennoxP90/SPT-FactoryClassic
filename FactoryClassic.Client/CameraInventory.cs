using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FactoryClassic.Shared;
using HarmonyLib;
using UnityEngine;

namespace FactoryClassic.Client
{
    /// <summary>
    /// Logs the render camera's component list once per Factory raid, on both tiles, so the two
    /// effect stacks can be diffed. Not gated to classic, because vanilla is half the comparison.
    /// </summary>
    internal static class CameraInventory
    {
        static bool _reported;

        internal static void Install()
        {
            new Harmony(BuildInfo.Guid + ".camerainventory").PatchAll(typeof(AfterEffectsInit));
            Plugin.Log.LogInfo("[CameraReport] armed");
        }

        internal static void Forget() => _reported = false;

        static bool OnFactory()
        {
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (FactoryScenes.IsFactoryScene(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).name))
                    return true;
            return false;
        }

        // A component whose script failed to resolve comes back null, which is itself worth seeing.
        static List<string> Names(GameObject host) =>
            host == null
                ? new List<string>()
                : host.GetComponents<Component>()
                      .Select(c => c == null ? "<missing script>" : c.GetType().Name)
                      .OrderBy(n => n, StringComparer.Ordinal)
                      .ToList();

        static void Report(string label, GameObject host)
        {
            var names = Names(host);
            Plugin.Log.LogInfo($"[CameraReport] {label} '{(host == null ? "none" : host.name)}': {names.Count} component(s)");
            Plugin.Log.LogInfo($"[CameraReport]   {string.Join(", ", names.ToArray())}");
        }

        [HarmonyPatch]
        internal static class AfterEffectsInit
        {
            static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("EffectsController"), "Init");

            static void Postfix(object __instance)
            {
                try
                {
                    if (_reported || !Plugin.CameraReport.Value) return;
                    if (!(__instance is MonoBehaviour controller) || !OnFactory()) return;
                    _reported = true;

                    var variant = PresetSwap.ClassicLoaded() ? "CLASSIC" : "vanilla";
                    Plugin.Log.LogInfo($"[CameraReport] {variant} tile");

                    Report("camera object", controller.gameObject);
                    Report("effects prefab",
                        AccessTools.Field(controller.GetType(), "_effectsPrefab")?.GetValue(controller) as GameObject);

                    // Suspect 2 from docs/TEXTURES.md.
                    Plugin.Log.LogInfo($"[CameraReport] streaming active={QualitySettings.streamingMipmapsActive} " +
                                       $"budget={QualitySettings.streamingMipmapsMemoryBudget:0} MB " +
                                       $"maxLevelReduction={QualitySettings.streamingMipmapsMaxLevelReduction} " +
                                       $"renderersPerFrame={QualitySettings.streamingMipmapsRenderersPerFrame} " +
                                       $"streamedRenderers={Texture.streamingRendererCount} " +
                                       $"streamedTextures={Texture.streamingTextureCount} " +
                                       $"pendingLoads={Texture.streamingTexturePendingLoadCount}");
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"[CameraReport] inventory failed: {e}");
                }
            }
        }
    }
}
