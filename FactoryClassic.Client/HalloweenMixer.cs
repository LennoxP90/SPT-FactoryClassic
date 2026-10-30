using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FactoryClassic.Shared;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace FactoryClassic.Client
{
    /// <summary>
    /// The rip exports the game's MasterMixer as an asset, so the bundle carries its own copy and every Halloween
    /// source plays into it. The game never drives that copy (snapshots, the world fade-in, the ambience parameters),
    /// so it stays at its start state with the World branch shut and the scene is silent. Each source, and every
    /// mixer, group or snapshot field on the scene's components (player groups reassign their players in Start, after
    /// this runs), is pointed at the same-named one on the game's own MasterMixer instead. While the event's sound
    /// scene is loaded, the game mixer's ambience channels are
    /// lifted by AmbienceGainDb (the event's mix runs its World bus 4 dB hotter than 4.1's), and handed back to the
    /// game when the scene unloads. The game puts those channels back to its own values after the scene has loaded (raid
    /// start, any sound setting applied), so a watcher in the scene re-applies the lift whenever they stop reading it.
    /// </summary>
    internal static class HalloweenMixer
    {
        static readonly Dictionary<Type, FieldInfo[]> MixerFields = new Dictionary<Type, FieldInfo[]>();
        static AudioMixer _lifted;
        static readonly string[] AmbienceChannels = { "AmbientInVolume", "AmbientOutVolume" };

        internal static void Install()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += scene =>
            {
                if (scene.name != FactoryScenes.HalloweenSoundScene || _lifted == null) return;
                foreach (var name in AmbienceChannels) _lifted.ClearFloat(name);
                Plugin.Log.LogInfo("[HalloweenMixer] ambience channels handed back to the game");
                _lifted = null;
            };
            Plugin.HalloweenAmbienceGainDb.SettingChanged += (_, __) => { if (_lifted != null) Lift(_lifted); };
            Plugin.Log.LogInfo("[HalloweenMixer] armed");
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!scene.name.StartsWith(FactoryScenes.HalloweenScene, StringComparison.Ordinal)) return;
            try
            {
                var roots = scene.GetRootGameObjects();
                var sources = roots.SelectMany(r => r.GetComponentsInChildren<AudioSource>(true)).ToList();
                var copy = sources.Select(s => s.outputAudioMixerGroup).FirstOrDefault(g => g)?.audioMixer;
                if (copy == null) return;

                var game = Resources.FindObjectsOfTypeAll<AudioMixer>().FirstOrDefault(m => m.name == copy.name && m != copy);
                if (game == null)
                {
                    Plugin.Log.LogWarning($"[HalloweenMixer] {scene.name}: no game {copy.name} besides the bundle's; its sources stay on the copy and stay silent");
                    return;
                }

                var byName = new Dictionary<string, AudioMixerGroup>();
                foreach (var g in game.FindMatchingGroups(string.Empty))
                    if (!byName.ContainsKey(g.name)) byName[g.name] = g;
                AudioMixerGroup Map(AudioMixerGroup g) => g && g.audioMixer == copy && byName.TryGetValue(g.name, out var real) ? real : g;

                int moved = 0, unmatched = 0;
                foreach (var s in sources)
                {
                    var g = s.outputAudioMixerGroup;
                    if (!g || g.audioMixer != copy) continue;
                    var real = Map(g);
                    if (real == g) { unmatched++; continue; }
                    s.outputAudioMixerGroup = real;
                    moved++;
                }

                object Remap(object value)
                {
                    switch (value)
                    {
                        case AudioMixerGroup g:
                            return Map(g);
                        case AudioMixerGroup[] groups:
                            var mapped = groups.Select(Map).ToArray();
                            return mapped.SequenceEqual(groups) ? value : mapped;
                        case AudioMixer m when m == copy:
                            return game;
                        case AudioMixerSnapshot snap when snap && snap.audioMixer == copy:
                            return (object)game.FindSnapshot(snap.name) ?? snap;
                        default:
                            return value;
                    }
                }

                int fields = 0;
                foreach (var c in roots.SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)))
                {
                    if (c == null) continue;
                    foreach (var f in FieldsOf(c.GetType()))
                    {
                        var value = f.GetValue(c);
                        var real = Remap(value);
                        if (ReferenceEquals(real, value)) continue;
                        f.SetValue(c, real);
                        fields++;
                    }
                }

                Plugin.Log.LogInfo($"[HalloweenMixer] {scene.name}: {moved} source(s) and {fields} component field(s) moved from the bundle's {copy.name} to the game's; {unmatched} source(s) without a same-named group");
                if (scene.name == FactoryScenes.HalloweenSoundScene)
                {
                    Lift(game);
                    var host = new GameObject("FactoryClassic_AmbienceLift");
                    SceneManager.MoveGameObjectToScene(host, scene);
                    host.AddComponent<Watcher>();
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[HalloweenMixer] {scene.name}: remap failed, its sounds stay silent: {e}");
            }
        }
    
        // Lives in the sound scene, so it goes when the scene unloads.
        class Watcher : MonoBehaviour
        {
            float _next, _loadedAt;
            bool _reported;

            void Start() => _loadedAt = Time.realtimeSinceStartup;

            void Update()
            {
                if (_lifted == null || Time.realtimeSinceStartup < _next) return;
                _next = Time.realtimeSinceStartup + 0.5f;
                var gain = Plugin.HalloweenAmbienceGainDb.Value;
                var held = true;
                for (int i = 0; i < AmbienceChannels.Length; i++)
                    if (!_lifted.GetFloat(AmbienceChannels[i], out var v) || Mathf.Abs(v - gain) >= 0.05f) held = false;
                if (held) return;
                for (int i = 0; i < AmbienceChannels.Length; i++) _lifted.SetFloat(AmbienceChannels[i], gain);
                if (_reported) return;
                _reported = true;
                Plugin.Log.LogInfo($"[HalloweenMixer] the game reset its ambience channels {Time.realtimeSinceStartup - _loadedAt:0.0} s after load; lifted {gain:0.#} dB again");
            }
        }

        // Every field up to MonoBehaviour that can hold a mixer reference, cached per type.
        static FieldInfo[] FieldsOf(Type type)
        {
            if (MixerFields.TryGetValue(type, out var known)) return known;
            var found = new List<FieldInfo>();
            for (var t = type; t != null && t != typeof(MonoBehaviour); t = t.BaseType)
                foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    if (f.FieldType == typeof(AudioMixerGroup) || f.FieldType == typeof(AudioMixerGroup[])
                        || f.FieldType == typeof(AudioMixer) || f.FieldType == typeof(AudioMixerSnapshot))
                        found.Add(f);
            return MixerFields[type] = found.ToArray();
        }

        static void Lift(AudioMixer mixer)
        {
            var gain = Plugin.HalloweenAmbienceGainDb.Value;
            var set = AmbienceChannels.Where(name => mixer.SetFloat(name, gain)).ToList();
            _lifted = mixer;
            Plugin.Log.LogInfo($"[HalloweenMixer] ambience channels {(set.Count == AmbienceChannels.Length ? "" : $"({set.Count} of {AmbienceChannels.Length} exposed) ")}lifted {gain:0.#} dB");
        }
    }
}
