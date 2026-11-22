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
    private MethodInfo? _declareVariant;
    private MethodInfo? _registerSource, _shipped;
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
        _declareVariant = type.GetMethod("DeclareVariant", [typeof(string)]);
        // MapVariants 1.2.0 and up. Absent before that, and then Register and DeclareVariant are used.
        _registerSource = type.GetMethod("RegisterSource", [typeof(string), typeof(string), typeof(Location)]);
        _shipped = type.GetMethod("Shipped", [typeof(string)]);

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

    /// <summary>MapVariants 1.2.0 and up: Classic and Halloween 2026 join a map as sources, beside any other mod's Factory.</summary>
    public bool HasSources => Bind() && _registerSource is not null && _shipped is not null;

    public bool RegisterSource(string locationId, string variantId, Location variant)
    {
        if (!HasSources) return false;
        try { return _registerSource!.Invoke(_providers, [locationId, variantId, variant]) is true; }
        catch (Exception e) { logger.Error($"[FC] handing '{locationId}' to MapVariants as source '{variantId}' threw: {e}"); return false; }
    }

    /// <summary>The shipped location as MapVariants captured it before any mod's Preload, or null before 1.2.0.</summary>
    public Location? Shipped(string locationId)
    {
        if (!HasSources) return null;
        try { return _shipped!.Invoke(_providers, [locationId]) as Location; }
        catch (Exception e) { logger.Warning($"[FC] reading the shipped '{locationId}' from MapVariants threw, the live one is used: {e.Message}"); return null; }
    }

    // Absent before MapVariants 3.1: then Halloween 2026 is never installed and every raid is plain classic.
    public bool DeclareVariant(string variant)
    {
        if (!Bind()) return false;
        if (_declareVariant is null)
        {
            logger.Warning($"[FC] this MapVariants cannot declare '{variant}'; update it to offer Halloween 2026");
            return false;
        }
        try
        {
            return _declareVariant.Invoke(_providers, [variant]) is true;
        }
        catch (Exception e)
        {
            logger.Error($"[FC] declaring '{variant}' to MapVariants threw: {e}");
            return false;
        }
    }

    /// <summary>
    /// THEIR wire value ("original", "backport" or "halloween2026"), or null when the bridge is unavailable.
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
