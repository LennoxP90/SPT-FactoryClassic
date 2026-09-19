using System.Text.Json.Serialization;
using FactoryClassic.Shared;
using JetBrains.Annotations;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils;
using SysPath = System.IO.Path;

namespace FactoryClassic.Server.Quests;

public sealed class GatedQuest
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("zones")] public List<string> Zones { get; set; } = [];
}

public sealed class QuestGateDocument
{
    [JsonPropertyName("zones")] public List<string> Zones { get; set; } = [];
    [JsonPropertyName("quests")] public Dictionary<string, GatedQuest> Quests { get; set; } = [];
}

/// <summary>
/// The quests the classic tile cannot satisfy, read from db/quest-gate.json. A committed file
/// rather than a startup scan: the evidence is in the client's scene files, and a list in the
/// repository shows a BSG change as a diff and a failing test instead of as silence. The names are
/// for display only; everything that decides anything keys on the quest id.
/// </summary>
[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.PostLoad), UsedImplicitly]
public class QuestGateTable(
    TemplateTable templateTable,
    JsonUtil jsonUtil,
    ISptLogger<QuestGateTable> logger) : IOnLoad
{
    private Dictionary<string, string> _gated = new(StringComparer.Ordinal);

    public string Mode { get; private set; } = QuestGateMode.Warn;

    /// <summary>
    /// Quest id to display name. Empty when the mode is off, so nothing downstream asks twice.
    /// </summary>
    public IReadOnlyDictionary<string, string> Gated => _gated;

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        var root = SysPath.GetDirectoryName(typeof(QuestGateTable).Assembly.Location)!;

        var configPath = SysPath.Combine(root, "config", "config.json");
        if (File.Exists(configPath))
            Mode = QuestGateMode.Normalise(jsonUtil.Deserialize<ServerConfig>(File.ReadAllText(configPath))?.QuestGate);

        if (Mode == QuestGateMode.Off)
        {
            logger.Info("[FC] quest gate off");
            return Task.CompletedTask;
        }

        var path = SysPath.Combine(root, "db", "quest-gate.json");
        if (!File.Exists(path))
        {
            logger.Error($"[FC] {path} is missing; the quest gate is inert");
            return Task.CompletedTask;
        }

        var document = jsonUtil.Deserialize<QuestGateDocument>(File.ReadAllText(path));
        if (document is null || document.Quests.Count == 0)
        {
            logger.Error($"[FC] {path} lists no quests; the quest gate is inert");
            return Task.CompletedTask;
        }

        // An id that no longer exists means the quest changed under us, so the entry can only be
        // wrong. Dropped rather than kept, so the gate under-reports instead of hiding a live quest.
        var live = templateTable.Quests;
        var stale = new List<string>();
        var kept = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (questId, entry) in document.Quests)
        {
            if (live.ContainsKey(questId))
                kept[questId] = string.IsNullOrWhiteSpace(entry.Name) ? questId : entry.Name;
            else
                stale.Add($"{entry.Name} ({questId})");
        }

        _gated = kept;

        if (stale.Count > 0)
            logger.Warning($"[FC] quest gate: {stale.Count} quest(s) no longer in the database, ignored: "
                           + string.Join(", ", stale) + ". Re-run analysis/find_original_only_quests.py.");

        logger.Info($"[FC] quest gate '{Mode}', {_gated.Count} quest(s) over {document.Zones.Count} zone(s) "
                    + "only the original tile carries");
        return Task.CompletedTask;
    }
}
