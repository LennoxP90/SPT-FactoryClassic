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

/// <summary>
/// Keeps the gated quests off the trader board while classic is in play, in hide mode only and for
/// quests the player has not accepted. It EDITS the list SPT built rather than rebuilding it, so
/// another mod's quests survive, and edits it as a JsonNode rather than binding to a model, so a
/// field our copy of the model lacks cannot vanish silently. See docs/QUESTS.md.
/// </summary>
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
