using System;
using System.Collections.Generic;
using FactoryClassic.Shared;
using Newtonsoft.Json.Linq;
using SPT.Common.Http;

namespace FactoryClassic.Client
{
    /// <summary>
    /// Which locations MapVariants' SERVER actually manages, asked once per session. The client
    /// registers independently of the server half, so without this a failed server build still lets
    /// the classic scenes load over the SHIPPED location's spawn points, which drops the player
    /// through the map with nothing in any log. Asked of MapVariants because it owns the registry.
    /// </summary>
    internal static class ManagedGuard
    {
        const string Route = "/mapvariants/managed";

        static HashSet<string> _managed;
        static bool _asked;

        internal static bool Managed(string locationId)
        {
            var id = LocationId.Normalise(locationId);
            if (id.Length == 0) return false;
            Ask();
            return _managed != null && _managed.Contains(id);
        }

        internal static void Forget() { _asked = false; _managed = null; }

        static void Ask()
        {
            if (_asked) return;
            _asked = true;
            try
            {
                var json = RequestHandler.GetJson(Route);
                if (string.IsNullOrWhiteSpace(json)) { Report(null); return; }

                // Normalise on the way in as well as out: SPT's casing is mixed, so a direct
                // Contains is right for factory4_day and wrong for Lighthouse.
                var ids = JObject.Parse(json)["locationIds"] as JArray;
                var set = new HashSet<string>();
                if (ids != null)
                    foreach (var id in ids)
                    {
                        var text = LocationId.Normalise(id?.ToString());
                        if (text.Length > 0) set.Add(text);
                    }
                _managed = set;
                Report(set);
            }
            catch (Exception e)
            {
                // Unanswerable counts as NOT managed: the cost of being wrong that way is a vanilla
                // raid, and the other way is classic scenes over vanilla coordinates.
                Plugin.Log.LogError($"[FC] could not read {Route} ({e.GetType().Name}: {e.Message}); "
                                  + "no classic raid will be loaded this session");
                _managed = null;
            }
        }

        static void Report(HashSet<string> set)
        {
            var day = set != null && set.Contains(LocationId.Normalise(ExtensionApiContract.DayLocationId));
            var night = set != null && set.Contains(LocationId.Normalise(ExtensionApiContract.NightLocationId));
            if (day && night) Plugin.Log.LogInfo("[FC] MapVariants manages both Factory locations");
            else Plugin.Log.LogError($"[FC] MapVariants does NOT manage every Factory location (day={day}, night={night}); "
                                   + "the shipped Factory loads for the one it does not, whatever is chosen");
        }
    }
}
