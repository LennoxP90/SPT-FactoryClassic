using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using SPT.Common.Http;

namespace FactoryClassic.Client
{
    // Asks the server which of this profile's accepted quests the classic tile cannot satisfy.
    //
    // The server answers rather than the client working it out, because the list depends on the
    // profile and on a server config key, and because the evidence behind it - which trigger zones
    // exist in which scenes - is a committed table there, not something to re-derive per prompt.
    internal static class QuestGateSync
    {
        const string Route = "/factoryclassic/questgate";

        static readonly string[] None = new string[0];

        // Quest names to put in front of the player, or nothing at all. Never throws: a warning that
        // cannot be fetched must not stop the player picking a map.
        internal static string[] AcceptedBlocked()
        {
            try
            {
                var json = RequestHandler.GetJson(Route);
                if (string.IsNullOrWhiteSpace(json)) return None;

                var accepted = JObject.Parse(json)["accepted"] as JArray;
                if (accepted == null || accepted.Count == 0) return None;

                var names = new List<string>(accepted.Count);
                foreach (var name in accepted)
                {
                    var text = name?.ToString();
                    if (!string.IsNullOrWhiteSpace(text)) names.Add(text);
                }

                Plugin.Log.LogInfo($"[QuestGate] {names.Count} accepted quest(s) cannot be finished on classic: {string.Join(", ", names.ToArray())}");
                return names.ToArray();
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[QuestGate] could not read the quest gate: {e.GetType().Name}: {e.Message}");
                return None;
            }
        }
    }
}
