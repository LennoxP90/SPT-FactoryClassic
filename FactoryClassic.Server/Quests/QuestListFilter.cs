using System.Text.Json.Nodes;
using JetBrains.Annotations;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils;

namespace FactoryClassic.Server.Quests;

// Keeps the gated quests off the trader board while classic is the map in play. Only in `hide` mode,
// and only for quests the player has not accepted.
//
// Every static router matching a url runs, in registration order, each handed the previous one's
// output. Ours carries no [Injectable] TypePriority, which defaults to int.MaxValue, so it is
// registered after SPT's own router at OnLoadOrder.Routers and receives the list SPT built.
//
// It EDITS that string rather than rebuilding the response from the database. Rebuilding would throw
// away whatever another mod did to the same route, and a mod that adds quests is exactly the kind of
// mod someone runs alongside this one. Editing as a JsonNode also keeps every field SPT emitted,
// which binding to a model would not: a property our copy of the model lacks would vanish silently.
[Injectable, UsedImplicitly]
public class QuestListFilter(
    JsonUtil jsonUtil,
    QuestGateService gate,
    ISptLogger<QuestListFilter> logger
) : StaticRouter(
    jsonUtil,
    [
        new RouteAction<EmptyRequestData>(
            "/client/quest/list",
            (url, info, sessionId, output, cancellationToken) =>
                ValueTask.FromResult(Filter(output, sessionId, gate, logger))),
    ]
)
{
    private static string Filter(string? output, MongoId sessionId, QuestGateService gate, ISptLogger<QuestListFilter> logger)
    {
        // Nothing built the list, so there is nothing to filter and nothing we should invent.
        if (string.IsNullOrWhiteSpace(output)) return output ?? "";

        var hidden = gate.Hidden(sessionId);
        if (hidden.Count == 0) return output;

        try
        {
            if (JsonNode.Parse(output) is not JsonObject root || root["data"] is not JsonArray quests)
                return output;

            var removed = 0;
            for (var i = quests.Count - 1; i >= 0; i--)
            {
                var questId = quests[i]?["_id"]?.GetValue<string>();
                if (questId is not null && hidden.Contains(questId))
                {
                    quests.RemoveAt(i);
                    removed++;
                }
            }

            if (removed == 0) return output;

            logger.Debug($"[FC] quest gate hid {removed} quest(s) from {sessionId}");
            return root.ToJsonString();
        }
        catch (Exception e)
        {
            // Serving the unfiltered list is the harmless failure. Serving a broken one is not.
            logger.Warning($"[FC] quest gate could not filter the quest list: {e.GetType().Name}: {e.Message}");
            return output;
        }
    }
}
