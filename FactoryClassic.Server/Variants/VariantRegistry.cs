using FactoryClassic.Shared;
using JetBrains.Annotations;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils;
using SysPath = System.IO.Path;
using Location = SPTarkov.Server.Core.Models.Eft.Common.Location;

namespace FactoryClassic.Server.Variants;

/// <summary>
/// Builds both Factories side by side and hands the pair to MapVariants, which owns the choice, the
/// routes and the swap from that moment on. This mod must not install into SPT's live table itself:
/// MapVariants assigns the chosen field set onto the one live Location, so anything of ours that
/// also replaced the dictionary entry would write over whichever object it last swapped in.
/// </summary>
[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.PostLoad), UsedImplicitly]
public class VariantRegistry(
    LocationTable locationTable,
    JsonUtil jsonUtil,
    MapVariantsBridge mapVariants,
    ISptLogger<VariantRegistry> logger) : IOnLoad
{
    private readonly Dictionary<string, Location> _vanilla = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Location> _classic = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _handedOver = new(StringComparer.OrdinalIgnoreCase);

    private string _lootMode = LootMode.Classic;

    // PostLoad, so other mods' edits to the shipped location are already in what is copied and the
    // handover lands before MapVariants' discovery pass at PostLoad + 500000. Discovery skips a
    // location that already has a variant recorded, so a registered Factory is never re-discovered.
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        var root = SysPath.GetDirectoryName(typeof(VariantRegistry).Assembly.Location)!;

        LoadConfig(root);

        foreach (var map in FactoryScenes.ServerNames) Register(root, map);
        foreach (var map in FactoryScenes.ServerNames) HandOver(map);

        logger.Info($"[FC] variant registry ready, loot mode '{_lootMode}'; MapVariants owns the variant choice");

        return Task.CompletedTask;
    }

    private void LoadConfig(string root)
    {
        var configPath = SysPath.Combine(root, "config", "config.json");
        if (!File.Exists(configPath))
        {
            logger.Warning($"[FC] {configPath} is missing; the classic tile is built with loot mode '{_lootMode}'");
            return;
        }

        var config = jsonUtil.Deserialize<ServerConfig>(File.ReadAllText(configPath));
        _lootMode = LootMode.Normalise(config?.LootMode);

        if (config?.Variant is { Length: > 0 } && !string.Equals(config.Variant, "classic", StringComparison.OrdinalIgnoreCase))
            logger.Warning($"[FC] config.json sets \"variant\": \"{config.Variant}\", which no longer does anything. "
                         + "Which Factory loads is now each player's own choice, in MapVariants' F12 settings under Factory.");
    }

    // One call per location id, because each id has its own Location object in SPT. Several ids
    // sharing one player-facing choice is a CLIENT concern, ManagedMap.LocationIds.
    //
    // The LIVE Location goes over as `original` deliberately: MapVariants captures both field sets
    // before it installs anything, so the PostLoad state becomes our original, other mods included.
    private void HandOver(string locationId)
    {
        if (!HasClassic(locationId)) return;

        if (mapVariants.Register(locationId, _vanilla[locationId], _classic[locationId]))
        {
            _handedOver.Add(locationId);
            logger.Info($"[FC] {locationId} handed to MapVariants; it owns the choice, the routes and the swap from here");
        }
        else
        {
            logger.Error($"[FC] {locationId} could NOT be handed to MapVariants; it stays vanilla only and no classic raid is possible there");
        }
    }

    private void Register(string root, string locationId)
    {
        var vanilla = locationTable.GetLocation(locationId);
        if (vanilla is null) { logger.Error($"[FC] no location '{locationId}'; the classic tile will not be available"); return; }

        var dir = SysPath.Combine(root, "db", "classic", locationId);
        if (!Directory.Exists(dir)) { logger.Error($"[FC] {dir} is missing; '{locationId}' stays vanilla only"); return; }

        try
        {
            _vanilla[locationId] = vanilla;
            _classic[locationId] = ClassicDataset.Build(vanilla, dir, _lootMode, jsonUtil, out var counts);
            logger.Info($"[FC] {locationId} classic dataset built: {counts}");
        }
        catch (Exception e)
        {
            _classic.Remove(locationId);
            logger.Error($"[FC] could not build the classic dataset for '{locationId}'; it stays vanilla only: {e}");
        }
    }

    public bool HasClassic(string locationId) => _classic.ContainsKey(locationId);

    public bool HandedOver(string locationId) => _handedOver.Contains(locationId);

    // Do not add a Classic(id) / Vanilla(id) accessor. After the handover MapVariants assigns the
    // chosen fields onto the very object _vanilla holds, so "vanilla" would hand back classic data.
    // Ask MapVariantsBridge.InstalledVariant what is installed instead.
}
