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
    VariantRegistry registry,
    IHttpContextAccessor httpContextAccessor,
    ISptLogger<VariantRoutes> logger
) : StaticRouter(
    jsonUtil,
    [
        new RouteAction<VariantChoiceRequest>(
            "/factoryclassic/variant",
            (url, info, sessionId, output, cancellationToken) =>
                ValueTask.FromResult(Record(info, sessionId, choiceStore, registry, httpResponseUtil, logger))),

        new RouteAction<EmptyRequestData>(
            "/factoryclassic/hostvariant",
            (url, info, sessionId, output, cancellationToken) =>
                ValueTask.FromResult(Read(httpContextAccessor.HttpContext?.Request, sessionId, choiceStore, registry, httpResponseUtil))),
    ]
)
{
    private static string Record(
        VariantChoiceRequest? info, MongoId sessionId, VariantChoiceStore choiceStore,
        VariantRegistry registry, HttpResponseUtil httpResponseUtil, ISptLogger<VariantRoutes> logger)
    {
        var locationId = (info?.LocationId ?? "").Trim().ToLowerInvariant();
        var variant = MapVariant.Normalise(info?.Variant);

        if (locationId.Length == 0 || !registry.HasClassic(locationId))
            return httpResponseUtil.NoBody(new VariantChoiceResponse { Variant = MapVariant.Original });

        choiceStore.Record(sessionId, locationId, variant);

        // Installed HERE, the moment the choice is made, rather than at raid start. This POST lands
        // while the player is still on the map screen, seconds before anything asks for the
        // location; a raid-start route lands after SPT and Fika have already built it.
        registry.Install(locationId, variant);

        logger.Info($"[FC] {sessionId} chose '{variant}' for '{locationId}'; the table now serves {registry.Installed(locationId)}");
        return httpResponseUtil.NoBody(new VariantChoiceResponse { Variant = variant });
    }

    private static string Read(
        HttpRequest? request, MongoId sessionId, VariantChoiceStore choiceStore,
        VariantRegistry registry, HttpResponseUtil httpResponseUtil)
    {
        var locationId = (request?.Query["locationId"].ToString() ?? "").Trim().ToLowerInvariant();

        var variant = VariantResolution.Resolve(
            choiceStore.ChoiceFor(sessionId, locationId),
            choiceStore.LatestFor(locationId),
            registry.ConfiguredDefault);

        return httpResponseUtil.NoBody(new VariantChoiceResponse { Variant = variant });
    }
}
