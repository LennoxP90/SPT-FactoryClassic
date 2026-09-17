using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace FactoryClassic.Client
{
    [BepInPlugin(BuildInfo.Guid, "FactoryClassic", BuildInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static string PluginDir;

        internal static ConfigEntry<bool> PromptSelection;
        internal static ConfigEntry<FactoryChoice> DefaultSelection;
        internal static ConfigEntry<bool> SpatialRouting;
        internal static ConfigEntry<bool> WirePortals;
        internal static ConfigEntry<bool> CameraReport;
        internal static ConfigEntry<bool> RepairLootClusters;

        // The configured fallback as a Shared wire value. Read at the point of use, never cached, so
        // an edit picked up by ConfigReload actually applies.
        internal static string DefaultVariant =>
            DefaultSelection.Value == FactoryChoice.Classic ? Shared.MapVariant.Classic : Shared.MapVariant.Original;

        void Awake()
        {
            Log = Logger;
            PluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            BindConfig();

            Safe(nameof(ConfigReload), () => ConfigReload.Install(Config));
            Safe(nameof(PresetSwap), PresetSwap.Install);
            Safe(nameof(RaidLifecycle), RaidLifecycle.Install);
            Safe(nameof(SpatialAudioRepair), SpatialAudioRepair.Install);
            Safe(nameof(CameraEffectsRepair), CameraEffectsRepair.Install);
            Safe(nameof(CameraInventory), CameraInventory.Install);
            Safe(nameof(LootClusterRepair), LootClusterRepair.Install);
            Safe(nameof(TransitRepair), TransitRepair.Install);
            Safe(nameof(TransitVariantClaim), TransitVariantClaim.Install);
            Safe(nameof(TransitChoicePrompt), TransitChoicePrompt.Install);
            Safe(nameof(WaypointsStandDown), WaypointsStandDown.Install);
            Safe(nameof(MapVariantPrompt), MapVariantPrompt.Install);
            Safe(nameof(RaidLoadingLabel), RaidLoadingLabel.Install);
            Safe("ApiSelfCheck", Api.ApiSelfCheck.Run);

            Log.LogInfo($"[FC] FactoryClassic {BuildInfo.Version} loaded, prompt={PromptSelection.Value} default={DefaultSelection.Value}");
        }

        void BindConfig()
        {
            PromptSelection = Config.Bind("General", "PromptSelection", true, Shown(
                "Ask which Factory to load when Factory is picked on the map screen. The answer applies to that "
                + "raid only. Off = never ask, and every Factory raid loads DefaultSelection.", 30));
            DefaultSelection = Config.Bind("General", "DefaultSelection", FactoryChoice.Classic, Shown(
                "Which Factory to load when PromptSelection is Off. Classic = the original layout, as in SPT 3.9.8. "
                + "Vanilla = the Factory SPT ships. Also the fallback for anyone who never answers the prompt, "
                + "a headless included.", 20));

            // Off is the broken default, not the safe one: with an empty table 4.1 has no propagation
            // at all, since every occluder except Fast reads baked routes only. It was off until
            // LocationInfoCapacities fixed the zero-length job buffers that crashed the raid.
            SpatialRouting = Config.Bind("General", "SpatialRouting", true, Shown(
                "Use the classic tile's real routing table, so sound carries between rooms through the openings that "
                + "connect them. Off falls back to an empty table, which means no room-to-room routing anywhere on the "
                + "map and everything decided by line of sight - audibly wrong through gates and doorways.", 10));

            WirePortals = Config.Bind("General", "WirePortals", true, Shown(
                "Repair the audio portals in the classic sound scene that connect no rooms, first from the room pair the "
                + "scene itself declares in roomConnections and then, where there is none, by reading the two rooms "
                + "either side of the portal's own collider. Without this they are acoustically absent, so sound does "
                + "not carry through the openings they sit in - several of them gates - and a door whose portal is "
                + "unwired throws inside its own opening animation.", 5));

            RepairLootClusters = Config.Bind("General", "RepairLootClusters", true, Shown(
                "Give the classic tile's loot clusters the connection group and Bot Zone the scene never recorded. Without "
                + "this the loot-patrol layer can never choose a target and every bot stands on the nearest cover point for "
                + "the whole raid. Off restores the shipped data, for comparison only.", 26));
            CameraReport = Config.Bind("Diagnostics", "CameraReport", true, Hidden(
                "Once per Factory raid, log what the render camera and its effects prefab carry, plus the texture "
                + "streaming globals. Open while the classic tile's dated look is being diagnosed: the answer is the "
                + "diff between a classic raid and a vanilla one."));
        }

        // ConfigurationManager reads Browsable off the tag object by duck typing, so hiding an entry
        // costs nothing and, unlike deleting it, leaves the instrument in place for a bug report.
        static ConfigDescription Hidden(string text) =>
            new ConfigDescription(text, null, new ConfigurationManagerAttributes { Browsable = false });

        // ConfigurationManager sorts by Order DESCENDING, then alphabetically. Without an explicit
        // order the two settings the mod exists for sort below incidental keys.
        static ConfigDescription Shown(string text, int order) =>
            new ConfigDescription(text, null, new ConfigurationManagerAttributes { Order = order });

        // One module's install must not take out the others.
        internal static void Safe(string name, Action install)
        {
            try { install(); }
            catch (Exception e) { Log.LogError($"[FC] {name} install failed, continuing without it: {e}"); }
        }
    }
}
