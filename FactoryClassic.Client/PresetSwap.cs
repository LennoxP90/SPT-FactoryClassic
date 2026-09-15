using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EFT;
using FactoryClassic.Shared;
using HarmonyLib;
using UnityEngine.SceneManagement;

namespace FactoryClassic.Client
{
    // A Factory raid loads THREE presets, not one: the time-of-day preset, a shared base preset and a
    // culling child. They cannot be told apart by ServerName - the base preset carries an empty one
    // and the night culling child carries "factory4_day" - so the gate is the scene paths.
    //
    // The swap is a list REPLACEMENT rather than a rename: 14 shipped keys against 7 classic ones.
    // Since every classic list is shorter than the shipped one it replaces, the first N entries are
    // rewritten in place and the surplus removed, which means no key object is ever constructed. The
    // removed entries are kept alive in a snapshot so a vanilla raid can put the list back exactly.
    [HarmonyPatch]
    internal static class PresetSwap
    {
        // The player's answer for this raid, set by the prompt. Null means nobody answered here, which
        // is the normal state on a Fika headless: it hosts under its own session and never sees a map
        // screen, so it asks the server instead.
        static string _sessionChoice;

        // Raised when a raid's variant becomes known, and AGAIN if the player goes back and picks the
        // other map before the raid starts. Announcing only the first assignment left a consumer
        // holding the first answer while the raid loaded the second.
        internal static event Action<string> Resolved;

        internal static string SessionChoice
        {
            get => _sessionChoice;
            set
            {
                _sessionChoice = value;

                // Clearing re-arms the announcement for the next raid, which a headless serving many
                // raids in one process depends on.
                if (value == null) { _announced = null; return; }

                var variant = MapVariant.Normalise(value);
                if (variant == _announced) return;
                _announced = variant;
                Announce(variant);
            }
        }

        static string _announced;

        // Each subscriber in its own try. This fans out into third-party handlers, and a bare Invoke
        // lets the first one that throws take the raid with it: from the prompt it would stop the
        // player's click doing anything, and from the preset hook the scenes would never be swapped.
        static void Announce(string variant)
        {
            var handlers = Resolved;
            if (handlers == null) return;

            foreach (var handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<string>)handler)(variant);
                }
                catch (Exception e)
                {
                    var owner = handler.Target?.GetType().FullName ?? handler.Method.DeclaringType?.FullName ?? "an unknown subscriber";
                    Plugin.Log.LogError($"[PresetSwap] a VariantResolved handler in {owner} threw, continuing: {e}");
                }
            }
        }

        // Asked at most once per raid, and cleared with the choice, so a headless serving many raids in
        // one process asks again for each.
        static bool _asked;

        static FieldInfo _keysField, _pathField, _rcidField;

        sealed class Snapshot
        {
            internal List<object> Entries;
            internal List<string> Paths;
            internal List<string> Rcids;
        }

        // The preset objects are cached for the process, so their originals are remembered once.
        static readonly Dictionary<object, Snapshot> Snapshots = new Dictionary<object, Snapshot>();

        internal static void Install()
        {
            new Harmony(BuildInfo.Guid + ".presetswap").PatchAll(typeof(PresetSwap));
            Plugin.Log.LogInfo("[PresetSwap] armed");
        }

        internal static void ForgetChoice()
        {
            SessionChoice = null;
            _asked = false;
        }

        // The preset's ServerName is the location id, but only the time-of-day preset carries one: the
        // base preset's is empty and the culling child's is wrong. It loads first, so by the time the
        // others arrive the answer is already in SessionChoice.
        static void AskIfUnanswered(ScenesPreset preset)
        {
            if (_asked || SessionChoice != null) return;

            var locationId = (preset.ServerName ?? "").Trim().ToLowerInvariant();
            if (locationId.Length == 0) return;
            if (!FactoryScenes.ServerNames.Any(id => string.Equals(id, locationId, StringComparison.OrdinalIgnoreCase))) return;

            _asked = true;
            var answer = VariantSync.Ask(locationId);
            if (answer == null)
            {
                Plugin.Log.LogWarning($"[PresetSwap] the server did not answer for '{locationId}'; falling back to the configured default");
                return;
            }

            SessionChoice = answer;
            Plugin.Log.LogInfo($"[PresetSwap] nobody answered a prompt this raid; the server says {VariantDisplay.For(answer)}");
        }

        // Which Factory this raid is loading, answered from the loaded scenes rather than from our own
        // state, so anything downstream agrees with what the engine actually has.
        internal static bool ClassicLoaded()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).name == FactoryScenes.ClassicSoundScene) return true;
            return false;
        }

        [HarmonyPatch(typeof(LoadScenesFromPresetOperation), nameof(LoadScenesFromPresetOperation.LoadPresetAsync))]
        [HarmonyPrefix]
        static void BeforePresetLoad(ScenesPreset preset)
        {
            try
            {
                if (!preset) return;
                var keys = KeyList(preset);
                if (keys == null) return;

                var snapshot = Remember(preset, keys);
                var kind = FactoryScenes.Classify(snapshot.Paths);
                if (kind == PresetKind.NotOurs) return;

                AskIfUnanswered(preset);

                var variant = MapVariant.Normalise(SessionChoice ?? Plugin.DefaultVariant);
                if (variant == MapVariant.Classic) Apply(keys, FactoryScenes.ClassicPathsFor(kind), kind);
                else Revert(keys, snapshot, kind);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[PresetSwap] prefix failed, the shipped Factory loads: {e}");
            }
        }

        static IList KeyList(ScenesPreset preset)
        {
            _keysField = _keysField ?? AccessTools.Field(typeof(ScenesPreset), "_scenesResourceKeys");
            if (_keysField == null)
            {
                Plugin.Log.LogError("[PresetSwap] ScenesPreset._scenesResourceKeys not found; layout changed, swap disabled");
                return null;
            }
            return _keysField.GetValue(preset) as IList;
        }

        // Taken once per preset object, from the shipped state, before anything is written.
        static Snapshot Remember(ScenesPreset preset, IList keys)
        {
            if (Snapshots.TryGetValue(preset, out var existing)) return existing;
            var snapshot = new Snapshot
            {
                Entries = keys.Cast<object>().ToList(),
                Paths = keys.Cast<object>().Select(GetPath).ToList(),
                Rcids = keys.Cast<object>().Select(GetRcid).ToList(),
            };
            Snapshots[preset] = snapshot;
            return snapshot;
        }

        static void Apply(IList keys, IReadOnlyList<string> want, PresetKind kind)
        {
            for (int i = 0; i < want.Count && i < keys.Count; i++) SetPath(keys[i], want[i]);
            for (int i = keys.Count - 1; i >= want.Count; i--) keys.RemoveAt(i);
            Plugin.Log.LogInfo($"[PresetSwap] {kind} preset now loads {keys.Count} classic scene(s)");
        }

        // The preset is mutated in place and lives for the process, so a vanilla raid after a classic
        // one must actively put the shipped list back rather than simply doing nothing.
        static void Revert(IList keys, Snapshot snapshot, PresetKind kind)
        {
            keys.Clear();
            for (int i = 0; i < snapshot.Entries.Count; i++)
            {
                var entry = snapshot.Entries[i];
                SetPath(entry, snapshot.Paths[i], snapshot.Rcids[i]);
                keys.Add(entry);
            }
            Plugin.Log.LogDebug($"[PresetSwap] {kind} preset restored to its {keys.Count} shipped scene(s)");
        }

        static string GetPath(object key)
        {
            if (key == null) return null;
            _pathField = _pathField ?? AccessTools.Field(key.GetType(), "path");
            return _pathField?.GetValue(key) as string;
        }

        static string GetRcid(object key)
        {
            if (key == null) return null;
            _rcidField = _rcidField ?? AccessTools.Field(key.GetType(), "rcid");
            return _rcidField?.GetValue(key) as string;
        }

        static void SetPath(object key, string path, string rcid = null)
        {
            if (key == null) return;
            _pathField = _pathField ?? AccessTools.Field(key.GetType(), "path");
            _rcidField = _rcidField ?? AccessTools.Field(key.GetType(), "rcid");
            if (_pathField == null || _rcidField == null)
            {
                Plugin.Log.LogError($"[PresetSwap] path or rcid not found on {key.GetType().Name}; layout changed, swap disabled");
                return;
            }
            _pathField.SetValue(key, path);

            // rcid is byte-identical to path in this build; an entry that shipped with an empty one
            // keeps it empty.
            var current = _rcidField.GetValue(key) as string;
            if (rcid != null) _rcidField.SetValue(key, rcid);
            else if (!string.IsNullOrEmpty(current)) _rcidField.SetValue(key, path);
        }
    }
}
