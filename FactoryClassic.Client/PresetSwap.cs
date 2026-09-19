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
    /// <summary>
    /// Points the scene preset at the classic tile. A Factory raid loads THREE presets - the
    /// time-of-day one, a shared base and a culling child - and the swap is a list REPLACEMENT, 14
    /// shipped keys against 7 classic ones, not a rename.
    /// </summary>
    [HarmonyPatch]
    internal static class PresetSwap
    {
        // Rests on Classic because that is where MapVariants' location table rests: its server
        // installs the backport dataset at load and its client answers "backport" until somebody
        // picks. The scenes must agree with the table.
        static string _variant = MapVariant.Classic;

        internal static string Variant => _variant;

        internal static void SetVariant(string variant) => _variant = MapVariant.Normalise(variant);

        /// <summary>
        /// Called by MapVariants with ITS wire value, at the decision point on the map screen and
        /// again on the load path. Never call it with one of our own values: their "backport" is our
        /// "classic", and a pass-through through either mod's Normalise silently means "original".
        /// </summary>
        internal static void OnMapVariantsAnswer(string theirVariant)
        {
            var ours = VariantVocabulary.FromMapVariants(theirVariant);

            if (MapVariant.IsClassic(ours) && !ServerManagesFactory(out var locationId))
            {
                Plugin.Log.LogError("[PresetSwap] MapVariants asked for the classic tile, but its server does not manage "
                                  + $"'{locationId ?? "factory"}'. Loading the shipped Factory instead: the location table holds "
                                  + "the shipped data and the two must agree.");
                SetVariant(MapVariant.Original);
                return;
            }

            SetVariant(ours);
            Plugin.Log.LogInfo($"[PresetSwap] MapVariants says {VariantDisplay.For(ours)}");
        }

        // MapVariants says "backport" whenever nobody has answered, which is only right when our
        // server half registered. Without this check, a server that did not would get the classic
        // scenes over the shipped tile's coordinates, with nothing in any log.
        static bool ServerManagesFactory(out string locationId)
        {
            locationId = MapVariantsApi.CurrentLocationId;

            // No location means nobody has answered yet, so both ids have to hold.
            return locationId != null
                ? ManagedGuard.Managed(locationId)
                : ManagedGuard.Managed(ExtensionApiContract.DayLocationId)
                  && ManagedGuard.Managed(ExtensionApiContract.NightLocationId);
        }

        static FieldInfo _keysField, _pathField, _rcidField;

        sealed class Snapshot
        {
            internal List<object> Entries;
            internal List<string> Paths;
            internal List<string> Rcids;
        }

        // The preset objects are cached for the process, so their shipped state is remembered once
        // and a vanilla raid after a classic one can actively put the list back.
        static readonly Dictionary<object, Snapshot> Snapshots = new Dictionary<object, Snapshot>();

        internal static void Install()
        {
            new Harmony(BuildInfo.Guid + ".presetswap").PatchAll(typeof(PresetSwap));
            Plugin.Log.LogInfo("[PresetSwap] armed");
        }

        /// <summary>
        /// Whether the classic scenes are loaded, answered from the engine rather than from our own
        /// state, so anything downstream agrees with what the game actually has.
        /// </summary>
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

                var variant = Variant;
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

            // rcid is byte-identical to path in this build, but an entry that shipped with an empty
            // one keeps it empty.
            var current = _rcidField.GetValue(key) as string;
            if (rcid != null) _rcidField.SetValue(key, rcid);
            else if (!string.IsNullOrEmpty(current)) _rcidField.SetValue(key, path);
        }
    }
}
