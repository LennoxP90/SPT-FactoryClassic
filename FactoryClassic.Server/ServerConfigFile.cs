using System.Text.Json;
using System.Text.Json.Nodes;

namespace FactoryClassic.Server;

/// <summary>
/// Reads and writes config/config.json for the SIC page. A write patches only the keys this mod owns
/// into the file as it stands, so keys it does not know survive.
/// </summary>
public static class ServerConfigFile
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public static string DefaultPath
        => Path.Combine(Path.GetDirectoryName(typeof(ServerConfigFile).Assembly.Location)!, "config", "config.json");

    // Defaults on any failure, never a throw: the caller is a settings page or a raid start.
    public static ServerConfig Read(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<ServerConfig>(File.ReadAllText(path)) ?? new ServerConfig();
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            return new ServerConfig();
        }
    }

    public static void Write(string path, ServerConfig config)
    {
        JsonObject root;
        try
        {
            root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? [];
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            root = [];
        }

        root["lootMode"] = config.LootMode;
        root["questGate"] = config.QuestGate;
        root["halloweenChance"] = config.HalloweenChance;

        var temp = path + ".tmp";
        File.WriteAllText(temp, root.ToJsonString(Indented));
        File.Move(temp, path, true);
    }
}
