using System.Text.Json.Serialization;
using FactoryClassic.Shared;
using JetBrains.Annotations;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils;

namespace FactoryClassic.Server.Variants;

// The body of a raid start or end. Only the location is read.
public sealed class RaidBoundaryRequest : IRequestData
{
    [JsonPropertyName("location")] public string Location { get; set; } = "";
    [JsonPropertyName("serverId")] public string ServerId { get; set; } = "";
}

// Installs the dataset for the raid that is starting, and returns to the configured default after.
//
// TIMING IS THE WHOLE POINT OF THIS FILE. SPT builds a raid's location - spawn points, exits and
// loot - in LocationLifecycleService.GenerateLocationAndLoot, by copying whatever the location table
// holds at that instant. Fika calls it from /fika/raid/create, five seconds BEFORE
// /client/match/local/start. Swapping the table on a raid-start route therefore lands after the raid
// has already been built: the client loads the classic scenes while the host spawns everyone on
// coordinates authored for the shipped tile, which on classic geometry is open air.
//
// So the table is kept correct at all times instead. VariantRegistry installs the configured default
// at load, VariantRoutes installs a player's choice the moment it is made, and these routes are a
// backstop for a raid that reached here some other way.
//
// The priority puts this router BEFORE SPT's own: every static router matching a url runs in
// registration order, and SPT's match router generates the location, so running after it is too
// late on that route too.
//
// The location id is never rewritten: both variants answer to factory4_day and factory4_night, which
// is what keeps quests, stats and insurance working across them.
[Injectable(TypePriority = OnLoadOrder.Routers - 1), UsedImplicitly]
public class VariantRaidHooks(
    JsonUtil jsonUtil,
    VariantRegistry registry,
    VariantChoiceStore choiceStore,
    TransitClaimStore transitClaims,
    ISptLogger<VariantRaidHooks> logger)
    : StaticRouter(jsonUtil,
    [
        // The earliest route that names the location. It lands before Fika creates the raid, so it
        // is the one that matters on a Fika host; a headless posts it too.
        new RouteAction<RaidBoundaryRequest>("/client/raid/configuration",
            (url, info, sessionId, output, cancellationToken) =>
                OnRaidStart(info, sessionId, output, registry, choiceStore, transitClaims, logger)),

        new RouteAction<RaidBoundaryRequest>("/client/match/local/start",
            (url, info, sessionId, output, cancellationToken) =>
                OnRaidStart(info, sessionId, output, registry, choiceStore, transitClaims, logger)),

        new RouteAction<RaidBoundaryRequest>("/client/match/local/end",
            (url, info, sessionId, output, cancellationToken) =>
                OnRaidEnd(info, output, registry, transitClaims, logger)),
    ])
{
    private static ValueTask<string> OnRaidStart(
        RaidBoundaryRequest? info, MongoId sessionId, string output,
        VariantRegistry registry, VariantChoiceStore choiceStore, TransitClaimStore transitClaims,
        ISptLogger<VariantRaidHooks> logger)
    {
        var locationId = Normalise(info?.Location);
        if (locationId.Length > 0 && registry.HasClassic(locationId))
        {
            try
            {
                // A live transit claim outranks everything: the raid is being arrived at rather than
                // chosen, and the first player into the zone already decided for the whole group. Both
                // raid-start routes run, so this has to be preferred on every call or the second would
                // re-resolve and overwrite the first.
                var claimed = transitClaims.Held(locationId);

                // A Fika headless posts this route under its OWN session, so it has no choice of its
                // own and inherits the last one made for the location.
                var variant = claimed.Length > 0
                    ? claimed
                    : VariantResolution.Resolve(
                        choiceStore.ChoiceFor(sessionId, locationId),
                        choiceStore.LatestFor(locationId),
                        registry.ConfiguredDefault);

                registry.Install(locationId, variant);
                logger.Info($"[FC] raid on '{locationId}': {(MapVariant.IsClassic(variant) ? "the CLASSIC tile" : "the shipped tile")}; "
                            + $"the table serves {registry.Installed(locationId)}");
            }
            catch (Exception e)
            {
                logger.Error($"[FC] could not install a variant for '{locationId}'; the shipped map is served: {e}");
            }
        }

        return ValueTask.FromResult(output);
    }

    private static ValueTask<string> OnRaidEnd(
        RaidBoundaryRequest? info, string output, VariantRegistry registry,
        TransitClaimStore transitClaims, ISptLogger<VariantRaidHooks> logger)
    {
        var locationId = Normalise(info?.Location);
        if (locationId.Length > 0 && registry.HasClassic(locationId))
        {
            // Released here and not at raid start, because both raid-start routes run and the second
            // would find nothing left to prefer.
            transitClaims.Release(locationId);
            registry.InstallDefaults();
            logger.Debug($"[FC] raid over on '{locationId}'; the table is back to the configured default");
        }

        return ValueTask.FromResult(output);
    }

    private static string Normalise(string? location) => (location ?? "").Trim().ToLowerInvariant();
}
