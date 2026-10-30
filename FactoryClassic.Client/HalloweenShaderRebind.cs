using System;
using System.Collections.Generic;
using FactoryClassic.Shared;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FactoryClassic.Client
{
    /// <summary>
    /// A bundle cannot reference the game's shader bundle, so ours carries its own compiled copies, and a
    /// copy draws flat white even with every texture bound. Every material in the Halloween scenes is
    /// pointed back at the game's own shader of the same name. Same shader,
    /// so the material's keywords stay as authored.
    /// </summary>
    internal static class HalloweenShaderRebind
    {
        internal static void Install()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            Plugin.Log.LogInfo("[Halloween] shader rebind armed");
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!scene.name.StartsWith(FactoryScenes.HalloweenScene, StringComparison.Ordinal)) return;
            try
            {
                var missing = new HashSet<string>();
                int rebound = 0;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                        foreach (var material in renderer.sharedMaterials)
                            if (Rebind(material, missing)) rebound++;
                Report(scene.name, rebound, missing);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[Halloween] shader rebind failed on {scene.name}: {e}");
            }
        }

        static bool Rebind(Material material, HashSet<string> missing)
        {
            if (!material || !material.shader) return false;
            var game = Shader.Find(material.shader.name);
            if (!game) { missing.Add(material.shader.name); return false; }
            if (game == material.shader) return false;
            material.shader = game;
            return true;
        }

        static void Report(string what, int rebound, HashSet<string> missing)
        {
            Plugin.Log.LogInfo($"[Halloween] {what}: {rebound} shader(s) rebound to the game's own");
            if (missing.Count > 0)
                Plugin.Log.LogWarning($"[Halloween] {what}: no game shader named {string.Join(", ", new List<string>(missing).ToArray())}; those draw with the bundle's copy");
        }
    }
}
