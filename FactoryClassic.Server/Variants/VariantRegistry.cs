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

// Both Factories side by side, one installed in SPT's live table at a time.
//
// LocationTable.GetDictionary() returns its cache by reference, so replacing an entry replaces what
// the whole server serves for that location. Swapping at raid start is far simpler than trying to
// answer every request per player, and it is what InterchangeRework settled on.
//
// Built at PostLoad so other mods' edits to the shipped location are already in what we copy.
[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.PostLoad), UsedImplicitly]
public class VariantRegistry(
    LocationTable locationTable,
    JsonUtil jsonUtil,
    ISptLogger<VariantRegistry> logger) : IOnLoad
{
    private readonly Dictionary<string, Location> _vanilla = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Location> _classic = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _gate = new();

    public string ConfiguredDefault { get; private set; } = MapVariant.Classic;

    private string _lootMode = LootMode.Classic;

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        var root = SysPath.GetDirectoryName(typeof(VariantRegistry).Assembly.Location)!;

        var configPath = SysPath.Combine(root, "config", "config.json");
        if (File.Exists(configPath))
        {
            var config = jsonUtil.Deserialize<ServerConfig>(File.ReadAllText(configPath));
            ConfiguredDefault = MapVariant.Normalise(config?.Variant);
            _lootMode = LootMode.Normalise(config?.LootMode);
        }
        else
        {
            logger.Warning($"[FC] {configPath} is missing; defaulting to {ConfiguredDefault}");
        }

        foreach (var map in FactoryScenes.ServerNames) Register(root, map);

        // Installed at load, not at raid start. A Fika headless can host a raid nobody chose a
        // variant for, and SPT and Fika both build the raid's location - spawn points included -
        // from whatever the table holds at that moment, which is earlier than any raid-start route.
        InstallDefaults();

        logger.Info($"[FC] variant registry ready, server default is '{ConfiguredDefault}', loot mode '{_lootMode}'");
        foreach (var map in FactoryScenes.ServerNames)
            if (HasClassic(map)) logger.Info($"[FC] {map} is serving {Installed(map)}");

        return Task.CompletedTask;
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

    public Location? Classic(string locationId) => _classic.GetValueOrDefault(locationId);

    public Location? Vanilla(string locationId) => _vanilla.GetValueOrDefault(locationId);

    public void Install(string locationId, string variant)
    {
        lock (_gate)
        {
            var wanted = MapVariant.IsClassic(variant) ? _classic : _vanilla;
            if (!wanted.TryGetValue(locationId, out var location)) return;
            locationTable.GetDictionary()[locationTable.GetMappedKey(locationId)] = location;
        }
    }

    public void RestoreVanilla(string locationId) => Install(locationId, MapVariant.Original);

    // The resting state between raids: what the server config asks for, not vanilla. Restoring
    // vanilla here would leave the table wrong for the next raid nobody chooses for, which is every
    // headless raid.
    public void InstallDefaults()
    {
        foreach (var map in FactoryScenes.ServerNames)
            if (HasClassic(map)) Install(map, ConfiguredDefault);
    }

    // Read back through the accessor SPT itself uses, so it reports where SPT actually reads rather
    // than what we believe we installed.
    // The wire value of whatever the table is serving. Read by reference against the two datasets, so
    // it reports what is actually installed rather than what was last asked for.
    //
    // Kept separate from Installed(), which is a sentence for a log line: handing that to a client as
    // a variant sent every transit and every headless to the shipped map, because
    // "CLASSIC, 120 spawn point(s)..." normalises to "original".
    public string InstalledVariant(string locationId)
        => IsServingClassic(locationId) ? MapVariant.Classic : MapVariant.Original;

    private bool IsServingClassic(string locationId)
    {
        var live = locationTable.GetLocation(locationId);
        return live is not null && ReferenceEquals(live, _classic.GetValueOrDefault(locationId));
    }

    public string Installed(string locationId)
    {
        var live = locationTable.GetLocation(locationId);
        if (live is null) return "no location in the table";

        var which = IsServingClassic(locationId) ? "CLASSIC"
                  : ReferenceEquals(live, _vanilla.GetValueOrDefault(locationId)) ? "VANILLA"
                  : "NEITHER - something else replaced it";
        return $"{which}, {live.Base?.SpawnPointParams?.Count() ?? -1} spawn point(s), {live.Base?.Exits?.Count() ?? -1} exit(s)";
    }
}
