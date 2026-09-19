#nullable enable
using System;

namespace FactoryClassic.Shared
{
    /// <summary>
    /// A bake's last eight bytes are its per-pair maxima. The propagation job sizes its buffers from
    /// the location info's copy and slices them unchecked, and the classic tile's copy is zero.
    /// </summary>
    public static class AudioBakeCapacities
    {
        public const int TailLength = 8;

        public static bool TryReadTail(byte[]? tail, out uint maxRoutesPerPair, out uint maxPortalsPerPair)
        {
            maxRoutesPerPair = 0;
            maxPortalsPerPair = 0;
            if (tail == null || tail.Length != TailLength) return false;

            maxRoutesPerPair = BitConverter.ToUInt32(tail, 0);
            maxPortalsPerPair = BitConverter.ToUInt32(tail, 4);
            return true;
        }

        // Only ever raise: an undersized buffer is the overrun, an oversized one is sliced down.
        public static uint Raise(uint current, uint fromBake) => fromBake > current ? fromBake : current;
    }
}
