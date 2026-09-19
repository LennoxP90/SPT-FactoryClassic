using System.Reflection;
using JetBrains.Annotations;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using Location = SPTarkov.Server.Core.Models.Eft.Common.Location;

namespace FactoryClassic.Server;

/// <summary>
/// Reaches MapVariants' server half by reflection rather than by a compile-time reference, which
/// would make this mod fail to LOAD when their assembly is absent or a different version, and would
/// drag their SPT Core floor in behind it. ModDependencies already refuses the mod when MapVariants
/// is missing, so this only has to survive it being present in a shape we do not expect.
/// </summary>
[Injectable(InjectionType.Singleton), UsedImplicitly]
public class MapVariantsBridge(IServiceProvider provider, ISptLogger<MapVariantsBridge> logger)
{
    private const string ProvidersTypeName = "MapVariants.Server.Variants.Providers";

    private object? _providers;
    private MethodInfo? _register;
    private MethodInfo? _installedVariant;
    private bool _bound, _reported;

    public bool Available => Bind();

    private bool Bind()
    {
        if (_bound) return _providers is not null;
        _bound = true;

        var type = FindType(ProvidersTypeName);
        if (type is null)
        {
            logger.Error($"[FC] {ProvidersTypeName} is not loaded; Factory has no variant switching. "
                       + "MapVariants is a hard dependency, so this should be unreachable - if it is not, the two mods disagree about the type name.");
            return false;
        }

        _providers = provider.GetService(type);
        _register = type.GetMethod("Register", [typeof(string), typeof(Location), typeof(Location)]);
        _installedVariant = type.GetMethod("InstalledVariant", [typeof(string)]);

        if (_providers is null || _register is null || _installedVariant is null)
        {
            logger.Error($"[FC] MapVariants' provider surface is not the expected layout "
                       + $"(instance={_providers is not null}, Register={_register is not null}, InstalledVariant={_installedVariant is not null}); "
                       + "Factory stays on whatever the table holds and no variant is offered.");
            _providers = null;
            return false;
        }

        logger.Debug("[FC] MapVariants provider surface bound");
        return true;
    }

    public bool Register(string locationId, Location original, Location backport)
    {
        if (!Bind()) return false;
        try
        {
            _register!.Invoke(_providers, [locationId, original, backport]);
            return true;
        }
        catch (Exception e)
        {
            logger.Error($"[FC] handing '{locationId}' to MapVariants threw; that location keeps whatever the table holds: {e}");
            return false;
        }
    }

    /// <summary>
    /// THEIR wire value ("original" or "backport"), or null when the bridge is unavailable.
    /// Translate with VariantVocabulary.FromMapVariants before comparing it to anything of ours.
    /// </summary>
    public string? InstalledVariant(string locationId)
    {
        if (!Bind()) return null;
        try
        {
            return _installedVariant!.Invoke(_providers, [locationId]) as string;
        }
        catch (Exception e)
        {
            if (!_reported) { _reported = true; logger.Error($"[FC] reading MapVariants' installed variant threw: {e}"); }
            return null;
        }
    }

    private static Type? FindType(string name)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType(name, false);
            if (type is not null) return type;
        }
        return null;
    }
}
