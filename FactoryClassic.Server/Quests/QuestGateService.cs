using FactoryClassic.Server.Variants;
using FactoryClassic.Shared;
using JetBrains.Annotations;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace FactoryClassic.Server.Quests;

/// <summary>
/// Turns "which quests are gated" into "what does this player see". Nothing here writes: the
/// profile is read to grade each quest and put down untouched. See docs/QUESTS.md.
/// </summary>
[Injectable(InjectionType.Singleton), UsedImplicitly]
public class QuestGateService(
    QuestGateTable table,
    ProfileHelper profileHelper,
    MapVariantsBridge mapVariants,
    VariantRegistry registry)
{
    public string Mode => table.Mode;

    private Dictionary<string, int> StatusesOf(MongoId sessionId)
    {
        var statuses = new Dictionary<string, int>(StringComparer.Ordinal);
        var quests = profileHelper.GetPmcProfile(sessionId)?.Quests;
        if (quests is null) return statuses;

        foreach (var quest in quests)
            statuses[quest.QId.ToString()] = (int)quest.Status;

        return statuses;
    }

    public List<string> Warnings(MongoId sessionId)
        => QuestGateMode.Warns(table.Mode)
            ? QuestGatePolicy.Warnings(table.Gated, StatusesOf(sessionId))
            : [];

    public HashSet<string> Hidden(MongoId sessionId)
    {
        if (!QuestGateMode.Hides(table.Mode) || table.Gated.Count == 0) return [];
        if (!AllFactoriesClassic()) return [];

        return QuestGatePolicy.Hidden(table.Gated.Keys, StatusesOf(sessionId));
    }

    // Day and night are separate locations with separate answers, so a player running classic days
    // and vanilla nights can still finish these quests at night.
    private bool AllFactoriesClassic()
    {
        foreach (var locationId in FactoryScenes.ServerNames)
            if (!ClassicInstalledAt(locationId)) return false;

        return true;
    }

    // An unanswerable bridge counts as not classic: hiding a quest a player could have done is
    // worse than not hiding one they cannot.
    private bool ClassicInstalledAt(string locationId)
    {
        if (!registry.HasClassic(locationId)) return false;

        var installed = mapVariants.InstalledVariant(locationId);
        return installed is not null
            && MapVariant.IsClassic(VariantVocabulary.FromMapVariants(installed));
    }
}
