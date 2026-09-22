using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FactoryClassic.Client
{
    /// <summary>
    /// AmandsGraphics maps active scene names to LevelSettings object names and has no classic entry,
    /// so its fallback Find returns null and ActivateAmandsGraphics throws before applying anything.
    /// </summary>
    [HarmonyPatch]
    internal static class AmandsGraphicsCompat
    {
        internal static void Install()
        {
            new Harmony(BuildInfo.Guid + ".amandsgraphics").PatchAll(typeof(AmandsGraphicsCompat));
            Plugin.Log.LogInfo(Target() == null
                ? "[AmandsGraphics] not installed; nothing to map"
                : "[AmandsGraphics] armed; the classic scene will be mapped to its LevelSettings");
        }

        static bool Prepare() => Target() != null;

        static MethodBase TargetMethod() => Target();

        static MethodBase Target() =>
            AccessTools.Method(AccessTools.TypeByName("AmandsGraphics.AmandsGraphicsClass"), "ActivateAmandsGraphics");

        static void Prefix(object __instance)
        {
            if (!PresetSwap.ClassicLoaded()) return;

            var table = AccessTools.Field(__instance.GetType(), "sceneLevelSettings")?.GetValue(null) as Dictionary<string, string>;
            var settings = Object.FindObjectOfType<LevelSettings>();
            if (table == null || settings == null)
            {
                Plugin.Log.LogWarning($"[AmandsGraphics] cannot map the classic scene: table={table != null} levelSettings={settings != null}");
                return;
            }

            var scene = SceneManager.GetActiveScene().name;
            table[scene] = settings.gameObject.name;
            Plugin.Log.LogInfo($"[AmandsGraphics] mapped scene '{scene}' to '{settings.gameObject.name}'");
        }
    }
}
