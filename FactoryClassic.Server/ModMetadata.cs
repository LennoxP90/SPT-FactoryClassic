using JetBrains.Annotations;
using SPTarkov.Server.Core.Models.Spt.Mod;

namespace FactoryClassic.Server;

[UsedImplicitly]
public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.lennoxp90.factoryclassic";
    public string Name { get; init; } = "FactoryClassic";
    public string Author { get; init; } = "LennoxP90";
    public List<string>? Contributors { get; init; }
    public SemanticVersioning.Version Version { get; init; } = new(typeof(ModMetadata).Assembly.GetName().Version!.ToString(3));

    // The real floor is the referenced SPTarkov.Server.Core version, which ModValidator throws on.
    // Keep this range and the package version in step.
    public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.0");

    public bool HasPrepatcher { get; init; } = false;
    public List<string>? Incompatibilities { get; init; }

    // Upper bound deliberately: this calls MapVariants' extension API, so a 2.0.0 is free to break
    // the call. Open-ended would load anyway and fail at the map screen instead of at load.
    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; } = new()
    {
        ["com.lennoxp90.mapvariants"] = new SemanticVersioning.Range(">=1.0.0 <2.0.0"),
    };
    public string? Url { get; init; }
    public string License { get; init; } = "MIT";
}
