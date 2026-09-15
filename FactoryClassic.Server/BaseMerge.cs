using FactoryClassic.Shared;
using SPTarkov.Server.Core.Models.Eft.Common;

namespace FactoryClassic.Server;

// Overlays the pre-rework location onto the shipped one. Only geometry-bound fields move: spawn
// points, exits and waves describe the tile, while lobby size and the 4.1-only keys (transits,
// secretExits, Events, HeatmapLayers, AccessKeysPvE, NewSpawnForPlayers) belong to the server
// version and stay. The legacy scene now carries BSG's own TRANSITS and Road_to_* objects, so
// keeping 4.1's transits is deliberate: the 3.9.8 file predates them entirely.
public static class BaseMerge
{
    public static void Apply(LocationBase shipped, LocationBase classic)
    {
        shipped.SpawnPointParams = classic.SpawnPointParams;
        shipped.Exits = classic.Exits;
        shipped.Waves = classic.Waves;

        // Never a wholesale replacement: 4.1 places PMCs through this same array and 3.9.8 did not,
        // so taking the classic list entire would delete every PMC with nothing in the log.
        shipped.BossLocationSpawn = BossSpawnMergePolicy.Merge(
            shipped.BossLocationSpawn,
            classic.BossLocationSpawn,
            spawn => spawn.BossName);
    }
}
