using System.Text.Json.Serialization;
using FactoryClassic.Shared;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils;

namespace FactoryClassic.Server.Variants;

// Must implement IRequestData. Without it this compiles and then the dispatcher throws on the first
// request.
public sealed class VariantChoiceRequest : IRequestData
{
    [JsonPropertyName("locationId")] public string LocationId { get; set; } = "";
    [JsonPropertyName("variant")] public string Variant { get; set; } = "";
}

// What a caller reads back: our own type, not SPT's {err, errMsg, data} envelope, so the client can
// deserialise it directly.
public sealed class VariantChoiceResponse
{
    [JsonPropertyName("variant")] public string Variant { get; set; } = MapVariant.Original;

    // Claimed, Changed or Joined - so the client can say whether the player decided or inherited.
    [JsonPropertyName("outcome")] public string Outcome { get; set; } = "";
}

// POST records the choice a player made on the map screen; GET reads one back. A Fika headless uses
// the GET to find out which Factory the starting player picked, since it hosts under its own session.
//
// The two paths are separate because a RouteAction that declares a body type fails on a null body,
// and SPT hands every GET a null one.
[Injectable, UsedImplicitly]
public class VariantRoutes(
    JsonUtil jsonUtil,
    HttpResponseUtil httpResponseUtil,
    VariantChoiceStore choiceStore,
    TransitClaimStore transitClaims,
    VariantRegistry registry,
    IHttpContextAccessor httpContextAccessor,
    ISptLogger<VariantRoutes> logger
) : StaticRouter(
    jsonUtil,
    [
        new RouteAction<VariantChoiceRequest>(
            "/factoryclassic/variant",
            (url, info, sessionId, output, cancellationToken) =>
                ValueTask.FromResult(Record(info, sessionId, choiceStore, transitClaims, registry, httpResponseUtil, logger))),

        new RouteAction<VariantChoiceRequest>(
            "/factoryclassic/transitclaim",
            (url, info, sessionId, output, cancellationToken) =>
                ValueTask.FromResult(ClaimTransit(info, sessionId, transitClaims, registry, httpResponseUtil, logger))),

        new RouteAction<EmptyRequestData>(
            "/factoryclassic/hostvariant",
            (url, info, sessionId, output, cancellationToken) =>
                ValueTask.FromResult(Read(httpContextAccessor.HttpContext?.Request, sessionId, choiceStore, registry, httpResponseUtil))),
    ]
)
{
    private static string Record(
        VariantChoiceRequest? info, MongoId sessionId, VariantChoiceStore choiceStore,
        TransitClaimStore transitClaims, VariantRegistry registry, HttpResponseUtil httpResponseUtil,
        ISptLogger<VariantRoutes> logger)
    {
        var locationId = (info?.LocationId ?? "").Trim().ToLowerInvariant();
        var variant = MapVariant.Normalise(info?.Variant);

        if (locationId.Length == 0 || !registry.HasClassic(locationId))
            return httpResponseUtil.NoBody(new VariantChoiceResponse { Variant = MapVariant.Original });

        // An explicit pick on the map screen outranks a claim inherited from a transit, so the claim
        // goes rather than being preferred by the next raid-start route.
        transitClaims.Release(locationId);
        choiceStore.Record(sessionId, locationId, variant);

        // Installed HERE, the moment the choice is made, rather than at raid start. This POST lands
        // while the player is still on the map screen, seconds before anything asks for the
        // location; a raid-start route lands after SPT and Fika have already built it.
        registry.Install(locationId, variant);

        logger.Info($"[FC] {sessionId} chose '{variant}' for '{locationId}'; the table now serves {registry.Installed(locationId)}");
        return httpResponseUtil.NoBody(new VariantChoiceResponse { Variant = variant });
    }

    // First player into a transit zone decides the variant for everyone who joins it. A transit has
    // no map screen, so without this nobody is prompted and each client falls back on its own stale
    // preference - which on Fika means one player loading different scenes from the ones served.
    private static string ClaimTransit(
        VariantChoiceRequest? info, MongoId sessionId, TransitClaimStore transitClaims,
        VariantRegistry registry, HttpResponseUtil httpResponseUtil, ISptLogger<VariantRoutes> logger)
    {
        var locationId = (info?.LocationId ?? "").Trim().ToLowerInvariant();

        if (locationId.Length == 0 || !registry.HasClassic(locationId))
            return httpResponseUtil.NoBody(new VariantChoiceResponse { Variant = MapVariant.Original });

        var winner = transitClaims.Claim(locationId, info?.Variant, sessionId.ToString(), out var outcome);

        // Installed as soon as it is claimed, which is while the transit is still counting down -
        // far earlier than the map-screen POST manages, and well before the destination raid is built.
        if (outcome != TransitClaimStore.Outcome.Joined)
        {
            registry.Install(locationId, winner);
            logger.Info($"[FC] {sessionId} {(outcome == TransitClaimStore.Outcome.Changed ? "changed the transit to" : "claimed")} "
                        + $"'{winner}' for '{locationId}'");
        }
        else
        {
            logger.Debug($"[FC] {sessionId} joined a transit to '{locationId}' already claimed as '{winner}'");
        }

        return httpResponseUtil.NoBody(new VariantChoiceResponse { Variant = winner, Outcome = outcome.ToString() });
    }

    private static string Read(
        HttpRequest? request, MongoId sessionId, VariantChoiceStore choiceStore,
        VariantRegistry registry, HttpResponseUtil httpResponseUtil)
    {
        var locationId = (request?.Query["locationId"].ToString() ?? "").Trim().ToLowerInvariant();

        // What the table is actually serving, rather than a per-session re-derivation of what it
        // ought to be. One source of truth: a Fika joiner and a headless both load the scenes that
        // match the data the server is about to hand them, which re-resolving could not guarantee.
        var variant = registry.HasClassic(locationId)
            ? registry.InstalledVariant(locationId)
            : MapVariant.Original;

        return httpResponseUtil.NoBody(new VariantChoiceResponse { Variant = variant });
    }
}
