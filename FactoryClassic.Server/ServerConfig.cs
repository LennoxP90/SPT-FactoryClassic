using System.Text.Json.Serialization;

namespace FactoryClassic.Server;

/// <summary>
/// config/config.json. Every property needs an explicit JsonPropertyName: JsonUtil matches
/// case-sensitively, so camelCase against PascalCase binds to defaults with nothing thrown.
/// </summary>
public class ServerConfig
{
    // Ignored since the MapVariants migration, and kept only so a stale value is reported.
    [JsonPropertyName("variant")]
    public string Variant { get; set; } = "classic";

    // classic | hybrid | modern. Read at load, so a change needs a server restart.
    [JsonPropertyName("lootMode")]
    public string LootMode { get; set; } = "classic";

    // warn | hide | off. See docs/QUESTS.md.
    [JsonPropertyName("questGate")]
    public string QuestGate { get; set; } = "warn";
}
