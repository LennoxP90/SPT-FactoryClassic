using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace FactoryClassic.Client
{
    [BepInPlugin(BuildInfo.Guid, "FactoryClassic", BuildInfo.Version)]
    // HARD, not soft. It also guarantees MapVariants' Awake has run before ours, which is what makes
    // calling Maps.Register from Awake legal.
    [BepInDependency("com.lennoxp90.mapvariants")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static string PluginDir;

        internal static ConfigEntry<bool> SpatialRouting;
        internal static ConfigEntry<bool> WirePortals;
        internal static ConfigEntry<bool> CameraReport;
        internal static ConfigEntry<bool> RepairLootClusters;

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
            Safe(nameof(WaypointsStandDown), WaypointsStandDown.Install);
            Safe("MapVariants", RegisterWithMapVariants);
            Safe("ApiSelfCheck", Api.ApiSelfCheck.Run);

            Log.LogInfo($"[FC] FactoryClassic {BuildInfo.Version} loaded; MapVariants owns the choice");
        }

        // Both Factory ids go over in ONE registration: they are one map to the player, one prompt
        // and one F12 default. The SERVER registers them separately, because each id has its own
        // Location object there.
        //
        // The variant names are the SHORT names alone. MapVariants composes "<map> - <variant>"
        // itself, so passing the full label would render "Factory - Factory - Classic".
        static void RegisterWithMapVariants()
        {
            var registered = MapVariantsApi.Register(
                new[] { Shared.ExtensionApiContract.DayLocationId, Shared.ExtensionApiContract.NightLocationId },
                Shared.VariantDisplay.MapName,
                Shared.VariantDisplay.ClassicName,   // backportName: the CLASSIC tile. Not the other way round.
                Shared.VariantDisplay.VanillaName,
                TilePath("factory_classic.png"),
                TilePath("factory_vanilla.png"),
                PresetSwap.OnMapVariantsAnswer,
                QuestGateSync.Warnings);

            if (registered) Log.LogInfo("[FC] registered both Factory locations with MapVariants");
            else Log.LogError("[FC] MapVariants refused the registration; the shipped Factory loads and nothing is offered. "
                            + "Look for a [MV] Register refused line above this one.");
        }

        // Absolute. MapVariants resolves a tile against its OWN plugin directory, so a relative path
        // is looked for in their folder and the miss is silent.
        static string TilePath(string file) => Path.Combine(PluginDir, "plugin-data", "ui", file);

        void BindConfig()
        {
            // Off is the broken setting, not the safe one: with an empty table 4.1 has no
            // propagation at all, since every occluder except Fast reads baked routes only.
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

        // Hidden from the F12 menu but still bound, so the instrument stays there for a bug report.
        static ConfigDescription Hidden(string text) =>
            new ConfigDescription(text, null, new ConfigurationManagerAttributes { Browsable = false });

        // ConfigurationManager sorts by Order DESCENDING, then alphabetically.
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
