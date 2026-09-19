using System;
using System.Reflection;
using HarmonyLib;

namespace FactoryClassic.Client
{
    /// <summary>
    /// MapVariants' public API, reached by reflection. A compile-time reference would load a second
    /// copy of MapVariants.Shared into the process and turn a version mismatch into a hard load
    /// failure. The BepInDependency already guarantees the plugin is there and that its Awake has
    /// run, so this only has to survive its API moving.
    /// </summary>
    internal static class MapVariantsApi
    {
        const string TypeName = "MapVariants.Client.Api.Maps";

        // A mismatch is reported and not acted on: declining to register would leave the player with
        // no way to choose at all, and every member below is already null-safe.
        const string KnownApiMajor = "3";

        static Type _type;
        static MethodInfo _register;
        static PropertyInfo _currentVariant, _currentLocationId, _apiVersion;
        static EventInfo _variantResolved;
        static bool _looked;

        internal static bool Available => Look() && _type != null;

        static bool Look()
        {
            if (_looked) return _type != null;
            _looked = true;

            _type = AccessTools.TypeByName(TypeName);
            if (_type == null)
            {
                Plugin.Log.LogError($"[FC] {TypeName} was not found. MapVariants is a hard dependency, so this should be "
                                  + "unreachable; if it is not, the two mods disagree about the type name and Factory has no variant switching.");
                return false;
            }

            _register = AccessTools.Method(_type, "Register");
            _currentVariant = AccessTools.Property(_type, "CurrentVariant");
            _currentLocationId = AccessTools.Property(_type, "CurrentLocationId");
            _apiVersion = AccessTools.Property(_type, "ApiVersion");
            _variantResolved = _type.GetEvent("VariantResolved", BindingFlags.Public | BindingFlags.Static);

            if (_register == null || _currentVariant == null || _currentLocationId == null || _variantResolved == null)
                Plugin.Log.LogError($"[FC] {TypeName} is not the expected layout "
                                  + $"(Register={_register != null}, CurrentVariant={_currentVariant != null}, "
                                  + $"CurrentLocationId={_currentLocationId != null}, VariantResolved={_variantResolved != null})");

            ReportApiVersion();
            return true;
        }

        static void ReportApiVersion()
        {
            var version = Read(_apiVersion);
            if (version == null) { Plugin.Log.LogWarning($"[FC] {TypeName} publishes no readable ApiVersion"); return; }

            var major = version.Split('.')[0];
            if (major == KnownApiMajor) Plugin.Log.LogInfo($"[FC] MapVariants API {version}");
            else Plugin.Log.LogWarning($"[FC] MapVariants publishes API {version}, and this build was written against "
                                     + $"{KnownApiMajor}.x. Registering anyway; if the map screen offers nothing, this line is why.");
        }

        internal static bool Register(string[] locationIds, string displayName, string backportName, string originalName,
                                      string backportTile, string originalTile,
                                      Action<string> onVariant, Func<string[]> backportWarnings)
        {
            if (!Look() || _register == null) return false;
            try
            {
                var result = _register.Invoke(null, new object[]
                {
                    locationIds, displayName, backportName, originalName, backportTile, originalTile, onVariant, backportWarnings
                });
                return result is bool ok && ok;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[FC] registering with MapVariants threw: {e}");
                return false;
            }
        }

        // THEIR wire values. Translate with VariantVocabulary before comparing to anything of ours.
        internal static string CurrentVariant => Read(_currentVariant);
        internal static string CurrentLocationId => Read(_currentLocationId);

        // Look() sets _looked first, so ReportApiVersion calling back through here returns
        // immediately rather than recursing.
        static string Read(PropertyInfo property)
        {
            if (!Look() || property == null) return null;
            try { return property.GetValue(null) as string; }
            catch (Exception e) { Plugin.Log.LogWarning($"[FC] reading {property.Name} from MapVariants threw: {e.Message}"); return null; }
        }

        internal static void Subscribe(Action<string> handler) => Hook("AddEventHandler", handler);
        internal static void Unsubscribe(Action<string> handler) => Hook("RemoveEventHandler", handler);

        static void Hook(string which, Action<string> handler)
        {
            if (!Look() || _variantResolved == null || handler == null) return;
            try
            {
                var accessor = which == "AddEventHandler" ? _variantResolved.GetAddMethod(true) : _variantResolved.GetRemoveMethod(true);
                accessor?.Invoke(null, new object[] { handler });
            }
            catch (Exception e) { Plugin.Log.LogWarning($"[FC] {which} on VariantResolved threw: {e.Message}"); }
        }
    }
}
