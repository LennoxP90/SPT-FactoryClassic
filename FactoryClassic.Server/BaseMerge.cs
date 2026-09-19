using FactoryClassic.Shared;
using SPTarkov.Server.Core.Models.Eft.Common;

namespace FactoryClassic.Server;

/// <summary>
/// Overlays the pre-rework location onto the shipped one. Only geometry-bound fields move; the
/// 4.1-only keys stay, transits included, because the 3.9.8 file predates them.
/// </summary>
public static class BaseMerge
{
    public static void Apply(LocationBase shipped, LocationBase classic)
    {
        shipped.SpawnPointParams = classic.SpawnPointParams;
        shipped.Exits = classic.Exits;
        shipped.Waves = classic.Waves;

        // Never a wholesale replacement: 4.1 places PMCs through this array and 3.9.8 did not.
        shipped.BossLocationSpawn = BossSpawnMergePolicy.Merge(
            shipped.BossLocationSpawn,
            classic.BossLocationSpawn,
            spawn => spawn.BossName);
    }
}
