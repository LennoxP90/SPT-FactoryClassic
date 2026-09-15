using FactoryClassic.Server.Variants;
using FactoryClassic.Shared;
using JetBrains.Annotations;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace FactoryClassic.Server.Quests;

// Turns "which quests are gated" into "what does this player see", by joining the committed list to
// the profile's own quest statuses.
//
// Nothing here writes. The profile is read to grade each quest and is put down again untouched,
// which is the boundary the whole feature is built around: an accepted quest is never hidden, never
// failed and never rewritten.
[Injectable(InjectionType.Singleton), UsedImplicitly]
public class QuestGateService(
    QuestGateTable table,
    ProfileHelper profileHelper,
    VariantChoiceStore choiceStore,
    VariantRegistry registry)
{
    public string Mode => table.Mode;

    // Quest id -> status, as the profile has it. A quest the profile has never seen is simply absent,
    // which QuestGatePolicy reads as never offered.
    private Dictionary<string, int> StatusesOf(MongoId sessionId)
    {
        var statuses = new Dictionary<string, int>(StringComparer.Ordinal);
        var quests = profileHelper.GetPmcProfile(sessionId)?.Quests;
        if (quests is null) return statuses;

        foreach (var quest in quests)
            statuses[quest.QId.ToString()] = (int)quest.Status;

        return statuses;
    }

    // The names to put in front of the player before a classic raid loads.
    public List<string> Warnings(MongoId sessionId)
        => QuestGateMode.Warns(table.Mode)
            ? QuestGatePolicy.Warnings(table.Gated, StatusesOf(sessionId))
            : [];

    // The ids to keep off the trader board, or an empty set when the mode is not `hide`.
    //
    // Only when classic is in play for EVERY Factory location. Day and night are separate locations
    // with separate choices, so a player running classic days and vanilla nights can still finish
    // these quests at night, and hiding them then would take away something they can do.
    public HashSet<string> Hidden(MongoId sessionId)
    {
        if (!QuestGateMode.Hides(table.Mode) || table.Gated.Count == 0) return [];
        if (!AllFactoriesClassic(sessionId)) return [];

        return QuestGatePolicy.Hidden(table.Gated.Keys, StatusesOf(sessionId));
    }

    private bool AllFactoriesClassic(MongoId sessionId)
    {
        foreach (var locationId in FactoryScenes.ServerNames)
        {
            if (!registry.HasClassic(locationId)) return false;

            var effective = VariantResolution.Resolve(
                choiceStore.ChoiceFor(sessionId, locationId),
                choiceStore.LatestFor(locationId),
                registry.ConfiguredDefault);

            if (!MapVariant.IsClassic(effective)) return false;
        }
        return true;
    }
}
