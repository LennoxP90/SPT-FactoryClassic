using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace FactoryClassic.Client
{
    // Stops Waypoints installing its Factory navmesh on the CLASSIC tile only.
    //
    // Waypoints ships factory4-navmesh.bundle and injects it with RemoveAllNavMeshData followed by
    // AddNavMeshData - a whole-map replacement, not an addition. The shipped bundle is dated November
    // 2025, so it is baked on the reworked Factory, and on the classic tile that puts walkable surface
    // where the geometry does not exist.
    //
    // It is not hypothetical: the first classic raid logged "Injected custom navmesh:
    // factory4-navmesh.bundle", and one scav then stood still in x and z at (30.5, -30.2) while its y
    // oscillated between -1 and -24. That is what a bot does when it walks onto navmesh with no floor
    // under it, and it was originally written up here as a missing collider.
    //
    // Declining is safe because the classic Factory_AI scene ships its own NavMeshData inside the
    // client (verified in sharedassets1), so BSG's own bake is what remains.
    //
    // On the vanilla tile Waypoints is left alone: its bundle is the right mesh for that map. With
    // Waypoints absent, TargetMethod resolves to nothing and Prepare stops the patch being applied.
    [HarmonyPatch]
    internal static class WaypointsStandDown
    {
        internal static void Install()
        {
            new Harmony(BuildInfo.Guid + ".waypoints").PatchAll(typeof(WaypointsStandDown));
            Plugin.Log.LogInfo(Injector() == null
                ? "[Waypoints] not installed; nothing to stand down"
                : "[Waypoints] armed; its Factory navmesh will be declined on the classic tile");
        }

        static bool Prepare() => Injector() != null;

        static MethodBase TargetMethod() => Injector();

        // Found by method name inside Waypoints' own assembly: no compile-time reference, and the
        // class name is not stable across its versions.
        static MethodBase Injector()
        {
            try
            {
                var assembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => !a.IsDynamic && a.GetName().Name.IndexOf("Waypoints", StringComparison.OrdinalIgnoreCase) >= 0);

                return assembly?.GetTypes()
                    .Select(type => type.GetMethod("InjectNavmesh", AccessTools.all))
                    .FirstOrDefault(method => method != null && method.IsStatic && method.GetParameters().Length == 1);
            }
            catch (Exception)
            {
                return null;
            }
        }

        static bool _said;

        static bool Prefix()
        {
            // Any other map, and the vanilla Factory, get Waypoints' bundle exactly as before.
            if (!PresetSwap.ClassicLoaded()) return true;

            if (!_said)
            {
                _said = true;
                Plugin.Log.LogInfo("[Waypoints] declined its Factory navmesh on the classic tile: it replaces the whole map's " +
                                   "mesh and is baked on the reworked layout. The classic Factory_AI scene carries its own bake.");
            }

            return false;
        }
    }
}
