using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace FactoryClassic.Client
{
    /// <summary>
    /// Stops Waypoints installing its Factory navmesh on the CLASSIC tile only. Its bundle is baked
    /// on the reworked layout and replaces the whole map's mesh, which puts walkable surface where
    /// the classic geometry does not exist. The classic Factory_AI scene carries BSG's own bake.
    /// See docs/BUGS.md.
    /// </summary>
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

        // By method name inside Waypoints' own assembly: no compile-time reference, and the class
        // name is not stable across its versions.
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
