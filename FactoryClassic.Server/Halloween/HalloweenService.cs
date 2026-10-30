using FactoryClassic.Shared;
using JetBrains.Annotations;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Utils;
using SysPath = System.IO.Path;

namespace FactoryClassic.Server.Halloween;

/// <summary>
/// The server's Halloween answer for a location. Only a location MapVariants has on the classic
/// tile rolls; anything else is never Halloween.
/// </summary>
[Injectable(InjectionType.Singleton), UsedImplicitly]
public class HalloweenService
{
    private readonly MapVariantsBridge _bridge;
    private readonly ISptLogger<HalloweenService> _logger;
    private readonly JsonUtil _jsonUtil;
    private readonly HalloweenOutcomes _outcomes;
    private readonly string _configPath;
    private string? _lastError;

    public HalloweenService(MapVariantsBridge bridge, JsonUtil jsonUtil, ISptLogger<HalloweenService> logger)
    {
        _bridge = bridge;
        _jsonUtil = jsonUtil;
        _logger = logger;
        _configPath = SysPath.Combine(SysPath.GetDirectoryName(typeof(HalloweenService).Assembly.Location)!, "config", "config.json");
        _outcomes = new HalloweenOutcomes(ReadChance, Random.Shared.NextDouble, () => DateTime.UtcNow);
    }

    public bool Outcome(string locationId, string session)
    {
        var id = LocationId.Normalise(locationId);
        if (!MapVariant.IsClassic(VariantVocabulary.FromMapVariants(_bridge.InstalledVariant(id)))) return false;

        var halloween = _outcomes.Outcome(id, session, out var fresh);
        if (fresh) _logger.Info($"[FC] '{id}' rolled {(halloween ? "HALLOWEEN" : "plain classic")} for this raid");
        return halloween;
    }

    public void Clear(string session) => _outcomes.Clear(session);

    // Null on any failure, which the roll reads as the default chance.
    private int? ReadChance()
    {
        try
        {
            return _jsonUtil.Deserialize<ServerConfig>(File.ReadAllText(_configPath))?.HalloweenChance;
        }
        catch (Exception e)
        {
            if (_lastError != e.Message)
            {
                _lastError = e.Message;
                _logger.Error($"[FC] reading halloweenChance from {_configPath} failed, using {HalloweenRoll.DefaultChance}: {e.Message}");
            }
            return null;
        }
    }
}
