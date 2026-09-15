using System;
using FactoryClassic.Shared;
using Newtonsoft.Json.Linq;
using SPT.Common.Http;

namespace FactoryClassic.Client
{
    // Tells the server which Factory the player picked, and asks what a host picked.
    //
    // RequestHandler rather than HttpClient, because SPT inflates request bodies and deflates
    // responses without any Content-Encoding header to say so; a hand-rolled POST dies inside SPT's
    // own inflater with an error that reads like a fault in the mod.
    internal static class VariantSync
    {
        const string ChoiceRoute = "/factoryclassic/variant";

        // A separate path for reads: a route declaring a body type fails on the null body SPT hands
        // every GET.
        const string HostRoute = "/factoryclassic/hostvariant";

        internal static void Post(string locationId, string variant)
        {
            try
            {
                var body = new JObject { ["locationId"] = locationId, ["variant"] = variant }.ToString();
                RequestHandler.PostJson(ChoiceRoute, body);
                Plugin.Log.LogDebug($"[Variant] told the server '{variant}' for '{locationId}'");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[Variant] could not post '{variant}': {e.GetType().Name}: {e.Message}");
            }
        }

        // What this session should load, for a caller that never answered a prompt: a Fika headless,
        // or a client whose raid began without one. The server answers with the asker's own recorded
        // choice, else the last choice made for that location, else its configured default.
        internal static string Ask(string locationId)
        {
            try
            {
                var json = RequestHandler.GetJson($"{HostRoute}?locationId={locationId}");
                if (string.IsNullOrWhiteSpace(json)) return null;

                var variant = JObject.Parse(json)["variant"]?.ToString();
                if (string.IsNullOrWhiteSpace(variant)) return null;

                Plugin.Log.LogDebug($"[Variant] the server says '{variant}' for '{locationId}'");
                return MapVariant.Normalise(variant);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[Variant] could not read the host's choice: {e.GetType().Name}: {e.Message}");
                return null;
            }
        }
    }
}
