using System.Text.Json.Serialization;
using JetBrains.Annotations;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils;

namespace FactoryClassic.Server.Quests;

public sealed class QuestGateResponse
{
    [JsonPropertyName("mode")] public string Mode { get; set; } = "";

    // Quest names, already filtered to the ones this player has accepted, ready to show as they are.
    [JsonPropertyName("accepted")] public List<string> Accepted { get; set; } = [];
}

// What the map-choice prompt needs to warn the player with, before it loads the classic tile.
//
// The client asks rather than being told, because the answer depends on the profile and changes
// every time a quest is taken or handed in.
[Injectable, UsedImplicitly]
public class QuestGateRoutes(
    JsonUtil jsonUtil,
    HttpResponseUtil httpResponseUtil,
    QuestGateService gate
) : StaticRouter(
    jsonUtil,
    [
        new RouteAction<EmptyRequestData>(
            "/factoryclassic/questgate",
            (url, info, sessionId, output, cancellationToken) =>
                ValueTask.FromResult(Read(sessionId, gate, httpResponseUtil))),
    ]
)
{
    private static string Read(MongoId sessionId, QuestGateService gate, HttpResponseUtil httpResponseUtil)
        => httpResponseUtil.NoBody(new QuestGateResponse
        {
            Mode = gate.Mode,
            Accepted = gate.Warnings(sessionId),
        });
}
