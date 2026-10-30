using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EFT.Interactive;
using FactoryClassic.Shared;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FactoryClassic.Client
{
    /// <summary>
    /// The game finds loot containers and interactive objects through each scene's LocationScene, whose arrays are
    /// filled when the scene is baked. The Halloween scenes carry only the event's new objects and no LocationScene,
    /// so their corpses were never registered and could not be looted. Each one gets a LocationScene of its own,
    /// filled before it wakes, which is when it registers. The rip also blanks every container's Id, which the
    /// server keys loot on, so each corpse gets its Id back from plugin-data/halloween/corpses.json by position, and
    /// the interaction fields the rip blanked with it (state, Operatable, open interaction) take the values 4.1's own
    /// corpse containers carry; blank, the corpse holds its loot but offers no way to open it.
    /// </summary>
    internal static class HalloweenRegistry
    {
        const float MatchRadius = 0.1f;

        class Corpse
        {
            [JsonProperty("id")] public string Id { get; set; }
            [JsonProperty("tpl")] public string Tpl { get; set; }
            [JsonProperty("position")] public float[] Position { get; set; }
        }

        static List<Corpse> _corpses;

        internal static void Install()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            Plugin.Log.LogInfo("[Halloween] registry armed");
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!scene.name.StartsWith(FactoryScenes.HalloweenScene, StringComparison.Ordinal)) return;
            try
            {
                var roots = scene.GetRootGameObjects();
                var host = new GameObject("FactoryClassic_HalloweenLocationScene");
                host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, scene);
                var ls = host.AddComponent<LocationScene>();

                // Awake registers every array; a null one would throw, so all start empty.
                foreach (var f in typeof(LocationScene).GetFields().Where(f => f.FieldType.IsArray && !f.IsStatic && f.GetValue(ls) == null))
                    f.SetValue(ls, Array.CreateInstance(f.FieldType.GetElementType(), 0));
                ls.LootableContainers = roots.SelectMany(r => r.GetComponentsInChildren<LootableContainer>(true)).ToArray();
                RestoreIds(scene.name, ls.LootableContainers);
                ls.WorldInteractiveObjects = roots.SelectMany(r => r.GetComponentsInChildren<WorldInteractiveObject>(true)).ToArray();

                host.SetActive(true);
                Plugin.Log.LogInfo($"[Halloween] {scene.name}: registered {ls.LootableContainers.Length} loot container(s), {ls.WorldInteractiveObjects.Length} interactive object(s)");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[Halloween] registry failed on {scene.name}, its corpses stay unlootable: {e}");
            }
        }
    
        static void RestoreIds(string scene, LootableContainer[] containers)
        {
            if (containers.Length == 0) return;
            if (_corpses == null)
                _corpses = JsonConvert.DeserializeObject<List<Corpse>>(File.ReadAllText(Path.Combine(Plugin.PluginDir, "plugin-data", "halloween", "corpses.json")));

            int restored = 0;
            float worst = 0f;
            foreach (var c in containers)
            {
                if (!string.IsNullOrEmpty(c.Id)) continue;
                var at = c.transform.position;
                var best = _corpses.OrderBy(k => (new Vector3(k.Position[0], k.Position[1], k.Position[2]) - at).sqrMagnitude).First();
                var distance = Vector3.Distance(new Vector3(best.Position[0], best.Position[1], best.Position[2]), at);
                if (distance > MatchRadius) continue;
                c.Id = best.Id;
                AsNativeCorpse(c, best.Tpl);
                restored++;
                worst = Mathf.Max(worst, distance);
            }
            Plugin.Log.LogInfo($"[Halloween] {scene}: restored {restored} of {containers.Length} container id(s), worst match {worst:0.###} m");
        }
    
        // Read from a corpse container in 4.1's own Factory_Rework_DesignStuff (container_Factory_Rework_DesignStuff_00131).
        static void AsNativeCorpse(LootableContainer c, string tpl)
        {
            c.Template = tpl;
            c.Operatable = true;
            c.NoInteractionsAllowed = false;
            c._doorState = (EDoorState)2;
            c.DoorAxis = (WorldInteractiveObject.EDoorAxis)2;
            c.DoorForward = (WorldInteractiveObject.EDoorAxis)2;
            c.ShutShift = 1f;
            c.DoorKeyOpenInteraction = (EInteraction)29;
            c._openInteraction = (EInteraction)28;
            c._closeInteraction = 0;
            c.ClosedPosition = new Vector3(0f, 0.14f, 0f);
            c.OpenPosition = new Vector3(0f, 0.14f, 0f);
            c._isConverted = true;
            c.SpawnChance = 100;
            c._shutSoundRolloff = 50;
            c._squeakSoundRolloff = 35;
            c._openSoundRolloff = 35;
        }
    }
}
