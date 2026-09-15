using System.Text.Json.Serialization;

namespace FactoryClassic.Server;

// JsonUtil sets no naming policy and no case insensitivity, so System.Text.Json matches
// case-sensitively: camelCase JSON against PascalCase properties binds everything to defaults with
// no exception thrown. Every property carries an explicit name for that reason.
public class ServerConfig
{
    [JsonPropertyName("variant")]
    public string Variant { get; set; } = "classic";

    // classic | hybrid | modern. Applies to the classic tile only and is read at load, so a change
    // takes effect on a server restart.
    [JsonPropertyName("lootMode")]
    public string LootMode { get; set; } = "classic";

    // warn | hide | off. Six quests place a Factory objective on a trigger zone that exists only in
    // the scenes 4.1 ships, so they can never be completed on the classic tile.
    //   warn - name any you have already accepted, and confirm, before a classic raid loads
    //   hide - also keep them off the trader board while classic is the map in play, unless accepted
    //   off  - say nothing
    [JsonPropertyName("questGate")]
    public string QuestGate { get; set; } = "warn";
}
