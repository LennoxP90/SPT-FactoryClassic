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

    // The real floor is the referenced SPTarkov.Server.Core version, which ModValidator compares
    // against the running server and throws on, out of the mod loader. Keep this range and the
    // package version in step.
    public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.0");

    public bool HasPrepatcher { get; init; } = false;
    public List<string>? Incompatibilities { get; init; }
    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public string? Url { get; init; }
    public string License { get; init; } = "MIT";
}
