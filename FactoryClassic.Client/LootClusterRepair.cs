using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace FactoryClassic.Client
{
    /// <summary>
    /// The classic scene's loot clusters ship with _connectionGroup 0 and no BotZone, because both
    /// fields postdate the scene, and LootPatrolLayer then rejects every cluster and parks each bot
    /// on the cover point under its feet. The group is derived the way the bake does it, from the
    /// cluster's central point to its core. See docs/BUGS.md.
    /// </summary>
    internal static class LootClusterRepair
    {
        internal static void Install()
        {
            var patrols = AccessTools.TypeByName("AIPatrolsData");
            var target = patrols == null ? null : AccessTools.Method(patrols, "Restore");
            if (target == null) { Plugin.Log.LogWarning("[LootClusters] AIPatrolsData.Restore not found; no repair"); return; }

            new Harmony(BuildInfo.Guid + ".lootclusters").Patch(target,
                postfix: new HarmonyMethod(typeof(LootClusterRepair), nameof(AfterRestore)));
            Plugin.Log.LogInfo("[LootClusters] armed");
        }

        static object Field(object target, string name) =>
            target == null ? null : AccessTools.Field(target.GetType(), name)?.GetValue(target);

        static object Prop(object target, string name) =>
            target == null ? null : AccessTools.Property(target.GetType(), name)?.GetValue(target);

        static void AfterRestore(object __instance)
        {
            try
            {
                if (!Plugin.RepairLootClusters.Value || !PresetSwap.ClassicLoaded()) return;

                var clusters = Field(__instance, "LootPointClusters") as IList;
                if (clusters == null || clusters.Count == 0) { Plugin.Log.LogInfo("[LootClusters] no clusters on this tile"); return; }

                var points = new ArrayList();
                if (Field(__instance, "SimpleLootPoints") is IList simple) points.AddRange(simple);
                if (Field(__instance, "ContainerLootPoints") is IList containers) points.AddRange(containers);

                var zoneType = AccessTools.TypeByName("BotZone");
                var zone = zoneType == null ? null : UnityEngine.Object.FindObjectOfType(zoneType);

                int repaired = 0, alreadyGood = 0, unresolved = 0;
                var groupsSeen = new System.Collections.Generic.HashSet<int>();

                foreach (var cluster in clusters)
                {
                    var groupField = AccessTools.Field(cluster.GetType(), "_connectionGroup");
                    var current = (int)(groupField?.GetValue(cluster) ?? -1);
                    if (current > 0) { alreadyGood++; continue; }

                    var centralId = (int)(Field(cluster, "_centralPointId") ?? -1);
                    var central = points.Cast<object>().FirstOrDefault(p => Equals(Field(p, "Id") ?? Prop(p, "Id"), centralId));
                    var core = Prop(central, "CorePointInGame");
                    var group = core == null ? -1 : (int)(Prop(core, "ConnectionGroupId") ?? -1);

                    if (group <= 0)
                    {
                        unresolved++;
                        Plugin.Log.LogWarning($"[LootClusters] cluster {Field(cluster, "_clusterId")}: central point {centralId} " +
                                              $"{(central == null ? "not found" : "has no core")}; left unrepaired");
                        continue;
                    }

                    groupField.SetValue(cluster, group);
                    groupsSeen.Add(group);

                    if (zone != null && Field(cluster, "_botZone") == null)
                        AccessTools.Method(cluster.GetType(), "LinkToBotZone")?.Invoke(cluster, new[] { zone });

                    repaired++;
                }

                Plugin.Log.LogInfo($"[LootClusters] {repaired}/{clusters.Count} cluster(s) set to group {string.Join(",", groupsSeen.Select(g => g.ToString()).ToArray())}; {alreadyGood} already good, {unresolved} unresolved");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[LootClusters] repair failed, clusters stay as shipped: {e}");
            }
        }
    }
}
