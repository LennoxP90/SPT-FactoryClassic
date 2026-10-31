using FactoryClassic.Shared;
using JetBrains.Annotations;
using SPTarkov.DI.Annotations;

namespace FactoryClassic.Server.Halloween;

/// <summary>
/// Whether a location is serving Halloween 2026: the variant the player picked, as MapVariants installed it.
/// </summary>
[Injectable(InjectionType.Singleton), UsedImplicitly]
public class HalloweenService(MapVariantsBridge bridge)
{
    public bool IsHalloween(string locationId)
        => VariantVocabulary.IsHalloween(bridge.InstalledVariant(LocationId.Normalise(locationId)));
}
